# Research: Document Upload and Management

**Feature**: 001-doc-upload-management
**Date**: 2026-04-02

## R1: Blazor Server File Upload Pattern

**Context**: Blazor Server uses SignalR for communication. File uploads must handle the stream correctly to avoid disposal issues.

**Decision**: Use `InputFile` component with `IBrowserFile` interface.

**Rationale**:
- `InputFile` is the built-in Blazor component for file selection; it exposes `IBrowserFile` objects.
- For files up to 25 MB, read the file into a `MemoryStream` in the component's `OnChange` handler before passing to the service layer. This avoids the `IBrowserFile.OpenReadStream()` being disposed when the SignalR circuit processes the next event.
- Set `maxAllowedSize` parameter on `OpenReadStream()` to `25 * 1024 * 1024` (25 MB) to enforce client-side limit.
- For multi-file upload, iterate `InputFileChangeEventArgs.GetMultipleFiles(maxAllowedFiles)` and process each file independently, collecting per-file success/failure results.
- Display progress by tracking bytes read in a loop rather than relying on browser upload progress (SignalR transfers chunks, not HTTP upload).

**Alternatives considered**:
- JavaScript interop for upload: Rejected — adds complexity and bypasses Blazor's built-in component. Not aligned with training clarity (Principle IV).
- Streaming directly to disk from `IBrowserFile`: Rejected — the stream can be disposed between Blazor render cycles, causing `ObjectDisposedException`.

## R2: File Serving via Controller Endpoint

**Context**: Blazor Server pages cannot return file download responses (they render HTML via SignalR). A standard ASP.NET Core controller is needed to serve files with authorization.

**Decision**: Add a minimal `DocumentController` with `[Authorize]` attribute and action methods for download and preview.

**Rationale**:
- ASP.NET Core MVC controllers coexist with Blazor Server in the same application. The existing `Program.cs` already calls `AddRazorPages()` — adding `AddControllers()` and `MapControllers()` is trivial.
- Download endpoint: `GET /api/documents/{id}/download` — reads file via `IFileStorageService.DownloadAsync()`, sets `Content-Disposition: attachment; filename="original-name.ext"`, returns `FileStreamResult`.
- Preview endpoint: `GET /api/documents/{id}/preview` — same file read, but sets `Content-Disposition: inline` and appropriate `Content-Type` (only for PDF/JPEG/PNG).
- Both endpoints call `DocumentService` to verify the requesting user has access before serving the file (IDOR protection, Principle II).
- The CSP header in `Program.cs` already allows `img-src 'self'` and `default-src 'self'`, which covers serving files from the same origin.

**Alternatives considered**:
- Razor Page handler (`OnGet` in a `.cshtml.cs`): Would work but controllers are more idiomatic for API-style endpoints and keep the file-serving concern separate from page rendering.
- Blazor JS interop to trigger download: Would still need a server endpoint to serve the actual file bytes, so the controller is needed regardless.

## R3: File Extension and Content-Type Validation

**Context**: FR-037 requires validating file content against the declared extension to prevent disguised uploads (e.g., `.exe` renamed to `.pdf`).

**Decision**: Use a two-layer validation approach: extension whitelist + file signature (magic bytes) check for critical types.

**Rationale**:
- **Layer 1 — Extension whitelist**: Check the file extension against the allowed set (`.pdf`, `.doc`, `.docx`, `.xls`, `.xlsx`, `.ppt`, `.pptx`, `.txt`, `.jpg`, `.jpeg`, `.png`). This is fast and catches most misuse.
- **Layer 2 — Magic bytes check**: For the most commonly disguised types, read the first few bytes of the file and validate against known signatures:
  - PDF: starts with `%PDF` (0x25504446)
  - JPEG: starts with `0xFFD8FF`
  - PNG: starts with `0x89504E47` (`‰PNG`)
  - ZIP-based Office formats (.docx, .xlsx, .pptx): start with `0x504B0304` (PK zip header)
  - Legacy Office (.doc, .xls, .ppt): start with `0xD0CF11E0` (OLE compound document)
  - Text files: skip magic-byte check (no reliable signature)
