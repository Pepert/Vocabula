terraform {
  required_version = ">= 1.11"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }

  # Backend bucket is created once by terraform/bootstrap/.
  # State locking uses the S3-native lockfile (Terraform >= 1.11);
  # no DynamoDB lock table is needed.
  backend "s3" {
    bucket       = "vocabula-terraform-state-614084726535"
    key          = "vocabula/terraform.tfstate"
    region       = "eu-west-3"
    use_lockfile = true
    encrypt      = true
  }
}

data "aws_caller_identity" "current" {}
