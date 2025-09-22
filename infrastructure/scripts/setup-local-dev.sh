#!/bin/bash

# SmartCommerce Local Development Environment Setup Script
# This script sets up the complete local development environment

set -euo pipefail

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Default values
SKIP_DOCKER=false
SKIP_DOTNET=false
SKIP_PYTHON=false
SKIP_TOOLS=false
INSTALL_VSCODE_EXTENSIONS=false

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
SmartCommerce Local Development Environment Setup

USAGE:
    $0 [OPTIONS]

OPTIONS:
    --skip-docker              Skip Docker and Docker Compose setup
    --skip-dotnet              Skip .NET SDK installation
    --skip-python              Skip Python and virtual environment setup
    --skip-tools               Skip development tools installation
    --vscode-extensions        Install recommended VS Code extensions
    -h, --help                 Show this help message

DESCRIPTION:
    This script sets up a complete local development environment for SmartCommerce
    including Docker, .NET 8, Python 3.11, and all required development tools.

REQUIREMENTS:
    - macOS, Linux, or Windows with WSL2
    - Internet connection for downloads
    - Administrator/sudo privileges for some installations

EOF
}

# Parse command line arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --skip-docker)
                SKIP_DOCKER=true
                shift
                ;;
            --skip-dotnet)
                SKIP_DOTNET=true
                shift
                ;;
            --skip-python)
                SKIP_PYTHON=true
                shift
                ;;
            --skip-tools)
                SKIP_TOOLS=true
                shift
                ;;
            --vscode-extensions)
                INSTALL_VSCODE_EXTENSIONS=true
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

# Detect operating system
detect_os() {
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        OS="linux"
        if [[ -f /etc/debian_version ]]; then
            DISTRO="debian"
        elif [[ -f /etc/redhat-release ]]; then
            DISTRO="redhat"
        else
            DISTRO="unknown"
        fi
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        OS="macos"
        DISTRO="macos"
    elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "cygwin" ]]; then
        OS="windows"
        DISTRO="windows"
    else
        OS="unknown"
        DISTRO="unknown"
    fi

    log_info "Detected OS: $OS ($DISTRO)"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    # Check if curl is available
    if ! command -v curl &> /dev/null; then
        log_error "curl is required but not installed. Please install curl first."
        exit 1
    fi

    # Check if git is available
    if ! command -v git &> /dev/null; then
        log_error "git is required but not installed. Please install git first."
        exit 1
    fi

    log_success "Prerequisites check completed"
}

# Install Docker and Docker Compose
install_docker() {
    if [[ "$SKIP_DOCKER" == true ]]; then
        log_info "Skipping Docker installation"
        return 0
    fi

    log_info "Installing Docker and Docker Compose..."

    if command -v docker &> /dev/null; then
        log_warning "Docker is already installed"
        docker --version
    else
        case $OS in
            "linux")
                case $DISTRO in
                    "debian")
                        # Install Docker on Debian/Ubuntu
                        curl -fsSL https://get.docker.com -o get-docker.sh
                        sudo sh get-docker.sh
                        sudo usermod -aG docker $USER
                        rm get-docker.sh
                        ;;
                    "redhat")
                        # Install Docker on RHEL/CentOS/Fedora
                        sudo dnf install -y docker docker-compose
                        sudo systemctl enable docker
                        sudo systemctl start docker
                        sudo usermod -aG docker $USER
                        ;;
                esac
                ;;
            "macos")
                log_info "Please install Docker Desktop for Mac from: https://docs.docker.com/desktop/mac/install/"
                log_warning "Manual installation required for macOS"
                ;;
            "windows")
                log_info "Please install Docker Desktop for Windows from: https://docs.docker.com/desktop/windows/install/"
                log_warning "Manual installation required for Windows"
                ;;
        esac
    fi

    # Install Docker Compose if not present
    if ! command -v docker-compose &> /dev/null; then
        log_info "Installing Docker Compose..."

        case $OS in
            "linux")
                sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
                sudo chmod +x /usr/local/bin/docker-compose
                ;;
            *)
                log_info "Docker Compose should be included with Docker Desktop"
                ;;
        esac
    fi

    log_success "Docker installation completed"
}

