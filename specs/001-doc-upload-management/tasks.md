# Tasks: Document Upload and Management

**Input**: Design documents from `/specs/001-doc-upload-management/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/document-api.md

**Tests**: No test tasks included — tests were not explicitly requested in the feature specification.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

All paths relative to repository root. Project structure: `ContosoDashboard/` (single Blazor Server project).

---

## Phase 1: Setup

**Purpose**: Project initialization — new model files, service interfaces, DI wiring

- [x] T001 [P] Create Document entity model in ContosoDashboard/Models/Document.cs per data-model.md (14 fields: DocumentId, Title, Description, Category, Tags, OriginalFileName, FileSize, FileType, FilePath, UploadedById, ProjectId, TaskId, CreatedDate, UpdatedDate; navigation properties for User, Project, TaskItem, DocumentShares, DocumentActivities)
- [x] T002 [P] Create DocumentShare entity model in ContosoDashboard/Models/DocumentShare.cs per data-model.md (5 fields: DocumentShareId, DocumentId, SharedWithUserId, SharedByUserId, SharedDate; navigation properties)
- [x] T003 [P] Create DocumentActivity entity model in ContosoDashboard/Models/DocumentActivity.cs per data-model.md (6 fields: DocumentActivityId, DocumentId, UserId, ActivityType, Details, ActivityDate; navigation properties)
- [x] T004 Add NotificationType enum values DocumentShared and DocumentAddedToProject in ContosoDashboard/Models/Notification.cs per research.md R6
- [x] T005 Update ApplicationDbContext in ContosoDashboard/Data/ApplicationDbContext.cs: add DbSet<Document>, DbSet<DocumentShare>, DbSet<DocumentActivity>; configure relationships, delete behaviors (cascade for shares/activities, restrict for user, set-null for project/task), unique constraint on (DocumentId, SharedWithUserId), and indexes per data-model.md
- [x] T006 [P] Create IFileStorageService interface in ContosoDashboard/Services/IFileStorageService.cs with methods: UploadAsync, DownloadAsync, DeleteAsync, ExistsAsync per contracts/document-api.md
- [x] T007 Create LocalFileStorageService in ContosoDashboard/Services/LocalFileStorageService.cs implementing IFileStorageService using System.IO against {ContentRootPath}/AppData/uploads/ per research.md R4 (inject IWebHostEnvironment; label as training-only per constitution principle IV)
- [x] T008 [P] Create IDocumentService interface in ContosoDashboard/Services/DocumentService.cs with all method signatures per contracts/document-api.md IDocumentService section
- [x] T009 [P] Create IDocumentActivityService interface in ContosoDashboard/Services/DocumentActivityService.cs with method signatures per contracts/document-api.md IDocumentActivityService section
- [x] T010 Register new services in ContosoDashboard/Program.cs: AddScoped IFileStorageService/LocalFileStorageService, IDocumentService/DocumentService, IDocumentActivityService/DocumentActivityService; add AddControllers() and MapControllers() for the download/preview endpoint

**Checkpoint**: Database schema auto-creates with new tables on next run. Service interfaces are injectable. No UI yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core file validation logic and authorization helpers used by ALL user stories

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T011 Implement file validation helper (static class or private methods in DocumentService) in ContosoDashboard/Services/DocumentService.cs: extension whitelist check (FR-002/FR-004), file size check ≤25 MB (FR-003), magic-bytes content validation for PDF/JPEG/PNG/Office formats (FR-037, research.md R3), and extension-to-MIME-type mapping dictionary
- [x] T012 Implement authorization helper method in DocumentService (private method HasAccessAsync): check ownership, shared-with status, project membership, Team Lead department access, Administrator full bypass per FR-034 and contracts/document-api.md authorization matrix
- [x] T013 Implement DocumentActivityService.LogActivityAsync in ContosoDashboard/Services/DocumentActivityService.cs: create and save DocumentActivity record (FR-031)

**Checkpoint**: Foundation ready — validation, authorization, and activity logging are available for all user story implementations.

---

## Phase 3: User Story 1 — Upload a Document (Priority: P1) 🎯 MVP

**Goal**: Users can upload one or more files with metadata (title, category, optional description/tags/project/task) and see them stored securely.

**Independent Test**: Upload a PDF with title + category → verify it appears in the user's document list with correct metadata.

### Implementation for User Story 1

- [x] T014 [US1] Implement DocumentService.UploadDocumentAsync in ContosoDashboard/Services/DocumentService.cs: validate file (call T011 helpers), generate GUID-based relative path ({userId}/{projectId|personal}/{guid}.{ext}), save file via IFileStorageService.UploadAsync, create Document entity, save to DB, log activity "Upload" via IDocumentActivityService, notify project members if ProjectId is set (FR-030) via INotificationService (FR-001, FR-005–FR-010, FR-035, FR-036)
- [x] T015 [US1] Implement DocumentService.GetUserDocumentsAsync in ContosoDashboard/Services/DocumentService.cs: query documents where UploadedById == userId with optional category/project/date-range filters and sortBy parameter (FR-011, FR-012, FR-013), include navigation properties for Project
- [x] T016 [US1] Create Documents.razor page in ContosoDashboard/Pages/Documents.razor: "My Documents" view with [Authorize] attribute, table showing title/category/upload date/file size/associated project, sort controls, filter dropdowns for category/project/date range, and Upload button that opens upload form (FR-011, FR-012, FR-013)
- [x] T017 [US1] Create upload form component in ContosoDashboard/Pages/DocumentUpload.razor: InputFile with multi-file support (IBrowserFile, maxAllowedSize 25MB per research.md R1), title input (required), category dropdown with 6 predefined values (FR-006), optional description textarea, optional tags input, optional project selector (only projects user belongs to via IProjectService), progress bar tracking bytes read per file (FR-010, research.md R5), per-file success/failure summary display (FR-001 partial failure)
- [x] T018 [US1] Add "Documents" link to navigation menu in ContosoDashboard/Shared/NavMenu.razor pointing to /documents

**Checkpoint**: User Story 1 fully functional — users can upload documents with metadata and see them listed. This is the MVP.

---

## Phase 4: User Story 2 — Browse and Search Documents (Priority: P2)

**Goal**: Users can filter, sort, and search their documents and see project documents on the project detail page.

**Independent Test**: Upload several documents with different categories → verify filters and search return correct results.

### Implementation for User Story 2

- [ ] T019 [US2] Implement DocumentService.SearchDocumentsAsync in ContosoDashboard/Services/DocumentService.cs: full-text search across title, description, tags, uploader DisplayName, and project Name; filter results by user access permissions (FR-014, FR-015)
- [ ] T020 [P] [US2] Implement DocumentService.GetProjectDocumentsAsync in ContosoDashboard/Services/DocumentService.cs: return documents where ProjectId matches, verify requesting user is project member or Admin (FR-026)
- [ ] T021 [US2] Add search bar to Documents.razor page in ContosoDashboard/Pages/Documents.razor: text input + search button calling SearchDocumentsAsync, display results in same table format (FR-014)
- [ ] T022 [US2] Add project documents section to ContosoDashboard/Pages/ProjectDetails.razor: list documents associated with the project using GetProjectDocumentsAsync, with download/preview links (FR-026)

**Checkpoint**: Users can find documents via filters, sorting, search, and through project pages.

---

## Phase 5: User Story 3 — Download and Preview Documents (Priority: P3)

**Goal**: Users can download documents with original filenames and preview PDFs/images in-browser.

**Independent Test**: Upload a PDF and a JPEG → verify download returns original file and preview renders in browser.

### Implementation for User Story 3

- [ ] T023 [US3] Implement DocumentService.GetDocumentByIdAsync in ContosoDashboard/Services/DocumentService.cs: fetch document by ID, verify access via HasAccessAsync (T012), return null if unauthorized (FR-034)
- [ ] T024 [US3] Create DocumentController in ContosoDashboard/Controllers/DocumentController.cs with [Authorize] and [Route("api/documents")]: implement GET {id}/download action (call GetDocumentByIdAsync for auth, IFileStorageService.DownloadAsync for file, return FileStreamResult with Content-Disposition attachment and original filename per FR-016, log "Download" activity) and GET {id}/preview action (same auth, return inline Content-Disposition for PDF/JPEG/PNG only, return 400 for unsupported types per FR-017) per contracts/document-api.md
- [ ] T025 [US3] Add download and preview action buttons to document rows in ContosoDashboard/Pages/Documents.razor: download link pointing to /api/documents/{id}/download, preview link (for PDF/image types only) pointing to /api/documents/{id}/preview opening in new tab

**Checkpoint**: Core upload→find→retrieve loop complete. Users can upload, find, download, and preview documents.

---

## Phase 6: User Story 4 — Manage Document Metadata and Versions (Priority: P4)

**Goal**: Document owners can edit metadata, replace files, and delete documents. Project Managers can delete project documents.

**Independent Test**: Upload a document → edit title/tags → replace file → delete → verify all changes and final removal.

### Implementation for User Story 4

- [ ] T026 [US4] Implement DocumentService.UpdateDocumentMetadataAsync in ContosoDashboard/Services/DocumentService.cs: verify ownership, update title/description/category/tags, set UpdatedDate, log "Edit" activity (FR-018)
- [ ] T027 [US4] Implement DocumentService.ReplaceDocumentFileAsync in ContosoDashboard/Services/DocumentService.cs: verify ownership, validate new file (reuse T011 helpers), delete old file via IFileStorageService, upload new file, update FilePath/FileSize/FileType/OriginalFileName/UpdatedDate, log "Replace" activity (FR-019)
- [ ] T028 [US4] Implement DocumentService.DeleteDocumentAsync in ContosoDashboard/Services/DocumentService.cs: verify ownership or Project Manager role for project documents or Admin role (FR-020, FR-021), delete file via IFileStorageService, remove Document entity (cascades shares/activities), log "Delete" activity before removal (FR-022)
- [ ] T029 [US4] Add edit/replace/delete UI to ContosoDashboard/Pages/Documents.razor: edit modal for metadata fields (title, description, category, tags), file replace using InputFile, delete button with confirmation dialog; show edit/delete only for owned documents or PM-managed project documents (FR-018, FR-019, FR-020, FR-021)

**Checkpoint**: Full document lifecycle management — create, read, update, replace, delete.

---

## Phase 7: User Story 5 — Share Documents (Priority: P5)

**Goal**: Owners share documents with specific users; recipients get notifications and see documents in "Shared with Me".

**Independent Test**: Share a document → verify recipient gets notification → verify it appears in "Shared with Me" → revoke → verify removal.

### Implementation for User Story 5

- [ ] T030 [US5] Implement DocumentService.ShareDocumentAsync in ContosoDashboard/Services/DocumentService.cs: verify ownership, create DocumentShare record (unique constraint prevents duplicates), send notification via INotificationService with NotificationType.DocumentShared, log "Share" activity (FR-023, FR-024)
- [ ] T031 [P] [US5] Implement DocumentService.RevokeShareAsync in ContosoDashboard/Services/DocumentService.cs: verify ownership, remove DocumentShare record (FR-023 revoke scenario from spec US5)
- [ ] T032 [P] [US5] Implement DocumentService.GetSharedDocumentsAsync in ContosoDashboard/Services/DocumentService.cs: query DocumentShares where SharedWithUserId == userId, include Document and UploadedBy navigation properties (FR-025)
- [ ] T033 [US5] Create SharedDocuments.razor page in ContosoDashboard/Pages/SharedDocuments.razor: [Authorize], "Shared with Me" view listing shared documents with title/category/upload date/shared by/shared date, download and preview links (FR-025)
- [ ] T034 [US5] Add share UI to document actions in ContosoDashboard/Pages/Documents.razor: share button opens modal with user selector (from IUserService), share/revoke functionality for owned documents (FR-023)
- [ ] T035 [US5] Add "Shared with Me" link to navigation menu in ContosoDashboard/Shared/NavMenu.razor pointing to /shared-documents

**Checkpoint**: Collaboration features complete — sharing, notifications, "Shared with Me" view.

---

## Phase 8: User Story 6 — Task and Dashboard Integration (Priority: P6)

**Goal**: Documents can be attached to tasks; dashboard shows recent documents widget and document count card.

**Independent Test**: Upload from task page → verify task-project association → verify dashboard widget shows recent uploads.

### Implementation for User Story 6

- [ ] T036 [US6] Implement DocumentService.GetTaskDocumentsAsync in ContosoDashboard/Services/DocumentService.cs: return documents where TaskId matches, verify requesting user has task access (FR-027)
- [ ] T037 [P] [US6] Implement DocumentService.GetRecentDocumentsAsync in ContosoDashboard/Services/DocumentService.cs: return last N documents uploaded by user, ordered by CreatedDate desc (FR-028)
- [ ] T038 [P] [US6] Implement DocumentService.GetAccessibleDocumentCountAsync in ContosoDashboard/Services/DocumentService.cs: count documents user owns + shared with user + project member documents (FR-029)
- [ ] T039 [US6] Add task documents section to ContosoDashboard/Pages/Tasks.razor (or task detail view): list documents attached to the specific task via GetTaskDocumentsAsync, add upload button that passes TaskId to upload form (auto-associates with task's project per FR-027)
- [ ] T040 [US6] Add "Recent Documents" widget to ContosoDashboard/Pages/Index.razor: call GetRecentDocumentsAsync(userId, 5), display last 5 uploads with title/category/date and link to Documents page (FR-028)
- [ ] T041 [US6] Add document count to dashboard summary in ContosoDashboard/Pages/Index.razor (and DashboardSummary model if needed): call GetAccessibleDocumentCountAsync, display as a summary card alongside existing cards (FR-029)

**Checkpoint**: Document features integrated with tasks and dashboard.

---

## Phase 9: User Story 7 — Audit and Activity Reporting (Priority: P7)

**Goal**: Administrators can view all document activity logs and generate reports.

**Independent Test**: Perform operations as multiple users → log in as Admin → verify activity log and reports.

### Implementation for User Story 7

- [ ] T042 [US7] Implement DocumentActivityService.GetActivitiesAsync in ContosoDashboard/Services/DocumentActivityService.cs: query activities with optional filters (documentId, userId, date range), include Document and User navigation properties, restrict to Administrator role (FR-032)
- [ ] T043 [P] [US7] Implement DocumentActivityService.GetUploadsByTypeReportAsync in ContosoDashboard/Services/DocumentActivityService.cs: group upload activities by Document.FileType, return counts (FR-033)
- [ ] T044 [P] [US7] Implement DocumentActivityService.GetTopUploadersReportAsync in ContosoDashboard/Services/DocumentActivityService.cs: group upload activities by UserId, return users ranked by count (FR-033)
- [ ] T045 [US7] Create DocumentAudit.razor page in ContosoDashboard/Pages/DocumentAudit.razor: [Authorize(Policy = "Administrator")], activity log table with filters (user, document, date range), report sections for "Most Uploaded Types" and "Most Active Uploaders" (FR-032, FR-033)
- [ ] T046 [US7] Add "Document Audit" link to navigation menu in ContosoDashboard/Shared/NavMenu.razor, visible only to Administrator role

**Checkpoint**: Audit and reporting complete — Admins can view all document activity and generate reports.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup, edge case handling, and documentation

- [ ] T047 [P] Add seed data for sample documents in ContosoDashboard/Data/ApplicationDbContext.cs: create 2–3 sample documents associated with the existing seed project and users to demonstrate the feature on first run
- [ ] T048 [P] Update ContosoDashboard/wwwroot/css/site.css: add styles for document upload progress bar, document list table, preview modal, and upload form if needed
- [ ] T049 Handle edge case: update ProjectDetails.razor to handle project deletion gracefully — set Document.ProjectId to null for orphaned documents (per data-model.md delete behavior)
- [ ] T050 Update README.md: add Document Upload and Management to the "Implemented Features" section, add new pages to the project structure, document the file storage location and migration path

**Checkpoint**: Feature fully complete, polished, and documented.

---

## Dependencies

```text
Phase 1 (Setup) ──→ Phase 2 (Foundational) ──→ Phase 3 (US1: Upload) 🎯 MVP
                                                     │
                                                     ├──→ Phase 4 (US2: Browse/Search)
                                                     │         │
                                                     │         └──→ Phase 5 (US3: Download/Preview)
                                                     │                    │
                                                     │                    └──→ Phase 6 (US4: Manage)
                                                     │
                                                     ├──→ Phase 7 (US5: Share) [needs US1]
                                                     │
                                                     ├──→ Phase 8 (US6: Integration) [needs US1 + US3]
                                                     │
                                                     └──→ Phase 9 (US7: Audit) [needs US1, activity logging from T013]
                                                     
