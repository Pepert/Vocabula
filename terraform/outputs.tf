output "alb_dns_name" {
  description = "Public DNS name of the load balancer. The API is reachable at http://<alb_dns_name>/."
  value       = aws_lb.vocabula.dns_name
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
