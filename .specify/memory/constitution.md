<!--
  Sync Impact Report
  ==================
  Version change: N/A → 1.0.0
  Modified principles: N/A (initial ratification)
  Added sections:
    - Core Principles (5 principles)
    - Technology Stack & Constraints
    - Development Workflow
    - Governance
  Removed sections: N/A
  Templates requiring updates:
    - .specify/templates/plan-template.md ✅ no changes needed (generic constitution gate)
    - .specify/templates/spec-template.md ✅ no changes needed
    - .specify/templates/tasks-template.md ✅ no changes needed
  Follow-up TODOs: none
-->

# ContosoDashboard Constitution

## Core Principles

### I. Offline-First Architecture

All features MUST function without cloud services or external
dependencies. Infrastructure concerns (storage, authentication,
external APIs) MUST be accessed through interface abstractions
registered via dependency injection.

- New infrastructure dependencies MUST define an interface
  (e.g., `IFileStorageService`) with a local implementation.
- Swapping to a cloud implementation (Azure SQL, Blob Storage,
  Entra ID) MUST require only DI configuration changes—no
  business logic or UI modifications.
- SQL Server SQLite is the sole database target for training.
  Connection strings MUST be the only difference for production.

**Rationale**: The project is used in offline training
environments where cloud access cannot be guaranteed. Interface
abstractions also teach industry-standard dependency inversion.

### II. Defense-in-Depth Security

Every layer of the application MUST enforce its own security
controls. Security MUST NOT depend on a single enforcement point.

- All protected Razor pages and components MUST carry the
  `[Authorize]` attribute.
- The service layer MUST validate that the authenticated user is
  authorized to access or modify the requested resource (IDOR
  prevention).
- Security headers (CSP, X-Frame-Options, X-XSS-Protection)
  MUST be applied via middleware.
- User-supplied input MUST be validated at system boundaries
  (page models, API endpoints) before reaching services.
- Files MUST be stored outside `wwwroot`; download endpoints
  MUST perform authorization checks.

**Rationale**: Mock authentication simplifies training login but
the authorization patterns MUST mirror production-grade defense
so students learn secure defaults.

### III. Clean Separation of Concerns

Code MUST follow the established layer structure: **Models →
Data → Services → Pages/Components**.

- Business logic MUST reside exclusively in the Services layer.
- Razor pages and components MUST NOT access
  `ApplicationDbContext` directly; they MUST use injected
  services.
- Data access (EF Core queries) MUST be confined to the Data
  layer or to Services that own the relevant entities.
- Dependencies MUST flow inward: Pages → Services → Data.
  Reverse dependencies are forbidden.

**Rationale**: Clean layering enables independent testing of
business rules and makes the cloud migration path viable by
isolating infrastructure from UI.

### IV. Training Clarity

The codebase MUST prioritize readability and educational value.
Patterns MUST be industry-standard but simplified for a training
context.

- Mock or training-only implementations MUST be clearly labeled
  with comments and README warnings.
- Known limitations and simplifications MUST be documented where
  they occur and in the project README.
- Production migration paths MUST be described for every mock
  component (auth, storage, database).
- Over-engineering MUST be avoided: introduce abstractions only
  when they serve a concrete training objective or a documented
  migration path.

**Rationale**: Students must distinguish training shortcuts from
production patterns. Explicit documentation prevents carrying
insecure or incomplete patterns into real projects.

### V. Spec-Driven Development

Features MUST begin with a specification and follow the
spec → plan → tasks → implement pipeline.

- Every feature MUST have a `spec.md` capturing user stories
  with priorities and acceptance scenarios.
- Implementation MUST be guided by a `plan.md` that passes a
  Constitution Check gate before design work begins.
- Tasks (`tasks.md`) MUST be organized by user story to enable
  independent implementation and testing of each story.
- Changes MUST be traceable: each task references its user story;
  each user story references acceptance criteria.

**Rationale**: Spec-driven development is the core methodology
this training project teaches. The constitution enforces the
discipline the students are learning.

## Technology Stack & Constraints

- **Framework**: ASP.NET Core 8.0 with Blazor Server
- **Database**: SQLite with Entity Framework Core
- **Authentication**: Cookie-based mock auth (training only);
  production target is Microsoft Entra ID
- **Authorization**: Claims-based identity with role-based
  access control (Administrator, Project Manager, Team Lead,
  Employee)
- **UI**: Bootstrap 5.3 with Bootstrap Icons
- **File Storage**: Local filesystem via `IFileStorageService`;
  production target is Azure Blob Storage
- **No external runtime dependencies**: The application MUST
  run without internet access or cloud subscriptions

## Development Workflow

1. **Specify** — Create or update `spec.md` from stakeholder
   requirements using the `/speckit.specify` command.
2. **Plan** — Generate `plan.md` with a Constitution Check gate
   using the `/speckit.plan` command.
3. **Tasks** — Break the plan into ordered, story-aligned tasks
   using the `/speckit.tasks` command.
4. **Implement** — Execute tasks in order; each task references
   its user story and target files.
5. **Validate** — Verify acceptance scenarios from the spec.
   Confirm constitution compliance before merging.

- Feature work MUST happen on dedicated branches
  (`###-feature-name`).
- Every `plan.md` MUST include a Constitution Check section
  that validates alignment with the principles above.
- Complexity additions (new layers, new projects, new
  abstractions) MUST be justified in a Complexity Tracking
  table when they deviate from these principles.

## Governance

This constitution is the highest-authority document for
architectural and process decisions in ContosoDashboard.

- **Supremacy**: When conflicts arise between ad-hoc decisions
  and this constitution, the constitution prevails.
- **Compliance**: All plan.md documents MUST pass a
  Constitution Check gate. Reviewers MUST verify principle
  alignment before approving changes.
- **Amendments**: Any change to this constitution MUST be
  documented with a version bump following semantic versioning:
  - MAJOR: Principle removal or backward-incompatible
    redefinition.
  - MINOR: New principle or materially expanded guidance.
  - PATCH: Clarifications, wording, or typo fixes.
- **Review Cadence**: The constitution SHOULD be reviewed when
  a new feature area is introduced or when the technology stack
  changes.

**Version**: 1.0.0 | **Ratified**: 2026-04-02 | **Last Amended**: 2026-04-02
