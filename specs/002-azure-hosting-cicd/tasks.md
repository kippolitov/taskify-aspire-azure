---
description: "Task list for Azure Cloud Hosting & Automated Deployment"
---

# Tasks: Azure Cloud Hosting & Automated Deployment

**Input**: Design documents from `/specs/002-azure-hosting-cicd/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Tests are NOT explicitly requested in this feature - infrastructure validation uses deployment smoke tests and Bicep linting

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and directory structure

- [X] T001 Create infra/ directory structure per implementation plan
- [X] T002 Create .github/workflows/ directory
- [X] T003 [P] Create scripts/ directory for deployment helpers
- [X] T004 [P] Create infra/resources/ subdirectory for Bicep modules
- [X] T005 [P] Create infra/hooks/ subdirectory for lifecycle scripts

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T006 Create azure.yaml in repository root with Aspire project mappings
- [X] T007 [P] Create Dockerfile for Taskify.Api in src/Taskify.Api/Dockerfile
- [X] T008 [P] Create Dockerfile for Taskify.Web in src/Taskify.Web/Dockerfile
- [X] T009 Create infra/main.bicep with root orchestration template structure (parameters only, no modules yet)
- [X] T010 Create infra/main.parameters.json with default parameter values

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Initial Azure Deployment (Priority: P1) 🎯 MVP

**Goal**: Deploy Taskify application to Azure manually so it is accessible via public HTTPS URLs with all components (web, API, database) running

**Independent Test**: Run `azd up` from repository root, verify application accessible at Container App URLs, test database connectivity, verify SignalR real-time features work

### Implementation for User Story 1

#### Bicep Infrastructure Modules

- [X] T011 [P] [US1] Create infra/resources/monitoring.bicep - Provision Log Analytics Workspace and Application Insights
- [X] T012 [P] [US1] Create infra/resources/keyvault.bicep - Provision Azure Key Vault with access policies for managed identities
- [X] T013 [P] [US1] Create infra/resources/postgresql.bicep - Provision Azure PostgreSQL Flexible Server with database, firewall rules, and backup configuration
- [X] T014 [US1] Create infra/resources/container-apps.bicep - Provision Container Apps Environment, API Container App, and Web Container App with secrets and environment variables
- [X] T015 [US1] Update infra/main.bicep - Add module references for monitoring, keyvault, postgresql, and container-apps with parameter passing and outputs

#### Environment Configuration

- [X] T016 [P] [US1] Create infra/main.parameters.dev.json - Development environment parameter overrides (Burstable DB, 0.25 CPU, scale-to-zero)
- [X] T017 [P] [US1] Update src/Taskify.Api/appsettings.json - Add connection string placeholder and Application Insights configuration
- [X] T018 [P] [US1] Update src/Taskify.Web/appsettings.json - Add API base URL configuration and Application Insights configuration

#### Deployment Lifecycle Hooks

- [X] T019 [P] [US1] Create infra/hooks/predeploy.sh - Pre-deployment validation (check Azure CLI, validate parameters, Bicep build check)
- [X] T020 [P] [US1] Create infra/hooks/postdeploy.sh - Post-deployment smoke tests (health endpoint checks, database connectivity verification)

#### Migration Support

- [X] T021 [US1] Update src/Taskify.Migrator/Program.cs - Ensure EF Core migrations can read connection string from Azure Key Vault or environment variable
- [X] T022 [US1] Update azure.yaml - Add hooks configuration for predeploy and postdeploy scripts

**Checkpoint**: At this point, User Story 1 should be fully functional - `azd up` deploys complete application to Azure

---

## Phase 4: User Story 2 - Automated Build & Deployment Pipeline (Priority: P2)

**Goal**: GitHub Actions workflows automate building, testing, and deploying code changes to Azure without manual intervention

**Independent Test**: Push a code change to main branch, verify GitHub Actions automatically builds, tests, and deploys to Azure; verify deployment succeeds and application is updated

### Implementation for User Story 2

#### CI Workflow

- [X] T023 [P] [US2] Create .github/workflows/ci.yml - Build job with .NET restore, build, and artifact upload
- [X] T024 [P] [US2] Add test job to .github/workflows/ci.yml - Run unit tests for Taskify.Api.Tests and Taskify.Web.Tests with code coverage
- [X] T025 [P] [US2] Add lint job to .github/workflows/ci.yml - Run dotnet format verification
- [X] T026 [P] [US2] Add validate-bicep job to .github/workflows/ci.yml - Validate Bicep templates with az bicep build and lint

#### Azure Deployment Workflow

- [X] T027 [US2] Create .github/workflows/azure-dev.yml - Configure workflow with triggers (push to main, manual dispatch), Azure login with OIDC
- [X] T028 [US2] Add provision-infrastructure job to .github/workflows/azure-dev.yml - Run azd provision
- [X] T029 [US2] Add deploy-apps job to .github/workflows/azure-dev.yml - Run azd deploy with container image deployment
- [X] T030 [US2] Add smoke-tests job to .github/workflows/azure-dev.yml - Verify deployment health by calling API and Web health endpoints
- [X] T030.1 [US2] Document Container Apps revision management and rollback procedure in quickstart.md - Address FR-012 rollback requirement

#### GitHub Configuration

- [X] T031 [US2] Configure Azure Service Principal with Federated Identity Credential for GitHub Actions OIDC authentication
- [X] T032 [US2] Add GitHub Secrets to repository - AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID, AZURE_CLIENT_ID for OIDC
- [X] T033 [US2] Configure GitHub Environment for development with protection rules (optional approvals)

**Checkpoint**: At this point, User Stories 1 AND 2 should both work - code pushed to main automatically deploys to Azure

---

## Phase 5: User Story 3 - Production Environment Support (Priority: P3)

**Goal**: Prepare infrastructure capability to add a production Azure environment when ready, with appropriate parameter files and deployment controls, so the same Bicep templates can provision production-tier resources without redesign

**Independent Test**: Create production parameter files and provision a production environment in a test subscription, verify it uses appropriate SKUs (General Purpose database, higher CPU/memory, minimum replicas, zone redundancy) distinct from development environment configuration

### Implementation for User Story 3

- [X] T034 [P] [US3] Create infra/main.parameters.prod.json - Production environment parameter template (General Purpose DB, 1.0 CPU, min replicas=1, high availability enabled) for future use
- [X] T035 [P] [US3] Update .github/workflows/azure-dev.yml - Add environment input parameter to workflow_dispatch trigger to support manual production deployment when ready
- [X] T036 [US3] Update .github/workflows/azure-dev.yml - Add conditional logic to select parameter file based on environment input (dev vs prod)
- [X] T037 [US3] Create GitHub Environment configuration template for production with required approvals and deployment protection rules documentation
- [X] T038 [US3] Update azure.yaml - Add environment-specific tags and configuration options to support multi-environment capability

**Checkpoint**: Infrastructure prepared for production deployment - parameter files and deployment controls ready when production environment needed

---

## Phase 6: User Story 4 - Infrastructure Provisioning & Management (Priority: P3)

**Goal**: Enable infrastructure-as-code capabilities for provisioning, updating, and destroying Azure environments consistently

**Independent Test**: Run provisioning scripts from clean state, verify all Azure resources created; modify infrastructure definition, verify updates applied without recreating everything; run deprovisioning, verify clean resource removal

### Implementation for User Story 4

#### Deployment Scripts

- [X] T039 [P] [US4] Create scripts/deploy-to-azure.ps1 - PowerShell wrapper for azd deployment with parameter validation and environment selection
- [X] T040 [P] [US4] Create scripts/run-smoke-tests.ps1 - Standalone smoke test script that verifies deployment health independent of azd hooks

#### Advanced Infrastructure

- [X] T041 [P] [US4] Create infra/resources/networking.bicep - (Optional) VNet configuration for private connectivity if needed
- [X] T042 [US4] Update infra/main.bicep - Add conditional networking module for production environments requiring VNet integration

#### Infrastructure Documentation

- [X] T043 [P] [US4] Create infra/README.md - Document Bicep module structure, parameter file usage, and manual provisioning commands
- [X] T044 [P] [US4] Update specs/002-azure-hosting-cicd/quickstart.md - Add sections for infrastructure updates, rollback procedures, and resource cleanup

**Checkpoint**: Infrastructure-as-code capabilities fully implemented - can provision/update/destroy environments repeatably

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and operational excellence

- [X] T045 [P] Create .github/workflows/benchmark.yml - Performance validation workflow with BenchmarkDotNet integration
- [X] T046 [P] Add cost monitoring alerts to infra/resources/monitoring.bicep - Configure budget alerts for Azure consumption
- [X] T047 [P] Update .gitignore - Ensure .azure/ directory and local.settings.json are ignored
- [X] T048 Security hardening - Review Key Vault access policies, ensure managed identities used instead of connection strings where possible
- [X] T049 [P] Create docs/azure-deployment.md - Comprehensive deployment documentation with troubleshooting guide
- [X] T050 Run quickstart.md validation - Follow all quickstart steps end-to-end to verify accuracy

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-6)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (US1 → US2 → US3 → US4)
- **Polish (Phase 7)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1) - Initial Deployment**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2) - Automated Pipeline**: Depends on User Story 1 completion (needs working Bicep templates and azure.yaml to automate)
- **User Story 3 (P3) - Environment-Specific**: Depends on User Story 2 completion (extends pipeline with multi-environment support)
- **User Story 4 (P3) - Infrastructure Management**: Can start after User Story 1 (extends infrastructure with additional scripts/docs) - May proceed in parallel with US2/US3

### Within Each User Story

#### User Story 1 Internal Dependencies:
- Monitoring module (T011) BEFORE other modules (provides Log Analytics for Container Apps)
- Key Vault module (T012) BEFORE Container Apps (secrets needed for container configuration)
- PostgreSQL module (T013) BEFORE Container Apps (database connection string needed)
- Container Apps module (T014) AFTER all dependencies
- main.bicep update (T015) AFTER all modules created
- Parameter files (T016-T018) can be parallel with module development
- Hooks (T019-T020) can be parallel with Bicep development
- Migration updates (T021-T022) AFTER Container Apps module (needs deployment pattern)

#### User Story 2 Internal Dependencies:
- CI workflow jobs (T023-T026) all parallelizable
- Azure deployment workflow (T027-T030) sequential within workflow, but file can be built in parallel stages
- GitHub configuration (T031-T033) AFTER workflows created (needs to test authentication)

#### User Story 3 Internal Dependencies:
- Production parameter file (T034) independent
- Workflow updates (T035-T036) sequential
- GitHub environment (T037) can be parallel
- azure.yaml update (T038) independent

#### User Story 4 Internal Dependencies:
- Deployment scripts (T039-T040) parallelizable
- Networking module (T041-T042) independent path
- Documentation (T043-T044) parallelizable, ideally AFTER implementation complete

### Parallel Opportunities

- **Phase 1 (Setup)**: All tasks T001-T005 can run in parallel (creating directories)
- **Phase 2 (Foundational)**: T007-T008 (Dockerfiles) parallel; T009-T010 (Bicep scaffolding) parallel
- **User Story 1**:
  - T011-T013 (monitoring, keyvault, postgresql modules) can run in parallel
  - T016-T018 (parameter and config files) can run in parallel
  - T019-T020 (deployment hooks) can run in parallel
- **User Story 2**:
  - T023-T026 (all CI workflow jobs) can run in parallel
  - T031-T033 (GitHub configuration) can overlap with workflow development
- **User Story 3**:
  - T034, T037, T038 can run in parallel (different files)
- **User Story 4**:
  - T039-T040 (scripts) can run in parallel
  - T041-T042 (networking) can run in parallel
  - T043-T044 (documentation) can run in parallel
- **Phase 7 (Polish)**: T045-T047, T049 can all run in parallel

---

## Parallel Execution Examples

### Parallel Example: User Story 1 - Bicep Modules

Different team members can work on different Bicep modules simultaneously:

```bash
# Developer 1: Monitoring
git checkout -b feature/us1-monitoring
# Work on T011: infra/resources/monitoring.bicep

