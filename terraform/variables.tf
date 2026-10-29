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

variable "allow_db_access_from" {
  description = "Your public IP as a CIDR (e.g. \"203.0.113.5/32\") to reach the RDS instance from your machine (DBeaver, psql, EF Core migrations). Keep empty (\"\") to keep the database unreachable from outside the VPC. The value lives in the gitignored prod.tfvars."
  type        = string
  default     = ""
}
