resource "aws_db_subnet_group" "vocabula" {
  name       = "vocabula"
  subnet_ids = aws_subnet.private[*].id

  tags = {
    name = "vocabula"
  }
}

# PostgreSQL instance holding all application data. Protected against
# accidental destruction (see the lifecycle block below).
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
  publicly_accessible    = var.allow_db_access_from != ""

  # Keep the instance running around the clock; the application is always-on.
  # Quiet backup window so backup I/O never lands in (European) prime time,
  # and the maintenance window is pinned just after it (RDS rejects windows
  # that overlap; AWS had auto-assigned one inside this backup slot). Sunday
  # 04:30 UTC = 05:30 Paris in winter, 06:30 in summer; single-AZ patching
  # can reboot the instance there, so it belongs at the deadest hour.
  backup_retention_period   = 7
  backup_window             = "03:00-04:00"
  maintenance_window        = "sun:04:30-sun:05:30"
  delete_automated_backups  = false
  skip_final_snapshot       = false
  final_snapshot_identifier = "vocabula-final"

  # The database holds user accounts, vocabulary and stories: deleting it
  # is never an accident we want Terraform to accept. Remove this block
  # deliberately (and destroy the snapshot by hand) if it ever must go.
  lifecycle {
    prevent_destroy = true
  }

  tags = {
    name = "vocabula"
  }
}
