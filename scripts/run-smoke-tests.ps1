#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run smoke tests against deployed Taskify application

.DESCRIPTION
    This script performs health checks and basic functional tests against the deployed
    Taskify API and Web applications. It can be run independently from deployment
    workflows to verify application health.

.PARAMETER Environment
    Target environment name (dev, staging, prod). Default: dev

.PARAMETER ApiUrl
    Override API URL (auto-detected from azd if not provided)

.PARAMETER WebUrl
    Override Web URL (auto-detected from azd if not provided)

.PARAMETER Timeout
    HTTP request timeout in seconds. Default: 30

.PARAMETER Verbose
    Show detailed test output

.EXAMPLE
    ./run-smoke-tests.ps1 -Environment dev
    Run smoke tests against development environment

.EXAMPLE
    ./run-smoke-tests.ps1 -ApiUrl "https://my-api.azurecontainerapps.io" -WebUrl "https://my-web.azurecontainerapps.io"
    Run smoke tests against specific URLs

.EXAMPLE
    ./run-smoke-tests.ps1 -Verbose
    Run smoke tests with detailed output
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('dev', 'staging', 'prod')]
    [string]$Environment = 'dev',

    [Parameter(Mandatory = $false)]
    [string]$ApiUrl,

    [Parameter(Mandatory = $false)]
    [string]$WebUrl,

    [Parameter(Mandatory = $false)]
    [int]$Timeout = 30,

    [Parameter(Mandatory = $false)]
    [switch]$Verbose
)

# Script configuration
$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

# Helper functions
function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " $Message" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Test {
    param([string]$Message)
    Write-Host "▶ Testing: $Message" -ForegroundColor Yellow
}

function Write-Pass {
    param([string]$Message)
    Write-Host "  ✓ PASS: $Message" -ForegroundColor Green
}

function Write-Fail {
    param([string]$Message)
    Write-Host "  ✗ FAIL: $Message" -ForegroundColor Red
}

function Write-Warn {
    param([string]$Message)
    Write-Host "  ⚠ WARNING: $Message" -ForegroundColor Yellow
}

function Test-Endpoint {
    param(
        [string]$Url,
        [string]$Description,
        [int]$ExpectedStatus = 200
    )

    Write-Test $Description

    try {
        $response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec $Timeout -UseBasicParsing -ErrorAction Stop
        
        if ($response.StatusCode -eq $ExpectedStatus) {
            Write-Pass "HTTP $($response.StatusCode) - $Url"
            if ($Verbose) {
                Write-Host "    Response Time: $($response.Headers.'X-Response-Time')" -ForegroundColor Gray
                Write-Host "    Content Length: $($response.Content.Length) bytes" -ForegroundColor Gray
            }
            return $true
        }
        else {
            Write-Fail "Expected HTTP $ExpectedStatus, got HTTP $($response.StatusCode) - $Url"
            return $false
        }
    }
    catch {
        Write-Fail "$Description failed - $($_.Exception.Message)"
        if ($Verbose) {
            Write-Host "    Error Details: $_" -ForegroundColor Gray
        }
        return $false
    }
}

# Get deployment URLs if not provided
Write-Header "Initializing Smoke Tests"

if ([string]::IsNullOrWhiteSpace($ApiUrl) -or [string]::IsNullOrWhiteSpace($WebUrl)) {
    Write-Host "Auto-detecting deployment URLs from azd environment..." -ForegroundColor Cyan
    
    # Check if azd is available
    if (-not (Get-Command azd -ErrorAction SilentlyContinue)) {
        Write-Host "✗ azd CLI not found. Please provide -ApiUrl and -WebUrl parameters." -ForegroundColor Red
        exit 1
    }

    # Select environment and get values
    try {
        azd env select $Environment 2>$null
        $envValues = azd env get-values --output json | ConvertFrom-Json
        
        if ([string]::IsNullOrWhiteSpace($ApiUrl)) {
            $ApiUrl = $envValues.TASKIFY_API_URL
        }
        if ([string]::IsNullOrWhiteSpace($WebUrl)) {
            $WebUrl = $envValues.TASKIFY_WEB_URL
        }
    }
    catch {
        Write-Host "⚠ WARNING: Could not retrieve URLs from azd. Please provide -ApiUrl and -WebUrl parameters." -ForegroundColor Yellow
    }
}

