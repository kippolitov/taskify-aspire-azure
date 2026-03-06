<!--
SYNC IMPACT REPORT
==================
Version change: (template / unversioned) → 1.0.0
Modified principles: N/A — initial ratification; all principle slots newly authored
Added sections:
  - Core Principles (4 principles; 5-slot template condensed to match 4 requested domains)
  - Technical Quality Gates  ✅ new — maps each principle to concrete plan.md gate criteria
  - Development Workflow     ✅ new — operationalises per-PR compliance expectations
  - Governance               ✅ formally defined with versioning policy and amendment procedure
Removed sections: N/A (template placeholders replaced, not sections removed)
Templates reviewed:
  - .specify/templates/plan-template.md  ✅ Constitution Check section present; gates language aligns
  - .specify/templates/spec-template.md  ✅ Success Criteria structure supports performance & UX measurables
  - .specify/templates/tasks-template.md ✅ Phase 1 linting/logging and test-first phases align with principles
  - .github/prompts/*.prompt.md          ✅ reviewed; no stale agent-specific references requiring change
Deferred TODOs: None — all placeholders resolved.
-->

# Speckit Taskify Copilot Constitution

## Core Principles

### I. Code Quality First

Every piece of code merged into the main branch MUST meet explicit quality standards.
Code reviews MUST verify: single responsibility per module, no commented-out dead code,
consistent naming conventions matching the project style guide, and cyclomatic complexity
≤ 10 per function. Linting and formatting gates MUST pass in CI before any PR is approved.
When unavoidable complexity exceeds these limits, it MUST be justified in the
`Complexity Tracking` table of the relevant `plan.md`.

**Rationale**: Unmanaged complexity compounds over time. Enforcing quality at every merge
prevents technical debt accumulation and keeps the codebase navigable as the product grows.

### II. Test-Driven Standards (NON-NEGOTIABLE)

TDD is mandatory for all feature work: tests MUST be written and reviewed before
implementation begins. The Red-Green-Refactor cycle is strictly enforced.

Minimum coverage thresholds:
- Unit tests: ≥ 80% line coverage across all modules.
- Critical paths (auth, data persistence, task mutation flows): ≥ 95% coverage.
- Every user story MUST have at least one independent integration test.
- Contract tests MUST be written for any public API or inter-service boundary change.

Tests MUST be observed to fail before implementation begins and to pass afterwards.
Skipped or disabled tests require explicit justification in the PR description.

**Rationale**: Tests define behaviour before code encodes it. High coverage on critical
paths prevents regressions that erode user trust and reduces the cost of future changes.

### III. User Experience Consistency

All user-facing surfaces MUST adhere to the project design system (design tokens, spacing
scale, typography, and component library). Interaction patterns MUST be predictable: the
same user action MUST produce the same visual feedback and result across all screens and
entry points. Accessibility is non-negotiable — all interactive elements MUST meet
WCAG 2.1 Level AA as a minimum bar.

Error messages MUST be human-readable, actionable, and consistent in tone and structure.
UI components MUST be reviewed against the design specification before shipping.

**Rationale**: Inconsistent UX increases cognitive load and support burden. A unified
experience builds trust and reduces onboarding friction for new and returning users.

### IV. Performance by Mandate

Performance budgets are requirements, not aspirations. The following budgets apply
unless explicitly amended through the governance process:

| Interaction | Budget |
|---|---|
| Initial screen render (median device + network) | ≤ 2 000 ms |
| API read response time (p95) | ≤ 200 ms |
| API write response time (p95) | ≤ 500 ms |
| Task list render (up to 500 items) | ≥ 60 fps / no jank |
| Background sync operations | MUST NOT block the main thread |

Performance MUST be measured in CI via automated benchmarks. Any PR introducing a
regression exceeding 10% of a budget MUST be blocked until resolved or the budget is
formally amended.

**Rationale**: Performance is a feature. Slow interfaces frustrate users and drive churn;
explicit measurable budgets prevent gradual regression going unnoticed across releases.

## Technical Quality Gates

Every implementation plan (`plan.md`) MUST include a **Constitution Check** section
that explicitly gates work against all four principles before Phase 0 research begins.
The check MUST be re-evaluated after Phase 1 design.

1. **Code Quality Gate** — Confirm linting and static-analysis configuration exists;
   cyclomatic complexity enforcement is active in CI; naming conventions are documented.
2. **Testing Gate** — Confirm test framework is configured; TDD workflow is activated;
   coverage thresholds are set and enforced in CI; contract test scaffold exists for
   any new API surface.
3. **UX Consistency Gate** — Confirm design tokens and component library are imported;
   accessibility tooling (e.g., axe-core, Storybook a11y addon) is configured; design
   specification is linked in the plan.
4. **Performance Gate** — Confirm benchmark suite exists or will be created in Phase 1
   Setup; CI pipeline includes a performance regression step with defined budgets.

Work MUST NOT advance to implementation phases until all four gates are addressed.
Unavoidable gaps MUST be documented in `Complexity Tracking` with a remediation date.

## Development Workflow

Pull requests MUST reference the spec and plan documents they implement. The PR
description MUST include:

- **User stories addressed**: list by ID and title.
- **Constitution Check**: one bullet per principle confirming compliance, or explicitly
  justifying any deviation with a remediation path.
- **Performance impact**: expected effect on each applicable budget, backed by benchmark
  output where measurable.
- **Test evidence**: coverage delta and test counts reported by CI.

Code reviews MUST assess compliance with all four principles before approval. A review
that does not address constitution compliance is incomplete.

Complexity violations that cannot be eliminated MUST be logged in the `Complexity
Tracking` table of `plan.md` with a justification and a future simplification path.

## Governance

This constitution supersedes all other practices and informal conventions. Amendments
require:

1. A written proposal identifying which principle(s) are affected and why the change
   is warranted.
2. At least one peer review of the proposal with documented agreement.
3. A migration plan for any in-flight or existing work that does not comply with the
   amended rule.
4. A version increment following semantic versioning rules:
   - **MAJOR** — removal of a principle or backward-incompatible redefinition of
     non-negotiable rules.
   - **MINOR** — new principle, new governance section, or materially expanded guidance
     that changes what is required.
   - **PATCH** — clarifications, wording refinements, typo fixes, or non-semantic
     changes that do not alter requirements.

All PRs and sprint/release reviews MUST verify compliance with this constitution.
A formal compliance review MUST occur at minimum once per release cycle. This file
(`.specify/memory/constitution.md`) is the authoritative source during planning,
implementation, and review.

**Version**: 1.0.0 | **Ratified**: 2026-03-05 | **Last Amended**: 2026-03-05
