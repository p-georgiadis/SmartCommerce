# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /app

# Copy project files
COPY ["SmartCommerce.OrderService.csproj", "./"]
COPY ["../SmartCommerce.Shared/SmartCommerce.Shared.csproj", "../SmartCommerce.Shared/"]

# Restore dependencies
RUN dotnet restore "SmartCommerce.OrderService.csproj"

# Copy source code
COPY . .
COPY ../SmartCommerce.Shared/ ../SmartCommerce.Shared/

# Build the application
RUN dotnet build "SmartCommerce.OrderService.csproj" -c Release -o /app/build

# Publish the application
RUN dotnet publish "SmartCommerce.OrderService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Create non-root user
RUN addgroup -g 1001 -S appgroup && \
    adduser -S appuser -u 1001 -G appgroup

# Install required packages for health checks
RUN apk add --no-cache curl

# Copy published application
COPY --from=build /app/publish .

# Set ownership
RUN chown -R appuser:appgroup /app
USER appuser

# Configure environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "SmartCommerce.OrderService.dll"]