using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public interface IDocumentService
{
    Task<Document> UploadDocumentAsync(int userId, Stream fileStream, string originalFileName, string title, string category, string? description, string? tags, int? projectId, int? taskId);
    Task<List<Document>> GetUserDocumentsAsync(int userId, string? category = null, int? projectId = null, DateTime? fromDate = null, DateTime? toDate = null, string? sortBy = null, bool descending = true);
    Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId);
    Task<List<Document>> GetTaskDocumentsAsync(int taskId, int requestingUserId);
    Task<List<Document>> GetSharedDocumentsAsync(int userId);
    Task<List<Document>> SearchDocumentsAsync(int userId, string query);
    Task<Document?> GetDocumentByIdAsync(int documentId, int requestingUserId);
    Task<List<Document>> GetRecentDocumentsAsync(int userId, int count);
    Task<int> GetAccessibleDocumentCountAsync(int userId);
    Task<bool> UpdateDocumentMetadataAsync(int documentId, int requestingUserId, string title, string? description, string category, string? tags);
    Task<bool> ReplaceDocumentFileAsync(int documentId, int requestingUserId, Stream newFileStream, string newFileName);
    Task<bool> DeleteDocumentAsync(int documentId, int requestingUserId);
    Task<bool> ShareDocumentAsync(int documentId, int ownerUserId, int targetUserId);
    Task<bool> RevokeShareAsync(int documentId, int ownerUserId, int targetUserId);
}

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IDocumentActivityService _activityService;
    private readonly INotificationService _notificationService;

    // Allowed file extensions
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".jpg", ".jpeg", ".png"
    };

    // Extension to MIME type mapping
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".txt"] = "text/plain",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png"
    };

    // Magic bytes signatures for content validation
    private static readonly Dictionary<string, byte[][]> MagicBytes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = [new byte[] { 0x25, 0x50, 0x44, 0x46 }], // %PDF
        [".jpg"] = [new byte[] { 0xFF, 0xD8, 0xFF }],
        [".jpeg"] = [new byte[] { 0xFF, 0xD8, 0xFF }],
        [".png"] = [new byte[] { 0x89, 0x50, 0x4E, 0x47 }], // ‰PNG
        [".doc"] = [new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }], // OLE compound
        [".xls"] = [new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }],
        [".ppt"] = [new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }],
        [".docx"] = [new byte[] { 0x50, 0x4B, 0x03, 0x04 }], // PK zip
        [".xlsx"] = [new byte[] { 0x50, 0x4B, 0x03, 0x04 }],
        [".pptx"] = [new byte[] { 0x50, 0x4B, 0x03, 0x04 }],
        // .txt has no reliable magic bytes — skip validation
    };

    private const long MaxFileSize = 25 * 1024 * 1024; // 25 MB

    public DocumentService(
        ApplicationDbContext context,
        IFileStorageService fileStorage,
        IDocumentActivityService activityService,
        INotificationService notificationService)
    {
        _context = context;
        _fileStorage = fileStorage;
        _activityService = activityService;
        _notificationService = notificationService;
    }

    public async Task<Document> UploadDocumentAsync(int userId, Stream fileStream, string originalFileName, string title, string category, string? description, string? tags, int? projectId, int? taskId)
    {
        // Validate extension
        if (!IsExtensionAllowed(originalFileName))
            throw new InvalidOperationException($"File type '{Path.GetExtension(originalFileName)}' is not allowed.");

        // Read file into memory for size check and magic bytes validation
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        if (!IsFileSizeAllowed(fileBytes.Length))
            throw new InvalidOperationException("File size exceeds the 25 MB limit.");

        // Validate magic bytes (read first 8 bytes for signature check)
        var header = fileBytes.Length >= 8 ? fileBytes[..8] : fileBytes;
        if (!ValidateMagicBytes(originalFileName, header))
            throw new InvalidOperationException("File content does not match the declared file type.");

        // If TaskId is set, auto-fill ProjectId from the task
        if (taskId.HasValue && !projectId.HasValue)
        {
            var task = await _context.Tasks.FindAsync(taskId.Value);
            if (task != null)
                projectId = task.ProjectId;
        }

        // Validate project membership if ProjectId is set
        if (projectId.HasValue)
        {
            var isMember = await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId.Value && pm.UserId == userId);
            var isManager = await _context.Projects.AnyAsync(p => p.ProjectId == projectId.Value && p.ProjectManagerId == userId);
            var isAdmin = await _context.Users.AnyAsync(u => u.UserId == userId && u.Role == UserRole.Administrator);
            if (!isMember && !isManager && !isAdmin)
                throw new InvalidOperationException("You are not a member of the specified project.");
        }

        // Generate GUID-based storage path
        var ext = Path.GetExtension(originalFileName);
        var projectFolder = projectId.HasValue ? projectId.Value.ToString() : "personal";
        var relativePath = Path.Combine(userId.ToString(), projectFolder, $"{Guid.NewGuid()}{ext}");

        // Upload file
        memoryStream.Position = 0;
        await _fileStorage.UploadAsync(memoryStream, relativePath);

        // Create document entity
        var document = new Document
        {
            Title = title,
            Description = description,
            Category = category,
            Tags = tags,
            OriginalFileName = originalFileName,
            FileSize = fileBytes.Length,
            FileType = GetMimeType(originalFileName),
            FilePath = relativePath,
            UploadedById = userId,
            ProjectId = projectId,
            TaskId = taskId,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // Log activity
        await _activityService.LogActivityAsync(document.DocumentId, userId, "Upload", $"Uploaded '{originalFileName}'");

        // Notify project members if project-associated
        if (projectId.HasValue)
        {
            var projectMembers = await _context.ProjectMembers
                .Where(pm => pm.ProjectId == projectId.Value && pm.UserId != userId)
                .Select(pm => pm.UserId)
                .ToListAsync();

            var project = await _context.Projects.FindAsync(projectId.Value);

            foreach (var memberId in projectMembers)
            {
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = memberId,
                    Title = "New Document Uploaded",
                    Message = $"'{title}' was uploaded to project '{project?.Name ?? "Unknown"}'.",
                    Type = NotificationType.DocumentAddedToProject,
                    Priority = NotificationPriority.Informational
                });
            }
        }

        return document;
    }

    public async Task<List<Document>> GetUserDocumentsAsync(int userId, string? category = null, int? projectId = null, DateTime? fromDate = null, DateTime? toDate = null, string? sortBy = null, bool descending = true)
    {
        var query = _context.Documents
            .Where(d => d.UploadedById == userId)
            .Include(d => d.Project)
            .AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(d => d.Category == category);

        if (projectId.HasValue)
            query = query.Where(d => d.ProjectId == projectId.Value);

        if (fromDate.HasValue)
            query = query.Where(d => d.CreatedDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(d => d.CreatedDate <= toDate.Value);

        query = sortBy?.ToLowerInvariant() switch
        {
            "title" => descending ? query.OrderByDescending(d => d.Title) : query.OrderBy(d => d.Title),
            "category" => descending ? query.OrderByDescending(d => d.Category) : query.OrderBy(d => d.Category),
            "filesize" => descending ? query.OrderByDescending(d => d.FileSize) : query.OrderBy(d => d.FileSize),
            _ => descending ? query.OrderByDescending(d => d.CreatedDate) : query.OrderBy(d => d.CreatedDate),
        };

        return await query.ToListAsync();
    }

    public Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId)
    {
        throw new NotImplementedException();
    }

    public Task<List<Document>> GetTaskDocumentsAsync(int taskId, int requestingUserId)
    {
        throw new NotImplementedException();
    }

    public Task<List<Document>> GetSharedDocumentsAsync(int userId)
    {
        throw new NotImplementedException();
    }

    public Task<List<Document>> SearchDocumentsAsync(int userId, string query)
    {
        throw new NotImplementedException();
    }

    public Task<Document?> GetDocumentByIdAsync(int documentId, int requestingUserId)
    {
        throw new NotImplementedException();
    }

    public Task<List<Document>> GetRecentDocumentsAsync(int userId, int count)
    {
        throw new NotImplementedException();
    }

    public Task<int> GetAccessibleDocumentCountAsync(int userId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> UpdateDocumentMetadataAsync(int documentId, int requestingUserId, string title, string? description, string category, string? tags)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ReplaceDocumentFileAsync(int documentId, int requestingUserId, Stream newFileStream, string newFileName)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteDocumentAsync(int documentId, int requestingUserId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ShareDocumentAsync(int documentId, int ownerUserId, int targetUserId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> RevokeShareAsync(int documentId, int ownerUserId, int targetUserId)
    {
        throw new NotImplementedException();
    }

    // --- Validation helpers (T011) ---

    internal static bool IsExtensionAllowed(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext);
    }

    internal static bool IsFileSizeAllowed(long fileSize)
    {
        return fileSize > 0 && fileSize <= MaxFileSize;
    }

    internal static bool ValidateMagicBytes(string fileName, byte[] fileHeader)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext)) return false;

        // .txt has no magic bytes — always pass
        if (ext.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!MagicBytes.TryGetValue(ext, out var signatures))
            return false;

        foreach (var signature in signatures)
        {
            if (fileHeader.Length >= signature.Length)
            {
                bool match = true;
                for (int i = 0; i < signature.Length; i++)
                {
                    if (fileHeader[i] != signature[i])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
        }

        return false;
    }

    internal static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(ext) && MimeTypes.TryGetValue(ext, out var mimeType))
            return mimeType;
        return "application/octet-stream";
    }

    // --- Authorization helper (T012) ---

    internal async Task<bool> HasAccessAsync(int documentId, int userId)
    {
        var document = await _context.Documents
            .Include(d => d.DocumentShares)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null) return false;

        // Owner always has access
        if (document.UploadedById == userId) return true;

        // Administrator has full access
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;
        if (user.Role == UserRole.Administrator) return true;

        // Shared with user
        if (document.DocumentShares.Any(ds => ds.SharedWithUserId == userId))
            return true;

        // Project member
        if (document.ProjectId.HasValue)
        {
            var isMember = await _context.ProjectMembers
                .AnyAsync(pm => pm.ProjectId == document.ProjectId.Value && pm.UserId == userId);
            if (isMember) return true;

            // Project manager
            var isManager = await _context.Projects
                .AnyAsync(p => p.ProjectId == document.ProjectId.Value && p.ProjectManagerId == userId);
            if (isManager) return true;
        }

        // Team Lead: read-only access to documents uploaded by users in the same department
        if (user.Role == UserRole.TeamLead)
        {
            var uploader = await _context.Users.FindAsync(document.UploadedById);
            if (uploader != null && uploader.Department == user.Department)
                return true;
        }

        return false;
    }
}
