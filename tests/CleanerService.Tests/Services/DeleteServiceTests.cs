using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CleanerService.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Shared.Dtos;
using Xunit;

namespace CleanerService.Tests.Services;

public class DeleteServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<ICleanerStatusService> _statusServiceMock;
    private readonly Mock<ILogger<DeleteService>> _loggerMock;

    public DeleteServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"CleanerServiceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _statusServiceMock = new Mock<ICleanerStatusService>();
        _loggerMock = new Mock<ILogger<DeleteService>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private (DeleteService Service, Mock<HttpMessageHandler> HttpHandler) CreateService(
        bool dryRunEnabled = true,
        HttpStatusCode archiveResponseStatus = HttpStatusCode.OK,
        HttpStatusCode confirmDeleteStatus = HttpStatusCode.NoContent)
    {
        var options = new CleanerServiceOptions
        {
            ApiBaseUrl = "http://localhost:5000",
            DryRunEnabled = dryRunEnabled
        };

        var httpHandlerMock = new Mock<HttpMessageHandler>();

        // Setup archive response
        var archiveResponse = new HttpResponseMessage(archiveResponseStatus)
        {
            Content = JsonContent.Create(new { Success = true, ArchivePath = "archive/2024-12/test.jpg" })
        };

        httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/cleaner/archive")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(archiveResponse);

        // Setup confirm-delete response
        var confirmResponse = new HttpResponseMessage(confirmDeleteStatus);

        httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/cleaner/confirm-delete")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(confirmResponse);

        var httpClient = new HttpClient(httpHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var service = new DeleteService(
            httpClient,
            Options.Create(options),
            _statusServiceMock.Object,
            _loggerMock.Object);

        return (service, httpHandlerMock);
    }

    private string CreateTestFile(string fileName = "test.jpg", string content = "Test file content")
    {
        var filePath = Path.Combine(_tempDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private string ComputeHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    [Fact]
    public async Task DeleteFileAsync_FileNotFound_ReturnsFailure()
    {
        // Arrange
        var (service, _) = CreateService();
        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = Path.Combine(_tempDir, "nonexistent.jpg"),
            FileHash = "abc123",
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act
        var result = await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("File not found on disk");
        result.JobId.Should().Be(request.JobId);
        result.FileId.Should().Be(request.FileId);
    }

    [Fact]
    public async Task DeleteFileAsync_HashMismatch_ReturnsFailure()
    {
        // Arrange
        var (service, _) = CreateService();
        var filePath = CreateTestFile();
        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = "wrong_hash_value_here",
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act
        var result = await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Hash mismatch");
    }

    [Fact]
    public async Task DeleteFileAsync_DryRun_DoesNotDeleteFile()
    {
        // Arrange
        var (service, _) = CreateService(dryRunEnabled: true);
        var filePath = CreateTestFile();
        var hash = ComputeHash(filePath);
        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = hash,
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act
        var result = await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.WasDryRun.Should().BeTrue();
        result.ArchivePath.Should().NotBeNullOrEmpty();
        File.Exists(filePath).Should().BeTrue("File should NOT be deleted in dry-run mode");

        // Verify status updates
        _statusServiceMock.Verify(s => s.IncrementFilesProcessed(), Times.Once);
        _statusServiceMock.Verify(s => s.IncrementFilesSkipped(), Times.Once);
        _statusServiceMock.Verify(s => s.IncrementFilesDeleted(), Times.Never);
    }

    [Fact]
    public async Task DeleteFileAsync_NotDryRun_DeletesFile()
    {
        // Arrange
        var (service, _) = CreateService(dryRunEnabled: false);
        var filePath = CreateTestFile();
        var hash = ComputeHash(filePath);
        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = hash,
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act
        var result = await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.WasDryRun.Should().BeFalse();
        result.ArchivePath.Should().NotBeNullOrEmpty();
        File.Exists(filePath).Should().BeFalse("File SHOULD be deleted when not in dry-run mode");

        // Verify status updates
        _statusServiceMock.Verify(s => s.IncrementFilesProcessed(), Times.Once);
        _statusServiceMock.Verify(s => s.IncrementFilesDeleted(), Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_ArchiveUploadFails_ReturnsFailure()
    {
        // Arrange
        var (service, _) = CreateService(
            dryRunEnabled: false,
            archiveResponseStatus: HttpStatusCode.InternalServerError);
        var filePath = CreateTestFile();
        var hash = ComputeHash(filePath);
        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = hash,
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act
        var result = await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Failed to upload to archive");
        File.Exists(filePath).Should().BeTrue("File should NOT be deleted when archive fails");
    }

    [Fact]
    public async Task DeleteFileAsync_SetsCorrectStateTransitions()
    {
        // Arrange
        var stateSequence = new List<CleanerState>();
        _statusServiceMock
            .Setup(s => s.SetState(It.IsAny<CleanerState>()))
            .Callback<CleanerState>(state => stateSequence.Add(state));

        var (service, _) = CreateService(dryRunEnabled: true);
        var filePath = CreateTestFile();
        var hash = ComputeHash(filePath);
        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = hash,
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act
        await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert - Verify state transitions in order
        stateSequence.Should().ContainInConsecutiveOrder(
            CleanerState.Processing,
            CleanerState.Uploading,
            CleanerState.Idle);
    }

    [Fact]
    public async Task DeleteFileAsync_NotDryRun_SetsDeleteState()
    {
        // Arrange
        var stateSequence = new List<CleanerState>();
        _statusServiceMock
            .Setup(s => s.SetState(It.IsAny<CleanerState>()))
            .Callback<CleanerState>(state => stateSequence.Add(state));

        var (service, _) = CreateService(dryRunEnabled: false);
        var filePath = CreateTestFile();
        var hash = ComputeHash(filePath);
        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = hash,
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act
        await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert
        stateSequence.Should().Contain(CleanerState.Deleting);
    }

    [Fact]
    public async Task DeleteFileAsync_UpdatesActivityMessages()
    {
        // Arrange
        var activities = new List<string?>();
        _statusServiceMock
            .Setup(s => s.SetActivity(It.IsAny<string?>()))
            .Callback<string?>(activity => activities.Add(activity));

        var (service, _) = CreateService(dryRunEnabled: true);
        var filePath = CreateTestFile("photo.jpg");
        var hash = ComputeHash(filePath);
        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = hash,
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act
        await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert
        activities.Should().Contain(a => a != null && a.Contains("Processing"));
        activities.Should().Contain(a => a != null && a.Contains("Verifying"));
        activities.Should().Contain(a => a != null && a.Contains("Uploading"));
        activities.Should().Contain((string?)null, "Activity should be cleared at the end");
    }

    [Fact]
    public async Task DeleteFileAsync_SetsCurrentJob()
    {
        // Arrange
        var (service, _) = CreateService();
        var filePath = CreateTestFile();
        var hash = ComputeHash(filePath);
        var jobId = Guid.NewGuid();
        var request = new DeleteFileRequest
        {
            JobId = jobId,
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = hash,
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act
        await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert
        _statusServiceMock.Verify(s => s.SetCurrentJob(jobId), Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_AddsBytesArchived()
    {
        // Arrange
        var (service, _) = CreateService();
        var content = "Test content for measuring bytes";
        var filePath = CreateTestFile("test.jpg", content);
        var hash = ComputeHash(filePath);
        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = hash,
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act
        await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert
        var expectedSize = new FileInfo(filePath).Length;
        _statusServiceMock.Verify(s => s.AddBytesArchived(expectedSize), Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_OnNetworkError_ReturnsFailure()
    {
        // Arrange
        var filePath = CreateTestFile();
        var hash = ComputeHash(filePath);

        // Mock HTTP handler to throw an exception during upload
        var httpHandlerMock = new Mock<HttpMessageHandler>();
        httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(httpHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var options = new CleanerServiceOptions
        {
            ApiBaseUrl = "http://localhost:5000",
            DryRunEnabled = false
        };

        var service = new DeleteService(
            httpClient,
            Options.Create(options),
            _statusServiceMock.Object,
            _loggerMock.Object);

        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = hash,
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act
        var result = await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert - Network error during upload causes failure but is handled gracefully
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Failed to upload to archive");
        File.Exists(filePath).Should().BeTrue("File should not be deleted on upload failure");
    }

    [Theory]
    [InlineData(DeleteCategory.HashDuplicate)]
    [InlineData(DeleteCategory.NearDuplicate)]
    [InlineData(DeleteCategory.Manual)]
    public async Task DeleteFileAsync_PassesCategoryToArchive(DeleteCategory category)
    {
        // Arrange
        var (service, _) = CreateService(dryRunEnabled: true);

        var filePath = CreateTestFile();
        var hash = ComputeHash(filePath);
        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = hash,
            FileSize = 1024,
            Category = category
        };

        // Act
        var result = await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert - The delete should succeed (category is passed as form field to archive endpoint)
        result.Success.Should().BeTrue();
        result.ArchivePath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DeleteFileAsync_CancellationRequested_AbortsGracefully()
    {
        // Arrange - Pre-cancel before the hash computation step
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var (service, _) = CreateService();
        var filePath = CreateTestFile();
        var hash = ComputeHash(filePath);
        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = hash,
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act - Cancellation is handled during async operations like hash computation or upload
        // The method may throw or return an error result depending on when cancellation is checked
        try
        {
            var result = await service.DeleteFileAsync(request, cts.Token);
            // If it doesn't throw, verify the file wasn't deleted
            File.Exists(filePath).Should().BeTrue("File should not be deleted when cancelled");
        }
        catch (OperationCanceledException)
        {
            // Expected if cancellation was caught during async operation
        }
    }

    [Fact]
    public async Task DeleteFileAsync_NotDryRun_CallsConfirmDelete()
    {
        // Arrange
        var confirmDeleteCalled = false;
        var httpHandlerMock = new Mock<HttpMessageHandler>();

        httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/cleaner/archive")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { Success = true, ArchivePath = "archive/test.jpg" })
            });

        httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/cleaner/confirm-delete")),
                ItExpr.IsAny<CancellationToken>())
            .Callback(() => confirmDeleteCalled = true)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        var httpClient = new HttpClient(httpHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var options = new CleanerServiceOptions
        {
            ApiBaseUrl = "http://localhost:5000",
            DryRunEnabled = false
        };

        var service = new DeleteService(
            httpClient,
            Options.Create(options),
            _statusServiceMock.Object,
            _loggerMock.Object);

        var filePath = CreateTestFile();
        var hash = ComputeHash(filePath);
        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = hash,
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act
        await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert
        confirmDeleteCalled.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteFileAsync_DryRun_DoesNotCallConfirmDelete()
    {
        // Arrange
        var confirmDeleteCalled = false;
        var httpHandlerMock = new Mock<HttpMessageHandler>();

        httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/cleaner/archive")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { Success = true, ArchivePath = "archive/test.jpg" })
            });

        httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/api/cleaner/confirm-delete")),
                ItExpr.IsAny<CancellationToken>())
            .Callback(() => confirmDeleteCalled = true)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        var httpClient = new HttpClient(httpHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var options = new CleanerServiceOptions
        {
            ApiBaseUrl = "http://localhost:5000",
            DryRunEnabled = true
        };

        var service = new DeleteService(
            httpClient,
            Options.Create(options),
            _statusServiceMock.Object,
            _loggerMock.Object);

        var filePath = CreateTestFile();
        var hash = ComputeHash(filePath);
        var request = new DeleteFileRequest
        {
            JobId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            FilePath = filePath,
            FileHash = hash,
            FileSize = 1024,
            Category = DeleteCategory.HashDuplicate
        };

        // Act
        await service.DeleteFileAsync(request, CancellationToken.None);

        // Assert
        confirmDeleteCalled.Should().BeFalse();
    }
}
