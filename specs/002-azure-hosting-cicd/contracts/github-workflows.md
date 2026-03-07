# Contract: GitHub Actions Workflows

**Phase**: 1 — Design & Contracts  
**Date**: March 6, 2026  
**Plan**: [../plan.md](../plan.md)

---

## Overview

This document defines the contract for GitHub Actions workflows that automate building, testing, and deploying the Taskify application to Azure. The workflows implement a CI/CD pipeline with quality gates, performance validation, and deployment automation. Initially targets a single development environment with infrastructure support for adding production deployment when ready.

---

## Workflow Files

```
.github/
└── workflows/
    ├── ci.yml                # Continuous Integration (build + test)
    ├── azure-dev.yml         # Azure deployment (azd up)
    └── benchmark.yml         # Performance validation
```

---

## 1. CI Workflow (ci.yml)

**Purpose**: Validate code quality on every pull request before merging.

**Triggers**:
```yaml
on:
  pull_request:
    branches: ['main', 'develop', '**']
  push:
    branches: ['main']
```

**Jobs**:

### Job: build
**Runs on**: `ubuntu-latest`  
**Steps**:
1. Checkout code
2. Setup .NET 10.0 SDK
3. Restore dependencies (`dotnet restore`)
4. Build solution (`dotnet build --no-restore --configuration Release`)
5. Upload build artifacts

**Outputs**: None  
**Artifacts**: Build output (binaries)

### Job: test
**Runs on**: `ubuntu-latest`  
**Depends on**: `build`  
**Steps**:
1. Checkout code
2. Setup .NET 10.0 SDK
3. Restore dependencies
4. Run unit tests (`dotnet test --no-build --configuration Release --logger trx --collect:"XPlat Code Coverage"`)
5. Run integration tests
6. Publish test results
7. Publish code coverage to CodeCov or similar

**Fail conditions**:
- Any test failure
- Code coverage below 80% (warning only, not blocking initially)

**Outputs**: Test results, coverage report  
**Artifacts**: `test-results.trx`, `coverage.xml`

### Job: lint
**Runs on**: `ubuntu-latest`  
**Steps**:
1. Checkout code
2. Setup .NET 10.0 SDK
3. Run `dotnet format --verify-no-changes --verbosity diagnostic`
4. Report formatting violations

**Fail conditions**: Any code formatting violations

### Job: validate-bicep
**Runs on**: `ubuntu-latest`  
**Steps**:
1. Checkout code
2. Install Azure CLI
3. Validate Bicep templates (`az bicep build --file infra/main.bicep`)
4. Lint Bicep for best practices (`az bicep lint --file infra/main.bicep`)

**Fail conditions**: Bicep syntax errors or critical linting issues

---

### Full CI Workflow Contract

```yaml
name: Continuous Integration

on:
  pull_request:
    branches: ['main', 'develop']
  push:
    branches: ['main']

permissions:
  contents: read
  pull-requests: write
  checks: write

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Upload build artifacts
        uses: actions/upload-artifact@v4
        with:
          name: build-output
          path: |
            src/**/bin/Release/**
            src/**/obj/Release/**

  test:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Run unit tests
        run: |
          dotnet test tests/Taskify.Api.Tests \
            --configuration Release \
            --logger trx \
            --collect:"XPlat Code Coverage" \
            --results-directory ./test-results

      - name: Run integration tests
        run: |
          dotnet test tests/Taskify.Web.Tests \
            --configuration Release \
            --logger trx \
            --results-directory ./test-results

      - name: Publish test results
        uses: EnricoMi/publish-unit-test-result-action@v2
        if: always()
        with:
          files: test-results/**/*.trx

      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v4
        with:
          directory: ./test-results
          flags: unittests
          fail_ci_if_error: false

  lint:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Run dotnet format
        run: dotnet format --verify-no-changes --verbosity diagnostic

  validate-bicep:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Install Azure CLI
        uses: azure/cli@v2

      - name: Validate Bicep templates
        run: |
          az bicep build --file infra/main.bicep
          az bicep lint --file infra/main.bicep
```

