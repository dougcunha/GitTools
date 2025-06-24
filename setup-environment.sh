#!/bin/bash
# Script to setup development environment for GitTools on Linux systems
# Installs .NET SDK and required tools for code coverage
# Do not run as root

set -e  # Exit on any error

DOTNET_VERSION="9.0"
INSTALL_REPORTGENERATOR=true

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

print_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Function to detect Linux distribution
detect_distro() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        DISTRO=$ID
        VERSION_ID=$VERSION_ID
    elif [ -f /etc/redhat-release ]; then
        DISTRO="rhel"
    elif [ -f /etc/debian_version ]; then
        DISTRO="debian"
    else
        DISTRO="unknown"
    fi
    
    print_info "Detected distribution: $DISTRO"
}

# Function to check if .NET is already installed
check_dotnet_installed() {
    if command -v dotnet >/dev/null 2>&1; then
        INSTALLED_VERSION=$(dotnet --version 2>/dev/null | cut -d'.' -f1,2)
        print_info "Found .NET version: $INSTALLED_VERSION"
        
        if [ "$INSTALLED_VERSION" = "$DOTNET_VERSION" ]; then
            print_success ".NET $DOTNET_VERSION is already installed"
            return 0
        else
            print_warning ".NET $INSTALLED_VERSION is installed, but we need $DOTNET_VERSION"
            return 1
        fi
    else
        print_info ".NET SDK not found"
        return 1
    fi
}

# Function to install .NET SDK on Ubuntu/Debian
install_dotnet_ubuntu_debian() {
    print_info "Installing .NET SDK on Ubuntu/Debian..."
    
    # Get Ubuntu version for package registration
    if [ "$DISTRO" = "ubuntu" ]; then
        UBUNTU_VERSION=$VERSION_ID
    else
        # For Debian, map to compatible Ubuntu version
        case $VERSION_ID in
            "11") UBUNTU_VERSION="20.04" ;;
            "12") UBUNTU_VERSION="22.04" ;;
            *) UBUNTU_VERSION="22.04" ;;
        esac
    fi
    
    # Download and install Microsoft package signing key
    wget https://packages.microsoft.com/config/ubuntu/$UBUNTU_VERSION/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    
    # Update package index
    sudo apt-get update
    
    # Install .NET SDK
    sudo apt-get install -y dotnet-sdk-9.0
}

# Function to install .NET SDK on CentOS/RHEL/Fedora
install_dotnet_rhel() {
    print_info "Installing .NET SDK on RHEL/CentOS/Fedora..."
    
    # Add Microsoft repository
    if [ "$DISTRO" = "fedora" ]; then
        sudo dnf install -y https://packages.microsoft.com/config/fedora/$(rpm -E %fedora)/packages-microsoft-prod.rpm
        sudo dnf install -y dotnet-sdk-9.0
    else
        # For RHEL/CentOS
        sudo yum install -y https://packages.microsoft.com/config/rhel/8/packages-microsoft-prod.rpm
        sudo yum install -y dotnet-sdk-9.0
    fi
}

# Function to install .NET SDK on Arch Linux
install_dotnet_arch() {
    print_info "Installing .NET SDK on Arch Linux..."
    
    # Update package database
    sudo pacman -Sy
    
    # Install .NET SDK from official repositories
    sudo pacman -S --noconfirm dotnet-sdk
}

# Function to install .NET SDK using snap (fallback)
install_dotnet_snap() {
    print_info "Installing .NET SDK using snap (fallback method)..."
    
    if ! command -v snap >/dev/null 2>&1; then
        print_error "Snap is not available. Please install .NET SDK manually."
        print_info "Visit: https://docs.microsoft.com/en-us/dotnet/core/install/linux"
        exit 1
    fi
    
    sudo snap install dotnet-sdk --classic --channel=9.0
}

