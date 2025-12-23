# 007: Add XML Documentation

**Status**: 🔲 Not Started
**Priority**: P3 (Low Priority)
**Agent**: A1
**Branch**: `feature/code-quality-xml-docs`
**Estimated Complexity**: Medium

## Objective

Add comprehensive XML documentation comments to all public APIs to improve code understanding and enable automatic API documentation generation.

## Dependencies

- `13-code-quality/001-static-analysis-configuration.md` (currently suppresses CS1591 warnings)

## Problem Statement

XML documentation generation is enabled but warnings are suppressed:
```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<NoWarn>$(NoWarn);CS1591</NoWarn>  <!-- Missing XML comments -->
```

Good documentation exists for some controllers and services, but not comprehensive.

## Acceptance Criteria

- [ ] Add XML comments to all public APIs
- [ ] Add XML comments to all public DTOs and requests
- [ ] Add XML comments to all public service interfaces
- [ ] Include `<summary>`, `<param>`, and `<returns>` tags
- [ ] Remove CS1591 suppression from Directory.Build.props
- [ ] Solution builds with 0 warnings
- [ ] Swagger UI shows improved descriptions

## Implementation Plan

### 1. Document Public APIs

**Controllers:**
```csharp
/// <summary>
/// Controller for managing indexed files.
/// </summary>
[ApiController]
[Route("api/files")]
public class IndexedFilesController : ControllerBase
{
    /// <summary>
    /// Query indexed files with filtering and pagination.
    /// </summary>
    /// <param name="query">Query parameters for filtering and pagination.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged list of indexed files.</returns>
    /// <response code="200">Returns the paged list of files.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<IndexedFileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<IndexedFileDto>>> Query(
        [FromQuery] FileQueryParameters query,
        CancellationToken ct = default)
    {
        // Implementation
    }
}
```

### 2. Document DTOs

```csharp
/// <summary>
/// Represents an indexed file with metadata.
/// </summary>
public record IndexedFileDto
{
    /// <summary>
    /// Gets the unique identifier for the file.
    /// </summary>
    public required Guid Id { get; init; }
    
    /// <summary>
    /// Gets the full path to the file on disk.
    /// </summary>
    public required string FilePath { get; init; }
    
    /// <summary>
    /// Gets the SHA256 hash of the file contents.
    /// </summary>
    public required string FileHash { get; init; }
    
    // ... more properties
}
```

### 3. Document Service Interfaces

```csharp
/// <summary>
/// Service for managing indexed files.
/// </summary>
public interface IIndexedFileService
{
    /// <summary>
    /// Queries indexed files with filtering and pagination.
    /// </summary>
    /// <param name="query">Query parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged response containing matching files.</returns>
    Task<PagedResponse<IndexedFileDto>> QueryAsync(
        FileQueryParameters query, 
        CancellationToken ct);
}
```

### 4. Document Request/Response Types

```csharp
/// <summary>
/// Request to create a new scan directory.
/// </summary>
public record CreateScanDirectoryRequest
{
    /// <summary>
    /// Gets the path to scan for files. Must be an absolute path.
    /// </summary>
    /// <example>/photos/vacation</example>
    public required string Path { get; init; }
    
    /// <summary>
    /// Gets a value indicating whether to scan subdirectories recursively.
    /// </summary>
    public bool IncludeSubdirectories { get; init; } = true;
}
```

### 5. Enable Warnings

Once all documentation is added, remove suppression:

**Directory.Build.props:**
```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <!-- Remove: <NoWarn>$(NoWarn);CS1591</NoWarn> -->
</PropertyGroup>
```

### 6. Configure Swagger

Ensure Swagger uses XML comments:

**Program.cs:**
```csharp
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});
```

## Files to Modify

```
src/Api/
├── Controllers/ (all controllers)
└── Services/ (all interfaces)

src/Shared/
├── Dtos/ (all DTOs)
├── Requests/ (all requests)
└── Responses/ (all responses)

src/IndexingService/
├── ApiClient/ (public interfaces)
└── Services/ (public interfaces)

Directory.Build.props (remove CS1591 suppression)
```

## Documentation Guidelines

Create in CONTRIBUTING.md:

### Required Elements

1. **<summary>**: Required for all public types and members
2. **<param>**: Required for all parameters
3. **<returns>**: Required for non-void methods
4. **<exception>**: Document thrown exceptions
5. **<remarks>**: Additional details when needed
6. **<example>**: Code examples for complex APIs

### Best Practices

- Start with action verb ("Gets", "Creates", "Validates")
- Be concise but complete
- Include units (bytes, milliseconds, etc.)
- Document null behavior
- Add examples for complex parameters

### Examples to Include

```csharp
/// <summary>
/// Gets the file size in bytes.
/// </summary>
public long FileSize { get; init; }

/// <summary>
/// Gets the date the photo was taken, extracted from EXIF metadata.
/// Returns null if EXIF data is not available.
/// </summary>
public DateTime? DateTaken { get; init; }
```

## Validation

1. Build solution - verify 0 warnings
2. Check Swagger UI - verify descriptions appear
3. Generate documentation site (optional)
4. Review with another developer

## Benefits

- **IntelliSense**: Better IDE tooltips
- **Swagger**: Improved API documentation
- **Maintainability**: Easier to understand code
- **Onboarding**: Helps new developers

## Tools

Optional documentation generation:
- [DocFX](https://dotnet.github.io/docfx/) - Generate documentation site
- [xmldoc2md](https://github.com/FransBouma/DocNet) - Generate Markdown docs

## Related Tasks

- `13-code-quality/001-static-analysis-configuration.md` - Enables XML doc generation

## References

- [XML Documentation Comments](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/)
- [Recommended XML Tags](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags)

## Completion Checklist

- [ ] Document all controller actions
- [ ] Document all DTOs and properties
- [ ] Document all request/response types
- [ ] Document all service interfaces
- [ ] Document all public methods
- [ ] Add examples for complex APIs
- [ ] Remove CS1591 suppression
- [ ] Verify solution builds with 0 warnings
- [ ] Verify Swagger shows descriptions
- [ ] Update CONTRIBUTING.md with guidelines
- [ ] PR created and reviewed
