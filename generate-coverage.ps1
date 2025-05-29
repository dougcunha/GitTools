#!/usr/bin/env pwsh
# Script to generate and view code coverage report

param(
    [switch]$OpenReport = $false
)

Write-Host "🧪 Running tests with code coverage..." -ForegroundColor Yellow

# Execute tests with coverage collection
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults --verbosity minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Test execution failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Tests executed successfully!" -ForegroundColor Green

# Find the most recent coverage file
$latestCoverageFile = Get-ChildItem -Path "TestResults\*\coverage.cobertura.xml" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $latestCoverageFile) {
    Write-Host "❌ Coverage file not found!" -ForegroundColor Red
    exit 1
}

Write-Host "📊 Generating HTML coverage report..." -ForegroundColor Yellow

# Generate HTML report
reportgenerator -reports:"$($latestCoverageFile.FullName)" -targetdir:"CoverageReport" -reporttypes:"Html"

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Report generation failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Report generated successfully at: CoverageReport\index.html" -ForegroundColor Green

# Show coverage summary
if (Test-Path "CoverageReport\Summary.txt") {
    Write-Host "`n📋 Coverage Summary:" -ForegroundColor Cyan
    Get-Content "CoverageReport\Summary.txt" | Select-Object -First 15
}

# Open report if requested
if ($OpenReport) {
    Write-Host "🌐 Opening report in browser..." -ForegroundColor Yellow
    Start-Process "CoverageReport\index.html"
}

Write-Host "`n💡 To open the report manually:" -ForegroundColor Blue
Write-Host "   - Open: CoverageReport\index.html" -ForegroundColor Blue
Write-Host "   - Or run: .\generate-coverage.ps1 -OpenReport" -ForegroundColor Blue
