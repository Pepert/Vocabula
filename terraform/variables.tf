variable "aws_region" {
  description = "AWS region hosting the whole stack."
  type        = string
  default     = "eu-west-3"
}

variable "mistral_api_key" {
  description = "Mistral API key. Stored in Secrets Manager and injected into the container by ECS."
  type        = string
  sensitive   = true
}

variable "db_username" {
  description = "RDS PostgreSQL master username."
  type        = string
  default     = "vocabula"
}

variable "db_password" {
  description = "RDS PostgreSQL master password. Stored in Secrets Manager."
  type        = string
  sensitive   = true
}

variable "db_name" {
  description = "RDS PostgreSQL database name."
  type        = string
  default     = "vocabula"
}

variable "image_tag" {
  description = "Tag of the container image in ECR to deploy."
  type        = string
  default     = "latest"
}

variable "allow_migrations_from" {
  description = "Set to your public IP as a CIDR (e.g. \"203.0.113.5/32\") to temporarily expose RDS for running EF Core migrations from your machine. Keep empty (\"\") in normal operation."
  type        = string
  default     = ""
}
