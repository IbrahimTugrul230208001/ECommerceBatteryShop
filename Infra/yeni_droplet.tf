terraform {
  required_version = ">= 1.0.0"
  required_providers {
    digitalocean = {
      source  = "digitalocean/digitalocean"
      version = "~> 2.0"
    }
  }
}

# Variable configuration for tokens and deployment flexibility
variable "do_token" {
  type        = string
  description = "DigitalOcean API Personal Access Token"
  sensitive   = true
}

variable "ssh_key_name" {
  type        = string
  description = "The name of an existing SSH key on your DigitalOcean account"
  default     = "LinuxSSH"
}

provider "digitalocean" {
  token = var.do_token
}

# Looks up your EXISTING SSH key on DigitalOcean (instead of creating a new one)
data "digitalocean_ssh_key" "deployer" {
  name = var.ssh_key_name
}

# Provision the $6/month Droplet
resource "digitalocean_droplet" "web" {
  image      = "ubuntu-24-04-x64" # Standard LTS Release
  name       = "pilbataryamarketim-monolith-prod"
  region     = "FRA1"        # frankfurt
  size       = "s-1vcpu-1gb" # The $6.00/mo tier instance size
  backups    = false
  monitoring = true
  ipv6       = true

  # Associates the existing SSH key to prevent password generation
  ssh_keys = [
    data.digitalocean_ssh_key.deployer.id
  ]

  tags = ["production", "monolith"]
}

# Output variables to view resource attributes upon successful execution
output "droplet_public_ipv4" {
  value       = digitalocean_droplet.web.ipv4_address
  description = "The public IPv4 address of the newly provisioned droplet."
}
