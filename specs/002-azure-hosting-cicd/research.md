# Research: Azure Cloud Hosting & Automated Deployment

**Phase**: 0 — Outline & Research  
**Date**: March 6, 2026  
**Plan**: [plan.md](plan.md)

---

## Research Questions

1. What Azure compute services are best for hosting .NET Aspire applications?
2. How does Azure Developer CLI (azd) integrate with .NET Aspire?
3. What GitHub Actions are needed for building and deploying to Azure?
4. What Azure database options are recommended for PostgreSQL workloads?
5. How should secrets and configuration be managed in Azure?
6. What monitoring and logging options are available for deployed applications?
7. What are the cost implications of different Azure service tiers?

---

## 1. Azure Compute Services for .NET Aspire

**Decision**: Azure Container Apps (ACA)

**Rationale**:
- **First-class Aspire support**: .NET Aspire AppHost has built-in Azure Container Apps deployment via `Aspire.Hosting.Azure` packages
- **Managed containers**: Serverless container hosting without Kubernetes complexity
- **Automatic HTTPS**: Built-in SSL certificate management and custom domain support
- **Scale to zero**: Cost-effective for development environments (free tier available)
- **Integrated observability**: Native Application Insights integration
- **Dapr support**: Built-in service-to-service communication patterns
- **Pricing**: Pay-per-use with generous free tier (180,000 vCore-seconds/month free)

**Alternatives considered**:
- **Azure App Service**: Limited to single web app per service; requires multiple App Service Plans for API + Web, increasing cost and complexity. Not optimized for microservices.
- **Azure Kubernetes Service (AKS)**: Over-engineered for this scale; requires cluster management, node pool maintenance, and higher operational overhead. Minimum ~$70/month for basic cluster.
- **Azure Container Instances (ACI)**: No load balancing or managed HTTPS; manual orchestration required; less suitable for production workloads.
- **Azure Functions**: Not suitable for long-running SignalR connections or stateful Blazor applications.

**Technical details**:
- Container Apps Environment provides shared networking and observability
- Each Aspire project (API, Web) becomes a Container App
- Automatic ingress configuration with HTTPS
- Support for internal and external endpoints
- Built-in horizontal scaling based on HTTP requests or custom metrics

---

## 2. Azure Developer CLI (azd) Integration

**Decision**: Use `azd` as the primary deployment tool with Bicep for infrastructure

**Rationale**:
- **Aspire-native**: azd is the recommended deployment tool for .NET Aspire applications
- **Infrastructure-as-code**: Uses Bicep templates for declarative resource management
- **Environment management**: Built-in support for dev/staging/prod environments via `azd env`
- **CI/CD friendly**: Simple `azd up` command suitable for GitHub Actions
- **Secrets management**: Integration with Azure Key Vault and .NET User Secrets
- **Manifest generation**: Aspire AppHost automatically generates deployment manifests

**azd workflow**:
```bash
azd init          # Initialize azd configuration (creates azure.yaml)
azd provision     # Deploy infrastructure (Bicep → Azure)
azd deploy        # Build containers and deploy applications
azd up            # Combined provision + deploy
```

**Key files**:
- `azure.yaml`: Maps Aspire projects to Azure resources
- `infra/main.bicep`: Root infrastructure template
- `infra/main.parameters.json`: Environment-specific parameters
- `.azure/`: Environment configurations (dev, prod)

**Integration with Aspire**:
- Aspire AppHost's `AddAzureContainerApps()` extension generates azd-compatible manifests
- Service dependencies (database, SignalR) automatically translated to Azure resources
- Connection strings managed via Azure Key Vault references

---

## 3. GitHub Actions CI/CD Workflows

**Decision**: Multi-workflow approach with separate build, test, and deployment stages

**Workflows needed**:

### a. **CI Workflow** (`ci.yml`)
- **Trigger**: On pull request to any branch
- **Jobs**:
  1. Build all projects (.NET restore, build)
  2. Run unit tests (xUnit with coverage)
  3. Run integration tests
  4. Run benchmarks and check for regressions
  5. Validate Bicep templates (`az bicep build`)
- **Artifacts**: Test results, coverage reports
- **Purpose**: Fast feedback; blocks merge if tests fail

### b. **Azure Deployment Workflow** (`azure-dev.yml`)
- **Trigger**: Push to `main` (production) or manual dispatch with environment selection
- **Jobs**:
  1. Checkout code
  2. Setup .NET SDK and azd CLI  
  3. Azure login (via OIDC or Service Principal)
  4. `azd provision` (infrastructure)
  5. Run EF Core migrations (Taskify.Migrator)
  6. `azd deploy` (application containers)
  7. Run smoke tests (health checks)
  8. Report deployment status
- **Secrets required**:
  - `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (OIDC)
  - Or `AZURE_CREDENTIALS` (Service Principal JSON)
- **Environment protection**: Require approval for production deployments

### c. **Benchmark Workflow** (`benchmark.yml`)
- **Trigger**: Scheduled (nightly) or manual
- **Jobs**:
  1. Run BenchmarkDotNet suite
  2. Compare against baseline
  3. Comment on PR if triggered from PR
  4. Upload results as artifacts
- **Purpose**: Performance regression detection

**Authentication approach**:
- **Recommended**: OpenID Connect (OIDC) with Federated Identity
  - No secrets to rotate
  - Azure AD verifies GitHub's identity token
  - Scoped to specific repo and branch
- **Alternative**: Service Principal with JSON credentials
  - Requires secret rotation
  - Stored in GitHub Secrets

**Reference workflows**:
- Microsoft's official azd GitHub Actions: `azure/azure-dev`
- .NET Aspire deployment samples

---

## 4. Azure Database for PostgreSQL

**Decision**: Azure Database for PostgreSQL - Flexible Server

**Rationale**:
- **Managed service**: Automatic backups, patching, high availability options
- **EF Core compatibility**: Full PostgreSQL wire protocol support
- **Performance**: Configurable compute (Burstable, General Purpose, Memory Optimized)
- **Cost-effective**: Burstable B1ms tier suitable for development (~$12/month)
- **Security**: VNet integration, private endpoints, Azure AD authentication
- **Backup**: 7-35 day automated backups with point-in-time restore

**Tier recommendations**:
- **Development**: Burstable B1ms (1 vCore, 2GB RAM) - ~$12/month
- **Production**: General Purpose D2s_v3 (2 vCores, 8GB RAM) - ~$150/month
- **High availability**: Zone-redundant for production (adds ~40% cost)

**Alternatives considered**:
- **Azure Cosmos DB for PostgreSQL**: Over-engineered for this scale; designed for distributed databases with horizontal scaling. 10x more expensive.
- **Container Apps PostgreSQL add-on**: Limited to development; not suitable for production.
- **Azure SQL Database**: Would require migration from PostgreSQL; EF Core works but SQL syntax differences.

**Connection configuration**:
- Connection string stored in Azure Key Vault
- Container Apps reference secrets via Key Vault integration
- Aspire ServiceDefaults handle connection string injection

---

## 5. Secrets and Configuration Management

**Decision**: Azure Key Vault for secrets + Container Apps environment variables for non-sensitive config

**Rationale**:
- **Centralized secrets**: Single source of truth for connection strings, API keys
- **Access control**: Azure RBAC controls which services can read which secrets
- **Audit logging**: All secret access logged to Azure Monitor
- **Rotation support**: Secrets can be updated without redeploying applications
- **Integration**: Container Apps can reference Key Vault secrets directly

**Configuration strategy**:

| Type | Storage | Example |
|------|---------|---------|
| Secrets | Azure Key Vault | PostgreSQL connection string, API keys |
| Environment config | Container Apps env vars | `ASPNETCORE_ENVIRONMENT=Production` |
| Application settings | `appsettings.json` + env override | Logging levels, feature flags |

**Managed Identity**:
- Container Apps use Managed Identity (system-assigned or user-assigned)
- No credentials in code or config files
- Key Vault access policies grant read permissions to Managed Identity
- PostgreSQL can authenticate via Managed Identity (optional)

**Local development**:
- Continue using .NET User Secrets for local development
- azd can sync secrets from Key Vault to local environment
- Use `dotnet user-secrets set` for local overrides

---

## 6. Monitoring and Logging

**Decision**: Azure Application Insights + Container Apps built-in logging

**Rationale**:
- **Aspire integration**: .NET Aspire ServiceDefaults include Application Insights configuration
- **Distributed tracing**: Automatic correlation across API, Web, and database calls
- **Real-time metrics**: Request rates, response times, failure rates
- **Custom telemetry**: Can add custom events/metrics via `TelemetryClient`
- **Log aggregation**: Centralized logs from all Container Apps
- **Alerting**: Define alerts on metrics (e.g., error rate > 5%)

**What to monitor**:
- API response times (p50, p95, p99)
- Error rates and exceptions
- Database query performance
- SignalR connection count
- Container Apps CPU and memory usage
- Deployment health (smoke tests)

**Cost**: Application Insights charges based on data ingestion
- Development: <1GB/month → nearly free
- Production estimate: ~5GB/month → ~$10/month

**Alternatives considered**:
- **Azure Monitor Logs only**: Less integrated with application code; requires manual instrumentation
- **Third-party APM** (Datadog, New Relic): Additional cost and complexity; Application Insights is Azure-native

---

## 7. Cost Analysis

**Estimated monthly costs** (US East region, pay-as-you-go pricing):

**Note**: Initial deployment targets a single development/testing environment to minimize costs. Production cost estimates provided for future planning only.

### Development Environment (Primary Deployment Target)
| Resource | SKU | Estimated Cost |
|----------|-----|----------------|
| Container Apps Environment | Consumption | Free tier (180k vCore-sec free) |
| Container App - API | 0.25 vCPU, 0.5GB RAM, scale-to-zero | ~$2/month (idle most of time) |
| Container App - Web | 0.25 vCPU, 0.5GB RAM, scale-to-zero | ~$2/month (idle most of time) |
| PostgreSQL Flexible Server | Burstable B1ms | ~$12/month |
| Application Insights | <1GB ingestion | ~$2/month |
| Key Vault | Standard | ~$0.50/month |
| **Total** | | **~$18-25/month** |

**Scale-to-zero optimization**: Container Apps shut down when idle (no requests for ~5 minutes), reducing compute costs to near-zero during non-usage periods. Cold start is <5 seconds when accessed.

### Production Environment (Future Reference - Not Initially Deployed)
| Resource | SKU | Estimated Cost |
|----------|-----|----------------|
| Container Apps Environment | Consumption | Free tier + overage |
| Container App - API | 1 vCPU, 2GB RAM, min 1 replica | ~$75/month |
| Container App - Web | 1 vCPU, 2GB RAM, min 1 replica | ~$75/month |
| PostgreSQL Flexible Server | General Purpose D2s_v3 | ~$150/month |
| PostgreSQL - Zone Redundant HA | Add-on | ~$60/month |
| Application Insights | ~5GB ingestion | ~$10/month |
| Key Vault | Standard | ~$0.50/month |
| **Total** | | **~$370/month** |

**Cost optimization strategies**:
- **Development**: Scale-to-zero reduces idle costs; Burstable PostgreSQL minimizes database expense
- **Production** (when deployed): Enable autoscaling to scale down during low-traffic periods; consider Reserved Instances for 30-40% savings on database

**Spec compliance**:
- Development estimate (~$25/month) is well under $100 budget ✅
- Production estimate (~$370/month) for future reference (infrastructure designed to support when ready)

---

## 8. Zero-Downtime Deployment Strategy

**Decision**: Blue-Green deployment via Container Apps revisions

**Rationale**:
- Container Apps support multiple revisions with traffic splitting
- Can deploy new revision (green), test it, then shift traffic
- Instant rollback by shifting traffic back to previous revision

**Workflow**:
1. Deploy new Container App revision (0% traffic)
2. Run smoke tests against new revision's direct URL
3. If tests pass, split traffic 10% new / 90% old (canary)
4. Monitor metrics for 5 minutes
5. If healthy, shift to 100% new revision
6. If unhealthy, revert to 100% old revision
7. After successful deployment, deactivate old revision

**Database migrations**:
- For backward-compatible changes: Run migrations before app deployment
- For breaking changes: Multi-phase deployment
  1. Deploy backward-compatible schema changes
  2. Deploy application supporting both old and new schema
  3. Migrate data
  4. Deploy application using only new schema
  5. Remove old schema

---

## 9. Security Best Practices

**Implemented security measures**:

1. **Managed Identity**: No credentials in code or environment variables
2. **Key Vault**: All secrets encrypted at rest and in transit
3. **HTTPS Only**: Container Apps configured to require HTTPS
4. **Network isolation**: Option to use VNet integration for container-to-database communication
5. **RBAC**: Least-privilege access for GitHub Actions Service Principal
6. **Audit logging**: All resource access logged to Azure Monitor
7. **Dependency scanning**: Dependabot enabled for NuGet packages
8. **Container scanning**: Azure Container Registry vulnerability scanning

**GitHub Actions security**:
- Use OIDC instead of long-lived credentials
- Scope Service Principal to specific resource groups
- Use environment protection rules for production
- Store all Azure credentials as encrypted GitHub Secrets
- Use `permissions:` block to limit token scope

---

## 10. Disaster Recovery and Business Continuity

**Backup strategy**:
- **Database**: Automated daily backups with 7-day retention (configurable to 35 days)
- **Infrastructure**: Bicep templates in Git (infrastructure-as-code = reproducible)
- **Application code**: Git repository is source of truth
- **Secrets**: Document secret rotation procedure; Key Vault has soft-delete enabled

**Recovery procedures**:
- **Application failure**: Rollback via Container Apps revision traffic shift
- **Database corruption**: Point-in-time restore from automated backup
- **Region outage**: (Out of scope for initial implementation; would require multi-region setup)
- **Accidental deletion**: Soft-delete enabled on Key Vault (90-day retention)

**RTO/RPO targets**:
- **RTO** (Recovery Time Objective): <30 minutes for application, <1 hour for database restore
- **RPO** (Recovery Point Objective): <5 minutes (database transaction log backups)

---

## Technology Stack Summary

| Component | Technology | Justification |
|-----------|-----------|---------------|
| Compute | Azure Container Apps | Aspire-native, serverless, cost-effective |
| Database | Azure PostgreSQL Flexible Server | Managed, compatible with EF Core, auto backups |
| Infrastructure | Bicep + Azure Developer CLI (azd) | Declarative, version-controlled, Aspire-optimized |
| CI/CD | GitHub Actions | Integrated with repo, familiar to team, good Azure support |
| Secrets | Azure Key Vault | Secure, auditable, Managed Identity integration |
| Monitoring | Application Insights | Aspire-native, distributed tracing, cost-effective |
| Authentication | OIDC Federated Identity | No secret rotation, secure |

---

## Next Steps

All research questions answered. Key decisions made:
- ✅ Azure Container Apps for compute
- ✅ Azure PostgreSQL Flexible Server for database  
- ✅ azd + Bicep for infrastructure-as-code
- ✅ GitHub Actions for CI/CD
- ✅ Application Insights for observability
- ✅ Blue-green deployment for zero-downtime

**Proceed to Phase 1**: Create data model, contracts (Bicep templates, GitHub Actions workflows), and quickstart guide.