# Install .NET SDK
install_dotnet() {
    if [[ "$SKIP_DOTNET" == true ]]; then
        log_info "Skipping .NET SDK installation"
        return 0
    fi

    log_info "Installing .NET 8.0 SDK..."

    if command -v dotnet &> /dev/null; then
        local version=$(dotnet --version)
        if [[ "$version" == 8.* ]]; then
            log_warning ".NET 8.0 SDK is already installed: $version"
            return 0
        fi
    fi

    case $OS in
        "linux")
            # Install .NET on Linux
            wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
            sudo dpkg -i packages-microsoft-prod.deb
            rm packages-microsoft-prod.deb
            sudo apt-get update
            sudo apt-get install -y dotnet-sdk-8.0
            ;;
        "macos")
            # Install .NET on macOS
            if command -v brew &> /dev/null; then
                brew install dotnet
            else
                log_info "Please install .NET SDK manually from: https://dotnet.microsoft.com/download"
            fi
            ;;
        "windows")
            log_info "Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download"
            log_warning "Manual installation required for Windows"
            ;;
    esac

    # Install Entity Framework tools
    if command -v dotnet &> /dev/null; then
        dotnet tool install --global dotnet-ef
        dotnet dev-certs https --trust
    fi

    log_success ".NET SDK installation completed"
}

