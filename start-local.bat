@echo off
REM Build and Run Script for Local Development (Windows)

echo ?? Starting ECommerce Battery Shop...

REM Create .env file if it doesn't exist
if not exist .env (
    echo ?? Creating .env file from .env.example...
    copy .env.example .env
    echo ??  Please update .env with your actual passwords!
)

REM Stop and remove existing containers
echo ?? Stopping existing containers...
docker-compose down

REM Build and start containers
echo ?? Building and starting containers...
docker-compose up --build -d

REM Wait for services to be ready
echo ? Waiting for services to be ready...
timeout /t 10 /nobreak

REM Show logs
echo ?? Container Status:
docker-compose ps

echo.
echo ? Application is running!
echo ?? Web App: http://localhost:5000
echo ???  PostgreSQL: localhost:5432
echo.
echo ?? View logs with: docker-compose logs -f
echo ?? Stop with: docker-compose down

pause
