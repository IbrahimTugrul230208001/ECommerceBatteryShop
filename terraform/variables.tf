variable "my_api_key" { 
  description = "DigitalOcean API Key"
  type      = string
  sensitive = true
}
variable "public_key_path" {
  description = "Path to the SSH public key"
  default     = "~/.ssh/id_rsa.pub" # Adjust this to your actual path
}