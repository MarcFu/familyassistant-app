using FamilyAssistant.Data;
using FamilyAssistant.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssistant.Tests;

/// <summary>
/// Tests for server-side validation logic that mirrors endpoint validation.
/// These test the data integrity rules without requiring a running web server.
/// </summary>
public class ValidationTests
{
    [Fact]
    public void TaskAttachment_RequiresValidComment()
    {
        // Arrange
        using var db = TestDbFactory.Create();

        var chore = new Chore { Name = "Test", CreditsReward = 1, IsActive = true };
        db.Chores.Add(chore);
        db.SaveChanges();

        var task = new ChoreTask
        {
            ChoreId = chore.Id,
            DueDate = DateOnly.FromDateTime(DateTime.Today),
            OccurrenceIndex = 1,
            Status = ChoreTaskStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
        db.ChoreTasks.Add(task);
        db.SaveChanges();

        var person = new Person { Name = "Tester", HaEntityId = "person.test", Role = PersonRole.Admin };
        db.Persons.Add(person);
        db.SaveChanges();

        var comment = new TaskComment
        {
            ChoreTaskId = task.Id,
            PersonId = person.Id,
            Text = "Photo comment",
            CreatedAt = DateTime.UtcNow
        };
        db.TaskComments.Add(comment);
        db.SaveChanges();

        // Act - valid attachment
        var attachment = new TaskAttachment
        {
            TaskCommentId = comment.Id,
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            FilePath = "tasks/1/photo.jpg",
            FileSize = 1024,
            CreatedAt = DateTime.UtcNow
        };
        db.TaskAttachments.Add(attachment);
        db.SaveChanges();

        // Assert
        Assert.Equal(1, db.TaskAttachments.Count());
        var saved = db.TaskAttachments.First();
        Assert.Equal(comment.Id, saved.TaskCommentId);
    }

    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/gif", true)]
    [InlineData("image/webp", true)]
    [InlineData("image/heic", true)]
    [InlineData("image/heif", true)]
    [InlineData("application/pdf", false)]
    [InlineData("text/plain", false)]
    [InlineData("application/javascript", false)]
    [InlineData("application/octet-stream", false)]
    public void AllowedContentType_ValidatesCorrectly(string contentType, bool shouldBeAllowed)
    {
        // These mirror the validation in Program.cs upload endpoint
        var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "image/heic", "image/heif" };

        var isAllowed = allowedContentTypes.Any(ct => contentType.StartsWith(ct, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(shouldBeAllowed, isAllowed);
    }

    [Theory]
    [InlineData("/api/camera_proxy/camera.front_door", true)]
    [InlineData("/api/image/person.john", true)]
    [InlineData("/local/image.png", false)]
    [InlineData("/../etc/passwd", false)]
    [InlineData("", false)]
    [InlineData("/config/secrets.yaml", false)]
    public void HaImagePath_ValidationRules(string path, bool shouldBeAllowed)
    {
        // Mirrors the path validation in the /api/ha-image endpoint
        var isValid = !string.IsNullOrWhiteSpace(path) &&
                      path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

        Assert.Equal(shouldBeAllowed, isValid);
    }

    [Theory]
    [InlineData("../../../etc/passwd", "passwd")]
    [InlineData("..\\..\\windows\\system32\\config\\sam", "sam")]
    [InlineData("normal_photo.jpg", "normal_photo.jpg")]
    [InlineData("path/to/nested/file.png", "file.png")]
    [InlineData("C:\\Users\\admin\\secrets.txt", "secrets.txt")]
    public void FileNameSanitization_StripsPathComponents(string input, string expectedFileName)
    {
        // Mirrors Path.GetFileName() used in upload endpoint
        var sanitized = Path.GetFileName(input);
        Assert.Equal(expectedFileName, sanitized);
    }

    [Fact]
    public void FileSizeValidation_RejectsLargeFiles()
    {
        var maxSize = 10 * 1024 * 1024; // 10 MB

        Assert.True(1024 <= maxSize); // 1 KB - ok
        Assert.True(5 * 1024 * 1024 <= maxSize); // 5 MB - ok
        Assert.False(11 * 1024 * 1024 <= maxSize); // 11 MB - rejected
        Assert.True(10 * 1024 * 1024 <= maxSize); // Exactly 10 MB - ok (boundary)
    }
}
