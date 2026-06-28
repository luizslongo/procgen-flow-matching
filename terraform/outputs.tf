# Outputs for the PCG Flow Matching infrastructure.

output "ecr_repository_url" {
  description = "URL of the ECR repository for the API image"
  value       = aws_ecr_repository.api.repository_url
}

output "db_endpoint" {
  description = "Connection endpoint of the RDS Postgres instance"
  value       = aws_db_instance.pcg.address
}

output "ecs_cluster_name" {
  description = "Name of the ECS cluster running the API service"
  value       = aws_ecs_cluster.main.name
}
