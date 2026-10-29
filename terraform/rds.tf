resource "aws_db_subnet_group" "vocabula" {
  name       = "vocabula"
  subnet_ids = aws_subnet.private[*].id

  tags = {
    name = "vocabula"
  }
}

# NOTE: skip_final_snapshot = true means `terraform destroy` deletes the
# database WITHOUT a final snapshot. Acceptable for the MVP; change this before
# the database holds data you care about.
resource "aws_db_instance" "vocabula" {
  identifier = "vocabula"

  engine            = "postgres"
  engine_version    = "17"
  instance_class    = "db.t4g.micro"
  allocated_storage = 20
  storage_type      = "gp3"
  db_name           = var.db_name
  username          = var.db_username
  password          = var.db_password

  db_subnet_group_name   = aws_db_subnet_group.vocabula.name
  vpc_security_group_ids = [aws_security_group.rds.id]
  publicly_accessible    = var.allow_migrations_from != ""

  # Keep the instance running around the clock; the application is always-on.
  backup_retention_period  = 7
  delete_automated_backups = true
  skip_final_snapshot      = true

  tags = {
    name = "vocabula"
  }
}
