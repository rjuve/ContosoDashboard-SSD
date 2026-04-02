# Data Model: Document Upload and Management

**Feature**: 001-doc-upload-management
**Date**: 2026-04-02
**Source**: spec.md Key Entities, clarifications, and stakeholder document constraints

## Entities

### Document

Represents an uploaded file and its metadata.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| DocumentId | int | PK, auto-increment | Primary key (integer, consistent with existing entities) |
| Title | string | Required, MaxLength(255) | User-provided document title |
| Description | string? | MaxLength(2000) | Optional description |
| Category | string | Required, MaxLength(50) | Text value from predefined list (not enum — stakeholder constraint) |
| Tags | string? | MaxLength(1000) | Comma-separated user-defined tags |
| OriginalFileName | string | Required, MaxLength(500) | Original filename as uploaded by the user (used for download) |
| FileSize | long | Required | File size in bytes |
| FileType | string | Required, MaxLength(255) | MIME type (e.g., "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet") |
| FilePath | string | Required, MaxLength(500) | GUID-based relative storage path (not user-facing) |
| UploadedById | int | Required, FK → User.UserId | User who uploaded the document |
| ProjectId | int? | FK → Project.ProjectId | Optional project association |
| TaskId | int? | FK → TaskItem.TaskId | Optional task association (auto-associates with task's project) |
| CreatedDate | DateTime | Required, default: UtcNow | Upload timestamp |
| UpdatedDate | DateTime | Required, default: UtcNow | Last modification timestamp |

**Predefined categories** (stored as text values):
- "Project Documents"
- "Team Resources"
- "Personal Files"
- "Reports"
- "Presentations"
- "Other"

### DocumentShare

Represents a sharing relationship between a Document and a User.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| DocumentShareId | int | PK, auto-increment | Primary key |
| DocumentId | int | Required, FK → Document.DocumentId | The shared document |
| SharedWithUserId | int | Required, FK → User.UserId | Recipient of the share |
| SharedByUserId | int | Required, FK → User.UserId | User who initiated the share |
| SharedDate | DateTime | Required, default: UtcNow | When the share was created |

**Unique constraint**: (DocumentId, SharedWithUserId) — a document can only be shared once with the same user.

### DocumentActivity

Represents a logged action on a document for audit purposes.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| DocumentActivityId | int | PK, auto-increment | Primary key |
| DocumentId | int | Required, FK → Document.DocumentId | Target document |
| UserId | int | Required, FK → User.UserId | User who performed the action |
| ActivityType | string | Required, MaxLength(50) | Operation type: "Upload", "Download", "Delete", "Share", "Edit", "Replace" |
| Details | string? | MaxLength(500) | Optional details (e.g., "Shared with user@contoso.com") |
| ActivityDate | DateTime | Required, default: UtcNow | When the action occurred |

**Note**: `ActivityType` is stored as text (not enum) for extensibility and database portability.

## Relationships

```text
User (1) ──────< Document (*)      [UploadedById → UserId]
Project (1) ───< Document (*)      [ProjectId → ProjectId, optional]
TaskItem (1) ──< Document (*)      [TaskId → TaskId, optional]
Document (1) ──< DocumentShare (*) [DocumentId → DocumentId, cascade delete]
User (1) ──────< DocumentShare (*) [SharedWithUserId → UserId]
User (1) ──────< DocumentShare (*) [SharedByUserId → UserId]
Document (1) ──< DocumentActivity (*) [DocumentId → DocumentId, cascade delete]
User (1) ──────< DocumentActivity (*) [UserId → UserId]
```

### Delete behavior

- **Document deleted** → Cascade delete `DocumentShare` and `DocumentActivity` records for that document.
- **User deleted** → Restrict (do not cascade). Documents remain; edge case says ownership transfers to Administrator.
- **Project deleted** → Set `Document.ProjectId` to null (documents remain in user's personal list).
- **Task deleted** → Set `Document.TaskId` to null (documents remain, project association preserved if set).

## Indexes

| Table | Columns | Type | Rationale |
|-------|---------|------|-----------|
| Document | UploadedById | Non-unique | Filter "My Documents" by uploader |
| Document | ProjectId | Non-unique | Filter documents by project |
| Document | TaskId | Non-unique | Filter documents by task |
| Document | Category | Non-unique | Filter by category |
| Document | CreatedDate | Non-unique | Sort/filter by date range |
| DocumentShare | SharedWithUserId | Non-unique | "Shared with Me" query |
| DocumentShare | (DocumentId, SharedWithUserId) | Unique | Prevent duplicate shares |
| DocumentActivity | DocumentId | Non-unique | Activity log per document |
| DocumentActivity | UserId | Non-unique | Activity log per user (admin reports) |
| DocumentActivity | ActivityDate | Non-unique | Date-range filtering for reports |

## Validation Rules

- **Title**: 1–255 characters, required.
- **Category**: Must be one of the 6 predefined values.
- **File extension**: Must be in the allowed set: `.pdf`, `.doc`, `.docx`, `.xls`, `.xlsx`, `.ppt`, `.pptx`, `.txt`, `.jpg`, `.jpeg`, `.png`.
- **File size**: Must be ≤ 25 MB (26,214,400 bytes).
- **File content**: Magic bytes must match the declared extension (for PDF, JPEG, PNG, Office formats).
- **ProjectId** (when set): User must be a member of the project (verified via `ProjectMembers` table).
- **TaskId** (when set): Task must exist and user must have access. Setting TaskId auto-fills ProjectId from the task.
- **Tags**: Optional; if provided, stored as comma-separated values.

## State Transitions

Documents do not have a formal state machine. The lifecycle is:

1. **Created** — File uploaded, metadata recorded, `CreatedDate` set.
2. **Updated** — Metadata edited or file replaced, `UpdatedDate` set.
3. **Shared** — `DocumentShare` record(s) created; notifications sent.
4. **Deleted** — File removed from disk, database record and related shares/activities cascade-deleted.