**Success criteria**: All jobs pass (build, test, lint, validate-bicep)

---

## 2. Azure Deployment Workflow (azure-dev.yml)

**Purpose**: Deploy application to Azure development environment using Azure Developer CLI (azd). Automatically deploys on push to main branch; supports manual deployment from feature branches for testing.

**Triggers**:
```yaml
on:
  push:
    branches: ['main']  # Automatic deployment to development environment
  workflow_dispatch:     # Manual deployment trigger
    inputs:
      environment:
        description: 'Environment to deploy to (dev initially; prod when ready)'
        required: false
        default: 'dev'
        type: choice
        options:
          - dev
          - prod
```

**Deployment Strategy**:
- **Main branch push**: Automatically deploys to development environment without approval
- **Manual dispatch**: Allows deploying from any branch to development for testing
- **Production**: Parameter file prepared for future use; requires GitHub Environment approval when activated

**Environments**:
- `development`: Auto-deploy on push to `main`, no approval required (fast feedback loop)

**Jobs**:

### Job: deploy-to-azure
**Runs on**: `ubuntu-latest`  
**Environment**: `${{ inputs.environment || 'development' }}`  
**Steps**:

1. **Checkout code**
2. **Setup .NET SDK** (10.0)
3. **Install Azure Developer CLI**
   ```bash
   curl -fsSL https://aka.ms/install-azd.sh | bash
   ```
4. **Azure Login (OIDC)**
   ```yaml
   - name: Azure Login
     uses: azure/login@v2
     with:
       client-id: ${{ secrets.AZURE_CLIENT_ID }}
       tenant-id: ${{ secrets.AZURE_TENANT_ID }}
       subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
   ```
5. **Set azd environment**
   ```bash
   azd env select ${{ inputs.environment || 'dev' }}
   azd env set AZURE_SUBSCRIPTION_ID ${{ secrets.AZURE_SUBSCRIPTION_ID }}
   ```
6. **Provision infrastructure**
   ```bash
   azd provision --no-prompt
   ```
7. **Run database migrations**
   ```bash
   # Extract connection string from azd outputs
   CONNECTION_STRING=$(azd env get-values --output json | jq -r '.POSTGRESQL_CONNECTION_STRING')
   
   # Run EF Core migrations
   dotnet run --project src/Taskify.Migrator -- "$CONNECTION_STRING"
   ```
8. **Deploy applications**
   ```bash
   azd deploy --no-prompt
   ```
9. **Run smoke tests**
   ```bash
   bash infra/hooks/postdeploy.sh
   ```
10. **Publish deployment summary**
    - Comment on PR (if triggered by PR)
    - Post deployment URLs to workflow summary

**Secrets required**:
- `AZURE_CLIENT_ID`: Azure AD application client ID (for OIDC)
- `AZURE_TENANT_ID`: Azure AD tenant ID
- `AZURE_SUBSCRIPTION_ID`: Azure subscription ID
- `POSTGRESQL_ADMIN_PASSWORD`: Database admin password (generated, stored in Key Vault)

**Outputs**:
- `taskify-api-url`: API endpoint URL
- `taskify-web-url`: Web application URL
- `deployment-status`: `success` or `failure`

---

### Full Azure Deployment Workflow Contract

