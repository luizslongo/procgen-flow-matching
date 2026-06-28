# Input variables for the PCG Flow Matching infrastructure.
# This Terraform is SCANNED by the DevSecOps pipeline (Checkov / tfsec), not
# applied. It models a realistic AWS deployment of the API + Postgres database.

variable "aws_region" {
  description = "AWS region to deploy into"
  type        = string
  default     = "us-east-1"
}

variable "project_name" {
  description = "Name prefix for created resources"
  type        = string
  default     = "pcg-flow-matching"
}

variable "container_image" {
  description = "Container image URI for the API service"
  type        = string
  default     = "pcg-flow-matching/api:latest"
}

# SECURITY NOTE (intentional finding): a default database password in source is
# flagged by Secret Detection and IaC scanning. Real deployments pass this via a
# secrets manager and leave no default. Documented in the report.
variable "db_password" {
  description = "Master password for the RDS Postgres instance"
  type        = string
  default     = "Pcg7xKq2Mv9Rtz4Wn1Lb"
}
