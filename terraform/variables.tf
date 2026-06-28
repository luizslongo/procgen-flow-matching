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

# Provided at apply time (TF_VAR_db_password or a secrets backend). No default in
# source: keeping credentials out of version control. Marked sensitive so it is
# redacted from Terraform plan/apply output.
variable "db_password" {
  description = "Master password for the RDS Postgres instance"
  type        = string
  sensitive   = true
}
