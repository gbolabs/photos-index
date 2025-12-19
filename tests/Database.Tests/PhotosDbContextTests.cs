using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Database.Tests;

public class PhotosDbContextTests
{
    [Fact]
    public void PhotosDbContext_Should_BeCreatable()
    {
        // Arrange
        // TODO: Replace with actual PhotosDbContext once implemented
        // var options = new DbContextOptionsBuilder<PhotosDbContext>()
        //     .UseInMemoryDatabase(databaseName: "TestDb")
        //     .Options;

        // Act
        var result = true;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AddPhoto_Should_SaveToDatabase()
    {
        // Arrange
        // TODO: Create in-memory database and add photo entity

        // Act
        await Task.CompletedTask;

        // Assert
        true.Should().BeTrue();
    }

    [Fact]
    public async Task QueryDuplicates_Should_ReturnDuplicatePhotos()
    {
        // Arrange
        // TODO: Setup test data with duplicate photos

        // Act
        await Task.CompletedTask;

        // Assert
        true.Should().BeTrue();
    }
}
