# Implementation Plan: Document Upload and Management

**Branch**: `001-doc-upload-management` | **Date**: 2026-04-02 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-doc-upload-management/spec.md`

## Summary

Add document upload and management capabilities to the ContosoDashboard application: users upload files with metadata (title, category, tags), browse/search/filter their documents, download or preview files in-browser, edit metadata, share documents with other users, attach documents to tasks, and Administrators access audit logs. Files are stored on the local filesystem via an `IFileStorageService` interface abstraction (constitution principle I) with GUID-based filenames for security.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: ASP.NET Core 10.0, Blazor Server, Entity Framework Core 10.0 (Sqlite provider), Microsoft.AspNetCore.Authentication.Cookies
**Storage**: SQLite via EF Core (`ApplicationDbContext`); local filesystem for uploaded files via `IFileStorageService`
**Testing**: Manual acceptance testing (no test framework currently configured in the project)
**Target Platform**: Windows / cross-platform (Kestrel), offline-capable
**Project Type**: Web application (Blazor Server)
**Performance Goals**: Upload ≤30s for 25 MB files; document list ≤2s for 500 documents; search ≤2s; preview ≤3s
**Constraints**: Offline-only (no cloud services), 25 MB max file size, files stored outside `wwwroot`, GUID-based filenames
**Scale/Scope**: 4 seed users, training context; designed for small-team usage patterns

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|-----------|------|--------|
| I. Offline-First Architecture | File storage MUST use `IFileStorageService` interface with `LocalFileStorageService` implementation; no cloud SDKs allowed | ✅ PASS — plan uses `IFileStorageService` / `LocalFileStorageService` registered via DI |
| II. Defense-in-Depth Security | New pages MUST have `[Authorize]`; service layer MUST enforce IDOR checks; files MUST be outside `wwwroot`; download endpoint MUST verify authorization | ✅ PASS — all four controls are in the plan |
| III. Clean Separation of Concerns | New code MUST follow Models → Data → Services → Pages layering; Pages MUST NOT access `ApplicationDbContext` directly | ✅ PASS — plan adds Document/DocumentShare/DocumentActivity models, `DocumentService`/`DocumentActivityService`, and Blazor pages that inject services only |
| IV. Training Clarity | `LocalFileStorageService` MUST be labeled as training-only; production migration path MUST be documented | ✅ PASS — quickstart.md will document migration path; code comments will label training-only implementations |
| V. Spec-Driven Development | Plan references spec.md user stories; tasks.md (next step) will be organized by user story | ✅ PASS — plan is traceable to spec |

**Result: All gates PASS. Proceeding to Phase 0.**

## Project Structure

### Documentation (this feature)

```text
specs/001-doc-upload-management/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── document-api.md  # Razor Pages endpoint contracts for file serving
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
ContosoDashboard/
├── Models/
│   ├── Document.cs              # New: Document entity
│   ├── DocumentShare.cs         # New: Sharing relationship entity
│   └── DocumentActivity.cs      # New: Audit log entity
├── Data/
│   └── ApplicationDbContext.cs   # Modified: Add DbSets + relationships for new entities
├── Services/
│   ├── IFileStorageService.cs    # New: File storage interface
│   ├── LocalFileStorageService.cs# New: Local filesystem implementation
│   ├── DocumentService.cs        # New: Document business logic (upload, search, share, etc.)
│   └── DocumentActivityService.cs# New: Audit logging service
├── Pages/
│   ├── Documents.razor           # New: My Documents view (browse, filter, sort, search)
│   ├── DocumentUpload.razor      # New: Upload modal/form component
│   ├── DocumentPreview.razor     # New: In-browser preview page (PDF/images)
│   ├── DocumentAudit.razor       # New: Admin audit log and reports
│   ├── SharedDocuments.razor     # New: "Shared with Me" view
│   ├── ProjectDetails.razor      # Modified: Add project documents section
│   ├── Tasks.razor               # Modified: Add task document attachment section
│   └── Index.razor               # Modified: Add "Recent Documents" widget
├── Controllers/
│   └── DocumentController.cs     # New: Download/preview endpoints (authorized file serving)
└── Program.cs                    # Modified: Register new services in DI
```

**Structure Decision**: Follows the existing single-project Blazor Server structure already in place. New files are added to existing `Models/`, `Services/`, `Pages/` directories. A new `Controllers/` directory is added for the file download/preview endpoint — Blazor Server doesn't support streaming file responses directly, so a Razor Pages controller endpoint is necessary to serve files with proper Content-Disposition headers and authorization checks.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| `IFileStorageService` abstraction | Required by Constitution Principle I (Offline-First) for cloud migration path | Direct `System.IO` calls in services would violate the constitution |
| `Controllers/` directory (new layer) | Blazor Server cannot stream file downloads; a controller endpoint is the standard ASP.NET Core pattern for file serving with auth | Serving files from `wwwroot` would violate Constitution Principle II (files must be outside `wwwroot`) |
