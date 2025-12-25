# 003: Distributed Processing Service Tests

**Priority**: P1 (Critical - prevent production regressions)
**Agent**: A5
**Branch**: `feature/integration-distributed-tests`
**Estimated Complexity**: Medium

## Context

v0.3.4 and v0.3.5 required hotfixes for issues that could have been caught with proper testing:
- v0.3.4: IndexingOrchestrator wasn't uploading file content in distributed mode
- v0.3.5: MetadataService DateTaken parsing returned DateTime with Kind=Unspecified, causing PostgreSQL errors

## Objective

Add unit and integration tests for MetadataService and ThumbnailService to prevent regressions in the distributed processing pipeline.

## Dependencies

- MetadataService and ThumbnailService deployed and working (v0.3.5+)

## Acceptance Criteria

- [ ] MetadataService unit tests for EXIF date parsing with UTC kind validation
- [ ] MetadataService unit tests for metadata extraction
- [ ] ThumbnailService unit tests for thumbnail generation
- [ ] Integration tests for distributed pipeline: Indexer -> API -> MinIO -> RabbitMQ -> Services -> API update
- [ ] DateTime UTC kind explicitly tested for PostgreSQL compatibility

## Files to Create

```
tests/
├── MetadataService.Tests/
│   ├── MetadataService.Tests.csproj
│   ├── FileDiscoveredConsumerTests.cs
│   └── DateTimeParsingTests.cs
├── ThumbnailService.Tests/
│   ├── ThumbnailService.Tests.csproj
│   └── FileDiscoveredConsumerTests.cs
└── Integration.Tests/
    └── DistributedProcessingTests.cs
```

## Key Test Cases

### DateTimeParsingTests.cs
```csharp
[Theory]
[InlineData("2024:01:15 14:30:00")]  // EXIF format
[InlineData("2024-01-15T14:30:00")]  // ISO format
public void TryParseExifDate_ShouldReturnUtcDateTime(string input)
{
    var result = TryParseExifDate(input, out var dateTime);
    Assert.True(result);
    Assert.Equal(DateTimeKind.Utc, dateTime.Kind);
}

[Fact]
public void ExtractedDateTaken_ShouldBeCompatibleWithPostgresTimestampTz()
{
    // Verify DateTime can be saved to PostgreSQL timestamp with time zone
}
```

### DistributedProcessingTests.cs
```csharp
[Fact]
public async Task DistributedMode_ShouldUploadFileContent()
{
    // Verify IndexingOrchestrator uploads file content when in distributed mode
}

[Fact]
public async Task FileDiscoveredMessage_ShouldTriggerMetadataExtraction()
{
    // End-to-end test with TestContainers for RabbitMQ + MinIO
}

[Fact]
public async Task MetadataExtractedMessage_ShouldUpdateDatabase()
{
    // Verify API consumer updates database with extracted metadata
}
```

## Test Coverage

- MetadataService: 80% minimum
- ThumbnailService: 80% minimum
- Distributed integration: 75% minimum

## Completion Checklist

- [ ] Create MetadataService.Tests project
- [ ] Add DateTime UTC parsing tests
- [ ] Add metadata extraction tests
- [ ] Create ThumbnailService.Tests project
- [ ] Add thumbnail generation tests
- [ ] Create distributed integration tests with TestContainers
- [ ] All tests passing
- [ ] PR created and reviewed
