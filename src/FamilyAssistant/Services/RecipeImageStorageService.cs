using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace FamilyAssistant.Services;

/// <summary>
/// Stores recipe images on disk and resizes them for UI usage.
/// </summary>
public class RecipeImageStorageService
{
    private readonly string _basePath;
    private readonly ILogger<RecipeImageStorageService> _logger;

    private const int MaxDimension = 2048;
    private const int JpegQuality = 85;

    public RecipeImageStorageService(IWebHostEnvironment env, ILogger<RecipeImageStorageService> logger)
    {
        _logger = logger;
        _basePath = env.IsDevelopment()
            ? Path.Combine(Directory.GetCurrentDirectory(), "data", "recipe-images")
            : "/data/recipe-images";

        Directory.CreateDirectory(_basePath);
    }

    public async Task<(string RelativePath, string ContentType, long FileSize)> StoreImageAsync(
        int recipeId,
        Stream imageStream,
        string originalFileName,
        CancellationToken ct = default)
    {
        var recipeDir = Path.Combine(_basePath, recipeId.ToString());
        Directory.CreateDirectory(recipeDir);

        var fileId = Guid.NewGuid().ToString("N")[..12];
        var fileName = $"{fileId}.jpg";
        var fullPath = Path.Combine(recipeDir, fileName);
        var relativePath = $"{recipeId}/{fileName}";

        try
        {
            using var image = await Image.LoadAsync(imageStream, ct);

            if (image.Width > MaxDimension || image.Height > MaxDimension)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaxDimension, MaxDimension)
                }));
            }

            image.Mutate(x => x.AutoOrient());
            await image.SaveAsync(fullPath, new JpegEncoder { Quality = JpegQuality }, ct);

            var fileSize = new FileInfo(fullPath).Length;
            _logger.LogInformation("Stored recipe image: {Path} ({Size} bytes)", relativePath, fileSize);

            return (relativePath, "image/jpeg", fileSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process/store image for recipe {RecipeId} ({OriginalFileName})", recipeId, originalFileName);
            throw;
        }
    }

    public string GetFullPath(string relativePath) => Path.Combine(_basePath, relativePath);

    public void DeleteImage(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public void DeleteRecipeImages(int recipeId)
    {
        var recipeDir = Path.Combine(_basePath, recipeId.ToString());
        if (Directory.Exists(recipeDir))
        {
            Directory.Delete(recipeDir, recursive: true);
        }
    }
}
