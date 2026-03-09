# GitHub Actions Configuration Guide

This guide walks you through setting up GitHub Actions authentication and secrets for automated Azure deployment.

## Prerequisites

- Azure CLI installed and authenticated (`az login`)
- GitHub repository created
- Repository admin access to configure secrets
- Azure subscription Contributor or Owner role

## Option 1: Azure OIDC Authentication (Recommended)

OIDC (OpenID Connect) provides passwordless authentication from GitHub Actions to Azure using Federated Identity Credentials. This is more secure than storing service principal secrets.

### Step 1: Create Azure AD App Registration

```bash
# Set variables
APP_NAME="GitHub-Actions-Taskify"
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
REPO_OWNER="<your-github-org>"  # e.g., "contoso"
REPO_NAME="<your-repo-name>"    # e.g., "taskify"

# Create App Registration
APP_ID=$(az ad app create \
  --display-name "$APP_NAME" \
  --query appId -o tsv)

echo "App Registration created with ID: $APP_ID"
```

### Step 2: Create Service Principal

```bash
# Create Service Principal from App Registration
az ad sp create --id $APP_ID

echo "Service Principal created for App ID: $APP_ID"
```

### Step 3: Assign Azure RBAC Role

```bash
# Assign Contributor role to the subscription
az role assignment create \
  --assignee $APP_ID \
  --role Contributor \
  --scope /subscriptions/$SUBSCRIPTION_ID

echo "Contributor role assigned to Service Principal"
```

### Step 4: Create Federated Identity Credential for Main Branch

```bash
# Create credential for main branch deployments (dev environment)
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "github-deploy-main-branch",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:'"$REPO_OWNER"'/'"$REPO_NAME"':ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'

echo "Federated credential created for main branch deployments"
```

### Step 5: (Optional) Create Federated Credential for Production Environment

```bash
# Create credential for 'production' GitHub environment (manual deployments)
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "github-deploy-production",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:'"$REPO_OWNER"'/'"$REPO_NAME"':environment:production",
    "audiences": ["api://AzureADTokenExchange"]
  }'

echo "Federated credential created for production environment"
```

**Note**: The workflow requires **both** credentials:
- **Main branch credential**: For automatic dev deployments on push to main
- **Production environment credential**: For manual production deployments via workflow_dispatch

### Step 6: Get Required IDs

```bash
# Get Tenant ID
TENANT_ID=$(az account show --query tenantId -o tsv)

# Display all required values for GitHub Secrets
echo ""
echo "================================================"
echo "GitHub Secrets Configuration"
echo "================================================"
echo "AZURE_CLIENT_ID:        $APP_ID"
echo "AZURE_TENANT_ID:        $TENANT_ID"
echo "AZURE_SUBSCRIPTION_ID:  $SUBSCRIPTION_ID"
echo "================================================"
echo ""
echo "Copy these values to GitHub repository secrets."
```

---

## Option 2: Service Principal with Secret (Alternative)

If OIDC is not available, use a client secret (less secure, expires after 2 years max):

```bash
# Create Service Principal with password
SP_OUTPUT=$(az ad sp create-for-rbac \
  --name "$APP_NAME" \
  --role Contributor \
  --scopes /subscriptions/$SUBSCRIPTION_ID \
  --sdk-auth)

echo "$SP_OUTPUT"
```

Copy the **entire JSON output** and store it as GitHub secret `AZURE_CREDENTIALS`.

**Disadvantages**:
- Secret expires (max 2 years)
- Must be rotated regularly
- More risk if leaked

---

## Configure GitHub Secrets

### Step 1: Navigate to Repository Settings

1. Go to your GitHub repository
2. Click **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**

### Step 2: Add Required Secrets

Add the following secrets (values from Step 6 above):

| Secret Name | Value | Description |
|-------------|-------|-------------|
| `AZURE_CLIENT_ID` | `<APP_ID>` | Azure AD App Registration client ID |
| `AZURE_TENANT_ID` | `<TENANT_ID>` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | `<SUBSCRIPTION_ID>` | Azure subscription ID |
| `POSTGRESQL_ADMIN_PASSWORD` | `<generated-password>` | PostgreSQL admin password (see below) |

### Step 3: Generate PostgreSQL Admin Password

```bash
# Generate a secure random password
POSTGRES_PASSWORD=$(openssl rand -base64 32)

echo "PostgreSQL Admin Password: $POSTGRES_PASSWORD"
```

Add this as GitHub secret `POSTGRESQL_ADMIN_PASSWORD`.

**Security note**: This password is used only during initial provisioning. Container Apps use Managed Identity to access PostgreSQL in production.

---

## Configure GitHub Environments

GitHub Environments provide deployment protection rules and environment-specific secrets.

### Create Production Environment (Optional but Recommended)

