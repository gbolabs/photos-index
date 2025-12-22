using Api.Controllers;
using Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;
using Xunit;

namespace Api.Tests.Controllers;

public class DuplicateGroupsControllerTests
{
    private readonly Mock<IDuplicateService> _mockService;
    private readonly Mock<ILogger<DuplicateGroupsController>> _mockLogger;
    private readonly DuplicateGroupsController _controller;

    public DuplicateGroupsControllerTests()
    {
        _mockService = new Mock<IDuplicateService>();
        _mockLogger = new Mock<ILogger<DuplicateGroupsController>>();
        _controller = new DuplicateGroupsController(_mockService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsPagedResponse()
    {
        // Arrange
        var expected = new PagedResponse<DuplicateGroupDto>
        {
            Items = [new DuplicateGroupDto { Id = Guid.NewGuid(), Hash = "abc123" }],
            Page = 1,
            PageSize = 20,
            TotalItems = 1
        };
        _mockService.Setup(s => s.GetGroupsAsync(1, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PagedResponse<DuplicateGroupDto>>().Subject;
        response.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAll_ClampsPaginationValues()
    {
        // Arrange
        _mockService.Setup(s => s.GetGroupsAsync(1, 100, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResponse<DuplicateGroupDto>.Empty());

        // Act
        await _controller.GetAll(page: -1, pageSize: 500);

        // Assert - should clamp to page=1 and pageSize=100 (max)
        _mockService.Verify(s => s.GetGroupsAsync(1, 100, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_ReturnsGroup_WhenFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var expected = new DuplicateGroupDto
        {
            Id = id,
            Hash = "abc123",
            FileCount = 3,
            TotalSize = 30000,
            Files = [
                new IndexedFileDto { Id = Guid.NewGuid(), FilePath = "/photos/file1.jpg", FileName = "file1.jpg", FileHash = "abc123" },
                new IndexedFileDto { Id = Guid.NewGuid(), FilePath = "/photos/file2.jpg", FileName = "file2.jpg", FileHash = "abc123" },
                new IndexedFileDto { Id = Guid.NewGuid(), FilePath = "/photos/file3.jpg", FileName = "file3.jpg", FileHash = "abc123" }
            ]
        };
        _mockService.Setup(s => s.GetGroupAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.GetById(id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DuplicateGroupDto>().Subject;
        response.Id.Should().Be(id);
        response.Files.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.GetGroupAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DuplicateGroupDto?)null);

        // Act
        var result = await _controller.GetById(id);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetOriginal_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var request = new SetOriginalRequest { FileId = fileId };
        _mockService.Setup(s => s.SetOriginalAsync(groupId, fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.SetOriginal(groupId, request);

        // Assert
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task SetOriginal_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var request = new SetOriginalRequest { FileId = fileId };
        _mockService.Setup(s => s.SetOriginalAsync(groupId, fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.SetOriginal(groupId, request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AutoSelect_ReturnsSelectedFileId_WhenSuccessful()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var selectedFileId = Guid.NewGuid();
        var request = new AutoSelectRequest { Strategy = AutoSelectStrategy.EarliestDate };
        _mockService.Setup(s => s.AutoSelectOriginalAsync(groupId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(selectedFileId);

        // Act
        var result = await _controller.AutoSelect(groupId, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        value.Should().NotBeNull();
    }

    [Fact]
    public async Task AutoSelect_ReturnsNotFound_WhenGroupNotExists()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var request = new AutoSelectRequest();
        _mockService.Setup(s => s.AutoSelectOriginalAsync(groupId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        // Act
        var result = await _controller.AutoSelect(groupId, request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AutoSelect_UsesDefaultRequest_WhenNull()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var selectedFileId = Guid.NewGuid();
        _mockService.Setup(s => s.AutoSelectOriginalAsync(groupId, It.IsAny<AutoSelectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(selectedFileId);

        // Act
        var result = await _controller.AutoSelect(groupId, null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockService.Verify(s => s.AutoSelectOriginalAsync(groupId, It.IsNotNull<AutoSelectRequest>(), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task AutoSelectAll_ReturnsProcessedCount()
    {
        // Arrange
        var request = new AutoSelectRequest { Strategy = AutoSelectStrategy.ShortestPath };
        _mockService.Setup(s => s.AutoSelectAllAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        // Act
        var result = await _controller.AutoSelectAll(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStatistics_ReturnsStats()
    {
        // Arrange
        var expected = new FileStatisticsDto
        {
            TotalFiles = 100,
            DuplicateGroups = 10,
            DuplicateFiles = 30,
            PotentialSavingsBytes = 50000000
        };
        _mockService.Setup(s => s.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.GetStatistics();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<FileStatisticsDto>().Subject;
        response.DuplicateGroups.Should().Be(10);
    }

    [Fact]
    public async Task DeleteNonOriginals_ReturnsQueuedCount_WhenSuccessful()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        _mockService.Setup(s => s.QueueNonOriginalsForDeletionAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var result = await _controller.DeleteNonOriginals(groupId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteNonOriginals_ReturnsNotFound_WhenGroupNotExists()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        _mockService.Setup(s => s.QueueNonOriginalsForDeletionAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _controller.DeleteNonOriginals(groupId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