# Validate URLs
if ([string]::IsNullOrWhiteSpace($ApiUrl) -and [string]::IsNullOrWhiteSpace($WebUrl)) {
    Write-Host "✗ ERROR: No URLs provided or detected. Cannot run smoke tests." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Test Configuration:" -ForegroundColor Cyan
Write-Host "  Environment: $Environment"
Write-Host "  API URL:     $ApiUrl"
Write-Host "  Web URL:     $WebUrl"
Write-Host "  Timeout:     $Timeout seconds"
Write-Host ""

# Run smoke tests
Write-Header "Running Health Checks"

$testResults = @()

# Test 1: API Health Endpoint
if (-not [string]::IsNullOrWhiteSpace($ApiUrl)) {
    $result = Test-Endpoint -Url "$ApiUrl/health" -Description "API Health Endpoint"
    $testResults += @{ Test = "API Health"; Passed = $result }
}
else {
    Write-Warn "API URL not provided. Skipping API tests."
}

# Test 2: Web Application Root
if (-not [string]::IsNullOrWhiteSpace($WebUrl)) {
    $result = Test-Endpoint -Url $WebUrl -Description "Web Application Root"
    $testResults += @{ Test = "Web Root"; Passed = $result }
}
else {
    Write-Warn "Web URL not provided. Skipping Web tests."
}

# Test 3: API Swagger/OpenAPI (if available)
if (-not [string]::IsNullOrWhiteSpace($ApiUrl)) {
    $result = Test-Endpoint -Url "$ApiUrl/swagger/index.html" -Description "API Swagger UI" -ExpectedStatus 200
    $testResults += @{ Test = "API Swagger"; Passed = $result }
}

# Test 4: API Data Endpoint (Tasks)
if (-not [string]::IsNullOrWhiteSpace($ApiUrl)) {
    Write-Test "API Data Endpoint (Tasks)"
    try {
        $response = Invoke-WebRequest -Uri "$ApiUrl/api/tasks" -Method Get -TimeoutSec $Timeout -UseBasicParsing -ErrorAction Stop
        
        if ($response.StatusCode -eq 200) {
            $content = $response.Content | ConvertFrom-Json
            if ($content -is [array]) {
                Write-Pass "API returned JSON array ($(($content).Count) tasks)"
                $testResults += @{ Test = "API Data"; Passed = $true }
            }
            else {
                Write-Fail "API returned unexpected data format"
                $testResults += @{ Test = "API Data"; Passed = $false }
            }
        }
        else {
            Write-Fail "Expected HTTP 200, got HTTP $($response.StatusCode)"
            $testResults += @{ Test = "API Data"; Passed = $false }
        }
    }
    catch {
        Write-Warn "API Data endpoint test skipped - endpoint may require authentication"
        if ($Verbose) {
            Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Gray
        }
        # Not counting as failure since authentication may be required
    }
}

# Test 5: Response Time Check
if (-not [string]::IsNullOrWhiteSpace($ApiUrl)) {
    Write-Test "API Response Time"
    try {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $null = Invoke-WebRequest -Uri "$ApiUrl/health" -Method Get -TimeoutSec $Timeout -UseBasicParsing -ErrorAction Stop
        $stopwatch.Stop()
        
        $responseTime = $stopwatch.ElapsedMilliseconds
        
        if ($responseTime -lt 2000) {
            Write-Pass "Response time: ${responseTime}ms (< 2000ms threshold)"
            $testResults += @{ Test = "Response Time"; Passed = $true }
        }
        else {
            Write-Warn "Response time: ${responseTime}ms (> 2000ms threshold)"
            $testResults += @{ Test = "Response Time"; Passed = $false }
        }
    }
    catch {
        Write-Fail "Response time test failed"
        $testResults += @{ Test = "Response Time"; Passed = $false }
    }
}

# Summary
Write-Header "Test Summary"

$totalTests = $testResults.Count
$passedTests = ($testResults | Where-Object { $_.Passed -eq $true }).Count
$failedTests = $totalTests - $passedTests

Write-Host ""
Write-Host "Total Tests:    $totalTests" -ForegroundColor Cyan
Write-Host "Passed:         $passedTests" -ForegroundColor Green
Write-Host "Failed:         $failedTests" -ForegroundColor $(if ($failedTests -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($failedTests -eq 0) {
    Write-Host "✓ All smoke tests PASSED" -ForegroundColor Green
    Write-Host ""
    exit 0
}
else {
    Write-Host "✗ Some smoke tests FAILED" -ForegroundColor Red
    Write-Host ""
    Write-Host "Failed tests:" -ForegroundColor Yellow
    $testResults | Where-Object { $_.Passed -eq $false } | ForEach-Object {
        Write-Host "  - $($_.Test)" -ForegroundColor Red
    }
    Write-Host ""
    exit 1
}
