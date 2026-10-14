#!/bin/bash
# Post-deployment smoke tests. They go through the web app, because the API is internal to
# the Container Apps environment (FR-038):
#   1. GET $WEB_URL/ returns 200 and lists a seeded User, which proves web -> API -> database.
#   2. GET $WEB_URL/health returns 200.
#   3. The API's address is NOT reachable from here.
# Any failure exits non-zero, so the deployment fails.
set -euo pipefail

echo "========================================="
echo "Post-deployment Smoke Tests"
echo "========================================="

# The deploy workflow passes the URLs; a local `azd deploy` reads them from the azd environment.
WEB_URL="${TASKIFY_WEB_URL:-}"
API_URL="${TASKIFY_API_URL:-}"
if [ -z "$WEB_URL" ] && command -v azd &> /dev/null && command -v jq &> /dev/null; then
    ENV_VALUES=$(azd env get-values --output json 2>/dev/null || echo "{}")
    WEB_URL=$(echo "$ENV_VALUES" | jq -r '.SERVICE_WEB_ENDPOINT_URL // empty')
    API_URL=$(echo "$ENV_VALUES" | jq -r '.SERVICE_API_ENDPOINT_URL // empty')
fi

if [ -z "$WEB_URL" ]; then
    echo "✗ Web URL not found (set TASKIFY_WEB_URL or run inside an azd environment)."
    exit 1
fi

# Endpoints are not printed: the workflow keeps them out of the public run log.

SEEDED_USER="Jordan Rivera"
ATTEMPTS=12
DELAY_SECONDS=10
FAILURES=0

# Retries cover cold starts: dev scales to zero.
check_web_home() {
    local body
    for attempt in $(seq 1 "$ATTEMPTS"); do
        body=$(curl -s --max-time 30 -w '\n%{http_code}' "$WEB_URL/" || true)
        if [ "$(echo "$body" | tail -n 1)" == "200" ] && echo "$body" | grep -q "$SEEDED_USER"; then
            echo "✓ Web home page lists the seeded Users (attempt $attempt)"
            return 0
        fi
        sleep "$DELAY_SECONDS"
    done
    echo "✗ Web home page did not return 200 with \"$SEEDED_USER\" after $ATTEMPTS attempts"
    return 1
}

check_web_health() {
    local status
    for attempt in $(seq 1 "$ATTEMPTS"); do
        status=$(curl -s -o /dev/null -w "%{http_code}" --max-time 30 "$WEB_URL/health" || true)
        if [ "$status" == "200" ]; then
            echo "✓ Web /health returned 200 (attempt $attempt)"
            return 0
        fi
        sleep "$DELAY_SECONDS"
    done
    echo "✗ Web /health did not return 200 (last: HTTP $status)"
    return 1
}

check_api_not_public() {
    if [ -z "$API_URL" ]; then
        echo "✗ API URL not found; cannot confirm the API is internal"
        return 1
    fi
    local status
    status=$(curl -s -o /dev/null -w "%{http_code}" --max-time 15 "$API_URL/health" || true)
    if [ "$status" == "200" ]; then
        echo "✗ API /health is reachable from outside the environment (HTTP 200)"
        return 1
    fi
    echo "✓ API is not reachable from outside the environment (HTTP $status)"
}

check_web_home || FAILURES=$((FAILURES + 1))
check_web_health || FAILURES=$((FAILURES + 1))
check_api_not_public || FAILURES=$((FAILURES + 1))

echo "========================================="
if [ "$FAILURES" -gt 0 ]; then
    echo "Smoke tests FAILED ($FAILURES check(s))"
    exit 1
fi
echo "Smoke tests PASSED"
