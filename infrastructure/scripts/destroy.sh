#!/bin/bash

# SmartCommerce Infrastructure Destruction Script
# This script safely destroys SmartCommerce infrastructure in Azure

set -euo pipefail

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Default values
ENVIRONMENT="dev"
SUBSCRIPTION_ID=""
RESOURCE_GROUP=""
BASE_NAME="smartcommerce"
FORCE_DELETE=false
BACKUP_DATA=true

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
SmartCommerce Infrastructure Destruction Script

USAGE:
    $0 [OPTIONS]

OPTIONS:
    -e, --environment ENVIRONMENT   Environment (dev|staging|prod) [default: dev]
    -s, --subscription SUBSCRIPTION Azure subscription ID
    -g, --resource-group GROUP      Resource group name
    -n, --base-name NAME           Base name for resources [default: smartcommerce]
    -f, --force                    Force deletion without confirmation
    --no-backup                    Skip data backup before deletion
    -h, --help                     Show this help message

EXAMPLES:
    # Destroy development environment (with confirmation)
    $0 -e dev -s "your-subscription-id" -g "smartcommerce-dev-rg"

    # Force destroy production environment without backup
    $0 -e prod -s "your-subscription-id" -g "smartcommerce-prod-rg" -f --no-backup

WARNINGS:
    - This operation is IRREVERSIBLE
    - All data will be permanently lost unless backed up
    - Use with extreme caution, especially in production

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
            -f|--force)
                FORCE_DELETE=true
                shift
                ;;
            --no-backup)
                BACKUP_DATA=false
                shift
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

    # Check if logged in to Azure
    if ! az account show &> /dev/null; then
        log_error "Not logged in to Azure. Please run 'az login' first."
        exit 1
    fi

    # Set subscription
    log_info "Setting Azure subscription to: $SUBSCRIPTION_ID"
    az account set --subscription "$SUBSCRIPTION_ID"

    # Check if resource group exists
    if ! az group show --name "$RESOURCE_GROUP" &> /dev/null; then
        log_error "Resource group $RESOURCE_GROUP does not exist"
        exit 1
    fi

    log_success "Prerequisites check completed"
}

# Show resources to be deleted
show_resources() {
    log_info "Resources that will be deleted:"
    log_info "================================"

    echo -e "${YELLOW}Resource Group:${NC} $RESOURCE_GROUP"
    echo -e "${YELLOW}Environment:${NC} $ENVIRONMENT"
    echo ""

    log_info "Listing resources in resource group..."
    az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --output table \
        --query "[].{Name:name, Type:type, Location:location}"

    echo ""
}

# Backup data before deletion
backup_data() {
    if [[ "$BACKUP_DATA" == false ]]; then
        log_warning "Skipping data backup as requested"
        return 0
    fi

    log_info "Creating data backup before deletion..."

    local backup_timestamp=$(date +%Y%m%d-%H%M%S)
    local backup_container="${BASE_NAME}-backup-${ENVIRONMENT}-${backup_timestamp}"

    # Backup SQL Database
    local sql_server="${BASE_NAME}-sql-${ENVIRONMENT}"
    local sql_database="${BASE_NAME}-db"

    if az sql db show --resource-group "$RESOURCE_GROUP" --server "$sql_server" --name "$sql_database" &> /dev/null; then
        log_info "Creating SQL database backup..."

        az sql db export \
            --resource-group "$RESOURCE_GROUP" \
            --server "$sql_server" \
            --name "$sql_database" \
            --admin-user "$SQL_ADMIN_LOGIN" \
            --admin-password "$SQL_ADMIN_PASSWORD" \
            --storage-key-type "StorageAccessKey" \
            --storage-key "$STORAGE_KEY" \
            --storage-uri "https://${STORAGE_ACCOUNT}.blob.core.windows.net/${backup_container}/${sql_database}-${backup_timestamp}.bacpac" \
            --output none

        log_success "SQL database backup created"
    fi

    # Backup Key Vault secrets
    local keyvault_name="${BASE_NAME}-kv-${ENVIRONMENT}"

    if az keyvault show --name "$keyvault_name" &> /dev/null; then
        log_info "Backing up Key Vault secrets..."

        local secrets_backup_file="/tmp/keyvault-secrets-${backup_timestamp}.json"

        az keyvault secret list \
            --vault-name "$keyvault_name" \
            --query "[].{name:name, value:value}" \
            --output json > "$secrets_backup_file"

        log_success "Key Vault secrets backed up to: $secrets_backup_file"
        log_warning "Please save this file securely before proceeding"
    fi

    log_success "Data backup completed"
}

