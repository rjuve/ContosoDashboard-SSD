# Quickstart: Document Upload and Management

**Feature**: 001-doc-upload-management
**Date**: 2026-04-02

## Prerequisites

- .NET 10.0 SDK
- SQLite (included via EF Core Sqlite provider — no separate install needed)
- The existing ContosoDashboard application builds and runs

## How to Build and Run

```powershell
cd ContosoDashboard
dotnet run
```

The application auto-creates the database (including new Document tables) via `EnsureCreated()` on startup. No migrations are needed for the training environment.

## How to Test the Feature

1. Navigate to `http://localhost:5000` and log in as any user.
2. Click **Documents** in the nav menu to access "My Documents".
3. Click **Upload** to open the upload form.
4. Select a file (PDF, Office doc, image, or text), provide a title, choose a category, and submit.
5. Verify the file appears in the document list with correct metadata.
6. Click **Download** to retrieve the file; click **Preview** for PDF/images.
7. Log in as a different user and verify they cannot access the first user's document by URL.

## Key Files

| File | Purpose |
|------|---------|
| `Models/Document.cs` | Entity model with metadata fields |
| `Models/DocumentShare.cs` | Sharing relationship entity |
| `Models/DocumentActivity.cs` | Audit log entity |
| `Data/ApplicationDbContext.cs` | Updated DbSets and relationships |
| `Services/IFileStorageService.cs` | File storage abstraction interface |
| `Services/LocalFileStorageService.cs` | Local filesystem implementation (training) |
| `Services/DocumentService.cs` | Business logic for all document operations |
| `Services/DocumentActivityService.cs` | Audit logging service |
| `Controllers/DocumentController.cs` | Download/preview HTTP endpoints |
| `Pages/Documents.razor` | My Documents view |
| `Pages/SharedDocuments.razor` | Shared with Me view |
| `Pages/DocumentAudit.razor` | Admin audit log |
| `Program.cs` | DI registration for new services |

## File Storage Location

Uploaded files are stored in:

```
{ContentRootPath}/AppData/uploads/{userId}/{projectId|personal}/{guid}.{ext}
```

This directory is outside `wwwroot` and is NOT served by the static files middleware. Files are only accessible through the authorized controller endpoints.

## Production Migration Path

### File Storage → Azure Blob Storage

1. Add `Azure.Storage.Blobs` NuGet package.
2. Create `AzureBlobStorageService : IFileStorageService` using the Azure Blob SDK.
3. Update `Program.cs` DI registration to swap `LocalFileStorageService` → `AzureBlobStorageService`.
4. Configure connection string in `appsettings.json`.
5. No changes to `DocumentService`, controllers, pages, or database schema.

### Database → Azure SQL

1. Update connection string in `appsettings.json` from SQLite to Azure SQL.
2. Switch EF Core provider from `UseSqlite()` to `UseSqlServer()`.
3. No schema changes — the same entity models and relationships work on both providers.

### Authentication → Microsoft Entra ID

1. Replace cookie authentication in `Program.cs` with `AddMicrosoftIdentityWebApp()`.
2. Update claims extraction to use Entra ID claim types.
3. No changes to authorization policies or service-level checks.

## Training Notes

- **`LocalFileStorageService`** is labeled as a training-only implementation. Production requires `AzureBlobStorageService`.
- **Mock authentication** is used for login. Production requires Microsoft Entra ID with proper password hashing and MFA.
- **Virus scanning** is stubbed out. Production requires integration with a real scanning service (e.g., Microsoft Defender for Cloud).
- **Database** uses `EnsureCreated()` for automatic schema setup. Production should use EF Core migrations.
