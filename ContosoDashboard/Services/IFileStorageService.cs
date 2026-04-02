namespace ContosoDashboard.Services;

public interface IFileStorageService
{
    Task UploadAsync(Stream fileStream, string relativePath);
    Task<Stream> DownloadAsync(string relativePath);
    Task DeleteAsync(string relativePath);
    Task<bool> ExistsAsync(string relativePath);
}