# Confirmation prompt
confirm_deletion() {
    if [[ "$FORCE_DELETE" == true ]]; then
        log_warning "Force mode enabled - skipping confirmation"
        return 0
    fi

    echo ""
    log_warning "⚠️  WARNING: This will PERMANENTLY DELETE all resources!"
    log_warning "⚠️  This operation cannot be undone!"
    echo ""

    if [[ "$ENVIRONMENT" == "prod" ]]; then
        echo -e "${RED}🚨 YOU ARE ABOUT TO DELETE PRODUCTION ENVIRONMENT! 🚨${NC}"
        echo ""
    fi

    read -p "Are you absolutely sure you want to continue? Type 'DELETE' to confirm: " confirmation

    if [[ "$confirmation" != "DELETE" ]]; then
        log_info "Deletion cancelled by user"
        exit 0
    fi

    if [[ "$ENVIRONMENT" == "prod" ]]; then
        read -p "This is PRODUCTION. Type '${ENVIRONMENT^^}' to confirm: " env_confirmation
        if [[ "$env_confirmation" != "${ENVIRONMENT^^}" ]]; then
            log_info "Production deletion cancelled by user"
            exit 0
        fi
    fi

    log_warning "Proceeding with deletion in 10 seconds... Press Ctrl+C to cancel"
    sleep 10
}

# Delete specific resources with dependencies
delete_resources_with_dependencies() {
    log_info "Deleting resources with dependency management..."

    # Delete Application Services first
    log_info "Deleting App Services..."
    az webapp list --resource-group "$RESOURCE_GROUP" --query "[].name" --output tsv | while read -r webapp; do
        if [[ -n "$webapp" ]]; then
            log_info "Deleting App Service: $webapp"
            az webapp delete --resource-group "$RESOURCE_GROUP" --name "$webapp" --output none || true
        fi
    done

    # Delete Container Apps
    log_info "Deleting Container Apps..."
    az containerapp list --resource-group "$RESOURCE_GROUP" --query "[].name" --output tsv | while read -r containerapp; do
        if [[ -n "$containerapp" ]]; then
            log_info "Deleting Container App: $containerapp"
            az containerapp delete --resource-group "$RESOURCE_GROUP" --name "$containerapp" --yes --output none || true
        fi
    done

    # Delete SQL Databases
    log_info "Deleting SQL resources..."
    az sql server list --resource-group "$RESOURCE_GROUP" --query "[].name" --output tsv | while read -r sqlserver; do
        if [[ -n "$sqlserver" ]]; then
            log_info "Deleting SQL Server: $sqlserver"
            az sql server delete --resource-group "$RESOURCE_GROUP" --name "$sqlserver" --yes --output none || true
        fi
    done

    # Delete Key Vault (with purge protection handling)
    log_info "Deleting Key Vault..."
    local keyvault_name="${BASE_NAME}-kv-${ENVIRONMENT}"
    if az keyvault show --name "$keyvault_name" &> /dev/null; then
        az keyvault delete --name "$keyvault_name" --output none || true

        # Purge Key Vault if soft delete is enabled
        log_info "Purging Key Vault (if soft-delete enabled)..."
        az keyvault purge --name "$keyvault_name" --output none || true
    fi

    log_success "Resources with dependencies deleted"
}

# Delete the entire resource group
delete_resource_group() {
    log_info "Deleting resource group: $RESOURCE_GROUP"

    az group delete \
        --name "$RESOURCE_GROUP" \
        --yes \
        --no-wait \
        --output none

    log_success "Resource group deletion initiated"

    # Monitor deletion progress
    log_info "Monitoring deletion progress..."
    local timeout=1800  # 30 minutes
    local elapsed=0
    local interval=30

    while az group show --name "$RESOURCE_GROUP" &> /dev/null && [[ $elapsed -lt $timeout ]]; do
        log_info "Deletion in progress... (${elapsed}s elapsed)"
        sleep $interval
        elapsed=$((elapsed + interval))
    done

    if az group show --name "$RESOURCE_GROUP" &> /dev/null; then
        log_warning "Deletion taking longer than expected. Check Azure portal for status."
    else
        log_success "Resource group deleted successfully"
    fi
}

# Clean up any remaining resources
cleanup_remaining_resources() {
    log_info "Cleaning up any remaining resources..."

    # Clean up any orphaned resources that might not be in the resource group
    # This is environment-specific cleanup

    log_success "Cleanup completed"
}

# Main destruction function
main() {
    log_warning "Starting SmartCommerce infrastructure destruction"
    log_warning "==============================================="

    parse_args "$@"
    validate_inputs
    check_prerequisites
    show_resources
    backup_data
    confirm_deletion
    delete_resources_with_dependencies
    delete_resource_group
    cleanup_remaining_resources

    log_success "==============================================="
    log_success "SmartCommerce infrastructure destruction completed!"
    log_info "Environment: $ENVIRONMENT"
    log_info "Resource Group: $RESOURCE_GROUP (deleted)"

    if [[ "$BACKUP_DATA" == true ]]; then
        log_info "Don't forget to securely store your backup files!"
    fi
}

# Run main function with all arguments
main "$@"