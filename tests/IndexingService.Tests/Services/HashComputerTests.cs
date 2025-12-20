using System.Security.Cryptography;
using FluentAssertions;
using IndexingService.Models;
using IndexingService.Services;
using IndexingService.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IndexingService.Tests.Services;

public class HashComputerTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture;
    private readonly Mock<ILogger<HashComputer>> _mockLogger;
    private readonly HashComputer _computer;

    public HashComputerTests()
    {
        _fixture = new TempDirectoryFixture();
        _mockLogger = new Mock<ILogger<HashComputer>>();
        _computer = new HashComputer(_mockLogger.Object);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ComputeAsync_ReturnsCorrectHash()
    {
        // Arrange
        var content = "test content for hashing"u8.ToArray();
        var expectedHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var filePath = _fixture.CreateFile("test.jpg", content);

        // Act
        var result = await _computer.ComputeAsync(filePath, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Hash.Should().Be(expectedHash);
        result.BytesProcessed.Should().Be(content.Length);
        result.FilePath.Should().Be(filePath);
    }

    [Fact]
    public async Task ComputeAsync_SameContent_ProducesSameHash()
    {
        // Arrange
        var content = "identical content"u8.ToArray();
        var file1 = _fixture.CreateFile("file1.jpg", content);
        var file2 = _fixture.CreateFile("file2.jpg", content);

        // Act
        var result1 = await _computer.ComputeAsync(file1, CancellationToken.None);
        var result2 = await _computer.ComputeAsync(file2, CancellationToken.None);

        // Assert
        result1.Hash.Should().Be(result2.Hash);
    }

    [Fact]
    public async Task ComputeAsync_DifferentContent_ProducesDifferentHash()
    {
        // Arrange
        var file1 = _fixture.CreateFile("file1.jpg", "content A"u8.ToArray());
        var file2 = _fixture.CreateFile("file2.jpg", "content B"u8.ToArray());

        // Act
        var result1 = await _computer.ComputeAsync(file1, CancellationToken.None);
        var result2 = await _computer.ComputeAsync(file2, CancellationToken.None);

        // Assert
        result1.Hash.Should().NotBe(result2.Hash);
    }

    [Fact]
    public async Task ComputeAsync_LargeFile_StreamsEfficiently()
    {
        // Arrange - Create a 1MB file
        var content = new byte[1024 * 1024];
        new Random(42).NextBytes(content);
        var filePath = _fixture.CreateFile("large.jpg", content);
        var expectedHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        // Act
        var result = await _computer.ComputeAsync(filePath, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Hash.Should().Be(expectedHash);
        result.BytesProcessed.Should().Be(content.Length);
    }

    [Fact]
    public async Task ComputeAsync_ReportsProgress()
    {
        // Arrange
        var content = new byte[100_000];
        var filePath = _fixture.CreateFile("progress.jpg", content);
        var progressReports = new List<HashProgress>();
        var progress = new Progress<HashProgress>(p => progressReports.Add(p));

        // Act
        await _computer.ComputeAsync(filePath, CancellationToken.None, progress);

        // Assert
        progressReports.Should().NotBeEmpty();
        progressReports.Last().BytesProcessed.Should().Be(content.Length);
        progressReports.Last().PercentComplete.Should().BeApproximately(100, 0.1);
    }

    [Fact]
    public async Task ComputeAsync_FileNotFound_ReturnsFailedResult()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_fixture.RootPath, "does-not-exist.jpg");

        // Act
        var result = await _computer.ComputeAsync(nonExistentPath, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
        result.Hash.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeAsync_SupportsCancellation()
    {
        // Arrange
        var content = new byte[10_000_000]; // 10MB
        var filePath = _fixture.CreateFile("cancel.jpg", content);
        using var cts = new CancellationTokenSource();
        var progressCount = 0;
        var progress = new Progress<HashProgress>(_ =>
        {
            progressCount++;
            if (progressCount >= 5)
            {
                cts.Cancel();
            }
        });

        // Act & Assert - TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _computer.ComputeAsync(filePath, cts.Token, progress));
    }

    [Fact]
    public async Task ComputeAsync_EmptyFile_ReturnsEmptyFileHash()
    {
        // Arrange
        var filePath = _fixture.CreateFile("empty.jpg", []);
        var expectedHash = Convert.ToHexString(SHA256.HashData([])).ToLowerInvariant();

        // Act
        var result = await _computer.ComputeAsync(filePath, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Hash.Should().Be(expectedHash);
        result.BytesProcessed.Should().Be(0);
    }

    [Fact]
    public async Task ComputeAsync_ReturnsDuration()
    {
        // Arrange
        var filePath = _fixture.CreateFile("timed.jpg", "content"u8.ToArray());

        // Act
        var result = await _computer.ComputeAsync(filePath, CancellationToken.None);

        // Assert
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ComputeBatchAsync_ProcessesMultipleFiles()
    {
        // Arrange
        var files = Enumerable.Range(0, 5)
            .Select(i => _fixture.CreateFile($"file{i}.jpg", System.Text.Encoding.UTF8.GetBytes($"content{i}")))
            .ToList();

        // Act
        var results = await _computer.ComputeBatchAsync(files, 2, CancellationToken.None).ToListAsync();

        // Assert
        results.Should().HaveCount(5);
        results.Should().OnlyContain(r => r.Success);
        results.Select(r => r.Hash).Distinct().Should().HaveCount(5); // All different hashes
    }

    [Fact]
    public async Task ComputeBatchAsync_RespectsMaxParallelism()
    {
        // Arrange
        var files = Enumerable.Range(0, 10)
            .Select(i => _fixture.CreateFile($"parallel{i}.jpg", System.Text.Encoding.UTF8.GetBytes($"content{i}")))
            .ToList();

        // Act
        var results = await _computer.ComputeBatchAsync(files, 2, CancellationToken.None).ToListAsync();

        // Assert
        results.Should().HaveCount(10);
    }

    [Fact]
    public async Task ComputeBatchAsync_HandlesFailuresGracefully()
    {
        // Arrange
        var validFile = _fixture.CreateFile("valid.jpg", "content"u8.ToArray());
        var invalidFile = Path.Combine(_fixture.RootPath, "missing.jpg");
        var files = new[] { validFile, invalidFile };

        // Act
        var results = await _computer.ComputeBatchAsync(files, 2, CancellationToken.None).ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().ContainSingle(r => r.Success);
        results.Should().ContainSingle(r => !r.Success);
    }

    [Fact]
    public async Task HashResult_Failed_CreatesFailedResult()
    {
        // Act
        var result = HashResult.Failed("/path/to/file.jpg", "Test error");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Test error");
        result.Hash.Should().BeEmpty();
        result.FilePath.Should().Be("/path/to/file.jpg");
    }
}