Phase 10 (Polish) ──→ after all user stories complete
```

## Parallel Execution Opportunities

**Within Phase 1**: T001, T002, T003, T006, T008, T009 can all run in parallel (independent new files).

**Across Phases (after Phase 3)**:
- Phase 4 (Browse/Search) + Phase 7 (Share) + Phase 9 (Audit) can start in parallel since they only depend on Phase 3 being complete.
- Phase 5 (Download/Preview) depends on Phase 4 only for the UI integration (search results need download links), but the controller (T024) can start after Phase 3.
- Phase 8 (Integration) needs download links from Phase 5 to function fully.

## Implementation Strategy

1. **MVP (Phase 1–3)**: Delivers a working upload + browse feature. Users can upload documents with metadata and see them listed. This can be demonstrated and validated immediately.
2. **Core Loop (Phase 4–5)**: Adds search/filter and download/preview to complete the upload→find→retrieve workflow.
3. **Management (Phase 6)**: Adds edit, replace, and delete for document lifecycle management.
4. **Collaboration (Phase 7)**: Adds sharing, notifications, and "Shared with Me" view.
5. **Integration (Phase 8)**: Connects documents to existing tasks and dashboard.
6. **Compliance (Phase 9)**: Adds audit logging UI and reporting for Administrators.
7. **Polish (Phase 10)**: Seed data, styling, edge cases, README update.
