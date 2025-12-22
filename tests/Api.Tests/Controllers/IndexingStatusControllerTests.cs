using Api.Controllers;
using Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Dtos;
using Shared.Requests;
using Xunit;

namespace Api.Tests.Controllers;

public class IndexingStatusControllerTests
{
    private readonly Mock<IIndexingStatusService> _mockService;
    private readonly Mock<ILogger<IndexingStatusController>> _mockLogger;
    private readonly IndexingStatusController _controller;

    public IndexingStatusControllerTests()
    {
        _mockService = new Mock<IIndexingStatusService>();
        _mockLogger = new Mock<ILogger<IndexingStatusController>>();
        _controller = new IndexingStatusController(_mockService.Object, _mockLogger.Object);
    }

    [Fact]
    public void GetStatus_ReturnsCurrentStatus()
    {
        // Arrange
        var expected = new IndexingStatusDto
        {
            IsRunning = true,
            CurrentDirectoryId = Guid.NewGuid(),
            CurrentDirectoryPath = "/photos",
            FilesScanned = 100,
            FilesIngested = 80,
            FilesFailed = 5,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            LastUpdatedAt = DateTime.UtcNow
        };
        _mockService.Setup(s => s.GetStatus()).Returns(expected);

        // Act
        var result = _controller.GetStatus();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var status = okResult.Value.Should().BeOfType<IndexingStatusDto>().Subject;
        status.IsRunning.Should().BeTrue();
        status.FilesScanned.Should().Be(100);
        status.FilesIngested.Should().Be(80);
        status.FilesFailed.Should().Be(5);
    }

    [Fact]
    public void GetStatus_ReturnsNotRunning_WhenIdle()
    {
        // Arrange
        var expected = new IndexingStatusDto
        {
            IsRunning = false,
            CurrentDirectoryId = null,
            CurrentDirectoryPath = null,
            FilesScanned = 0,
            FilesIngested = 0,
            FilesFailed = 0,
            StartedAt = null,
            LastUpdatedAt = null
        };
        _mockService.Setup(s => s.GetStatus()).Returns(expected);

        // Act
        var result = _controller.GetStatus();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var status = okResult.Value.Should().BeOfType<IndexingStatusDto>().Subject;
        status.IsRunning.Should().BeFalse();
        status.CurrentDirectoryId.Should().BeNull();
    }

    [Fact]
    public void StartIndexing_ReturnsAccepted()
    {
        // Arrange
        var request = new StartIndexingRequest
        {
            DirectoryId = Guid.NewGuid(),
            DirectoryPath = "/photos/vacation"
        };

        // Act
        var result = _controller.StartIndexing(request);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
        _mockService.Verify(s => s.StartIndexing(request.DirectoryId, request.DirectoryPath), Times.Once);
    }

    [Fact]
    public void UpdateProgress_ReturnsNoContent()
    {
        // Arrange
        var request = new UpdateProgressRequest
        {
            FilesScanned = 50,
            FilesIngested = 45,
            FilesFailed = 2
        };

        // Act
        var result = _controller.UpdateProgress(request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockService.Verify(s => s.UpdateProgress(50, 45, 2), Times.Once);
    }

    [Fact]
    public void StopIndexing_ReturnsNoContent()
    {
        // Act
        var result = _controller.StopIndexing();

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockService.Verify(s => s.StopIndexing(), Times.Once);
    }
}
