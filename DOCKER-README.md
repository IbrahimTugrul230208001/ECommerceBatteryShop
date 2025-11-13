# Docker Setup for ECommerce Battery Shop

This document explains how to run the application using Docker and Docker Compose.

## Prerequisites

- [Docker](https://www.docker.com/get-started) (20.10 or higher)
- [Docker Compose](https://docs.docker.com/compose/install/) (2.0 or higher)

## Quick Start (Local Development)

### Windows
```cmd
start-local.bat
```

### Linux/Mac
```bash
chmod +x start-local.sh
./start-local.sh
```

## Manual Setup

### 1. Configure Environment Variables

Create a `.env` file from the example:
```bash
cp .env.example .env
```

Edit `.env` and update the database password:
```env
DB_PASSWORD=YourSecurePassword123!
```

### 2. Build and Run

Start all services:
```bash
docker-compose up --build -d
```

This will:
- Build the .NET application
- Start PostgreSQL database
- Run migrations (if configured)
- Start the web application on http://localhost:5000

### 3. View Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f web
docker-compose logs -f postgres
```

### 4. Stop Services

```bash
docker-compose down
```

To also remove volumes (?? This will delete all data):
```bash
docker-compose down -v
```

## Production Deployment (Azure)

### Using Azure Container Registry (ACR)

1. **Build and push to ACR:**
```bash
# Login to Azure
az login

# Create ACR if needed
az acr create --resource-group myResourceGroup --name myregistry --sku Basic

# Login to ACR
az acr login --name myregistry

# Build and push
docker build -t myregistry.azurecr.io/ecommerce-web:latest .
docker push myregistry.azurecr.io/ecommerce-web:latest
```

2. **Deploy to Azure Container Instances:**
```bash
az container create \
  --resource-group myResourceGroup \
  --name ecommerce-web \
  --image myregistry.azurecr.io/ecommerce-web:latest \
  --dns-name-label ecommerce-battery-shop \
  --ports 80 443 \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__PostgreSQL="Host=YOUR_AZURE_POSTGRES.postgres.database.azure.com;..."
```

### Using Docker Compose on Azure VM

1. **SSH into your Azure VM**
2. **Clone your repository**
3. **Create production `.env` file:**
```bash
cat > .env << EOF
AZURE_POSTGRES_CONNECTION_STRING="Host=your-server.postgres.database.azure.com;Port=5432;Database=ecommerce_db;Username=adminuser@your-server;Password=YourPassword;SslMode=Require"
EOF
```

4. **Run with production compose file:**
```bash
docker-compose -f docker-compose.prod.yml up -d
```

## Database Migrations

### Run migrations manually:
```bash
docker-compose exec web dotnet ef database update
```

### Or connect to the database directly:
```bash
docker-compose exec postgres psql -U postgres -d ecommerce_db
```

## Troubleshooting

### Container won't start
```bash
# Check logs
docker-compose logs web

# Check if ports are already in use
netstat -ano | findstr :5000
```

### Database connection issues
```bash
# Verify PostgreSQL is running
docker-compose ps postgres

# Check database logs
docker-compose logs postgres

# Test connection
docker-compose exec postgres pg_isready -U postgres
```

### Reset everything
```bash
# Stop and remove everything including volumes
docker-compose down -v

# Rebuild from scratch
docker-compose up --build
```

## Health Checks

- Web App: http://localhost:5000/health
- Database: `docker-compose exec postgres pg_isready`

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `DB_PASSWORD` | PostgreSQL password | YourSecurePassword123! |
| `POSTGRES_DB` | Database name | ecommerce_db |
| `POSTGRES_USER` | Database user | postgres |
| `ASPNETCORE_ENVIRONMENT` | Application environment | Production |
| `WEB_PORT` | Web application port | 5000 |
| `DB_PORT` | PostgreSQL port | 5432 |

## Best Practices

1. **Always use `.env` for sensitive data** - Never commit `.env` to git
2. **Use strong passwords** in production
3. **Enable SSL/TLS** for PostgreSQL in production
4. **Regular backups** of PostgreSQL data volume
5. **Monitor container health** using health checks
6. **Use secrets management** for production (Azure Key Vault, Docker Secrets)

## Backup and Restore

### Backup Database
```bash
docker-compose exec postgres pg_dump -U postgres ecommerce_db > backup.sql
```

### Restore Database
```bash
docker-compose exec -T postgres psql -U postgres ecommerce_db < backup.sql
```

## Useful Commands

```bash
# Rebuild a specific service
docker-compose build web

# Restart a service
docker-compose restart web

# View resource usage
docker stats

# Clean up unused images
docker system prune -a

# Access container shell
docker-compose exec web bash
docker-compose exec postgres psql -U postgres
```

## Support

For issues or questions, please refer to the main project README or create an issue in the repository.
