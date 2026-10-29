terraform {
  required_version = ">= 1.6"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }

  # Backend bucket and lock table are created once by terraform/bootstrap/.
  backend "s3" {
    bucket         = "vocabula-terraform-state-614084726535"
    key            = "vocabula/terraform.tfstate"
    region         = "eu-west-3"
    dynamodb_table = "vocabula-terraform-state-locks"
    encrypt        = true
  }
}

provider "aws" {
  region = var.aws_region
}

data "aws_caller_identity" "current" {}