# Developer 2: Key Vault
git checkout -b feature/us1-keyvault
# Work on T012: infra/resources/keyvault.bicep

# Developer 3: PostgreSQL
git checkout -b feature/us1-postgresql
# Work on T013: infra/resources/postgresql.bicep

# Developer 4: Deployment hooks
git checkout -b feature/us1-hooks
# Work on T019-T020: infra/hooks/*.sh
```

All branches can be developed and tested independently, then integrated via PRs.

### Parallel Example: User Story 2 - CI Workflow Jobs

CI workflow jobs can be developed and tested in parallel:

```bash
# Developer 1: Build and test jobs
# Work on T023-T024: .github/workflows/ci.yml (build + test)

# Developer 2: Lint and validation
# Work on T025-T026: .github/workflows/ci.yml (lint + bicep validation)

# Developer 3: Azure deployment workflow
# Work on T027-T030: .github/workflows/azure-dev.yml
```

Jobs within ci.yml can be committed incrementally and tested via pull requests.

---

## Implementation Strategy

### MVP Scope (Minimum Viable Product)

**Goal**: Get Taskify deployed to Azure in development environment

**Includes**:
- Phase 1: Setup (T001-T005)
- Phase 2: Foundational (T006-T010)
- Phase 3: User Story 1 (T011-T022)

**Excludes**:
- Automated CI/CD pipelines (manual deployment via `azd up`)
- Production environment
- Advanced infrastructure management scripts

**Deliverable**: Working Azure deployment accessible via HTTPS, can be deployed manually by developers

**Timeline**: 1-2 days for experienced developer

---

### Full Feature Scope

**Goal**: Complete automated CI/CD with multi-environment support

**Includes**:
- All of MVP scope
- Phase 4: User Story 2 (T023-T033) - Automated pipelines
- Phase 5: User Story 3 (T034-T038) - Production environment
- Phase 6: User Story 4 (T039-T044) - Infrastructure management
- Phase 7: Polish (T045-T050) - Operational excellence

**Deliverable**: Fully automated deployment pipeline with dev/prod environments, monitoring, cost controls, and comprehensive documentation

**Timeline**: 3-5 days for experienced developer

---

## Task Completion Checklist

After completing each task, verify:

- [ ] Code follows .NET and Bicep naming conventions
- [ ] All file paths match specification exactly
- [ ] Bicep templates pass `az bicep build` validation
- [ ] GitHub Actions workflows use correct syntax (YAML validated)
- [ ] Secrets never committed to repository
- [ ] Environment variables documented in deployment-config.md
- [ ] Changes committed with descriptive commit message referencing task ID
- [ ] If applicable, pull request created and linked to task

---

## Success Criteria (from spec.md)

Upon completion of all tasks, the following must be verifiable:

1. ✅ **Manual Deployment**: `azd up` successfully deploys Taskify to Azure development environment
2. ✅ **Automated Deployment**: Git push to main triggers GitHub Actions that deploy to Azure
3. ✅ **Multi-Environment**: Both development and production environments can be deployed with appropriate resource sizing
4. ✅ **Infrastructure-as-Code**: Bicep templates provision all required Azure resources (Container Apps, PostgreSQL, Key Vault, monitoring)
5. ✅ **Application Functionality**: Deployed application matches local development behavior (API endpoints, Blazor UI, SignalR real-time, database persistence)
6. ✅ **Security**: Secrets managed via Key Vault, HTTPS enforced, managed identities configured
7. ✅ **Monitoring**: Application Insights collecting telemetry, distributed tracing working
8. ✅ **Cost Control**: Development environment <$30/month, production environment <$400/month
9. ✅ **Documentation**: Quickstart guide enables new developer to deploy without assistance
10. ✅ **Performance**: Deployed application meets <200ms API p95, <2s initial render goals

---

## Notes

- **Test Strategy**: This feature uses deployment validation instead of traditional unit tests (Bicep validation, smoke tests, integration tests in CI)
- **Bicep Best Practices**: Follow Azure Well-Architected Framework recommendations from research.md
- **azd Integration**: Leverage Aspire + azd native integration for seamless deployment
- **Cost Optimization**: Development environment configured for scale-to-zero to minimize costs
- **Security**: Use Federated Identity (OIDC) for GitHub Actions to avoid storing Azure credentials as secrets
- **Rollback Strategy**: Container Apps support revision management for zero-downtime rollback
- **Database Migrations**: EF Core migrations run automatically via Taskify.Migrator during deployment

---

**Total Tasks**: 50  
**Core MVP Tasks**: 22 (T001-T022)  
**Parallelizable Tasks**: 28 (marked with [P])  
**Estimated Effort**: 3-5 days (experienced developer, full feature scope)