# Function to install ReportGenerator tool
install_reportgenerator() {
    if [ "$INSTALL_REPORTGENERATOR" = true ]; then
        print_info "Installing ReportGenerator for code coverage reports..."
        
        if command -v dotnet >/dev/null 2>&1; then
            dotnet tool install -g dotnet-reportgenerator-globaltool
            print_success "ReportGenerator installed successfully"
        else
            print_error "Cannot install ReportGenerator: .NET SDK not found"
            exit 1
        fi
    fi
}

# Function to verify installation
verify_installation() {
    print_info "Verifying installation..."
    
    if command -v dotnet >/dev/null 2>&1; then
        DOTNET_VERSION_INSTALLED=$(dotnet --version)
        print_success ".NET SDK installed successfully: $DOTNET_VERSION_INSTALLED"
        
        # Show SDKs and runtimes
        echo ""
        print_info "Installed .NET SDKs:"
        dotnet --list-sdks
        
        echo ""
        print_info "Installed .NET runtimes:"
        dotnet --list-runtimes
    else
        print_error ".NET SDK installation failed"
        exit 1
    fi
    
    if command -v reportgenerator >/dev/null 2>&1; then
        print_success "ReportGenerator is available"
    else
        print_warning "ReportGenerator not found in PATH. You may need to restart your shell or add ~/.dotnet/tools to PATH"
    fi
}

# Function to setup PATH for .NET tools
setup_path() {
    DOTNET_TOOLS_PATH="$HOME/.dotnet/tools"
    PROFILE_FILE="$HOME/.bashrc"
    
    # Check if using zsh
    if [ -n "$ZSH_VERSION" ]; then
        PROFILE_FILE="$HOME/.zshrc"
    fi
    
    # Add .NET tools to PATH if not already present
    if ! echo "$PATH" | grep -q "$DOTNET_TOOLS_PATH"; then
        echo "export PATH=\"\$PATH:$DOTNET_TOOLS_PATH\"" >> "$PROFILE_FILE"
        print_info "Added .NET tools to PATH in $PROFILE_FILE"
        print_warning "Please restart your shell or run: source $PROFILE_FILE"
    fi
}

# Main installation function
main() {
    print_info "GitTools Development Environment Setup"
    print_info "======================================"
    
    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --no-reportgenerator)
                INSTALL_REPORTGENERATOR=false
                shift
                ;;
            --help|-h)
                echo "Usage: $0 [--no-reportgenerator] [--help]"
                echo "  --no-reportgenerator  Skip ReportGenerator installation"
                echo "  --help, -h            Show this help message"
                exit 0
                ;;
            *)
                print_error "Unknown option: $1"
                echo "Use --help for usage information"
                exit 1
                ;;
        esac
    done
    
    # Check if running as root
    if [ "$EUID" -eq 0 ]; then
        print_error "Please do not run this script as root"
        exit 1
    fi
    
    # Detect distribution
    detect_distro
    
    # Check if .NET is already installed
    if check_dotnet_installed; then
        print_info "Skipping .NET SDK installation"
    else
        # Install .NET SDK based on distribution
        case $DISTRO in
            "ubuntu"|"debian")
                install_dotnet_ubuntu_debian
                ;;
            "rhel"|"centos"|"rocky"|"almalinux")
                install_dotnet_rhel
                ;;
            "fedora")
                install_dotnet_rhel
                ;;
            "arch"|"manjaro")
                install_dotnet_arch
                ;;
            *)
                print_warning "Unknown or unsupported distribution: $DISTRO"
                print_info "Attempting to install using snap..."
                install_dotnet_snap
                ;;
        esac
    fi
    
    # Install ReportGenerator
    install_reportgenerator
    
    # Setup PATH
    setup_path
    
    # Verify installation
    verify_installation
    
    echo ""
    print_success "Environment setup completed successfully!"
    print_info "You can now run the following commands:"
    echo "  - dotnet build                    # Build the project"
    echo "  - dotnet test                     # Run tests"
    echo "  - ./generate-coverage.sh          # Generate coverage report"
    
    echo ""
    print_warning "If reportgenerator command is not found, restart your shell or run:"
    echo "  source ~/.bashrc  # or ~/.zshrc if using zsh"
}

# Run main function
main "$@"
