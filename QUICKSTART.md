# SmartCommerce Quick Start Guide

## 🚀 Get Running in 5 Minutes

### Prerequisites
- Docker Desktop installed and running
- .NET 8.0 SDK
- Python 3.12+
- Azure CLI (for cloud deployment)

### Local Development Setup

1. **Clone and prepare environment**:
```bash
# Navigate to project
cd /home/panog/SmartCommerce

# Copy environment template
cp .env.example .env

# Start all infrastructure services
docker-compose -f docker-compose.override.yml up -d

# Wait for services to start (30 seconds)
sleep 30
```

2. **Start .NET Services**:
```bash
# Terminal 1 - Order Service
cd services/dotnet-services/SmartCommerce.OrderService
dotnet run

# Terminal 2 - Catalog Service
cd services/dotnet-services/SmartCommerce.CatalogService
dotnet run

# Terminal 3 - Payment Service
cd services/dotnet-services/SmartCommerce.PaymentService
dotnet run

# Terminal 4 - User Service
cd services/dotnet-services/SmartCommerce.UserService
dotnet run

# Terminal 5 - Notification Service
cd services/dotnet-services/SmartCommerce.NotificationService
dotnet run
```

3. **Start Python Services**:
```bash
# Terminal 6 - Recommendation Engine
cd services/python-services/recommendation-engine
pip install -r requirements.txt
uvicorn app.main:app --host 0.0.0.0 --port 8001

# Terminal 7 - Price Optimization
cd services/python-services/price-optimization
pip install -r requirements.txt
uvicorn app.main:app --host 0.0.0.0 --port 8002

# Terminal 8 - Fraud Detection
cd services/python-services/fraud-detection
pip install -r requirements.txt
uvicorn app.main:app --host 0.0.0.0 --port 8003

# Terminal 9 - Inventory Analytics
cd services/python-services/inventory-analytics
pip install -r requirements.txt
uvicorn app.main:app --host 0.0.0.0 --port 8004

# Terminal 10 - Search Service
cd services/python-services/search-service
pip install -r requirements.txt
uvicorn app.main:app --host 0.0.0.0 --port 8005
```

### 🧪 Test Your Setup

```bash
# Health checks for all services
curl http://localhost:5000/health  # Order Service
curl http://localhost:5001/health  # User Service
curl http://localhost:5002/health  # Catalog Service
curl http://localhost:5003/health  # Payment Service
curl http://localhost:5004/health  # Notification Service
curl http://localhost:8001/health  # Recommendation Engine
curl http://localhost:8002/health  # Price Optimization
curl http://localhost:8003/health  # Fraud Detection
curl http://localhost:8004/health  # Inventory Analytics
curl http://localhost:8005/health  # Search Service
```

### 🌐 Access Development Tools

- **API Documentation**: http://localhost:5000/swagger (Order Service)
- **Database Admin**: http://localhost:8080 (pgAdmin)
- **Redis Commander**: http://localhost:8081
- **Grafana Dashboard**: http://localhost:3001 (admin/devpassword)
- **Prometheus Metrics**: http://localhost:9090
- **Jaeger Tracing**: http://localhost:16686
- **Elasticsearch**: http://localhost:9200
- **Kibana**: http://localhost:5601
- **MinIO Storage**: http://localhost:9001 (minioadmin/devpassword)
- **MailHog Email Testing**: http://localhost:8025

## ☁️ Azure Cloud Deployment

### Prerequisites Setup
```bash
# Login to Azure
az login

# Set subscription
az account set --subscription "your-subscription-id"

# Create resource group
az group create --name smartcommerce-rg --location eastus
```

### 1. Deploy Infrastructure
```bash
# Deploy using our automation script
chmod +x infrastructure/scripts/deploy.sh
./infrastructure/scripts/deploy.sh --environment prod --resource-group smartcommerce-rg
```

### 2. Configure Secrets
```bash
# Set required secrets in Key Vault
az keyvault secret set --vault-name smartcommerce-kv-prod --name SqlAdminPassword --value "YourSecurePassword123!"
az keyvault secret set --vault-name smartcommerce-kv-prod --name ServiceBusConnectionString --value "$(az servicebus namespace authorization-rule keys list --resource-group smartcommerce-rg --namespace-name smartcommerce-sb-prod --name RootManageSharedAccessKey --query primaryConnectionString -o tsv)"
```

### 3. Deploy Services
```bash
# Build and deploy .NET services
for service in order-service catalog-service payment-service user-service notification-service; do
  docker build -t smartcommerceacr.azurecr.io/$service:latest services/dotnet-services/SmartCommerce.${service^Service}/
  docker push smartcommerceacr.azurecr.io/$service:latest
done

# Deploy Python container apps
for service in recommendation-engine price-optimization fraud-detection inventory-analytics search-service; do
  docker build -t smartcommerceacr.azurecr.io/$service:latest services/python-services/$service/
  docker push smartcommerceacr.azurecr.io/$service:latest
  az containerapp update --name smartcommerce-$service-prod --resource-group smartcommerce-rg --image smartcommerceacr.azurecr.io/$service:latest
done
```

## 🧪 Testing

```bash
# Run integration tests
cd tests/integration
dotnet test

# Run Python service tests
cd tests/integration/python
python -m pytest

# Load testing
cd tests/load
k6 run order-service-load-test.js

# End-to-end testing
cd tests/e2e
npx cypress run
```

## 📊 Architecture Overview

The app consists of:
- **5 .NET 8.0 Services** (Azure App Service): Order, Catalog, Payment, User, Notification
- **5 Python Services** (Azure Container Apps): ML Recommendation, Price Optimization, Fraud Detection, Inventory Analytics, Search
- **Infrastructure**: Azure SQL, Redis Cache, Service Bus, Key Vault, Application Insights
- **Development Tools**: PostgreSQL, Elasticsearch, Grafana, Prometheus, Jaeger

## 🔧 Next Steps

1. **Frontend Development**: Build React/Angular frontend consuming the APIs
2. **Mobile Apps**: Create mobile apps using the same API endpoints
3. **Advanced ML**: Enhance ML models with more sophisticated algorithms
4. **Performance**: Optimize with CDN, caching strategies, and database tuning
5. **Security**: Implement OAuth 2.0, rate limiting, and WAF rules

## 🆘 Troubleshooting

- **Port conflicts**: Check if ports 5000-5004, 8001-8005 are available
- **Docker issues**: Restart Docker Desktop and run `docker-compose down && docker-compose up -d`
- **Database connection**: Ensure PostgreSQL container is running with `docker ps`
- **Azure deployment**: Check Azure portal for deployment status and logs

## 📚 Documentation

- Full API documentation available at each service's `/swagger` endpoint
- Architecture details in `/docs/architecture.md`
- Deployment guide in `/docs/deployment.md`
- Monitoring setup in `/docs/monitoring.md`