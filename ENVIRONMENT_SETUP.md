# Environment Setup Scripts

This directory contains scripts to help set up the development environment for GitTools across different platforms.

## Scripts Available

### `setup-environment.sh` (Linux/macOS)

Automated setup script for Linux environments that installs:

- .NET 9.0 SDK
- ReportGenerator tool for code coverage
- Configures PATH for .NET tools

**Usage:**

```bash
# Basic setup (installs everything)
./setup-environment.sh

# Setup without ReportGenerator
./setup-environment.sh --no-reportgenerator

# Get help
./setup-environment.sh --help
```

**Supported Distributions:**

- Ubuntu/Debian (apt)
- RHEL/CentOS/Rocky/AlmaLinux (yum/dnf)
- Fedora (dnf)
- Arch Linux/Manjaro (pacman)
- Any distribution with snap support (fallback)

### `generate-coverage.ps1` (Windows)

Code coverage report generation for Windows using PowerShell.

**Usage:**

```powershell
# Generate coverage report
./generate-coverage.ps1

# Generate and open in browser
./generate-coverage.ps1 -OpenReport
```

### `generate-coverage.sh` (Linux/macOS)

Code coverage report generation for Linux/macOS using Bash.

**Usage:**

```bash
# Generate coverage report
./generate-coverage.sh

# Generate and open in browser
./generate-coverage.sh --open-report
```

## Quick Start for New Environments

### Linux (Codex, WSL, etc.)

```bash
# 1. Clone the repository
git clone <repository-url>
cd GitTools

# 2. Setup environment
chmod +x setup-environment.sh
./setup-environment.sh

# 3. Build and test
dotnet build
dotnet test

# 4. Generate coverage report
chmod +x generate-coverage.sh
./generate-coverage.sh
```

### Windows

```powershell
# 1. Clone the repository
git clone <repository-url>
cd GitTools

# 2. Ensure .NET 9.0 SDK is installed
dotnet --version

# 3. Install ReportGenerator (if not already installed)
dotnet tool install -g dotnet-reportgenerator-globaltool

# 4. Build and test
dotnet build
dotnet test

# 5. Generate coverage report
./generate-coverage.ps1
```

## Requirements

- **Linux:** bash, wget, and package manager (apt/yum/dnf/pacman/snap)
- **Windows:** PowerShell 5.1+ and .NET SDK
- **Both:** Git for version control

## Troubleshooting

### Linux Issues

**Permission denied:**

```bash
chmod +x setup-environment.sh
chmod +x generate-coverage.sh
```

**ReportGenerator not found after installation:**

```bash
# Restart shell or reload profile
source ~/.bashrc  # or ~/.zshrc for zsh users
```

**Package manager issues:**

- Update package lists: `sudo apt update` (Ubuntu/Debian)
- Clear package cache if needed
- For snap issues, try manual .NET installation from Microsoft docs

### Windows Issues

**Execution policy errors:**

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

**ReportGenerator not found:**

```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool
```

## Contributing

When modifying these scripts:

1. Test on target platforms
2. Maintain cross-platform compatibility
3. Keep error handling robust
4. Update documentation accordingly
