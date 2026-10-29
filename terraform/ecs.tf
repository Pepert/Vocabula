resource "aws_ecs_cluster" "vocabula" {
  name = "vocabula"

  tags = {
    Name = "vocabula"
  }
}

# Execution role: used by the ECS agent to pull the image, write logs and
# read the Secrets Manager secret before starting the container.
resource "aws_iam_role" "task_execution" {
  name = "vocabula-task-execution"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "task_execution" {
  role       = aws_iam_role.task_execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

resource "aws_iam_role_policy" "task_execution_secrets" {
  name = "read-vocabula-secret"
  role = aws_iam_role.task_execution.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "secretsmanager:DescribeSecret",
          "secretsmanager:GetSecretValue"
        ]
        Resource = [
          aws_secretsmanager_secret.vocabula.arn,
          "${aws_secretsmanager_secret.vocabula.arn}:*"
        ]
      }
    ]
  })
}

# Task role: what the application itself can do. Empty for now; add policies
# here later if the app ever needs AWS APIs (e.g. S3 uploads).
resource "aws_iam_role" "task" {
  name = "vocabula-task"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })
}

resource "aws_cloudwatch_log_group" "vocabula_api" {
  name              = "/ecs/vocabula-api"
  retention_in_days = 14
}

resource "aws_ecs_task_definition" "vocabula_api" {
  family                   = "vocabula-api"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = 256
  memory                   = 512
  runtime_platform {
    cpu_architecture = "ARM64"
  }

  execution_role_arn = aws_iam_role.task_execution.arn
  task_role_arn      = aws_iam_role.task.arn

  container_definitions = jsonencode([
    {
      name      = "vocabula-api"
      image     = "${aws_ecr_repository.vocabula_api.repository_url}:${var.image_tag}"
      essential = true

      portMappings = [
        {
          containerPort = 8080
          protocol      = "tcp"
        }
      ]

      environment = [
        {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = "Production"
        },
        {
          name  = "AUTHENTICATION__COGNITO__AUTHORITY"
          value = "https://cognito-idp.${var.aws_region}.amazonaws.com/${aws_cognito_user_pool.vocabula.id}"
        },
        {
          name  = "AUTHENTICATION__COGNITO__APPCLIENTID"
          value = aws_cognito_user_pool_client.vocabula_app.id
        }
      ]

      secrets = [
        {
          name      = "MISTRAL_API_KEY"
          valueFrom = "${aws_secretsmanager_secret.vocabula.arn}:MISTRAL_API_KEY::"
        },
        {
          name      = "ConnectionStrings__VocabulaDb"
          valueFrom = "${aws_secretsmanager_secret.vocabula.arn}:ConnectionStrings__VocabulaDb::"
        }
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.vocabula_api.name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "vocabula-api"
        }
      }
    }
  ])

  tags = {
    Name = "vocabula-api"
  }
}
