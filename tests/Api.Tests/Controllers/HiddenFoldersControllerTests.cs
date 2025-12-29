using Api.Controllers;
using Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Dtos;
using Shared.Responses;
using Xunit;

namespace Api.Tests.Controllers;

public class HiddenFoldersControllerTests
{
    private readonly Mock<IHiddenFolderService> _mockService;
    private readonly Mock<ILogger<HiddenFoldersController>> _mockLogger;
    private readonly HiddenFoldersController _controller;

    public HiddenFoldersControllerTests()
    {
        _mockService = new Mock<IHiddenFolderService>();
        _mockLogger = new Mock<ILogger<HiddenFoldersController>>();
        _controller = new HiddenFoldersController(_mockService.Object, _mockLogger.Object);
    }

    #region GetAll Tests

    [Fact]
    public async Task GetAll_ReturnsOk_WithHiddenFolders()
    {
        // Arrange
        var folders = new List<HiddenFolderDto>
        {
            new() { Id = Guid.NewGuid(), FolderPath = "/photos/private", CreatedAt = DateTime.UtcNow, AffectedFileCount = 5 },
            new() { Id = Guid.NewGuid(), FolderPath = "/photos/temp", CreatedAt = DateTime.UtcNow, AffectedFileCount = 3 }
        };
        _mockService.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(folders);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<IReadOnlyList<HiddenFolderDto>>().Subject;
        response.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_ReturnsOk_WithEmptyList()
    {
        // Arrange
        _mockService.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HiddenFolderDto>());

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<IReadOnlyList<HiddenFolderDto>>().Subject;
        response.Should().BeEmpty();
    }

    #endregion

    #region GetFolderPaths Tests

    [Fact]
    public async Task GetFolderPaths_ReturnsOk_WithPaths()
    {
        // Arrange
        var paths = new List<FolderPathDto>
        {
            new() { Path = "/photos/2023", FileCount = 100 },
            new() { Path = "/photos/2024", FileCount = 50 }
        };
        _mockService.Setup(s => s.GetFolderPathsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paths);

        // Act
        var result = await _controller.GetFolderPaths(null);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<IReadOnlyList<FolderPathDto>>().Subject;
        response.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetFolderPaths_PassesSearchParameter()
    {
        // Arrange
        _mockService.Setup(s => s.GetFolderPathsAsync("vacation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FolderPathDto>());

        // Act
        await _controller.GetFolderPaths("vacation");

        // Assert
        _mockService.Verify(s => s.GetFolderPathsAsync("vacation", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_ReturnsCreated_WhenSuccessful()
    {
        // Arrange
        var request = new CreateHiddenFolderRequest
        {
            FolderPath = "/photos/private",
            Description = "Private photos"
        };
        var createdFolder = new HiddenFolderDto
        {
            Id = Guid.NewGuid(),
            FolderPath = "/photos/private",
            Description = "Private photos",
            CreatedAt = DateTime.UtcNow,
            AffectedFileCount = 10
        };
        _mockService.Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdFolder);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeOfType<HiddenFolderDto>().Subject;
        response.FolderPath.Should().Be("/photos/private");
        response.AffectedFileCount.Should().Be(10);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenFolderPathEmpty()
    {
        // Arrange
        var request = new CreateHiddenFolderRequest { FolderPath = "" };

        // Act
        var result = await _controller.Create(request);

        // Assert
        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequest.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        error.Code.Should().Be("BAD_REQUEST");
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenFolderPathWhitespace()
    {
        // Arrange
        var request = new CreateHiddenFolderRequest { FolderPath = "   " };

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenRuleAlreadyExists()
    {
        // Arrange
        var request = new CreateHiddenFolderRequest { FolderPath = "/photos/private" };
        _mockService.Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Rule already exists"));

        // Act
        var result = await _controller.Create(request);

        // Assert
        var conflictResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        var error = conflictResult.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        error.Code.Should().Be("CONFLICT");
    }

    [Fact]
    public async Task Create_ReturnsInternalError_OnException()
    {
        // Arrange
        var request = new CreateHiddenFolderRequest { FolderPath = "/photos/private" };
        _mockService.Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.Create(request);

        // Assert
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region Delete Tests

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
    public async Task Delete_ReturnsNotFound_WhenFolderNotExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mockService.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Delete(id);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var error = notFoundResult.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        error.Code.Should().Be("NOT_FOUND");
    }

    #endregion
}
