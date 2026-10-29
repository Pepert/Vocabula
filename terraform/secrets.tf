# One JSON secret holding every sensitive value the container needs.
# ECS injects the individual keys as environment variables at container start,
# so the application itself never talks to Secrets Manager.

resource "aws_secretsmanager_secret" "vocabula" {
  name = "vocabula/prod"

  tags = {
    Name = "vocabula-prod"
  }
}

resource "aws_secretsmanager_secret_version" "vocabula" {
  secret_id = aws_secretsmanager_secret.vocabula.id

  secret_string = jsonencode({
    MISTRAL_API_KEY               = var.mistral_api_key
    ConnectionStrings__VocabulaDb = "Host=${aws_db_instance.vocabula.address};Port=${aws_db_instance.vocabula.port};Database=${var.db_name};Username=${var.db_username};Password=${var.db_password}"
  })
}
