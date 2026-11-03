# Social sign-in: Google (built-in) and LinkedIn (generic OIDC) as identity
# providers on the Cognito user pool. Users clicking these buttons on the
# hosted UI (https://vocabula-prod.auth.eu-west-3.amazoncognito.com)
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

  provider_details = {
    client_id        = var.google_client_id
    client_secret    = var.google_client_secret
    authorize_scopes = "openid email profile"
  }

  attribute_mapping = {
    email = "email"
  }
}

resource "aws_cognito_identity_provider" "linkedin" {
  user_pool_id  = aws_cognito_user_pool.vocabula.id
  provider_name = "LinkedIn"
  provider_type = "OIDC"

  provider_details = {
    client_id                 = var.linkedin_client_id
    client_secret             = var.linkedin_client_secret
    authorize_scopes          = "openid email profile"
    oidc_issuer               = "https://www.linkedin.com/oauth"
    attributes_request_method = "GET"
  }

  attribute_mapping = {
    email = "email"
  }
}
