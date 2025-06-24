#!/bin/bash
# Script to generate and view code coverage report

OPEN_REPORT=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --open-report|-o)
            OPEN_REPORT=true
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [--open-report|-o] [--help|-h]"
            echo "  --open-report, -o    Open the report in the default browser after generation"
            echo "  --help, -h           Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

echo "🧪 Running tests with code coverage..."

# Execute tests with coverage collection
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults --verbosity minimal

if [ $? -ne 0 ]; then
    echo "❌ Test execution failed!"
    exit 1
fi

echo "✅ Tests executed successfully!"

# Find the most recent coverage file
LATEST_COVERAGE_FILE=$(find TestResults/*/coverage.cobertura.xml -type f 2>/dev/null | xargs ls -t | head -n 1)

if [ -z "$LATEST_COVERAGE_FILE" ]; then
    echo "❌ Coverage file not found!"
    exit 1
fi

echo "📊 Generating HTML coverage report..."

# Generate HTML report
reportgenerator -reports:"$LATEST_COVERAGE_FILE" -targetdir:"CoverageReport" -reporttypes:"Html"

if [ $? -ne 0 ]; then
    echo "❌ Report generation failed!"
    exit 1
fi

echo "✅ Report generated successfully at: CoverageReport/index.html"

# Show coverage summary
if [ -f "CoverageReport/Summary.txt" ]; then
    echo ""
    echo "📋 Coverage Summary:"
    head -n 15 "CoverageReport/Summary.txt"
fi

# Open report if requested
if [ "$OPEN_REPORT" = true ]; then
    echo "🌐 Opening report in browser..."
    
    # Try different ways to open the browser based on the system
    if command -v xdg-open > /dev/null 2>&1; then
        xdg-open "CoverageReport/index.html"
    elif command -v open > /dev/null 2>&1; then
        open "CoverageReport/index.html"
    elif command -v gnome-open > /dev/null 2>&1; then
        gnome-open "CoverageReport/index.html"
    else
        echo "⚠️  Could not automatically open browser. Please open CoverageReport/index.html manually."
    fi
fi

echo ""
echo "💡 To open the report manually:"
echo "   - Open: CoverageReport/index.html"
echo "   - Or run: ./generate-coverage.sh --open-report"
