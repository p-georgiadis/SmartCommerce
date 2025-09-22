#!/bin/bash

# SmartCommerce Infrastructure Deployment Script
# This script deploys the complete SmartCommerce infrastructure to Azure

set -euo pipefail

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BICEP_DIR="$PROJECT_ROOT/infrastructure/bicep"
PARAMS_DIR="$BICEP_DIR/parameters"

# Default values
ENVIRONMENT="dev"
LOCATION="eastus"
SUBSCRIPTION_ID=""
RESOURCE_GROUP=""
BASE_NAME="smartcommerce"
SQL_ADMIN_LOGIN="sqladmin"
SQL_ADMIN_PASSWORD=""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Help function
show_help() {
    cat << EOF
SmartCommerce Infrastructure Deployment Script

USAGE:
    $0 [OPTIONS]

OPTIONS:
    -e, --environment ENVIRONMENT   Environment (dev|staging|prod) [default: dev]
    -l, --location LOCATION         Azure region [default: eastus]
    -s, --subscription SUBSCRIPTION Azure subscription ID
    -g, --resource-group GROUP      Resource group name
    -n, --base-name NAME           Base name for resources [default: smartcommerce]
    -u, --sql-user USERNAME        SQL admin username [default: sqladmin]
    -p, --sql-password PASSWORD   SQL admin password (required)
    -h, --help                     Show this help message

EXAMPLES:
    # Deploy to development environment
    $0 -e dev -s "your-subscription-id" -g "smartcommerce-dev-rg" -p "YourSQLPassword123!"

    # Deploy to production environment
    $0 -e prod -s "your-subscription-id" -g "smartcommerce-prod-rg" -p "YourSQLPassword123!"

REQUIREMENTS:
    - Azure CLI installed and logged in
    - Bicep CLI installed
    - Valid Azure subscription
    - Appropriate permissions to create resources

EOF
}

# Parse command line arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -e|--environment)
                ENVIRONMENT="$2"
                shift 2
                ;;
            -l|--location)
                LOCATION="$2"
                shift 2
                ;;
            -s|--subscription)
                SUBSCRIPTION_ID="$2"
                shift 2
                ;;
            -g|--resource-group)
                RESOURCE_GROUP="$2"
                shift 2
                ;;
            -n|--base-name)
                BASE_NAME="$2"
                shift 2
                ;;
            -u|--sql-user)
                SQL_ADMIN_LOGIN="$2"
                shift 2
                ;;
            -p|--sql-password)
                SQL_ADMIN_PASSWORD="$2"
                shift 2
                ;;
            -h|--help)
                show_help
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                show_help
                exit 1
                ;;
        esac
    done
}

