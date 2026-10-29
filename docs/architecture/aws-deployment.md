# AWS deployment (ECS Fargate)

How the Vocabula API runs on AWS, and the exact commands to deploy it from a
local machine. The infrastructure is defined with Terraform in [`terraform/`](../../terraform/)
and follows the architecture chosen for the MVP.

## Architecture

```mermaid
flowchart LR
    U[Browser / future frontend] --> ALB[Application Load Balancer\nHTTP :80]
    ALB -->|HTTP :8080| T[ECS Fargate task\nvocabula-api, ARM64\n0.25 vCPU / 512 MB]
    T -->|PostgreSQL :5432\nprivate subnets| RDS[(RDS PostgreSQL 17\ndb.t4g.micro)]
    T -->|HTTPS|M[Mistral API]
    T -->|HTTPS JWKS| C[Cognito user pool\neu-west-3]
    ECS[ECS agent] -->|fetch at container start| S[Secrets Manager\nvocabula/prod]
    T -->|push image| ECR[(ECR\nvocabula-api)]
```

Key decisions:

- **ECS Fargate (ARM64/Graviton)** over Lambda: the knowledge extraction calls
  the Mistral OCR API with a 2-minute timeout, EF Core holds a normal database
  connection pool, and the app is a plain Kestrel container. Always-on at MVP
  scale.
- **Dedicated VPC**: the Fargate task runs in public subnets with a public IP
  (no NAT gateway, which alone costs ~$32/month). RDS runs in private subnets
  and is reachable only from the ECS task.
- **Secrets Manager**: one JSON secret (`vocabula/prod`) holds
  `MISTRAL_API_KEY` and the PostgreSQL connection string. The ECS agent
  injects them as environment variables at container start, so the
  application code never talks to AWS and needs no changes.
- **HTTP only** for now; a 443 listener with an ACM certificate is added once
  the frontend needs HTTPS (it needs a domain name first).

Approximate monthly cost: **~$45** (ALB $16, Fargate 0.25 vCPU/512 MB $14,
RDS db.t4g.micro $13, Secrets Manager $0.40/secret, data transfer).

## Prerequisites

- Terraform >= 1.6 (`brew install hashicorp/tap/terraform`)
- AWS CLI configured with credentials that can create resources
- Docker (with buildx for ARM64 images; native on Apple Silicon)

## One-time setup

### 1. Fill in the variables

```bash
cp terraform/prod.tfvars.example terraform/prod.tfvars
# edit terraform/prod.tfvars: set mistral_api_key and db_password
```

`prod.tfvars` is gitignored: it holds the Mistral API key and the database
password. Note that Terraform state also contains these values; the state
bucket is private to your account.

### 2. Bootstrap the Terraform state backend (once, ever)

```bash
cd terraform/bootstrap
terraform init
terraform apply
cd ../..
```

This creates the S3 bucket that stores the state and the DynamoDB table that
locks it. The main configuration references these names in its backend block,
so nothing else is needed afterwards.

### 3. Deploy the infrastructure

```bash
cd terraform
terraform init
terraform plan -var-file=prod.tfvars
terraform apply -var-file=prod.tfvars
```

The first apply creates the VPC, ECR repository, Secrets Manager secret, RDS
instance, ECS cluster, task definition and load balancer. The ECS service will
not start tasks yet because the ECR repository is still empty.

### 4. Build and push the container image

```bash
ECR_URL=$(cd terraform && terraform output -raw ecr_repository_url)

aws ecr get-login-password --region eu-west-3 \
  | docker login --username AWS --password-stdin $(dirname $ECR_URL | cut -d/ -f1)

docker buildx build \
  --platform linux/arm64 \
  -t $ECR_URL:latest \
  --push .
```

The Dockerfile runs the test suite as part of the build, so the first build
takes a few minutes. Once the image is pushed, the ECS service starts a task;
after the health check (`GET /health`) passes twice, the API is live.

### 5. Verify

```bash
ALB=$(cd terraform && terraform output -raw alb_dns_name)
curl http://$ALB/health
# {"status":"healthy"}
```

The Swagger UI is at `http://$ALB/swagger` (for a valid Cognito token, see
`AuthenticationTests` / the Cognito user pool in `eu-west-3`).

## Database migrations

RDS is in private subnets, so migrations run from your machine by temporarily
opening access to your IP:

```bash
MY_IP=$(curl -s https://checkip.amazonaws.com)
cd terraform
# edit prod.tfvars: allow_migrations_from = "$MY_IP/32"
terraform apply -var-file=prod.tfvars
cd ..

# Run the EF Core migration against the remote database
export ConnectionStrings__VocabulaDb=$(aws secretsmanager get-secret-value \
  --secret-id vocabula/prod --region eu-west-3 \
  --query SecretString --output text | python3 -c \
  "import json,sys; print(json.load(sys.stdin)['ConnectionStrings__VocabulaDb'])")
export MISTRAL_API_KEY=dummy
export AUTHENTICATION__COGNITO__AUTHORITY=https://cognito-idp.eu-west-3.amazonaws.com/eu-west-3_pVJEZ5jFX
export AUTHENTICATION__COGNITO__APPCLIENTID=7bnbulcbgqfguguap1chtmfu8a

dotnet ef database update \
  --project src/Vocabula.Infrastructure \
  --startup-project src/Vocabula.Api

# Close the temporary access again
cd terraform
# edit prod.tfvars: allow_migrations_from = ""
terraform apply -var-file=prod.tfvars
```

The dummy values for the Mistral/Cognito variables are only there because
`Program.cs` validates their presence at design time; they are not used to
talk to AWS.

## Day-to-day operations

- **Deploy a new version**: push a new image (`docker buildx build ... --push`),
  then force a new deployment if you re-used the same tag:
  `aws ecs update-service --cluster vocabula --service vocabula-api --force-new-deployment --region eu-west-3`
- **Rotate a secret**: update `terraform/prod.tfvars`, then
  `terraform apply -var-file=prod.tfvars` and force a new deployment.
- **Logs**: CloudWatch log group `/ecs/vocabula-api`.
- **Destroy everything**:
  `cd terraform && terraform destroy -var-file=prod.tfvars`.
  ⚠️ `aws_db_instance` is configured with `skip_final_snapshot = true`, so
  destroying deletes the database and its data without a final snapshot.
  Change that before the database holds anything you care about.

## Open questions

- HTTPS: which domain name, and how is DNS managed (Route 53)?
- Should the Fargate service scale to zero overnight (no users yet) to cut
  the ~$14/month compute cost?
