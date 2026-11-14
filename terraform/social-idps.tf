# Social sign-in: Google (built-in), LinkedIn (generic OIDC) and Apple as
# identity providers on the Cognito user pool. Users clicking these buttons
# on the hosted UI (https://auth.vocabula.dev) authenticate against the
# provider and receive regular Cognito tokens; the API is unchanged.
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

variable "apple_client_id" {
  description = "Apple Services ID (e.g. dev.vocabula.auth). Must have Sign in with Apple enabled, with auth.vocabula.dev as domain and .../oauth2/idpresponse as return URL."
  type        = string
  sensitive   = true
}

variable "apple_team_id" {
  description = "Apple Developer 10-character Team ID (Membership page)."
  type        = string
  sensitive   = true
}

variable "apple_key_id" {
  description = "Key ID of the Sign in with Apple private key (.p8)."
  type        = string
  sensitive   = true
}

variable "apple_private_key" {
  description = "Contents of the Sign in with Apple .p8 private key, including the BEGIN/END PRIVATE KEY lines. Cognito signs Apple's short-lived client-secret JWT with it."
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

# Apple has no static client secret: the secret is an ES256-signed JWT with a
# maximum lifetime of six months. Cognito generates it from the .p8 private
# key on demand, so sign-in keeps working without manual rotation as long as
# the key stays valid. Apple's scopes are plain words ("email name"), not
# OpenID scopes, and Apple returns the user's name on the first sign-in only.
resource "aws_cognito_identity_provider" "apple" {
  user_pool_id  = aws_cognito_user_pool.vocabula.id
  provider_name = "SignInWithApple"
  provider_type = "SignInWithApple"

  provider_details = {
    client_id        = var.apple_client_id
    team_id          = var.apple_team_id
    key_id           = var.apple_key_id
    private_key      = var.apple_private_key
    authorize_scopes = "email name"
  }

  attribute_mapping = {
    email    = "email"
    username = "sub"
  }
}
