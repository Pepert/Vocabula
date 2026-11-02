# CI/CD: lets GitHub Actions deploy the API without long-lived AWS keys.
#
# GitHub Actions authenticates via its OIDC token (token.actions.githubusercontent.com)
# and assumes a role that can only push images to ECR and trigger a new
# deployment of the ECS service. Terraform plans and applies stay manual.

variable "github_oidc_repo_prefix" {
  description = "Repository identifier in GitHub's OIDC sub claim: 'owner@<owner-id>/repo@<repo-id>'. GitHub embedded the numeric IDs in 2025 to make the claim immune to owner/repo renames; the IDs are stable and not secret. The CI run logs the exact sub claim it presents (see the debug step in the deploy workflow) if this ever needs updating."
  type        = string
  default     = "repo:Pepert@17185529/Vocabula@1352346632"
}

resource "aws_iam_openid_connect_provider" "github" {
  url            = "https://token.actions.githubusercontent.com"
  client_id_list = ["sts.amazonaws.com"]

  # GitHub's two published signing-certificate thumbprints (the community
  # standard set; GitHub's docs list exactly these).
  thumbprint_list = [
    "6938fd4d98bab03faadb97b34396831e3780aea1",
    "1c58a3a8518e8759bf075b76b750d4f2df264fcd"
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
        "${var.github_oidc_repo_prefix}:ref:refs/heads/main",
        "${var.github_oidc_repo_prefix}:environment:production"
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
      # BatchGetImage: buildx inspects existing manifests when pushing
      # multiple tags, even though AWS's docs list it only for pulls.
      "ecr:BatchGetImage",
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
