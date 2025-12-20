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

public class IndexedFilesControllerTests
{
    private readonly Mock<IIndexedFileService> _mockService;
    private readonly Mock<ILogger<IndexedFilesController>> _mockLogger;
    private readonly IndexedFilesController _controller;

    public IndexedFilesControllerTests()
    {
        _mockService = new Mock<IIndexedFileService>();
        _mockLogger = new Mock<ILogger<IndexedFilesController>>();
        _controller = new IndexedFilesController(_mockService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Query_ReturnsPagedResponse()
    {
        // Arrange
        var query = new FileQueryParameters { Page = 1, PageSize = 20 };
        var expected = new PagedResponse<IndexedFileDto>
        {
            Items = [new IndexedFileDto { Id = Guid.NewGuid(), FilePath = "/photos/test.jpg", FileName = "test.jpg", FileHash = "abc123" }],
            Page = 1,
            PageSize = 20,
            TotalItems = 1
        };
        _mockService.Setup(s => s.QueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.Query(query);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PagedResponse<IndexedFileDto>>().Subject;
        response.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_ReturnsFile_WhenFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var expected = new IndexedFileDto { Id = id, FilePath = "/photos/test.jpg", FileName = "test.jpg", FileHash = "abc123" };
        _mockService.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.GetById(id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<IndexedFileDto>().Subject;
        response.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IndexedFileDto?)null);

        // Act
        var result = await _controller.GetById(id);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetThumbnail_ReturnsFile_WhenFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var thumbnailData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG magic bytes
        _mockService.Setup(s => s.GetThumbnailAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thumbnailData);

        // Act
        var result = await _controller.GetThumbnail(id);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("image/jpeg");
        fileResult.FileContents.Should().BeEquivalentTo(thumbnailData);
    }

    [Fact]
    public async Task GetThumbnail_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.GetThumbnailAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _controller.GetThumbnail(id);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task BatchIngest_ReturnsBatchResponse_WhenSuccessful()
    {
        // Arrange
        var request = new BatchIngestFilesRequest
        {
            ScanDirectoryId = Guid.NewGuid(),
            Files = [new IngestFileItem { FilePath = "/photos/test.jpg", FileName = "test.jpg", FileHash = "abc123", FileSize = 1000 }]
        };
        var expected = new BatchOperationResponse
        {
            TotalRequested = 1,
            Succeeded = 1,
            Failed = 0
        };
        _mockService.Setup(s => s.BatchIngestAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.BatchIngest(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<BatchOperationResponse>().Subject;
        response.Succeeded.Should().Be(1);
    }

    [Fact]
    public async Task BatchIngest_ReturnsBadRequest_WhenNoFiles()
    {
        // Arrange
        var request = new BatchIngestFilesRequest { Files = [] };

        // Act
        var result = await _controller.BatchIngest(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        error.Code.Should().Be("BAD_REQUEST");
    }

    [Fact]
    public async Task GetStatistics_ReturnsStats()
    {
        // Arrange
        var expected = new FileStatisticsDto
        {
            TotalFiles = 100,
            TotalSizeBytes = 10000000,
            DuplicateGroups = 5,
            DuplicateFiles = 15,
            PotentialSavingsBytes = 5000000
        };
        _mockService.Setup(s => s.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.GetStatistics();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<FileStatisticsDto>().Subject;
        response.TotalFiles.Should().Be(100);
        response.DuplicateGroups.Should().Be(5);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.SoftDeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(id);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.SoftDeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Delete(id);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
