using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public interface IDocumentActivityService
{
    Task LogActivityAsync(int documentId, int userId, string activityType, string? details = null);
    Task<List<DocumentActivity>> GetActivitiesAsync(int? documentId = null, int? userId = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<Dictionary<string, int>> GetUploadsByTypeReportAsync();
    Task<List<(User user, int uploadCount)>> GetTopUploadersReportAsync(int count = 10);
}

public class DocumentActivityService : IDocumentActivityService
{
    private readonly ApplicationDbContext _context;

    public DocumentActivityService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogActivityAsync(int documentId, int userId, string activityType, string? details = null)
    {
        var activity = new DocumentActivity
        {
            DocumentId = documentId,
            UserId = userId,
            ActivityType = activityType,
            Details = details,
            ActivityDate = DateTime.UtcNow
        };

        _context.DocumentActivities.Add(activity);
        await _context.SaveChangesAsync();
    }

    public Task<List<DocumentActivity>> GetActivitiesAsync(int? documentId = null, int? userId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        throw new NotImplementedException(); // Implemented in Phase 9 (T042)
    }

    public Task<Dictionary<string, int>> GetUploadsByTypeReportAsync()
    {
        throw new NotImplementedException(); // Implemented in Phase 9 (T043)
    }

    public Task<List<(User user, int uploadCount)>> GetTopUploadersReportAsync(int count = 10)
    {
        throw new NotImplementedException(); // Implemented in Phase 9 (T044)
    }
}
