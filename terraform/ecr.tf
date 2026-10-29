resource "aws_ecr_repository" "vocabula_api" {
  name = "vocabula-api"

  image_tag_mutability = "MUTABLE"
  force_delete         = true

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = {
    name = "vocabula-api"
  }
}

resource "aws_ecr_lifecycle_policy" "vocabula_api" {
  repository = aws_ecr_repository.vocabula_api.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep the 10 most recent images"
        selection = {
          tagStatus   = "tagged"
          tagPrefixes = ["latest", "v"]
          countType   = "imageCountMoreThan"
          countNumber = 10
        }
        action = {
          type = "expire"
        }
      }
    ]
  })
}
