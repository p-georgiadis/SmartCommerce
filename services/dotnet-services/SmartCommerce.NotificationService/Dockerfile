# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["SmartCommerce.NotificationService/SmartCommerce.NotificationService.csproj", "SmartCommerce.NotificationService/"]
COPY ["SmartCommerce.Shared/SmartCommerce.Shared.csproj", "SmartCommerce.Shared/"]
RUN dotnet restore "SmartCommerce.NotificationService/SmartCommerce.NotificationService.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/SmartCommerce.NotificationService"
RUN dotnet build "SmartCommerce.NotificationService.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "SmartCommerce.NotificationService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create non-root user
RUN adduser --disabled-password --gecos '' dotnetuser
USER dotnetuser

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartCommerce.NotificationService.dll"]