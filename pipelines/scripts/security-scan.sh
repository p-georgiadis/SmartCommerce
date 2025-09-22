#!/bin/bash

# SmartCommerce Security Scanning Script
# This script performs comprehensive security scanning across the codebase

set -euo pipefail

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
REPORTS_DIR="$PROJECT_ROOT/security-reports"

# Default values
SCAN_TYPE="all"
OUTPUT_FORMAT="json"
FAIL_ON_HIGH=true
FAIL_ON_CRITICAL=true

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
SmartCommerce Security Scanning Script

USAGE:
    $0 [OPTIONS]

OPTIONS:
    -t, --type TYPE                Scan type (all|sast|dast|deps|containers|secrets) [default: all]
    -f, --format FORMAT           Output format (json|xml|sarif|html) [default: json]
    --fail-on-high                Fail pipeline on high severity issues [default: true]
    --fail-on-critical            Fail pipeline on critical severity issues [default: true]
    --no-fail                     Don't fail pipeline on any issues
    -h, --help                    Show this help message

SCAN TYPES:
    all         Run all security scans
    sast        Static Application Security Testing
    dast        Dynamic Application Security Testing
    deps        Dependency vulnerability scanning
    containers  Container image security scanning
    secrets     Secret detection scanning

EXAMPLES:
    # Run all security scans
    $0

    # Run only dependency scanning
    $0 --type deps

    # Run SAST with SARIF output
    $0 --type sast --format sarif

EOF
}

# Parse command line arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -t|--type)
                SCAN_TYPE="$2"
                shift 2
                ;;
            -f|--format)
                OUTPUT_FORMAT="$2"
                shift 2
                ;;
            --fail-on-high)
                FAIL_ON_HIGH=true
                shift
                ;;
            --fail-on-critical)
                FAIL_ON_CRITICAL=true
                shift
                ;;
            --no-fail)
                FAIL_ON_HIGH=false
                FAIL_ON_CRITICAL=false
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

