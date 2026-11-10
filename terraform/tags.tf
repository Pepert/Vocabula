# Tags applied to every resource created by this configuration.
#
# The AWS provider's default_tags merge these into each resource's tags,
# on top of any per-resource tags (e.g. name).

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      project     = "vocabula"
      environment = "prod"
    }
  }
}

# Aliased provider for the us-east-1 region: Cognito custom domains are
# served by CloudFront, which only trusts certificates from us-east-1
# (see auth-domain.tf).
provider "aws" {
  alias  = "us_east_1"
  region = "us-east-1"

  default_tags {
    tags = {
      project     = "vocabula"
      environment = "prod"
    }
  }
}
