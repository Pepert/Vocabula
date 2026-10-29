# Tags applied to every resource created by this configuration.
#
# The AWS provider's default_tags merge these into each resource's tags,
# on top of any per-resource tags (e.g. Name).

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      project     = "vocabula"
      environment = "prod"
    }
  }
}
