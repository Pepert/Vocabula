# Cognito user pool for the API's authentication.
#
# Settings replicate the manually-created pool (eu-west-3_pVJEZ5jFX), which is
# replaced by this one. Users are NOT managed here: create them with the AWS
# CLI (see docs/architecture/aws-deployment.md) so that passwords never enter
# Terraform state.

variable "cognito_domain_prefix" {
  description = "Prefix for the Cognito hosted UI domain (must be unique within the region's shared namespace; change it if apply fails with a domain conflict)."
  type        = string
  default     = "vocabula-prod"
}

resource "aws_cognito_user_pool" "vocabula" {
  name = "vocabula"

  # Email as username; email is verified automatically on sign-up.
  username_attributes      = ["email"]
  auto_verified_attributes = ["email"]

  password_policy {
    minimum_length                   = 12
    require_uppercase                = true
    require_lowercase                = true
    require_numbers                  = true
    require_symbols                  = false
    temporary_password_validity_days = 7
  }

  sign_in_policy {
    allowed_first_auth_factors = ["PASSWORD"]
  }

  # Self-service sign-up allowed, like the previous pool.
  admin_create_user_config {
    allow_admin_create_user_only = false
  }

  mfa_configuration = "OFF"

  account_recovery_setting {
    recovery_mechanism {
      name     = "verified_email"
      priority = 1
    }
  }

  email_configuration {
    email_sending_account = "COGNITO_DEFAULT"
  }

  verification_message_template {
    default_email_option = "CONFIRM_WITH_CODE"
  }
}

resource "aws_cognito_user_pool_client" "vocabula_app" {
  name         = "vocabula-app"
  user_pool_id = aws_cognito_user_pool.vocabula.id

  # No client secret: the consumers are public clients (browser app / local
  # development). Safe because tokens are only issued to the pool's users.
  generate_secret = false

  explicit_auth_flows = [
    "ALLOW_ADMIN_USER_PASSWORD_AUTH", # local testing via admin-initiate-auth
    "ALLOW_USER_SRP_AUTH",            # production sign-in (Secure Remote Password)
    "ALLOW_USER_AUTH",
    "ALLOW_REFRESH_TOKEN_AUTH"
  ]

  token_validity_units {
    access_token  = "minutes"
    id_token      = "minutes"
    refresh_token = "days"
  }

  access_token_validity  = 60
  id_token_validity      = 60
  refresh_token_validity = 5

  # OAuth2 flows for the hosted UI (used by the frontend later).
  allowed_oauth_flows_user_pool_client = true
  allowed_oauth_flows                  = ["code", "implicit"]
  allowed_oauth_scopes                 = ["email", "openid", "phone"]
  supported_identity_providers         = ["COGNITO"]

  callback_urls = [
    "http://localhost:5001",
    "http://localhost:5001/swagger/oauth2-redirect.html",
    "https://d84l1y8p4kdic.cloudfront.net"
  ]

  logout_urls = ["http://localhost:5001"]
}

resource "aws_cognito_user_pool_domain" "vocabula" {
  domain       = var.cognito_domain_prefix
  user_pool_id = aws_cognito_user_pool.vocabula.id
}
