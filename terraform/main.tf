# 1. Setup the Provider
terraform {
  required_providers {
    digitalocean = {
      source  = "digitalocean/digitalocean"
      version = "~> 2.0"
    }
  }
}

# 2. Configure the API Token
provider "digitalocean" {
  token = var.my_api_key
}

# 3. Add your SSH Key so you can actually login
resource "digitalocean_ssh_key" "default" {
  name       = "my-terraform-key"
  public_key = file(var.public_key_path) # Reads the file from your local disk
}
# 4. Create the Droplet
resource "digitalocean_droplet" "web_server" {
  image  = "ubuntu-22-04-x64"
  name   = "monolith-prod"
  region = "nyc3"
  size   = "s-1vcpu-1gb"
  ssh_keys = [digitalocean_ssh_key.default.id]
  # 5. The "Manual" part: Auto-installing Docker & Nginx
  user_data = <<-EOF
              #!/bin/bash
              apt-get update
              apt-get install -y docker.io nginx
              systemctl start docker
              systemctl enable docker
              EOF
}

# 6. Firewall for security
resource "digitalocean_firewall" "web" {
  name        = "web-firewall"
  droplet_ids = [digitalocean_droplet.web_server.id]

  # SSH access
  inbound_rule {
    protocol         = "tcp"
    port_range       = "22"
    source_addresses = ["0.0.0.0/0", "::/0"]
  }

  # HTTP
  inbound_rule {
    protocol         = "tcp"
    port_range       = "80"
    source_addresses = ["0.0.0.0/0", "::/0"]
  }

  # HTTPS
  inbound_rule {
    protocol         = "tcp"
    port_range       = "443"
    source_addresses = ["0.0.0.0/0", "::/0"]
  }

  # Allow all outbound TCP
  outbound_rule {
    protocol              = "tcp"
    port_range            = "1-65535"
    destination_addresses = ["0.0.0.0/0", "::/0"]
  }

  # Allow all outbound UDP
  outbound_rule {
    protocol              = "udp"
    port_range            = "1-65535"
    destination_addresses = ["0.0.0.0/0", "::/0"]
  }

  # Allow ICMP (ping)
  outbound_rule {
    protocol              = "icmp"
    destination_addresses = ["0.0.0.0/0", "::/0"]
  }
}

# 7. Output the droplet IP
output "droplet_ip" {
  description = "The public IP address of the droplet"
  value       = digitalocean_droplet.web_server.ipv4_address
}