- Map extensions to MIME types using a static dictionary. The `FileType` field in the Document entity stores the MIME type (up to 255 characters, sufficient for all Office document MIME types like `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`).

**Alternatives considered**:
- Full content-type sniffing library (e.g., `Mime-Detective` NuGet): Rejected — adds an external dependency for a training project. The magic-bytes approach is simple and educational.
- Trusting the browser-provided `Content-Type`: Rejected — easily spoofed; not secure.

## R4: Local File Storage Path Pattern

**Context**: Files must be stored outside `wwwroot` to prevent direct HTTP access. The path pattern must support GUID-based filenames and be compatible with future Azure Blob Storage migration.

**Decision**: Store files in `{ContentRootPath}/AppData/uploads/{userId}/{projectId|personal}/{guid}.{ext}`.

**Rationale**:
- `ContentRootPath` (from `IWebHostEnvironment`) is the application root, not the web root. The `AppData/uploads/` subdirectory is outside `wwwroot` and never served statically.
- The path pattern `{userId}/{projectId|personal}/{guid}.{ext}` matches the stakeholder document's recommendation and works directly as Azure Blob container paths for future migration.
- GUID-based filenames prevent path traversal attacks and filename collisions.
- The `IFileStorageService` interface methods:
  - `Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)` — returns the stored relative path
  - `Task<Stream> DownloadAsync(string filePath)` — returns a readable stream
  - `Task DeleteAsync(string filePath)` — removes the file
  - `Task<bool> ExistsAsync(string filePath)` — checks if file exists (useful for replace operations)
- `LocalFileStorageService` constructs absolute paths from `ContentRootPath` + the relative path. The relative path is what gets stored in the database's `FilePath` column — portable for cloud migration.

**Alternatives considered**:
- Store in `wwwroot/uploads/` with directory browsing disabled: Rejected — violates Constitution Principle II (files MUST be outside `wwwroot`).
- Store in a temp directory: Rejected — temp directories may be cleaned by the OS.

## R5: Blazor Server File Upload Progress Indicator

**Context**: FR-010 requires a progress indicator during upload. Blazor Server transfers files via SignalR in chunks, not as a standard HTTP upload with native browser progress events.

**Decision**: Track progress by reading the `IBrowserFile` stream in a loop with a fixed buffer size and updating a percentage bound to the UI.

**Rationale**:
- Read the file in 512 KB chunks using `Stream.ReadAsync(buffer, 0, bufferSize)`.
- After each chunk, update a `double progressPercent` field and call `StateHasChanged()` to refresh the progress bar.
- The total size is known from `IBrowserFile.Size`.
- This gives accurate progress for larger files. For small files (<1 MB), the upload completes so fast that the progress bar fills instantly, which is acceptable.

**Alternatives considered**:
- JavaScript interop with `XMLHttpRequest.upload.onprogress`: Rejected — this would bypass the Blazor file upload pipeline and require a separate HTTP upload endpoint alongside the existing SignalR pipeline. Over-engineering for a training project (Principle IV).

## R6: Notification Integration

**Context**: FR-024 and FR-030 require notifications when documents are shared or added to projects. The existing `NotificationService` and `Notification` model need extension.

**Decision**: Extend the existing `NotificationType` enum with new values for document events; reuse the existing `NotificationService.CreateNotificationAsync()` method.

**Rationale**:
- The current `NotificationType` enum in `Notification.cs` includes values like `TaskAssignment`, `ProjectUpdate`, etc. Adding `DocumentShared` and `DocumentAddedToProject` follows the established pattern.
- The existing `INotificationService.CreateNotificationAsync(Notification)` method can be called from `DocumentService` after successful upload/share operations — same pattern used by `TaskService`.
- No changes to the notification UI (`Notifications.razor`) are needed; new notification types will display automatically with appropriate titles and messages.

**Alternatives considered**:
- Separate notification service for documents: Rejected — violates simplicity; the existing pattern handles this cleanly.