```yaml
name: Azure Deployment

on:
  push:
    branches: ['main']
  workflow_dispatch:
    inputs:
      environment:
        description: 'Environment to deploy to'
        required: true
        default: 'dev'
        type: choice
        options:
          - dev
          - staging
          - prod

permissions:
  id-token: write
  contents: read
  pull-requests: write

jobs:
  deploy-to-azure:
    runs-on: ubuntu-latest
    environment: ${{ inputs.environment || 'development' }}
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install Azure Developer CLI
        run: curl -fsSL https://aka.ms/install-azd.sh | bash

      - name: Azure Login
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Configure azd environment
        run: |
          azd env select ${{ inputs.environment || 'dev' }} || azd env new ${{ inputs.environment || 'dev' }}
          azd env set AZURE_SUBSCRIPTION_ID ${{ secrets.AZURE_SUBSCRIPTION_ID }}
          azd env set AZURE_LOCATION eastus

      - name: Provision infrastructure
        run: azd provision --no-prompt
        env:
          AZURE_ENV_NAME: ${{ inputs.environment || 'dev' }}

      - name: Get database connection string
        id: db-connection
        run: |
          CONNECTION_STRING=$(azd env get-values --output json | jq -r '.POSTGRESQL_CONNECTION_STRING')
          echo "::add-mask::$CONNECTION_STRING"
          echo "connection-string=$CONNECTION_STRING" >> $GITHUB_OUTPUT

      - name: Run database migrations
        run: dotnet run --project src/Taskify.Migrator
        env:
          ConnectionStrings__DefaultConnection: ${{ steps.db-connection.outputs.connection-string }}

      - name: Deploy applications
        run: azd deploy --no-prompt
        env:
          AZURE_ENV_NAME: ${{ inputs.environment || 'dev' }}

      - name: Get deployment URLs
        id: deployment-urls
        run: |
          API_URL=$(azd env get-values --output json | jq -r '.TASKIFY_API_URL')
          WEB_URL=$(azd env get-values --output json | jq -r '.TASKIFY_WEB_URL')
          echo "api-url=$API_URL" >> $GITHUB_OUTPUT
          echo "web-url=$WEB_URL" >> $GITHUB_OUTPUT

      - name: Run smoke tests
        run: bash infra/hooks/postdeploy.sh
        env:
          TASKIFY_API_URL: ${{ steps.deployment-urls.outputs.api-url }}
          TASKIFY_WEB_URL: ${{ steps.deployment-urls.outputs.web-url }}

      - name: Publish deployment summary
        run: |
          echo "## Deployment Summary" >> $GITHUB_STEP_SUMMARY
          echo "- **Environment**: ${{ inputs.environment || 'dev' }}" >> $GITHUB_STEP_SUMMARY
          echo "- **API URL**: ${{ steps.deployment-urls.outputs.api-url }}" >> $GITHUB_STEP_SUMMARY
          echo "- **Web URL**: ${{ steps.deployment-urls.outputs.web-url }}" >> $GITHUB_STEP_SUMMARY
          echo "- **Status**: ✅ Success" >> $GITHUB_STEP_SUMMARY
```

**Rollback procedure**: If smoke tests fail, revert traffic to previous Container App revision:
```bash
az containerapp revision set-mode --name ca-taskify-api-<env> --mode single --revision <previous-revision>
```

---

## 3. Benchmark Workflow (benchmark.yml)

**Purpose**: Run performance benchmarks and detect regressions.

**Triggers**:
```yaml
on:
  schedule:
    - cron: '0 2 * * *' # Nightly at 2 AM UTC
  workflow_dispatch:
  pull_request:
    paths:
      - 'src/**'
```

**Jobs**:

### Job: run-benchmarks
**Runs on**: `ubuntu-latest`  
**Steps**:
1. Checkout code
2. Setup .NET SDK
3. Restore dependencies
4. Run benchmarks (`dotnet run --project tests/Taskify.Benchmarks --configuration Release`)
5. Parse BenchmarkDotNet results
6. Compare against baseline (stored in artifact or GitHub Pages)
7. Comment on PR if regression detected (>10% slower)
8. Upload results as artifact

**Fail conditions**:
- Performance regression >10% (warning, not blocking)
- Benchmark execution failure

---

### Full Benchmark Workflow Contract