# Validate inputs
validate_inputs() {
    log_info "Validating inputs..."

    # Validate environment
    if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|prod)$ ]]; then
        log_error "Invalid environment: $ENVIRONMENT. Must be one of: dev, staging, prod"
        exit 1
    fi

    # Validate required parameters
    if [[ -z "$SUBSCRIPTION_ID" ]]; then
        log_error "Subscription ID is required. Use -s or --subscription"
        exit 1
    fi

    if [[ -z "$RESOURCE_GROUP" ]]; then
        RESOURCE_GROUP="${BASE_NAME}-${ENVIRONMENT}-rg"
        log_info "Using default resource group: $RESOURCE_GROUP"
    fi

    if [[ -z "$SQL_ADMIN_PASSWORD" ]]; then
        log_error "SQL admin password is required. Use -p or --sql-password"
        exit 1
    fi

    # Validate password strength
    if [[ ${#SQL_ADMIN_PASSWORD} -lt 12 ]]; then
        log_error "SQL password must be at least 12 characters long"
        exit 1
    fi

    log_success "Input validation completed"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    # Check Azure CLI
    if ! command -v az &> /dev/null; then
        log_error "Azure CLI is not installed. Please install it first."
        exit 1
    fi

    # Check Bicep CLI
    if ! command -v bicep &> /dev/null; then
        log_error "Bicep CLI is not installed. Please install it first."
        exit 1
    fi

    # Check if logged in to Azure
    if ! az account show &> /dev/null; then
        log_error "Not logged in to Azure. Please run 'az login' first."
        exit 1
    fi

    # Set subscription
    log_info "Setting Azure subscription to: $SUBSCRIPTION_ID"
    az account set --subscription "$SUBSCRIPTION_ID"

    log_success "Prerequisites check completed"
}

# Create resource group
create_resource_group() {
    log_info "Creating resource group: $RESOURCE_GROUP"

    if az group show --name "$RESOURCE_GROUP" &> /dev/null; then
        log_warning "Resource group $RESOURCE_GROUP already exists"
    else
        az group create \
            --name "$RESOURCE_GROUP" \
            --location "$LOCATION" \
            --tags \
                Environment="$ENVIRONMENT" \
                Project="SmartCommerce" \
                ManagedBy="Infrastructure-Script"

        log_success "Resource group created: $RESOURCE_GROUP"
    fi
}

# Deploy infrastructure
deploy_infrastructure() {
    log_info "Deploying SmartCommerce infrastructure..."

    local deployment_name="smartcommerce-${ENVIRONMENT}-$(date +%Y%m%d-%H%M%S)"
    local bicep_file="$BICEP_DIR/main.bicep"
    local params_file="$PARAMS_DIR/${ENVIRONMENT}.parameters.json"

    # Check if main Bicep file exists
    if [[ ! -f "$bicep_file" ]]; then
        log_error "Main Bicep file not found: $bicep_file"
        exit 1
    fi

    # Create parameters file if it doesn't exist
    if [[ ! -f "$params_file" ]]; then
        log_warning "Parameters file not found: $params_file"
        log_info "Creating default parameters file..."
        create_parameters_file "$params_file"
    fi

    # Deploy using Azure CLI
    log_info "Starting deployment: $deployment_name"

    az deployment group create \
        --resource-group "$RESOURCE_GROUP" \
        --name "$deployment_name" \
        --template-file "$bicep_file" \
        --parameters \
            baseName="$BASE_NAME" \
            environment="$ENVIRONMENT" \
            location="$LOCATION" \
            sqlAdminLogin="$SQL_ADMIN_LOGIN" \
            sqlAdminPassword="$SQL_ADMIN_PASSWORD" \
        --verbose

    local exit_code=$?

    if [[ $exit_code -eq 0 ]]; then
        log_success "Infrastructure deployment completed successfully"

        # Get deployment outputs
        log_info "Retrieving deployment outputs..."
        az deployment group show \
            --resource-group "$RESOURCE_GROUP" \
            --name "$deployment_name" \
            --query "properties.outputs" \
            --output table
    else
        log_error "Infrastructure deployment failed with exit code: $exit_code"
        exit $exit_code
    fi
}

# Create default parameters file
create_parameters_file() {
    local params_file="$1"

    mkdir -p "$(dirname "$params_file")"

    cat > "$params_file" << EOF
{
  "\$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "baseName": {
      "value": "$BASE_NAME"
    },
    "environment": {
      "value": "$ENVIRONMENT"
    },
    "location": {
      "value": "$LOCATION"
    },
    "sqlAdminLogin": {
      "value": "$SQL_ADMIN_LOGIN"
    }
  }
}
EOF

    log_success "Created parameters file: $params_file"
}

# Post-deployment configuration
post_deployment_config() {
    log_info "Running post-deployment configuration..."

    # Set Key Vault secrets
    local keyvault_name="${BASE_NAME}-kv-${ENVIRONMENT}"

    log_info "Setting Key Vault secrets..."

    # Set SQL connection string secret
    local sql_server="${BASE_NAME}-sql-${ENVIRONMENT}"
    local sql_database="${BASE_NAME}-db"
    local sql_connection_string="Server=tcp:${sql_server}.database.windows.net,1433;Initial Catalog=${sql_database};Persist Security Info=False;User ID=${SQL_ADMIN_LOGIN};Password=${SQL_ADMIN_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    az keyvault secret set \
        --vault-name "$keyvault_name" \
        --name "SqlConnectionString" \
        --value "$sql_connection_string" \
        --output none

    # Set Service Bus connection string secret (placeholder)
    local servicebus_name="${BASE_NAME}-sb-${ENVIRONMENT}"
    local servicebus_connection_string=$(az servicebus namespace authorization-rule keys list \
        --resource-group "$RESOURCE_GROUP" \
        --namespace-name "$servicebus_name" \
        --name RootManageSharedAccessKey \
        --query primaryConnectionString \
        --output tsv)

    az keyvault secret set \
        --vault-name "$keyvault_name" \
        --name "ServiceBusConnectionString" \
        --value "$servicebus_connection_string" \
        --output none

    log_success "Post-deployment configuration completed"
}

# Main deployment function
main() {
    log_info "Starting SmartCommerce infrastructure deployment"
    log_info "================================================"

    parse_args "$@"
    validate_inputs
    check_prerequisites
    create_resource_group
    deploy_infrastructure
    post_deployment_config

    log_success "================================================"
    log_success "SmartCommerce infrastructure deployment completed!"
    log_info "Environment: $ENVIRONMENT"
    log_info "Resource Group: $RESOURCE_GROUP"
    log_info "Location: $LOCATION"
    log_info "Base Name: $BASE_NAME"
}

# Run main function with all arguments
main "$@"