#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy Taskify application to Azure using Azure Developer CLI (azd)

.DESCRIPTION
    This script is a PowerShell wrapper around azd that provides parameter validation,
    environment selection, and deployment orchestration. It supports both infrastructure
    provisioning and application deployment with configurable options.

.PARAMETER Environment
    Target environment name (dev, staging, prod). Default: dev

.PARAMETER ProvisionOnly
    Provision infrastructure only, skip application deployment

.PARAMETER DeployOnly
    Deploy applications only, skip infrastructure provisioning

.PARAMETER SkipMigrations
    Skip database migrations during deployment

.PARAMETER DryRun
    Validate deployment without making changes (what-if mode)

.EXAMPLE
    ./deploy-to-azure.ps1 -Environment dev
    Deploy to development environment (full deployment)

.EXAMPLE
    ./deploy-to-azure.ps1 -Environment prod -ProvisionOnly
    Provision production infrastructure without deploying applications

.EXAMPLE
    ./deploy-to-azure.ps1 -DeployOnly
    Deploy applications to current environment without reprovisioning infrastructure

.EXAMPLE
    ./deploy-to-azure.ps1 -DryRun
    Validate Bicep templates without deploying
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('dev', 'staging', 'prod')]
    [string]$Environment = 'dev',

    [Parameter(Mandatory = $false)]
    [switch]$ProvisionOnly,

    [Parameter(Mandatory = $false)]
    [switch]$DeployOnly,

    [Parameter(Mandatory = $false)]
    [switch]$SkipMigrations,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun
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

function Write-Step {
    param([string]$Message)
    Write-Host "▶ $Message" -ForegroundColor Green
}

function Write-Error-Message {
    param([string]$Message)
    Write-Host "✗ ERROR: $Message" -ForegroundColor Red
}

function Write-Warning-Message {
    param([string]$Message)
    Write-Host "⚠ WARNING: $Message" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

# Validate prerequisites
Write-Header "Validating Prerequisites"

Write-Step "Checking Azure CLI..."
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error-Message "Azure CLI not found. Install from: https://aka.ms/install-azure-cli"
    exit 1
}
Write-Success "Azure CLI installed"

Write-Step "Checking Azure Developer CLI..."
if (-not (Get-Command azd -ErrorAction SilentlyContinue)) {
    Write-Error-Message "Azure Developer CLI (azd) not found. Install from: https://aka.ms/install-azd"
    exit 1
}
Write-Success "azd installed"

Write-Step "Checking .NET SDK..."
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error-Message ".NET SDK not found. Install from: https://dotnet.microsoft.com/download"
    exit 1
}
Write-Success ".NET SDK installed"

Write-Step "Verifying Azure authentication..."
try {
    $null = az account show 2>&1
    $subscription = (az account show --query name -o tsv)
    Write-Success "Authenticated to Azure subscription: $subscription"
}
catch {
    Write-Error-Message "Not logged in to Azure. Run 'az login' or 'azd auth login'"
    exit 1
}

# Validate Bicep templates (dry-run or always)
if ($DryRun -or -not $DeployOnly) {
    Write-Header "Validating Bicep Templates"
    
    Write-Step "Building main.bicep..."
    az bicep build --file ./infra/main.bicep
    if ($LASTEXITCODE -ne 0) {
        Write-Error-Message "Bicep validation failed for main.bicep"
        exit 1
    }
    Write-Success "main.bicep validated"

    $modules = @('monitoring', 'keyvault', 'postgresql', 'container-apps')
    foreach ($module in $modules) {
        Write-Step "Building $module.bicep..."
        az bicep build --file "./infra/resources/$module.bicep"
        if ($LASTEXITCODE -ne 0) {
            Write-Error-Message "Bicep validation failed for $module.bicep"
            exit 1
        }
    }
    Write-Success "All Bicep modules validated"
}

if ($DryRun) {
    Write-Success "Dry-run complete. No changes made."
    exit 0
}

# Configure azd environment
Write-Header "Configuring Environment"

Write-Step "Selecting azd environment: $Environment"
azd env select $Environment 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Warning-Message "Environment '$Environment' does not exist. Creating..."
    azd env new $Environment
}

Write-Step "Setting environment variables..."
azd env set AZURE_ENV_NAME $Environment

$location = Read-Host -Prompt "Azure location (default: eastus)"
if ([string]::IsNullOrWhiteSpace($location)) {
    $location = "eastus"
}
azd env set AZURE_LOCATION $location
Write-Success "Environment configured"

# Provision infrastructure
if (-not $DeployOnly) {
    Write-Header "Provisioning Azure Infrastructure"
    
    $paramFile = "./infra/main.parameters.$Environment.json"
    if (Test-Path $paramFile) {
        Write-Step "Using parameter file: $paramFile"
        azd provision --no-prompt --parameters $paramFile
    }
    else {
        Write-Warning-Message "No parameter file found for environment '$Environment', using defaults"
        azd provision --no-prompt
    }
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error-Message "Infrastructure provisioning failed"
        exit 1
    }
    Write-Success "Infrastructure provisioned successfully"
}

# Run database migrations
if (-not $ProvisionOnly -and -not $SkipMigrations) {
    Write-Header "Running Database Migrations"
    
    Write-Step "Retrieving database connection string..."
    $envValues = azd env get-values --output json | ConvertFrom-Json
    $connectionString = $envValues.POSTGRESQL_CONNECTION_STRING
    
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        Write-Warning-Message "Connection string not found. Skipping migrations."
    }
    else {
        Write-Step "Applying EF Core migrations..."
        $env:ConnectionStrings__DefaultConnection = $connectionString
        dotnet run --project src/Taskify.Migrator
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error-Message "Database migration failed"
            exit 1
        }
        Write-Success "Migrations applied successfully"
    }
}

# Deploy applications
if (-not $ProvisionOnly) {
    Write-Header "Deploying Applications"
    
    Write-Step "Building and deploying containers..."
    azd deploy --no-prompt
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error-Message "Application deployment failed"
        exit 1
    }
    Write-Success "Applications deployed successfully"
}

# Run smoke tests
if (-not $ProvisionOnly) {
    Write-Header "Running Smoke Tests"
    
    Write-Step "Executing post-deployment validation..."
    if (Test-Path ./infra/hooks/postdeploy.sh) {
        bash ./infra/hooks/postdeploy.sh
        if ($LASTEXITCODE -ne 0) {
            Write-Warning-Message "Smoke tests failed. Application may not be fully ready."
        }
        else {
            Write-Success "Smoke tests passed"
        }
    }
    else {
        Write-Warning-Message "Post-deployment script not found. Skipping smoke tests."
    }
}

# Display deployment summary
Write-Header "Deployment Summary"

Write-Step "Retrieving deployment outputs..."
$envValues = azd env get-values --output json | ConvertFrom-Json

Write-Host ""
Write-Host "Environment:        $Environment" -ForegroundColor Cyan
Write-Host "Subscription:       $subscription" -ForegroundColor Cyan
Write-Host "Location:           $location" -ForegroundColor Cyan
Write-Host ""

if ($envValues.TASKIFY_API_URL) {
    Write-Host "API URL:            $($envValues.TASKIFY_API_URL)" -ForegroundColor Green
}
if ($envValues.TASKIFY_WEB_URL) {
    Write-Host "Web URL:            $($envValues.TASKIFY_WEB_URL)" -ForegroundColor Green
}
Write-Host ""

Write-Success "Deployment complete! 🎉"
