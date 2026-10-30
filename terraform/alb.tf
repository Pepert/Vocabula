resource "aws_lb" "vocabula" {
  name               = "vocabula"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = aws_subnet.public[*].id

  tags = {
    name = "vocabula"
  }
}

resource "aws_lb_target_group" "vocabula_api" {
  name        = "vocabula-api"
  port        = 8080
  protocol    = "HTTP"
  target_type = "ip"
  vpc_id      = aws_vpc.vocabula.id

  health_check {
    enabled             = true
    path                = "/health"
    port                = "traffic-port"
    protocol            = "HTTP"
    matcher             = "200"
    interval            = 30
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }

  tags = {
    name = "vocabula-api"
  }
}

resource "aws_lb_listener" "https" {
  load_balancer_arn = aws_lb.vocabula.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = aws_acm_certificate_validation.api.certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.vocabula_api.arn
  }
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.vocabula.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type = "redirect"

    redirect {
      port        = "443"
      protocol    = "HTTPS"
      status_code = "HTTP_301"
    }
  }
}

resource "aws_ecs_service" "vocabula_api" {
  name            = "vocabula-api"
  cluster         = aws_ecs_cluster.vocabula.id
  task_definition = aws_ecs_task_definition.vocabula_api.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.public[*].id
    security_groups  = [aws_security_group.ecs_task.id]
    assign_public_ip = true
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.vocabula_api.arn
    container_name   = "vocabula-api"
    container_port   = 8080
  }

  # Do not deregister targets until the new task passes its health check.
  deployment_minimum_healthy_percent = 100
  deployment_maximum_percent         = 200

  tags = {
    name = "vocabula-api"
  }
}
