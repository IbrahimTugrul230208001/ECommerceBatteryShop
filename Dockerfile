# Use the official .NET 8 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["ECommerceBatteryShop/ECommerceBatteryShop.csproj", "ECommerceBatteryShop/"]
COPY ["ECommerceBatteryShop.Domain/ECommerceBatteryShop.Domain.csproj", "ECommerceBatteryShop.Domain/"]
COPY ["ECommerceBatteryShop.DataAccess/ECommerceBatteryShop.DataAccess.csproj", "ECommerceBatteryShop.DataAccess/"]
RUN dotnet restore "ECommerceBatteryShop/ECommerceBatteryShop.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/ECommerceBatteryShop"
RUN dotnet build "ECommerceBatteryShop.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "ECommerceBatteryShop.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy published app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ECommerceBatteryShop.dll"]
