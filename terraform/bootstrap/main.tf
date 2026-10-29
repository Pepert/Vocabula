# One-time bootstrap for the Terraform state backend.
#
# Run this ONCE from your machine (see docs/architecture/aws-deployment.md).
# It creates the S3 bucket that stores the main configuration's state and the
# DynamoDB table that locks it against concurrent runs. The main configuration
# in the parent directory references these exact names in its backend block.

terraform {
  required_version = ">= 1.6"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      project     = "vocabula"
      environment = "prod"
    }
  }
}

data "aws_caller_identity" "current" {}

variable "aws_region" {
  description = "AWS region for the state bucket."
  type        = string
  default     = "eu-west-3"
}

resource "aws_s3_bucket" "state" {
  bucket = "vocabula-terraform-state-${data.aws_caller_identity.current.account_id}"

  tags = {
    Name = "vocabula-terraform-state"
  }
}

resource "aws_s3_bucket_versioning" "state" {
  bucket = aws_s3_bucket.state.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "state" {
  bucket = aws_s3_bucket.state.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "state" {
  bucket = aws_s3_bucket.state.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_dynamodb_table" "locks" {
  name         = "vocabula-terraform-state-locks"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "LockID"

  attribute {
    name = "LockID"
    type = "S"
  }

  tags = {
    Name = "vocabula-terraform-state-locks"
  }
}

output "state_bucket" {
  value = aws_s3_bucket.state.id
}

output "locks_table" {
  value = aws_dynamodb_table.locks.name
}
