#!/bin/bash

# Build and Run Script for Local Development

echo "?? Starting ECommerce Battery Shop..."

# Create .env file if it doesn't exist
if [ ! -f .env ]; then
  echo "?? Creating .env file from .env.example..."
    cp .env.example .env
    echo "??  Please update .env with your actual passwords!"
fi

# Stop and remove existing containers
echo "?? Stopping existing containers..."
docker-compose down

# Build and start containers
echo "?? Building and starting containers..."
docker-compose up --build -d

# Wait for services to be healthy
echo "? Waiting for services to be ready..."
sleep 10

# Show logs
echo "?? Container Status:"
docker-compose ps

echo ""
echo "? Application is running!"
echo "?? Web App: http://localhost:5000"
echo "???  PostgreSQL: localhost:5432"
echo ""
echo "?? View logs with: docker-compose logs -f"
echo "?? Stop with: docker-compose down"