1. Go to **Settings** → **Environments**
2. Click **New environment**
3. Name: `production`
4. Click **Configure environment**
5. **Protection rules**:
   - ✅ **Required reviewers**: Add yourself and/or team members (1+ approvals)
   - ✅ **Wait timer**: 0 minutes (or add delay for final checks)
   - ✅ **Deployment branches**: Selected branches only → `main`
6. Click **Save protection rules**

**Purpose**: Manual approval gate for production deployments via `workflow_dispatch`.

**Note**: The workflow does **not** use a "development" environment. Dev deployments automatically run when pushing to the `main` branch without any environment gates.

### Verify Environment Configuration

```bash
# Test GitHub Actions workflow permissions
# Push a test commit to main branch:
echo "# Test" >> README.md
git add README.md
git commit -m "test: trigger GitHub Actions"
git push origin main

# Monitor workflow run in GitHub UI:
# https://github.com/<org>/<repo>/actions
```

---

## Verify Setup

### Test OIDC Authentication Locally (Optional)

```bash
# Login using Service Principal
az login --service-principal \
  --username $APP_ID \
  --tenant $TENANT_ID \
  --allow-no-subscriptions \
  --federated-token "$(curl -H 'Authorization: bearer $ACTIONS_ID_TOKEN_REQUEST_TOKEN' '$ACTIONS_ID_TOKEN_REQUEST_URL' | jq -r '.value')"

# Verify access
az account show
```

**Note**: This only works from within GitHub Actions. For local testing, use `az login` with your user account.

### Test GitHub Actions Workflow

1. **Trigger CI workflow**: Open a pull request
   - CI workflow (`.github/workflows/ci.yml`) should run automatically
   - Verify all jobs pass (build, test, lint, validate-bicep)

2. **Trigger deployment workflow**: Merge PR to `main`
   - Azure deployment workflow (`.github/workflows/azure-dev.yml`) should run automatically
   - Verify OIDC authentication succeeds
   - Verify `azd provision` and `azd deploy` complete successfully

3. **Manual deployment**: Use workflow_dispatch
   - Go to **Actions** → **Azure Deployment**
   - Click **Run workflow**
   - Select `environment: dev`
   - Click **Run workflow**

---

## Troubleshooting

### Error: "Failed to authenticate with Azure"

**Cause**: Federated credential subject doesn't match.

**Solution**: Verify the credential subject matches your repository and environment:

```bash
az ad app federated-credential list --id $APP_ID --query "[].{Name:name, Subject:subject}"
```

**Expected subject formats**:
- Development: `repo:org/repo:environment:development`
- Production: `repo:org/repo:environment:production`
- Main branch: `repo:org/repo:ref:refs/heads/main`

### Error: "Insufficient permissions"

**Cause**: Service Principal lacks Contributor role.

**Solution**: Re-assign role:

```bash
az role assignment create \
  --assignee $APP_ID \
  --role Contributor \
  --scope /subscriptions/$SUBSCRIPTION_ID
```

### Error: "azd provision failed: resource already exists"

**Cause**: Resource names conflict with existing resources.

**Solution**: The Bicep templates use `uniqueString()` to generate unique suffixes. If still failing, check for orphaned resources:

```bash
# List all Taskify resource groups
az group list --query "[?contains(name, 'taskify')].name" -o table

# Delete orphaned resources if safe
az group delete --name rg-taskify-dev-<old-suffix> --yes --no-wait
```

---

## Security Best Practices

1. **Use OIDC instead of secrets** — No password rotation needed
2. **Limit Service Principal permissions** — Use Contributor role only on specific resource groups if possible
3. **Enable GitHub Environment protection** — Require approvals for production
4. **Rotate secrets regularly** — If using client secrets, rotate every 6-12 months
5. **Audit access logs** — Monitor Azure AD sign-in logs for Service Principal activity
6. **Use Azure RBAC** — Avoid storing credentials in GitHub Secrets; use Managed Identity wherever possible

---

## Next Steps

- ✅ Configure secrets and environments (complete)
- ✅ Test CI/CD workflows
- 📖 Read [Quickstart Guide](../specs/002-azure-hosting-cicd/quickstart.md) for manual deployment
- 📖 Read [Bicep Resources Contract](../specs/002-azure-hosting-cicd/contracts/bicep-resources.md) to understand infrastructure
- 🚀 Push code to `main` and watch automated deployment!

---

## References

- [Azure OIDC with GitHub Actions](https://learn.microsoft.com/azure/developer/github/connect-from-azure-openid-connect)
- [GitHub Environments](https://docs.github.com/en/actions/deployment/targeting-different-environments/using-environments-for-deployment)
- [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/)
- [Service Principal RBAC](https://learn.microsoft.com/azure/role-based-access-control/overview)
