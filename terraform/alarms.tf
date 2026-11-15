# First-line alerting for a solo operator. One SNS email topic carries three
# CloudWatch alarms (API 5xx, dead ECS service, low database storage) and one
# AWS budget guard; AWS emails the alert address directly, no phone app
# needed. Alarms are intentionally coarse: they answer "is the API reachable,
# is the task alive, is the disk filling up" - the three ways a store launch
# dies quietly.

variable "alert_email" {
  description = "Email address receiving CloudWatch and budget alerts. Confirm the AWS subscription email once after the first apply."
  type        = string
}

resource "aws_sns_topic" "alerts" {
  name = "vocabula-alerts"

  tags = {
    name = "vocabula-alerts"
  }
}

resource "aws_sns_topic_subscription" "alerts_email" {
  topic_arn = aws_sns_topic.alerts.arn
  protocol  = "email"
  endpoint  = var.alert_email
}

# Any 5xx from the API within a 5-minute window. The app itself has no
# in-memory failure path, so a 5xx means the task crashed, was OOM-killed,
# or returned an error while Mistral/RDS was unreachable.
resource "aws_cloudwatch_metric_alarm" "api_5xx" {
  alarm_name          = "vocabula-api-5xx"
  alarm_description   = "The API returned 5xx responses in the last 5 minutes."
  namespace           = "AWS/ApplicationELB"
  metric_name         = "HTTPCode_Target_5XX_Count"
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  comparison_operator = "GreaterThanThreshold"
  threshold           = 0
  treat_missing_data  = "notBreaching"
  alarm_actions       = [aws_sns_topic.alerts.arn]
  ok_actions          = [aws_sns_topic.alerts.arn]

  dimensions = {
    # ApplicationELB dimensions use the ARN suffix ("app/vocabula/<id>").
    LoadBalancer = split("loadbalancer/", aws_lb.vocabula.arn)[1]
  }
}

# No running task: the service is down. Missing metric data counts as
# breaching so a deleted or never-started service still trips the alarm.
# Deploys keep at least one healthy task (deployment_minimum_healthy_percent
# = 100), so zero tasks is never a deploy artifact. Two periods = 10 minutes
# of downtime before the email lands.
resource "aws_cloudwatch_metric_alarm" "api_task_count" {
  alarm_name          = "vocabula-api-no-running-task"
  alarm_description   = "The vocabula-api ECS service has no running task."
  namespace           = "AWS/ECS"
  metric_name         = "RunningTaskCount"
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 2
  comparison_operator = "LessThanThreshold"
  threshold           = 0.5
  treat_missing_data  = "breaching"
  alarm_actions       = [aws_sns_topic.alerts.arn]
  ok_actions          = [aws_sns_topic.alerts.arn]

  dimensions = {
    ClusterName = aws_ecs_cluster.vocabula.name
    ServiceName = aws_ecs_service.vocabula_api.name
  }
}

# The 20 GB database has under 2 GB free; long before the writers fail we
# want to either clean up or grow the volume.
resource "aws_cloudwatch_metric_alarm" "rds_storage_low" {
  alarm_name          = "vocabula-rds-storage-low"
  alarm_description   = "Database free storage under 2 GB of the 20 GB volume."
  namespace           = "AWS/RDS"
  metric_name         = "FreeStorageSpace"
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 2
  comparison_operator = "LessThanThreshold"
  threshold           = 2000000000
  treat_missing_data  = "notBreaching"
  alarm_actions       = [aws_sns_topic.alerts.arn]
  ok_actions          = [aws_sns_topic.alerts.arn]

  dimensions = {
    DBInstanceIdentifier = aws_db_instance.vocabula.identifier
  }
}

# Budget guard. Normal monthly AWS spend sits around USD 40-50; a runaway
# (open sign-ups hammering Mistral does not appear here, but a stuck deploy
# loop, forgotten resources, or a surprise in ECR/CloudWatch volume would).
# Alerts at 60% (covering the normal ceiling + margin) and 80% (anomaly),
# plus a mid-month forecasted alert so the surprise arrives early.
resource "aws_budgets_budget" "monthly" {
  name         = "vocabula-monthly"
  budget_type  = "COST"
  limit_amount = "100"
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 60
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = [var.alert_email]
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 80
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = [var.alert_email]
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 80
    threshold_type             = "PERCENTAGE"
    notification_type          = "FORECASTED"
    subscriber_email_addresses = [var.alert_email]
  }

  tags = {
    name = "vocabula-monthly"
  }
}