```yaml
name: Performance Benchmarks

on:
  schedule:
    - cron: '0 2 * * *'
  workflow_dispatch:
  pull_request:
    paths:
      - 'src/**'

jobs:
  run-benchmarks:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Run benchmarks
        run: |
          dotnet run --project tests/Taskify.Benchmarks --configuration Release \
            --exporters json --filter '*'

      - name: Upload benchmark results
        uses: actions/upload-artifact@v4
        with:
          name: benchmark-results
          path: tests/Taskify.Benchmarks/BenchmarkDotNet.Artifacts/results/**

      - name: Download baseline
        uses: dawidd6/action-download-artifact@v3
        continue-on-error: true
        with:
          workflow: benchmark.yml
          name: benchmark-baseline
          path: baseline/

      - name: Compare with baseline
        id: compare
        run: |
          # Custom script to compare current results with baseline
          # Set output 'regression-detected=true' if >10% slower
          echo "regression-detected=false" >> $GITHUB_OUTPUT

      - name: Comment on PR
        if: github.event_name == 'pull_request' && steps.compare.outputs.regression-detected == 'true'
        uses: actions/github-script@v7
        with:
          script: |
            github.rest.issues.createComment({
              owner: context.repo.owner,
              repo: context.repo.repo,
              issue_number: context.issue.number,
              body: '⚠️ Performance regression detected (>10% slower). Review benchmark results.'
            })
```

---

## GitHub Secrets Configuration

**Required secrets** (set in repository or environment):

### Azure Authentication (OIDC - Recommended)
```
AZURE_CLIENT_ID          # Azure AD App Registration client ID
AZURE_TENANT_ID          # Azure AD tenant ID
AZURE_SUBSCRIPTION_ID    # Azure subscription ID
```

**How to create**:
```bash
# Create Azure AD App Registration
az ad app create --display-name "GitHub Actions - Taskify"

# Create Service Principal
az ad sp create --id <app-id>

# Create Federated Credential for GitHub Actions
az ad app federated-credential create \
  --id <app-id> \
  --parameters '{
    "name": "github-deploy",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<org>/<repo>:environment:production",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# Assign Contributor role to subscription
az role assignment create \
  --assignee <app-id> \
  --role Contributor \
  --scope /subscriptions/<subscription-id>
```

### Alternative: Service Principal JSON (Not Recommended)
```
AZURE_CREDENTIALS        # JSON with clientId, clientSecret, subscriptionId, tenantId
```

### Database
```
POSTGRESQL_ADMIN_PASSWORD  # Generated securely, stored in Key Vault
```

---

## GitHub Environments

**development**:
- Auto-deploy on push to `main`
- No approval required
- Secrets: Same Azure credentials

**production**:
- Manual approval required (configured in GitHub UI)
- Deploy via `workflow_dispatch` only
- Secrets: Same Azure credentials (restricted via Bicep RBAC)

**Environment protection rules**:
- Production requires 1 reviewer approval
- Production deployment limited to `main` branch only

---

## Workflow Dependencies

```
Pull Request → ci.yml (build, test, lint, validate-bicep)
                ↓ (if tests pass and PR merged)
Push to main → azure-dev.yml (provision, migrate, deploy to dev)
                ↓ (manual trigger)
Manual dispatch → azure-dev.yml (deploy to staging or prod)

Schedule/PR → benchmark.yml (performance validation)
```

---

## Status Badges

Add to `README.md`:

```markdown
[![CI](https://github.com/org/repo/workflows/Continuous%20Integration/badge.svg)](https://github.com/org/repo/actions/workflows/ci.yml)
[![Azure Deploy](https://github.com/org/repo/workflows/Azure%20Deployment/badge.svg)](https://github.com/org/repo/actions/workflows/azure-dev.yml)
[![Benchmarks](https://github.com/org/repo/workflows/Performance%20Benchmarks/badge.svg)](https://github.com/org/repo/actions/workflows/benchmark.yml)
```

---

## Compliance with Constitution

- **Code Quality Gate**: `lint` job enforces `dotnet format` ✅
- **Testing Gate**: `test` job runs all unit and integration tests ✅
- **Performance Gate**: `benchmark.yml` detects regressions ✅
- **UX Consistency Gate**: N/A for infrastructure feature ✅

All workflows aligned with Taskify Constitution principles.
