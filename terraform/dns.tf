# DNS and TLS for the API.
#
# vocabula.dev is registered at Porkbun; DNS is delegated to this Route 53
# zone (set the zone's nameservers at the registrar, see the deployment doc).
# The ACM certificate is issued in the ALB's region with DNS validation, and
# api.<domain> points at the load balancer.

variable "domain_name" {
  description = "Registered domain name. DNS is hosted in Route 53."
  type        = string
  default     = "vocabula.dev"
}

resource "aws_route53_zone" "vocabula" {
  name = var.domain_name

  tags = {
    name = var.domain_name
  }
}

resource "aws_acm_certificate" "api" {
  domain_name       = "api.${var.domain_name}"
  validation_method = "DNS"

  tags = {
    name = "api.${var.domain_name}"
  }
}

resource "aws_route53_record" "api_certificate_validation" {
  for_each = {
    for dvo in aws_acm_certificate.api.domain_validation_options : dvo.domain_name => {
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

resource "aws_acm_certificate_validation" "api" {
  certificate_arn         = aws_acm_certificate.api.arn
  validation_record_fqdns = [for r in aws_route53_record.api_certificate_validation : r.fqdn]
}

resource "aws_route53_record" "api" {
  name    = "api.${var.domain_name}"
  type    = "A"
  zone_id = aws_route53_zone.vocabula.zone_id

  alias {
    name                   = aws_lb.vocabula.dns_name
    zone_id                = aws_lb.vocabula.zone_id
    evaluate_target_health = true
  }
}
