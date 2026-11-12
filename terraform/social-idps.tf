# Social sign-in: Google (built-in) and LinkedIn (generic OIDC) as identity
# providers on the Cognito user pool. Users clicking these buttons on the
# hosted UI (https://auth.vocabula.dev)
# authenticate against the provider and receive regular Cognito tokens; the
# API is unchanged.
#
# LinkedIn publishes its OIDC discovery document at
# https://www.linkedin.com/oauth/.well-known/openid-configuration, so the
# configured issuer must be https://www.linkedin.com/oauth (Cognito derives
# the well-known path from it; the root path returns 404).

variable "google_client_id" {
  description = "Google Cloud OAuth 2.0 client ID. Must have the hosted UI domain as authorized origin and .../oauth2/idpresponse as redirect URI."
  type        = string
  sensitive   = true
}

variable "google_client_secret" {
  description = "Google Cloud OAuth 2.0 client secret."
  type        = string
  sensitive   = true
}

variable "linkedin_client_id" {
  description = "LinkedIn app client ID. The app must have the 'Sign In with LinkedIn using OpenID Connect' product and the idpresponse redirect URI."
  type        = string
  sensitive   = true
}

variable "linkedin_client_secret" {
  description = "LinkedIn app primary client secret."
  type        = string
  sensitive   = true
}

resource "aws_cognito_identity_provider" "google" {
  user_pool_id  = aws_cognito_user_pool.vocabula.id
  provider_name = "Google"
  # Google is a built-in Cognito provider type, not a generic OIDC one.
  provider_type = "Google"

  # Cognito auto-populates the Google endpoints and defaults when the
  # provider is created; declaring them here keeps Terraform from planning
  # their removal.
  provider_details = {
    client_id                     = var.google_client_id
    client_secret                 = var.google_client_secret
    authorize_scopes              = "openid email profile"
    oidc_issuer                   = "https://accounts.google.com"
    authorize_url                 = "https://accounts.google.com/o/oauth2/v2/auth"
    token_url                     = "https://www.googleapis.com/oauth2/v4/token"
    attributes_url                = "https://people.googleapis.com/v1/people/me?personFields="
    attributes_url_add_attributes = "true"
    token_request_method          = "POST"
  }

  attribute_mapping = {
    email    = "email"
    username = "sub"
  }
}

resource "aws_cognito_identity_provider" "linkedin" {
  user_pool_id  = aws_cognito_user_pool.vocabula.id
  provider_name = "LinkedIn"
  provider_type = "OIDC"

  provider_details = {
    client_id                     = var.linkedin_client_id
    client_secret                 = var.linkedin_client_secret
    authorize_scopes              = "openid email profile"
    oidc_issuer                   = "https://www.linkedin.com/oauth"
    attributes_request_method     = "GET"
    attributes_url_add_attributes = "false"
  }

  attribute_mapping = {
    email    = "email"
    username = "sub"
  }
}