# Setup reports directory
setup_reports_dir() {
    log_info "Setting up reports directory..."

    mkdir -p "$REPORTS_DIR"
    rm -rf "$REPORTS_DIR"/*

    log_success "Reports directory ready: $REPORTS_DIR"
}

# Install security tools
install_security_tools() {
    log_info "Installing security scanning tools..."

    # Install Trivy for container scanning
    if ! command -v trivy &> /dev/null; then
        log_info "Installing Trivy..."
        case "$(uname -s)" in
            "Linux")
                sudo apt-get update
                sudo apt-get install wget apt-transport-https gnupg lsb-release
                wget -qO - https://aquasecurity.github.io/trivy-repo/deb/public.key | sudo apt-key add -
                echo "deb https://aquasecurity.github.io/trivy-repo/deb $(lsb_release -sc) main" | sudo tee -a /etc/apt/sources.list.d/trivy.list
                sudo apt-get update
                sudo apt-get install trivy
                ;;
            "Darwin")
                if command -v brew &> /dev/null; then
                    brew install trivy
                fi
                ;;
        esac
    fi

    # Install Bandit for Python SAST
    if ! command -v bandit &> /dev/null; then
        log_info "Installing Bandit..."
        pip install bandit[toml]
    fi

    # Install Safety for Python dependencies
    if ! command -v safety &> /dev/null; then
        log_info "Installing Safety..."
        pip install safety
    fi

    # Install Semgrep for multi-language SAST
    if ! command -v semgrep &> /dev/null; then
        log_info "Installing Semgrep..."
        pip install semgrep
    fi

    # Install GitLeaks for secret detection
    if ! command -v gitleaks &> /dev/null; then
        log_info "Installing GitLeaks..."
        case "$(uname -s)" in
            "Linux")
                wget -q https://github.com/zricethezav/gitleaks/releases/latest/download/gitleaks_linux_x64.tar.gz
                tar -xzf gitleaks_linux_x64.tar.gz
                sudo mv gitleaks /usr/local/bin/
                rm gitleaks_linux_x64.tar.gz
                ;;
            "Darwin")
                if command -v brew &> /dev/null; then
                    brew install gitleaks
                fi
                ;;
        esac
    fi

    log_success "Security tools installation completed"
}

# Run SAST (Static Application Security Testing)
run_sast_scan() {
    log_info "Running Static Application Security Testing (SAST)..."

    # Bandit for Python services
    log_info "Running Bandit scan on Python services..."
    find "$PROJECT_ROOT/services/python-services" -name "*.py" -type f | while read -r python_file; do
        if [[ -f "$python_file" ]]; then
            service_name=$(basename "$(dirname "$(dirname "$python_file")")")
            bandit -r "$(dirname "$(dirname "$python_file")")" \
                -f "$OUTPUT_FORMAT" \
                -o "$REPORTS_DIR/bandit-${service_name}.${OUTPUT_FORMAT}" \
                -ll || true
        fi
    done

    # Semgrep for multi-language analysis
    log_info "Running Semgrep scan..."
    semgrep --config=auto \
        --output "$REPORTS_DIR/semgrep.$OUTPUT_FORMAT" \
        --"$OUTPUT_FORMAT" \
        "$PROJECT_ROOT" || true

    # .NET specific security analysis
    log_info "Running .NET security analysis..."
    for dotnet_service in "$PROJECT_ROOT/services/dotnet-services"/*; do
        if [[ -d "$dotnet_service" && -f "$dotnet_service"/*.csproj ]]; then
            service_name=$(basename "$dotnet_service")
            log_info "Analyzing $service_name..."

            # Run security code scan
            dotnet list "$dotnet_service" package --vulnerable \
                --include-transitive > "$REPORTS_DIR/dotnet-vulnerable-${service_name}.txt" 2>/dev/null || true
        fi
    done

    log_success "SAST scan completed"
}

# Run dependency vulnerability scanning
run_dependency_scan() {
    log_info "Running dependency vulnerability scanning..."

    # Python dependencies with Safety
    log_info "Scanning Python dependencies..."
    for python_service in "$PROJECT_ROOT/services/python-services"/*; do
        if [[ -d "$python_service" && -f "$python_service/requirements.txt" ]]; then
            service_name=$(basename "$python_service")
            log_info "Scanning $service_name dependencies..."

            safety check \
                --file "$python_service/requirements.txt" \
                --output "$OUTPUT_FORMAT" \
                --output-file "$REPORTS_DIR/safety-${service_name}.${OUTPUT_FORMAT}" || true
        fi
    done

    # .NET dependencies
    log_info "Scanning .NET dependencies..."
    for dotnet_service in "$PROJECT_ROOT/services/dotnet-services"/*; do
        if [[ -d "$dotnet_service" && -f "$dotnet_service"/*.csproj ]]; then
            service_name=$(basename "$dotnet_service")
            log_info "Scanning $service_name dependencies..."

            # Use dotnet list package to check for vulnerabilities
            cd "$dotnet_service"
            dotnet list package --vulnerable --include-transitive \
                > "$REPORTS_DIR/dotnet-deps-${service_name}.txt" 2>/dev/null || true
            cd "$PROJECT_ROOT"
        fi
    done

    # Infrastructure dependencies (Bicep/ARM)
    log_info "Scanning infrastructure dependencies..."
    if [[ -d "$PROJECT_ROOT/infrastructure" ]]; then
        # Scan for known vulnerable configurations
        grep -r "Microsoft.Storage/storageAccounts" "$PROJECT_ROOT/infrastructure" \
            > "$REPORTS_DIR/infrastructure-storage-scan.txt" 2>/dev/null || true
    fi

    log_success "Dependency vulnerability scanning completed"
}

# Run container image security scanning
run_container_scan() {
    log_info "Running container image security scanning..."

    # Scan Dockerfiles for best practices
    log_info "Scanning Dockerfiles..."
    find "$PROJECT_ROOT" -name "Dockerfile*" -type f | while read -r dockerfile; do
        service_name=$(basename "$(dirname "$dockerfile")")
        log_info "Scanning Dockerfile for $service_name..."

        # Use Trivy to scan Dockerfile
        trivy config "$dockerfile" \
            --format "$OUTPUT_FORMAT" \
            --output "$REPORTS_DIR/dockerfile-${service_name}.${OUTPUT_FORMAT}" || true
    done

    # Build and scan container images
    log_info "Building and scanning container images..."
    for service_dir in "$PROJECT_ROOT/services"/*/*; do
        if [[ -d "$service_dir" && -f "$service_dir/Dockerfile" ]]; then
            service_name=$(basename "$service_dir")
            log_info "Building and scanning $service_name image..."

            # Build image
            docker build -t "smartcommerce/${service_name}:security-scan" "$service_dir" || continue

            # Scan image with Trivy
            trivy image \
                --format "$OUTPUT_FORMAT" \
                --output "$REPORTS_DIR/image-${service_name}.${OUTPUT_FORMAT}" \
                "smartcommerce/${service_name}:security-scan" || true

            # Clean up image
            docker rmi "smartcommerce/${service_name}:security-scan" || true
        fi
    done

    log_success "Container security scanning completed"
}

