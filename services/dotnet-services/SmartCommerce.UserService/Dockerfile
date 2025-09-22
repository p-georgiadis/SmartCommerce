# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["SmartCommerce.UserService/SmartCommerce.UserService.csproj", "SmartCommerce.UserService/"]
COPY ["SmartCommerce.Shared/SmartCommerce.Shared.csproj", "SmartCommerce.Shared/"]
RUN dotnet restore "SmartCommerce.UserService/SmartCommerce.UserService.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/SmartCommerce.UserService"
RUN dotnet build "SmartCommerce.UserService.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "SmartCommerce.UserService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create non-root user
RUN adduser --disabled-password --gecos '' dotnetuser
USER dotnetuser

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartCommerce.UserService.dll"]