namespace ContosoDashboard.Services;

// Training-only implementation: stores files on the local filesystem.
// In production, replace with an Azure Blob Storage or S3 implementation of IFileStorageService.
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public LocalFileStorageService(IWebHostEnvironment environment)
    {
        _basePath = Path.Combine(environment.ContentRootPath, "AppData", "uploads");
    }

    public async Task UploadAsync(Stream fileStream, string relativePath)
    {
        var absolutePath = Path.Combine(_basePath, relativePath);
        var directory = Path.GetDirectoryName(absolutePath)!;

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        using var fileStreamOut = new FileStream(absolutePath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(fileStreamOut);
    }

    public Task<Stream> DownloadAsync(string relativePath)
    {
        var absolutePath = Path.Combine(_basePath, relativePath);

        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("File not found.", absolutePath);

        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string relativePath)
    {
        var absolutePath = Path.Combine(_basePath, relativePath);

        if (File.Exists(absolutePath))
            File.Delete(absolutePath);

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string relativePath)
    {
        var absolutePath = Path.Combine(_basePath, relativePath);
        return Task.FromResult(File.Exists(absolutePath));
    }
}