# Run secret detection scanning
run_secrets_scan() {
    log_info "Running secret detection scanning..."

    # GitLeaks scan
    log_info "Running GitLeaks scan..."
    gitleaks detect \
        --source "$PROJECT_ROOT" \
        --report-format "$OUTPUT_FORMAT" \
        --report-path "$REPORTS_DIR/gitleaks.$OUTPUT_FORMAT" \
        --verbose || true

    # Custom patterns for common secrets
    log_info "Running custom secret patterns scan..."
    {
        echo "=== Custom Secret Patterns Scan ==="
        echo "Scanning for potential secrets..."

        # API keys, tokens, passwords in source code
        grep -r -i -n \
            -E "(api[_-]?key|secret|token|password|pwd|auth)" \
            --include="*.cs" --include="*.py" --include="*.js" --include="*.json" --include="*.yml" --include="*.yaml" \
            "$PROJECT_ROOT" | grep -v -E "(test|example|placeholder|TODO|FIXME)" || true

        # Database connection strings
        grep -r -i -n \
            -E "(connectionstring|server=|database=|uid=|password=)" \
            --include="*.config" --include="*.json" --include="*.yml" \
            "$PROJECT_ROOT" || true

        # Azure-specific secrets
        grep -r -i -n \
            -E "(DefaultEndpointsProtocol|AccountName|AccountKey|BlobEndpoint)" \
            --include="*.config" --include="*.json" --include="*.yml" \
            "$PROJECT_ROOT" || true

    } > "$REPORTS_DIR/custom-secrets-scan.txt"

    log_success "Secret detection scanning completed"
}

# Analyze scan results
analyze_results() {
    log_info "Analyzing security scan results..."

    local critical_issues=0
    local high_issues=0
    local medium_issues=0
    local low_issues=0

    # Count issues from different scans
    for report in "$REPORTS_DIR"/*.json; do
        if [[ -f "$report" ]]; then
            # This is a simplified analysis - real implementation would parse each tool's JSON format
            critical_count=$(grep -c -i "critical\|severity.*high\|CRITICAL" "$report" 2>/dev/null || echo "0")
            high_count=$(grep -c -i "high\|HIGH" "$report" 2>/dev/null || echo "0")
            medium_count=$(grep -c -i "medium\|MEDIUM" "$report" 2>/dev/null || echo "0")
            low_count=$(grep -c -i "low\|LOW\|info\|INFO" "$report" 2>/dev/null || echo "0")

            critical_issues=$((critical_issues + critical_count))
            high_issues=$((high_issues + high_count))
            medium_issues=$((medium_issues + medium_count))
            low_issues=$((low_issues + low_count))
        fi
    done

    # Generate summary report
    cat > "$REPORTS_DIR/security-summary.txt" << EOF
SmartCommerce Security Scan Summary
===================================
Scan Type: $SCAN_TYPE
Date: $(date)
Build ID: ${BUILD_BUILDID:-"local"}

Issue Counts:
- Critical: $critical_issues
- High: $high_issues
- Medium: $medium_issues
- Low: $low_issues

Total Issues: $((critical_issues + high_issues + medium_issues + low_issues))

Reports Generated:
$(ls -la "$REPORTS_DIR" | grep -v "^total\|^d" | awk '{print "- " $9}')

Recommendations:
- Review and remediate critical and high severity issues immediately
- Plan remediation for medium severity issues
- Consider low severity issues for future improvements
- Integrate security scanning into CI/CD pipeline
- Regular security training for development team

EOF

    log_info "Security scan analysis completed"
    log_info "Critical issues: $critical_issues"
    log_info "High issues: $high_issues"
    log_info "Medium issues: $medium_issues"
    log_info "Low issues: $low_issues"

    # Determine if pipeline should fail
    local should_fail=false

    if [[ "$FAIL_ON_CRITICAL" == true && $critical_issues -gt 0 ]]; then
        log_error "Critical security issues found: $critical_issues"
        should_fail=true
    fi

    if [[ "$FAIL_ON_HIGH" == true && $high_issues -gt 0 ]]; then
        log_error "High severity security issues found: $high_issues"
        should_fail=true
    fi

    if [[ "$should_fail" == true ]]; then
        log_error "Security scan failed due to high/critical issues"
        return 1
    else
        log_success "Security scan completed without critical failures"
        return 0
    fi
}

# Main security scanning function
main() {
    log_info "Starting SmartCommerce security scanning"
    log_info "======================================="

    parse_args "$@"
    setup_reports_dir
    install_security_tools

    case $SCAN_TYPE in
        "all")
            run_sast_scan
            run_dependency_scan
            run_container_scan
            run_secrets_scan
            ;;
        "sast")
            run_sast_scan
            ;;
        "deps")
            run_dependency_scan
            ;;
        "containers")
            run_container_scan
            ;;
        "secrets")
            run_secrets_scan
            ;;
        *)
            log_error "Invalid scan type: $SCAN_TYPE"
            show_help
            exit 1
            ;;
    esac

    analyze_results

    log_success "======================================="
    log_success "Security scanning completed!"
    log_info "Reports available in: $REPORTS_DIR"
}

# Run main function with all arguments
main "$@"