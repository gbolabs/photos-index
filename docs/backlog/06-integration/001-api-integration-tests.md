# 001: API Integration Tests

**Priority**: P3 (Quality Assurance)
**Agent**: A5
**Branch**: `feature/integration-api-tests`
**Estimated Complexity**: High

## Objective

Implement comprehensive integration tests for the API layer using TestContainers for PostgreSQL.

## Dependencies

- `02-api-layer/001-scan-directories.md`
- `02-api-layer/002-indexed-files.md`
- `02-api-layer/003-duplicate-groups.md`

## Acceptance Criteria

- [ ] WebApplicationFactory configured with TestContainers PostgreSQL
- [ ] Full HTTP round-trip tests for all endpoints
- [ ] Database state verification after operations
- [ ] Concurrent request handling tests
- [ ] Error response format verification
- [ ] Authentication/authorization tests (if applicable)
- [ ] Rate limiting tests (if applicable)
- [ ] API versioning tests (if applicable)

## TDD Steps

This module IS the tests, so follow standard integration test patterns.

## Files to Create

```
tests/Integration.Tests/
├── Integration.Tests.csproj
├── Fixtures/
│   ├── PostgresContainerFixture.cs
│   ├── WebAppFactory.cs
│   └── TestDataSeeder.cs
├── Api/
│   ├── ScanDirectoriesApiTests.cs
│   ├── IndexedFilesApiTests.cs
│   ├── DuplicateGroupsApiTests.cs
│   └── HealthCheckTests.cs
├── Helpers/
│   ├── HttpClientExtensions.cs
│   └── AssertionHelpers.cs
└── appsettings.Testing.json
```

## Test Infrastructure

```csharp
// Fixtures/PostgresContainerFixture.cs
public class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("photos_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

// Fixtures/WebAppFactory.cs
public class WebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres = new();

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PhotosDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add test database
            services.AddDbContext<PhotosDbContext>(options =>
            {
                options.UseNpgsql(_postgres.ConnectionString);
            });

            // Ensure schema is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PhotosDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
```

## Test Examples

```csharp
// Api/ScanDirectoriesApiTests.cs
public class ScanDirectoriesApiTests : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client;
    private readonly WebAppFactory _factory;

    public ScanDirectoriesApiTests(WebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoDirectories()
    {
        // Act
        var response = await _client.GetAsync("/api/scan-directories");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<ScanDirectoryDto>>();
        result!.Items.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task Create_ReturnsCreatedDirectory()
    {
        // Arrange
        var request = new CreateScanDirectoryRequest
        {
            Path = "/photos/test",
            IsEnabled = true,
            IncludeSubdirectories = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/scan-directories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ScanDirectoryDto>();
        result!.Path.Should().Be("/photos/test");
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_ForDuplicatePath()
    {
        // Arrange
        var request = new CreateScanDirectoryRequest { Path = "/photos/dup", IsEnabled = true };
        await _client.PostAsJsonAsync("/api/scan-directories", request);

        // Act
        var response = await _client.PostAsJsonAsync("/api/scan-directories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_RemovesDirectory()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/scan-directories",
            new CreateScanDirectoryRequest { Path = "/photos/delete-me", IsEnabled = true });
        var created = await createResponse.Content.ReadFromJsonAsync<ScanDirectoryDto>();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/scan-directories/{created!.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/scan-directories/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// Api/IndexedFilesApiTests.cs
public class IndexedFilesApiTests : IClassFixture<WebAppFactory>
{
    [Fact]
    public async Task BatchIngest_CreatesMultipleFiles()
    {
        // Test batch upsert
    }

    [Fact]
    public async Task Query_WithFilters_ReturnsFilteredResults()
    {
        // Seed data, then query with filters
    }

    [Fact]
    public async Task GetThumbnail_ReturnsImage()
    {
        // Test thumbnail endpoint returns correct content type
    }

    [Fact]
    public async Task GetStatistics_ReturnsCorrectCounts()
    {
        // Seed data, verify statistics
    }
}
```

## Test Data Seeder

```csharp
// Fixtures/TestDataSeeder.cs
public static class TestDataSeeder
{
    public static async Task<ScanDirectory> SeedDirectoryAsync(
        PhotosDbContext db,
        string path = "/photos/test")
    {
        var directory = new ScanDirectory
        {
            Id = Guid.NewGuid(),
            Path = path,
            IsEnabled = true,
            IncludeSubdirectories = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ScanDirectories.Add(directory);
        await db.SaveChangesAsync();
        return directory;
    }

    public static async Task<IndexedFile> SeedFileAsync(
        PhotosDbContext db,
        Guid directoryId,
        string fileName = "test.jpg",
        string? hash = null)
    {
        var file = new IndexedFile
        {
            Id = Guid.NewGuid(),
            ScanDirectoryId = directoryId,
            FilePath = $"/photos/{fileName}",
            FileName = fileName,
            Sha256Hash = hash ?? Guid.NewGuid().ToString("N"),
            FileSizeBytes = 1024,
            FileModifiedUtc = DateTime.UtcNow,
            IndexedAtUtc = DateTime.UtcNow
        };
        db.IndexedFiles.Add(file);
        await db.SaveChangesAsync();
        return file;
    }

    public static async Task SeedDuplicateGroupAsync(
        PhotosDbContext db,
        Guid directoryId,
        int fileCount = 3)
    {
        var hash = Guid.NewGuid().ToString("N");
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Sha256Hash = hash
        };
        db.DuplicateGroups.Add(group);

        for (int i = 0; i < fileCount; i++)
        {
            var file = new IndexedFile
            {
                Id = Guid.NewGuid(),
                ScanDirectoryId = directoryId,
                FilePath = $"/photos/dup{i}.jpg",
                FileName = $"dup{i}.jpg",
                Sha256Hash = hash,
                DuplicateGroupId = group.Id,
                IsOriginal = i == 0
            };
            db.IndexedFiles.Add(file);
        }

        await db.SaveChangesAsync();
    }
}
```

## Test Coverage

- All API endpoints: 90% minimum
- Error scenarios: 100%
- Edge cases: 90% minimum

## Completion Checklist

- [ ] Create Integration.Tests project
- [ ] Configure TestContainers with PostgreSQL
- [ ] Create WebAppFactory with test database
- [ ] Create TestDataSeeder helper
- [ ] Write ScanDirectoriesApiTests (CRUD, validation)
- [ ] Write IndexedFilesApiTests (query, batch, thumbnail)
- [ ] Write DuplicateGroupsApiTests (list, select, delete)
- [ ] Write HealthCheckTests
- [ ] Add concurrent request tests
- [ ] Add error response format tests
- [ ] Ensure test isolation (clean database between tests)
- [ ] All tests passing
- [ ] PR created and reviewed
