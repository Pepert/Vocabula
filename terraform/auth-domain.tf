# Custom domain for the Cognito hosted UI: auth.vocabula.dev
#
# Cognito serves custom domains through a CloudFront distribution, and
# CloudFront only trusts certificates issued in us-east-1 — hence the
# aliased provider in tags.tf for the ACM certificate, while the user pool
# itself stays in eu-west-3. DNS validation happens in the vocabula.dev
# Route 53 zone (Route 53 is global, so the default provider can manage it).
#
# The prefixed domain (vocabula-prod.auth.eu-west-3.amazoncognito.com,
# cognito.tf) keeps working alongside this one: sessions signed in through
# it stay valid. After applying, the external identity providers must
# whitelist the new redirect URI manually (see aws-deployment.md).

resource "aws_acm_certificate" "auth" {
  provider          = aws.us_east_1
  domain_name       = "auth.${var.domain_name}"
  validation_method = "DNS"

  tags = {
    name = "auth.${var.domain_name}"
  }
}

resource "aws_route53_record" "auth_certificate_validation" {
  for_each = {
    for dvo in aws_acm_certificate.auth.domain_validation_options : dvo.domain_name => {
      name   = dvo.resource_record_name
      record = dvo.resource_record_value
      type   = dvo.resource_record_type
    }
  }

  allow_overwrite = true
  name            = each.value.name
  records         = [each.value.record]
  ttl             = 60
  type            = each.value.type
  zone_id         = aws_route53_zone.vocabula.zone_id
}

resource "aws_acm_certificate_validation" "auth" {
  provider                = aws.us_east_1
  certificate_arn         = aws_acm_certificate.auth.arn
  validation_record_fqdns = [for r in aws_route53_record.auth_certificate_validation : r.fqdn]
}

resource "aws_cognito_user_pool_domain" "auth" {
  user_pool_id    = aws_cognito_user_pool.vocabula.id
  domain          = "auth.${var.domain_name}"
  certificate_arn = aws_acm_certificate_validation.auth.certificate_arn
}

# Route 53 alias records pointing at the CloudFront distribution Cognito
# provisions behind the custom domain (both IPv4 and IPv6, as recommended).
resource "aws_route53_record" "auth" {
  name    = "auth.${var.domain_name}"
  type    = "A"
  zone_id = aws_route53_zone.vocabula.zone_id

  alias {
    name                   = aws_cognito_user_pool_domain.auth.cloudfront_distribution
    zone_id                = aws_cognito_user_pool_domain.auth.cloudfront_distribution_zone_id
    evaluate_target_health = false
  }
}

resource "aws_route53_record" "auth_ipv6" {
  name    = "auth.${var.domain_name}"
  type    = "AAAA"
  zone_id = aws_route53_zone.vocabula.zone_id

  alias {
    name                   = aws_cognito_user_pool_domain.auth.cloudfront_distribution
    zone_id                = aws_cognito_user_pool_domain.auth.cloudfront_distribution_zone_id
    evaluate_target_health = false
  }
}
