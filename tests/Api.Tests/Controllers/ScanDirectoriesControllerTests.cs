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

public class ScanDirectoriesControllerTests
{
    private readonly Mock<IScanDirectoryService> _mockService;
    private readonly Mock<ILogger<ScanDirectoriesController>> _mockLogger;
    private readonly ScanDirectoriesController _controller;

    public ScanDirectoriesControllerTests()
    {
        _mockService = new Mock<IScanDirectoryService>();
        _mockLogger = new Mock<ILogger<ScanDirectoriesController>>();
        _controller = new ScanDirectoriesController(_mockService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsPagedResponse()
    {
        // Arrange
        var expected = new PagedResponse<ScanDirectoryDto>
        {
            Items = [new ScanDirectoryDto { Id = Guid.NewGuid(), Path = "/photos" }],
            Page = 1,
            PageSize = 50,
            TotalItems = 1
        };
        _mockService.Setup(s => s.GetAllAsync(1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PagedResponse<ScanDirectoryDto>>().Subject;
        response.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAll_ClampsPaginationValues()
    {
        // Arrange
        _mockService.Setup(s => s.GetAllAsync(1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResponse<ScanDirectoryDto>.Empty());

        // Act
        await _controller.GetAll(page: -1, pageSize: 500);

        // Assert - should clamp to page=1 and pageSize=100 (max)
        _mockService.Verify(s => s.GetAllAsync(1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_ReturnsDirectory_WhenFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var expected = new ScanDirectoryDto { Id = id, Path = "/photos" };
        _mockService.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.GetById(id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ScanDirectoryDto>().Subject;
        response.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScanDirectoryDto?)null);

        // Act
        var result = await _controller.GetById(id);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Create_ReturnsCreated_WhenSuccessful()
    {
        // Arrange
        var request = new CreateScanDirectoryRequest { Path = "/photos/new" };
        var created = new ScanDirectoryDto { Id = Guid.NewGuid(), Path = "/photos/new" };

        _mockService.Setup(s => s.PathExistsAsync(request.Path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockService.Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        var response = createdResult.Value.Should().BeOfType<ScanDirectoryDto>().Subject;
        response.Path.Should().Be("/photos/new");
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenPathExists()
    {
        // Arrange
        var request = new CreateScanDirectoryRequest { Path = "/photos/existing" };

        _mockService.Setup(s => s.PathExistsAsync(request.Path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var conflictResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        var error = conflictResult.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        error.Code.Should().Be("CONFLICT");
    }

    [Fact]
    public async Task Update_ReturnsUpdatedDirectory()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateScanDirectoryRequest { IsEnabled = false };
        var updated = new ScanDirectoryDto { Id = id, Path = "/photos", IsEnabled = false };

        _mockService.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScanDirectoryDto { Id = id, Path = "/photos", IsEnabled = true });
        _mockService.Setup(s => s.UpdateAsync(id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        // Act
        var result = await _controller.Update(id, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ScanDirectoryDto>().Subject;
        response.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateScanDirectoryRequest { IsEnabled = false };

        _mockService.Setup(s => s.UpdateAsync(id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScanDirectoryDto?)null);

        // Act
        var result = await _controller.Update(id, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenNewPathExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateScanDirectoryRequest { Path = "/photos/existing" };

        _mockService.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScanDirectoryDto { Id = id, Path = "/photos/original" });
        _mockService.Setup(s => s.PathExistsAsync("/photos/existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Update(id, request);

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()))
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
        _mockService.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Delete(id);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task TriggerScan_ReturnsAccepted_WhenSuccessful()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.TriggerScanAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.TriggerScan(id);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task TriggerScan_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.TriggerScanAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.TriggerScan(id);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
