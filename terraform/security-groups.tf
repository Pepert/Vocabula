resource "aws_security_group" "alb" {
  name_prefix = "vocabula-alb-"
  vpc_id      = aws_vpc.vocabula.id

  ingress {
    description = "HTTP from anywhere"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "HTTPS from anywhere (for a future ACM certificate)"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    description = "All outbound"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    name = "vocabula-alb"
  }
}

resource "aws_security_group" "ecs_task" {
  name_prefix = "vocabula-task-"
  vpc_id      = aws_vpc.vocabula.id

  ingress {
    description     = "App port from the load balancer only"
    from_port       = 8080
    to_port         = 8080
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }

  egress {
    description = "All outbound (image pulls, Mistral API, Cognito JWKS)"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    name = "vocabula-task"
  }
}

resource "aws_security_group" "rds" {
  name_prefix = "vocabula-rds-"
  vpc_id      = aws_vpc.vocabula.id

  ingress {
    description     = "PostgreSQL from the ECS task"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.ecs_task.id]
  }

  dynamic "ingress" {
    for_each = var.allow_migrations_from != "" ? [var.allow_migrations_from] : []
    content {
      description = "Temporary access for running EF Core migrations from the developer machine"
      from_port   = 5432
      to_port     = 5432
      protocol    = "tcp"
      cidr_blocks = [ingress.value]
    }
  }

  egress {
    description = "All outbound"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    name = "vocabula-rds"
  }
}
