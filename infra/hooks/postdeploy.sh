#!/bin/bash
set -e

echo "========================================="
echo "Post-deployment Smoke Tests"
echo "========================================="

# Get deployment outputs from azd environment
echo "Retrieving deployment outputs..."

# Check if azd env get-values is available
if ! command -v azd &> /dev/null; then
    echo "WARNING: azd CLI not found. Skipping smoke tests."
    echo "Install azd: https://aka.ms/install-azd"
    exit 0
fi

# Get environment values
echo "Loading environment variables..."
ENV_VALUES=$(azd env get-values --output json 2>/dev/null || echo "{}")

if [ "$ENV_VALUES" == "{}" ]; then
    echo "WARNING: No environment values found. Deployment may not be complete."
    echo "Skipping smoke tests."
    exit 0
fi

# Extract URLs using jq if available, otherwise skip
if ! command -v jq &> /dev/null; then
    echo "WARNING: jq not found. Cannot parse environment values."
    echo "Install jq to enable smoke tests: https://stedolan.github.io/jq/"
    exit 0
fi

API_URL=$(echo "$ENV_VALUES" | jq -r '.TASKIFY_API_URL // empty')
WEB_URL=$(echo "$ENV_VALUES" | jq -r '.TASKIFY_WEB_URL // empty')

echo ""
echo "Deployment endpoints:"
echo "  API: $API_URL"
echo "  Web: $WEB_URL"

# Test API health endpoint
if [ -n "$API_URL" ]; then
    echo ""
    echo "Testing API health endpoint..."
    
    HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" --max-time 30 "${API_URL}/health" || echo "000")
    
    if [ "$HTTP_STATUS" == "200" ]; then
        echo "✓ API health check PASSED (HTTP $HTTP_STATUS)"
    else
        echo "✗ API health check FAILED (HTTP $HTTP_STATUS)"
        echo "WARNING: API may not be fully deployed or may be starting up."
        echo "Please verify manually: ${API_URL}/health"
    fi
else
    echo "WARNING: API URL not found in environment. Skipping API health check."
fi

# Test Web health endpoint (if it exists)
if [ -n "$WEB_URL" ]; then
    echo ""
    echo "Testing Web application endpoint..."
    
    HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" --max-time 30 "$WEB_URL" || echo "000")
    
    if [ "$HTTP_STATUS" == "200" ]; then
        echo "✓ Web application check PASSED (HTTP $HTTP_STATUS)"
    else
        echo "✗ Web application check FAILED (HTTP $HTTP_STATUS)"
        echo "WARNING: Web app may not be fully deployed or may be starting up."
        echo "Please verify manually: $WEB_URL"
    fi
else
    echo "WARNING: Web URL not found in environment. Skipping Web check."
fi

echo ""
echo "========================================="
echo "Post-deployment smoke tests COMPLETED"
echo "========================================="
echo ""
echo "Application URLs:"
if [ -n "$API_URL" ]; then
    echo "  🔗 API:  $API_URL"
fi
if [ -n "$WEB_URL" ]; then
    echo "  🔗 Web:  $WEB_URL"
fi
echo ""
echo "Note: Container Apps may take 1-2 minutes to fully start."
echo "If health checks failed, wait a moment and try accessing the URLs manually."
