# CI/CD: lets GitHub Actions deploy the API without long-lived AWS keys.
#
# GitHub Actions authenticates via its OIDC token (token.actions.githubusercontent.com)
# and assumes a role that can only push images to ECR and trigger a new
# deployment of the ECS service. Terraform plans and applies stay manual.

variable "github_repository" {
  description = "GitHub repository (owner/name) allowed to assume the CI deploy role."
  type        = string
  default     = "Pepert/Vocabula"
}

# GitHub's OIDC certificate thumbprints, fetched live at apply time from the
# endpoint GitHub is actually serving. If GitHub ever rotates its certificate
# chain, the workflow fails loudly at the OIDC handshake and re-running
# `terraform apply` (which re-fetches this chain) is the whole fix — no code
# change needed.
data "tls_certificate" "github_oidc" {
  url = "https://token.actions.githubusercontent.com"
}

resource "aws_iam_openid_connect_provider" "github" {
  url            = "https://token.actions.githubusercontent.com"
  client_id_list = ["sts.amazonaws.com"]

  thumbprint_list = [
    for cert in data.tls_certificate.github_oidc.certificates : cert.sha1_fingerprint
  ]

  tags = {
    name = "github-actions"
  }
}

data "aws_iam_policy_document" "ci_deploy_assume" {
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [aws_iam_openid_connect_provider.github.arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:sub"
      values = [
        "repo:${var.github_repository}:ref:refs/heads/main",
        "repo:${var.github_repository}:environment:production"
      ]
    }
  }
}

resource "aws_iam_role" "ci_deploy" {
  name               = "vocabula-ci-deploy"
  assume_role_policy = data.aws_iam_policy_document.ci_deploy_assume.json

  tags = {
    name = "vocabula-ci-deploy"
  }
}

data "aws_iam_policy_document" "ci_deploy" {
  statement {
    sid = "PushImages"

    actions = [
      "ecr:GetAuthorizationToken",
      "ecr:BatchCheckLayerAvailability",
      "ecr:InitiateLayerUpload",
      "ecr:UploadLayerPart",
      "ecr:CompleteLayerUpload",
      "ecr:PutImage"
    ]

    resources = [
      "arn:aws:ecr:${var.aws_region}:${data.aws_caller_identity.current.account_id}:repository/${aws_ecr_repository.vocabula_api.name}"
    ]
  }

  # ecr:GetAuthorizationToken is account-scoped, not repo-scoped.
  statement {
    sid = "GetAuthorizationToken"

    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }

  statement {
    sid = "TriggerDeployment"

    actions = [
      "ecs:UpdateService",
      "ecs:DescribeServices"
    ]

    resources = [
      aws_ecs_service.vocabula_api.id
    ]
  }
}

resource "aws_iam_role_policy" "ci_deploy" {
  name = "push-and-deploy"
  role = aws_iam_role.ci_deploy.id

  policy = data.aws_iam_policy_document.ci_deploy.json
}

output "ci_deploy_role_arn" {
  description = "IAM role GitHub Actions assumes to deploy the API."
  value       = aws_iam_role.ci_deploy.arn
}
