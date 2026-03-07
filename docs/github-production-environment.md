# GitHub Production Environment Configuration

This document provides a step-by-step guide to configure the **production** GitHub Environment with appropriate protection rules and deployment controls.

## Prerequisites

- Repository admin access
- Production Azure environment ready
- Azure Service Principal configured with OIDC (see [GitHub Actions Setup Guide](../../docs/github-actions-setup.md))

---

## Step 1: Create Production Environment

1. Navigate to your GitHub repository
2. Go to **Settings** → **Environments**
3. Click **New environment**
4. Enter name: `production`
5. Click **Configure environment**

---

## Step 2: Configure Protection Rules

### Required Reviewers

**Purpose**: Prevent accidental production deployments; require human approval.

1. Under **Deployment protection rules**, check **Required reviewers**
2. Click **Add reviewers**
3. Select team members who should approve production deployments
   - Recommendation: At least 2 reviewers from different teams
   - Suggested roles: Engineering Lead, DevOps Engineer, Product Owner
4. **Number of required approvals**: 1 (minimum) or 2 (recommended for production)
5. Click **Save protection rules**

### Wait Timer (Optional)

**Purpose**: Add a mandatory delay before deployment starts (time for final checks).

1. Under **Deployment protection rules**, check **Wait timer**
2. Enter **wait time**: 
   - 0 minutes (no delay) — immediate deployment after approval
   - 5 minutes — allows time to cancel if approval was accidental
   - 30 minutes — for scheduled maintenance windows
3. Click **Save protection rules**

### Deployment Branches

**Purpose**: Restrict production deployments to specific branches only.

1. Under **Deployment branches**, select **Selected branches**
2. Click **Add deployment branch rule**
3. Enter branch pattern: `main`
4. Click **Add rule**
5. **Result**: Only code from the `main` branch can be deployed to production

---

## Step 3: Configure Environment Secrets (Optional)

If production requires different credentials than development:

1. Scroll to **Environment secrets**
2. Click **Add secret**
3. Add production-specific secrets:
   - `POSTGRESQL_ADMIN_PASSWORD` (production database password)
   - `AZURE_CLIENT_ID` (if using separate Service Principal for production)
   - `AZURE_SUBSCRIPTION_ID` (if production is in a different subscription)

**Note**: If secrets are identical between environments, use repository-level secrets instead.

---

## Step 4: Configure Deployment Policies (Enterprise Only)

If your organization has GitHub Enterprise:

1. Under **Deployment protection rules**, enable **Require deployment approval from specific users or teams**
2. Configure custom deployment policies:
   - **Business hours only**: Restrict deployments to 9 AM - 5 PM UTC
   - **Change freeze windows**: Block deployments during holidays or critical business periods
   - **Automated checks**: Require passing security scans or compliance checks

---

## Step 5: Test Production Deployment Workflow

### Trigger Manual Production Deployment

1. Go to **Actions** → **Azure Deployment**
2. Click **Run workflow**
3. Select branch: `main`
4. Select environment: `prod`
5. Click **Run workflow**

**Expected behavior**:
- Workflow starts in "Waiting" state
- Selected reviewers receive notification
- Reviewer must approve before deployment proceeds
- If wait timer is set, deployment waits additional time after approval
- Deployment runs with production parameter file (`main.parameters.prod.json`)

### Approve Deployment

1. Reviewers receive email notification
2. Reviewer navigates to **Actions** → workflow run
3. Reviewer clicks **Review deployments**
4. Reviewer selects **production** environment
5. Reviewer enters approval comment (e.g., "Approved for release v1.2.3")
6. Reviewer clicks **Approve and deploy**

### Monitor Deployment

1. Watch workflow progress in real-time
2. Verify smoke tests pass
3. Check deployment summary for production URLs
4. Test production application manually

---

## Step 6: Document Emergency Procedures

Create a runbook for production incidents:

### Emergency Rollback

If production deployment causes issues:

```bash
# Identify previous working revision
az containerapp revision list \
  --name ca-taskify-api-prod-<uniqueId> \
  --resource-group rg-taskify-prod-<uniqueId> \
  --output table

# Deactivate current (broken) revision
az containerapp revision deactivate \
  --name <current-revision> \
  --resource-group rg-taskify-prod-<uniqueId>

# Container Apps automatically activates previous revision
```

See [Quickstart Guide - Rollback Section](../../specs/002-azure-hosting-cicd/quickstart.md#rollback-and-revision-management) for detailed rollback procedures.

### Deployment Freeze

To prevent deployments during incidents:

1. Go to **Settings** → **Environments** → **production**
2. Edit **Deployment branches**
3. Temporarily change to **Protected branches only** (if no protected branches exist, this blocks all deployments)
4. Or add a wait timer of 999 minutes
5. Restore original settings after incident resolved

---

## Recommended Protection Configuration

| Setting | Development | Production |
|---------|-------------|------------|
| **Required reviewers** | None | 1-2 approvers |
| **Wait timer** | 0 minutes | 0 minutes (approval is sufficient) |
| **Deployment branches** | `main` only | `main` only |
| **Environment secrets** | Shared with repo | Production-specific |
| **Auto-approve** | Yes (on push to main) | No (manual workflow_dispatch only) |

---

## Security Best Practices

1. **Separate subscriptions**: Use different Azure subscriptions for dev and prod
2. **RBAC isolation**: Production Service Principal should have minimal permissions
3. **Audit logs**: Enable Azure Activity Log export for production deployments
4. **Secret rotation**: Rotate production PostgreSQL password every 90 days
5. **Change management**: Document all production deployments in a change log
6. **Disaster recovery**: Test backup restoration monthly

---

## Next Steps

- ✅ Production environment configured
- ✅ Protection rules enabled
- 📖 Document deployment schedule (e.g., Tuesdays/Thursdays 2 PM UTC)
- 📖 Create incident response runbook
- 📖 Set up Azure Monitor alerts for production

---

## References

- [GitHub Environments Documentation](https://docs.github.com/en/actions/deployment/targeting-different-environments/using-environments-for-deployment)
- [Deployment Protection Rules](https://docs.github.com/en/actions/deployment/targeting-different-environments/using-environments-for-deployment#deployment-protection-rules)
- [Azure Deployment Rollback Guide](../../specs/002-azure-hosting-cicd/quickstart.md#rollback-and-revision-management)