# Install Python and virtual environment
install_python() {
    if [[ "$SKIP_PYTHON" == true ]]; then
        log_info "Skipping Python installation"
        return 0
    fi

    log_info "Installing Python 3.11 and setting up virtual environment..."

    case $OS in
        "linux")
            case $DISTRO in
                "debian")
                    sudo apt-get update
                    sudo apt-get install -y python3.11 python3.11-venv python3.11-dev python3-pip
                    ;;
                "redhat")
                    sudo dnf install -y python3.11 python3.11-venv python3.11-devel python3-pip
                    ;;
            esac
            ;;
        "macos")
            if command -v brew &> /dev/null; then
                brew install python@3.11
            else
                log_info "Please install Python 3.11 manually from: https://www.python.org/downloads/"
            fi
            ;;
        "windows")
            log_info "Please install Python 3.11 from: https://www.python.org/downloads/"
            log_warning "Manual installation required for Windows"
            ;;
    esac

    # Create virtual environment for Python services
    local python_services_dir="$PROJECT_ROOT/services/python-services"
    if [[ -d "$python_services_dir" ]]; then
        log_info "Creating Python virtual environments..."

        for service_dir in "$python_services_dir"/*; do
            if [[ -d "$service_dir" && -f "$service_dir/requirements.txt" ]]; then
                local service_name=$(basename "$service_dir")
                local venv_dir="$service_dir/venv"

                log_info "Setting up virtual environment for $service_name..."

                python3.11 -m venv "$venv_dir"
                source "$venv_dir/bin/activate"
                pip install --upgrade pip
                pip install -r "$service_dir/requirements.txt"
                deactivate

                log_success "Virtual environment created for $service_name"
            fi
        done
    fi

    log_success "Python installation and setup completed"
}

# Install development tools
install_dev_tools() {
    if [[ "$SKIP_TOOLS" == true ]]; then
        log_info "Skipping development tools installation"
        return 0
    fi

    log_info "Installing development tools..."

    case $OS in
        "linux")
            case $DISTRO in
                "debian")
                    sudo apt-get update
                    sudo apt-get install -y curl wget git jq unzip build-essential
                    ;;
                "redhat")
                    sudo dnf install -y curl wget git jq unzip gcc gcc-c++ make
                    ;;
            esac
            ;;
        "macos")
            if command -v brew &> /dev/null; then
                brew install curl wget git jq
            else
                log_warning "Homebrew not found. Please install development tools manually."
            fi
            ;;
    esac

    # Install Azure CLI
    if ! command -v az &> /dev/null; then
        log_info "Installing Azure CLI..."
        case $OS in
            "linux")
                curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
                ;;
            "macos")
                if command -v brew &> /dev/null; then
                    brew install azure-cli
                fi
                ;;
            "windows")
                log_info "Please install Azure CLI from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
                ;;
        esac
    fi

    # Install Bicep CLI
    if ! command -v bicep &> /dev/null; then
        log_info "Installing Bicep CLI..."
        az bicep install
    fi

    log_success "Development tools installation completed"
}

# Install VS Code extensions
install_vscode_extensions() {
    if [[ "$INSTALL_VSCODE_EXTENSIONS" == false ]]; then
        log_info "Skipping VS Code extensions installation"
        return 0
    fi

    if ! command -v code &> /dev/null; then
        log_warning "VS Code not found. Please install VS Code first."
        return 0
    fi

    log_info "Installing recommended VS Code extensions..."

    local extensions=(
        "ms-dotnettools.csharp"
        "ms-python.python"
        "ms-azuretools.vscode-bicep"
        "ms-azuretools.vscode-docker"
        "ms-vscode.vscode-json"
        "redhat.vscode-yaml"
        "ms-vscode-remote.remote-containers"
        "github.copilot"
        "esbenp.prettier-vscode"
        "bradlc.vscode-tailwindcss"
    )

    for extension in "${extensions[@]}"; do
        log_info "Installing extension: $extension"
        code --install-extension "$extension" --force
    done

    log_success "VS Code extensions installation completed"
}

# Setup project configuration
setup_project_config() {
    log_info "Setting up project configuration..."

    # Create .env files for development
    local env_file="$PROJECT_ROOT/.env.development"
    if [[ ! -f "$env_file" ]]; then
        cat > "$env_file" << EOF
# SmartCommerce Development Environment Configuration

# Application
ENVIRONMENT=development
DEBUG=true

# Database
DATABASE_URL=postgresql://postgres:password@localhost:5432/smartcommerce_dev

# Redis
REDIS_HOST=localhost
REDIS_PORT=6379
REDIS_PASSWORD=

# Service Bus (local development)
AZURE_SERVICE_BUS_CONNECTION_STRING=Endpoint=sb://localhost:5672/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=

# Key Vault (local development)
AZURE_KEY_VAULT_URL=

# Machine Learning Models
SPACY_MODEL=en_core_web_sm
SENTENCE_TRANSFORMER_MODEL=all-MiniLM-L6-v2

# Logging
LOG_LEVEL=DEBUG
LOG_FORMAT=console

EOF
        log_success "Created development environment file: $env_file"
    fi

    # Setup Docker Compose override for development
    local compose_override="$PROJECT_ROOT/docker-compose.override.yml"
    if [[ ! -f "$compose_override" ]]; then
        cat > "$compose_override" << EOF
version: '3.8'

services:
  postgres:
    environment:
      - POSTGRES_DB=smartcommerce_dev
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=password
    ports:
      - "5432:5432"
    volumes:
      - postgres_dev_data:/var/lib/postgresql/data

  redis:
    ports:
      - "6379:6379"
    volumes:
      - redis_dev_data:/data

volumes:
  postgres_dev_data:
  redis_dev_data:

EOF
        log_success "Created Docker Compose override: $compose_override"
    fi

    log_success "Project configuration setup completed"
}

# Start development services
start_dev_services() {
    log_info "Starting development services..."

    cd "$PROJECT_ROOT"

    if [[ -f "docker-compose.dev.yml" ]]; then
        docker-compose -f docker-compose.dev.yml up -d
        log_success "Development services started"

        # Wait for services to be ready
        log_info "Waiting for services to be ready..."
        sleep 10

        # Check service health
        if docker-compose -f docker-compose.dev.yml ps | grep -q "Up"; then
            log_success "Development services are running"
        else
            log_warning "Some services may not be running properly. Check with: docker-compose -f docker-compose.dev.yml ps"
        fi
    else
        log_warning "docker-compose.dev.yml not found. Skipping service startup."
    fi
}

# Print setup summary
print_summary() {
    log_success "==============================================="
    log_success "SmartCommerce Development Environment Setup Complete!"
    log_success "==============================================="

    echo ""
    log_info "What was installed:"
    [[ "$SKIP_DOCKER" == false ]] && echo "  ✓ Docker and Docker Compose"
    [[ "$SKIP_DOTNET" == false ]] && echo "  ✓ .NET 8.0 SDK and Entity Framework tools"
    [[ "$SKIP_PYTHON" == false ]] && echo "  ✓ Python 3.11 and virtual environments"
    [[ "$SKIP_TOOLS" == false ]] && echo "  ✓ Azure CLI and Bicep CLI"
    [[ "$INSTALL_VSCODE_EXTENSIONS" == true ]] && echo "  ✓ VS Code extensions"

    echo ""
    log_info "Next steps:"
    echo "  1. Restart your terminal or run: source ~/.bashrc"
    echo "  2. Navigate to the project directory: cd $PROJECT_ROOT"
    echo "  3. Start development services: docker-compose -f docker-compose.dev.yml up -d"
    echo "  4. Build .NET services: dotnet build"
    echo "  5. Run tests: ./scripts/run-tests.sh"

    echo ""
    log_info "Useful commands:"
    echo "  • View logs: docker-compose -f docker-compose.dev.yml logs -f"
    echo "  • Stop services: docker-compose -f docker-compose.dev.yml down"
    echo "  • Run specific service: cd services/dotnet-services/SmartCommerce.OrderService && dotnet run"

    echo ""
    log_warning "Note: If you installed Docker, you may need to logout and login again for group changes to take effect."
}

# Main setup function
main() {
    log_info "Starting SmartCommerce development environment setup"
    log_info "===================================================="

    parse_args "$@"
    detect_os
    check_prerequisites
    install_docker
    install_dotnet
    install_python
    install_dev_tools
    install_vscode_extensions
    setup_project_config
    start_dev_services
    print_summary
}

# Run main function with all arguments
main "$@"