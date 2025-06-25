#!/bin/bash
# Script to setup development environment for GitTools on Linux systems
# Installs .NET SDK and required tools for code coverage
# Can be run as root (e.g., in containerized environments like Codex)

set -e  # Exit on any error

DOTNET_VERSION="9.0"
INSTALL_REPORTGENERATOR=true
IS_ROOT=false

# Check if running as root
if [ "$EUID" -eq 0 ]; then
    IS_ROOT=true
fi

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
        
        # Accept .NET 6.0, 7.0, 8.0, or 9.0 as compatible versions, but prefer 9.0
        case $INSTALLED_VERSION in
            "9.0")
                print_success ".NET $INSTALLED_VERSION is already installed and is the preferred version"
                return 0
                ;;
            "6.0"|"7.0"|"8.0")
                print_warning ".NET $INSTALLED_VERSION is installed, but we prefer version 9.0"
                return 1
                ;;
            *)
                print_warning ".NET $INSTALLED_VERSION is installed, but we prefer a newer version"
                return 1
                ;;
        esac
    else
        print_info ".NET SDK not found"
        return 1
    fi
}

# Function to check available .NET packages
check_available_dotnet_packages() {
    print_info "Checking available .NET SDK packages..."
    if command -v apt-cache >/dev/null 2>&1; then
        AVAILABLE_PACKAGES=$(apt-cache search dotnet-sdk | grep -E "dotnet-sdk-[0-9]" || true)
        if [ -n "$AVAILABLE_PACKAGES" ]; then
            print_info "Available .NET SDK packages:"
            echo "$AVAILABLE_PACKAGES"
        else
            print_warning "No .NET SDK packages found in repositories"
        fi
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
    
    if [ "$IS_ROOT" = true ]; then
        dpkg -i packages-microsoft-prod.deb
        
        # Ensure software-properties-common is installed for add-apt-repository
        print_info "Installing software-properties-common..."
        apt-get update
        apt-get install -y software-properties-common
        
        # Add the .NET backports PPA for .NET 9.0
        print_info "Adding .NET backports PPA for .NET 9.0..."
        add-apt-repository -y ppa:dotnet/backports
        apt-get update
        
        # Check what packages are available
        check_available_dotnet_packages
        
        # Try to install .NET 9.0 first, then fallback to older versions
        apt-get install -y dotnet-sdk-9.0 || apt-get install -y dotnet-sdk-8.0 || apt-get install -y dotnet-sdk-7.0 || apt-get install -y dotnet-sdk-6.0
    else
        sudo dpkg -i packages-microsoft-prod.deb
        
        # Ensure software-properties-common is installed for add-apt-repository
        print_info "Installing software-properties-common..."
        sudo apt-get update
        sudo apt-get install -y software-properties-common
        
        # Add the .NET backports PPA for .NET 9.0
        print_info "Adding .NET backports PPA for .NET 9.0..."
        sudo add-apt-repository -y ppa:dotnet/backports
        sudo apt-get update
        
        # Check what packages are available
        check_available_dotnet_packages
        
        # Try to install .NET 9.0 first, then fallback to older versions
        sudo apt-get install -y dotnet-sdk-9.0 || sudo apt-get install -y dotnet-sdk-8.0 || sudo apt-get install -y dotnet-sdk-7.0 || sudo apt-get install -y dotnet-sdk-6.0
    fi
    
    rm packages-microsoft-prod.deb
}

# Function to install .NET SDK on CentOS/RHEL/Fedora
install_dotnet_rhel() {
    print_info "Installing .NET SDK on RHEL/CentOS/Fedora..."
    
    # Add Microsoft repository
    if [ "$DISTRO" = "fedora" ]; then
        if [ "$IS_ROOT" = true ]; then
            dnf install -y https://packages.microsoft.com/config/fedora/$(rpm -E %fedora)/packages-microsoft-prod.rpm
            dnf install -y dotnet-sdk-9.0 || dnf install -y dotnet-sdk-8.0 || dnf install -y dotnet-sdk-7.0 || dnf install -y dotnet-sdk-6.0
        else
            sudo dnf install -y https://packages.microsoft.com/config/fedora/$(rpm -E %fedora)/packages-microsoft-prod.rpm
            sudo dnf install -y dotnet-sdk-9.0 || sudo dnf install -y dotnet-sdk-8.0 || sudo dnf install -y dotnet-sdk-7.0 || sudo dnf install -y dotnet-sdk-6.0
        fi
    else
        # For RHEL/CentOS
        if [ "$IS_ROOT" = true ]; then
            yum install -y https://packages.microsoft.com/config/rhel/8/packages-microsoft-prod.rpm
            yum install -y dotnet-sdk-9.0 || yum install -y dotnet-sdk-8.0 || yum install -y dotnet-sdk-7.0 || yum install -y dotnet-sdk-6.0
        else
            sudo yum install -y https://packages.microsoft.com/config/rhel/8/packages-microsoft-prod.rpm
            sudo yum install -y dotnet-sdk-9.0 || sudo yum install -y dotnet-sdk-8.0 || sudo yum install -y dotnet-sdk-7.0 || sudo yum install -y dotnet-sdk-6.0
        fi
    fi
}

# Function to install .NET SDK on Arch Linux
install_dotnet_arch() {
    print_info "Installing .NET SDK on Arch Linux..."
    
    # Update package database and install .NET SDK
    if [ "$IS_ROOT" = true ]; then
        pacman -Sy
        pacman -S --noconfirm dotnet-sdk
    else
        sudo pacman -Sy
        sudo pacman -S --noconfirm dotnet-sdk
    fi
}

# Function to install .NET SDK using snap (fallback)
install_dotnet_snap() {
    print_info "Installing .NET SDK using snap (fallback method)..."
    
    if ! command -v snap >/dev/null 2>&1; then
        print_error "Snap is not available. Please install .NET SDK manually."
        print_info "Visit: https://docs.microsoft.com/en-us/dotnet/core/install/linux"
        exit 1
    fi
    
    if [ "$IS_ROOT" = true ]; then
        snap install dotnet-sdk --classic --channel=9.0/stable || snap install dotnet-sdk --classic --channel=8.0/stable || snap install dotnet-sdk --classic --channel=7.0/stable || snap install dotnet-sdk --classic
    else
        sudo snap install dotnet-sdk --classic --channel=9.0/stable || sudo snap install dotnet-sdk --classic --channel=8.0/stable || sudo snap install dotnet-sdk --classic --channel=7.0/stable || sudo snap install dotnet-sdk --classic
    fi
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
    
    if [ "$IS_ROOT" = true ]; then
        # When running as root, add to system-wide PATH
        PROFILE_FILE="/etc/profile.d/dotnet-tools.sh"
        if [ ! -f "$PROFILE_FILE" ]; then
            echo "export PATH=\"\$PATH:$DOTNET_TOOLS_PATH\"" > "$PROFILE_FILE"
            chmod +x "$PROFILE_FILE"
            print_info "Added .NET tools to system PATH in $PROFILE_FILE"
        fi
        # Also export for current session
        export PATH="$PATH:$DOTNET_TOOLS_PATH"
    else
        # When running as regular user, add to user profile
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
    fi
}

# Main installation function
main() {
    print_info "GitTools Development Environment Setup"
    print_info "======================================"
    
    if [ "$IS_ROOT" = true ]; then
        print_warning "Running as root - adapting behavior for containerized environments"
    fi
    
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
