output "alb_dns_name" {
  description = "Public DNS name of the load balancer. Use api_url in production instead."
  value       = aws_lb.vocabula.dns_name
}

output "api_url" {
  description = "Stable, HTTPS API endpoint served via the load balancer."
  value       = "https://api.${var.domain_name}"
}

output "domain_nameservers" {
  description = "Nameservers to set at the registrar (Porkbun) for the Route 53 zone."
  value       = aws_route53_zone.vocabula.name_servers
}

output "ecr_repository_url" {
  description = "ECR repository to push the container image to."
  value       = aws_ecr_repository.vocabula_api.repository_url
}

output "rds_address" {
  description = "RDS PostgreSQL endpoint (host only, inside the VPC)."
  value       = aws_db_instance.vocabula.address
}

output "secrets_manager_arn" {
  description = "Secrets Manager secret holding the application secrets."
  value       = aws_secretsmanager_secret.vocabula.arn
}

output "cognito_user_pool_id" {
  description = "Cognito user pool ID used by the API."
  value       = aws_cognito_user_pool.vocabula.id
}

output "cognito_authority" {
  description = "Cognito authority URL (Authentication:Cognito:Authority) the API is configured with."
  value       = "https://cognito-idp.${var.aws_region}.amazonaws.com/${aws_cognito_user_pool.vocabula.id}"
}

output "cognito_app_client_id" {
  description = "Cognito app client ID (Authentication:Cognito:AppClientId) the API is configured with."
  value       = aws_cognito_user_pool_client.vocabula_app.id
}

output "cognito_hosted_ui_domain" {
  description = "Cognito hosted UI domain (for the frontend's OAuth flows)."
  value       = aws_cognito_user_pool_domain.vocabula.domain
}
