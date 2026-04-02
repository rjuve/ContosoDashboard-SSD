# Document API Contracts

**Feature**: 001-doc-upload-management
**Date**: 2026-04-02
**Type**: ASP.NET Core Controller endpoints (file serving)

## Overview

The ContosoDashboard is a Blazor Server application. Document upload is handled via Blazor's `InputFile` component and SignalR. However, file **download** and **preview** require standard HTTP endpoints because Blazor Server cannot stream binary file responses to the browser.

These endpoints are implemented as ASP.NET Core controller actions in `DocumentController.cs`.

## Base Path

```
/api/documents
```

## Endpoints

### GET /api/documents/{id}/download

**Purpose**: Download a document file with the original filename.

**Authorization**: `[Authorize]` — user must own the document, have it shared with them, be a member of the document's project, be a Team Lead in the uploader's department (read-only), or be an Administrator.

**Parameters**:
| Parameter | Location | Type | Required | Description |
|-----------|----------|------|----------|-------------|
| id | Path | int | Yes | Document ID |

**Success Response** (200):
- Content-Type: The document's stored MIME type (e.g., `application/pdf`)
- Content-Disposition: `attachment; filename="original-filename.ext"`
- Body: File binary stream

**Error Responses**:
| Status | Condition |
|--------|-----------|
| 401 | User not authenticated |
| 403 | User does not have access to this document |
| 404 | Document ID does not exist |

**Activity Logging**: On successful download, a `DocumentActivity` record is created with `ActivityType = "Download"`.

---

### GET /api/documents/{id}/preview

**Purpose**: Render a document inline in the browser (PDF and images only).

**Authorization**: Same as download endpoint.

**Parameters**:
| Parameter | Location | Type | Required | Description |
|-----------|----------|------|----------|-------------|
| id | Path | int | Yes | Document ID |

**Supported Types**:
- `application/pdf`
- `image/jpeg`
- `image/png`

**Success Response** (200):
- Content-Type: The document's stored MIME type
- Content-Disposition: `inline; filename="original-filename.ext"`
- Body: File binary stream

**Error Responses**:
| Status | Condition |
|--------|-----------|
| 400 | File type does not support inline preview |
| 401 | User not authenticated |
| 403 | User does not have access to this document |
| 404 | Document ID does not exist |

**Activity Logging**: No activity log for preview (to avoid noise from repeated views). Downloads are logged separately.

## Service Interface Contracts

### IFileStorageService

```text
UploadAsync(Stream fileStream, string relativePath)  → Task
DownloadAsync(string relativePath)                    → Task<Stream>
DeleteAsync(string relativePath)                      → Task
ExistsAsync(string relativePath)                      → Task<bool>
```

- `relativePath` format: `{userId}/{projectId|personal}/{guid}.{ext}`
- `LocalFileStorageService` resolves absolute path as: `{ContentRootPath}/AppData/uploads/{relativePath}`
- Future `AzureBlobStorageService` uses `relativePath` as the blob name in a container.

### IDocumentService

```text
UploadDocumentAsync(int userId, Stream fileStream, string originalFileName, string title, string category, string? description, string? tags, int? projectId, int? taskId) → Task<Document>
GetUserDocumentsAsync(int userId, string? category, int? projectId, DateTime? fromDate, DateTime? toDate, string? sortBy, bool descending) → Task<List<Document>>
GetProjectDocumentsAsync(int projectId, int requestingUserId) → Task<List<Document>>
GetTaskDocumentsAsync(int taskId, int requestingUserId) → Task<List<Document>>
GetSharedDocumentsAsync(int userId) → Task<List<Document>>
SearchDocumentsAsync(int userId, string query) → Task<List<Document>>
GetDocumentByIdAsync(int documentId, int requestingUserId) → Task<Document?>
GetRecentDocumentsAsync(int userId, int count) → Task<List<Document>>
GetAccessibleDocumentCountAsync(int userId) → Task<int>
UpdateDocumentMetadataAsync(int documentId, int requestingUserId, string title, string? description, string category, string? tags) → Task<bool>
ReplaceDocumentFileAsync(int documentId, int requestingUserId, Stream newFileStream, string newFileName) → Task<bool>
DeleteDocumentAsync(int documentId, int requestingUserId) → Task<bool>
ShareDocumentAsync(int documentId, int ownerUserId, int targetUserId) → Task<bool>
RevokeShareAsync(int documentId, int ownerUserId, int targetUserId) → Task<bool>
```

All methods enforce authorization internally:
- Checks user ownership, project membership, share status, Team Lead department access, or Administrator role.
- Returns `null` or `false` for unauthorized access (same pattern as existing `TaskService`).

### IDocumentActivityService

```text
LogActivityAsync(int documentId, int userId, string activityType, string? details) → Task
GetActivitiesAsync(int? documentId, int? userId, DateTime? fromDate, DateTime? toDate) → Task<List<DocumentActivity>>
GetUploadsByTypeReportAsync() → Task<Dictionary<string, int>>
GetTopUploadersReportAsync(int count) → Task<List<(User user, int uploadCount)>>
```

Admin-only methods verify the requesting user's role before returning data.

## Authorization Matrix

| Role | Own Documents | Shared Documents | Project Documents | Department Documents | All Documents |
|------|--------------|-----------------|-------------------|---------------------|---------------|
| Employee | CRUD | Read | Read (members) | — | — |
| Team Lead | CRUD | Read | Read (members) | Read (department) | — |
| Project Manager | CRUD | Read | CRUD (managed projects) | — | — |
| Administrator | CRUD | Read | Read | Read | CRUD (full access) |

**CRUD** = Create, Read, Update, Delete
**Read** = View, Download, Preview
