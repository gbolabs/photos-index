# 005: Improve Exception Handling

**Status**: 🔲 Not Started
**Priority**: P2 (Medium Priority)
**Agent**: A1
**Branch**: `feature/code-quality-exception-handling`
**Estimated Complexity**: Medium

## Objective

Replace generic `catch (Exception ex)` blocks with more specific exception types and introduce custom exception classes for domain-specific errors.

## Dependencies

None

## Problem Statement

Code analysis found 19 generic exception catches across the codebase. While this works, it can hide specific errors and makes debugging harder.

Current pattern:
```csharp
try
{
    // File operation
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to read file: {Path}", filePath);
    return null;
}
```

Better approach:
```csharp
try
{
    // File operation
}
catch (FileNotFoundException ex)
{
    _logger.LogWarning(ex, "File not found: {Path}", filePath);
    return null;
}
catch (UnauthorizedAccessException ex)
{
    _logger.LogError(ex, "Access denied to file: {Path}", filePath);
    return null;
}
catch (IOException ex)
{
    _logger.LogWarning(ex, "IO error reading file: {Path}", filePath);
    return null;
}
```

## Acceptance Criteria

- [ ] Review all 19 `catch (Exception ex)` blocks
- [ ] Replace with specific exception types where appropriate
- [ ] Create custom exception classes for domain errors
- [ ] Improve exception messages with more context
- [ ] All tests still pass
- [ ] Exception handling is consistent across services

## Implementation Plan

### 1. Create Custom Exceptions

**src/Shared/Exceptions/PhotosIndexException.cs:**
```csharp
namespace Shared.Exceptions;

/// <summary>
/// Base exception for Photos Index application.
/// </summary>
public abstract class PhotosIndexException : Exception
{
    protected PhotosIndexException(string message) : base(message) { }
    protected PhotosIndexException(string message, Exception innerException) 
        : base(message, innerException) { }
}
```

**src/Shared/Exceptions/FileHashException.cs:**
```csharp
public class FileHashException : PhotosIndexException
{
    public string FilePath { get; }
    
    public FileHashException(string filePath, string message, Exception innerException)
        : base(message, innerException)
    {
        FilePath = filePath;
    }
}
```

**src/Shared/Exceptions/ThumbnailGenerationException.cs:**
```csharp
public class ThumbnailGenerationException : PhotosIndexException
{
    public string FilePath { get; }
    
    public ThumbnailGenerationException(string filePath, string message, Exception innerException)
        : base(message, innerException)
    {
        FilePath = filePath;
    }
}
```

### 2. Identify Exception Locations

Found in:
- `src/Api/Services/IndexedFileService.cs` (file operations)
- `src/IndexingService/Services/HashComputer.cs` (file access)
- `src/IndexingService/Services/MetadataExtractor.cs` (image loading)
- `src/IndexingService/Services/FileScanner.cs` (file enumeration)

### 3. Refactor Exception Handlers

**Example - IndexedFileService.cs:**

Before:
```csharp
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to read thumbnail: {Path}", entity.ThumbnailPath);
    return null;
}
```

After:
```csharp
catch (FileNotFoundException ex)
{
    _logger.LogWarning(ex, "Thumbnail file not found: {Path}", entity.ThumbnailPath);
    return null;
}
catch (UnauthorizedAccessException ex)
{
    _logger.LogError(ex, "Access denied to thumbnail: {Path}", entity.ThumbnailPath);
    return null;
}
catch (IOException ex)
{
    _logger.LogWarning(ex, "IO error reading thumbnail: {Path}", entity.ThumbnailPath);
    return null;
}
```

**Example - HashComputer.cs:**

Before:
```csharp
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to compute hash for {Path}", filePath);
    return null;
}
```

After:
```csharp
catch (FileNotFoundException ex)
{
    _logger.LogWarning("File not found for hashing: {Path}", filePath);
    return null;
}
catch (UnauthorizedAccessException ex)
{
    _logger.LogError(ex, "Access denied when hashing: {Path}", filePath);
    throw new FileHashException(filePath, "Access denied to file", ex);
}
catch (IOException ex)
{
    _logger.LogWarning(ex, "IO error computing hash: {Path}", filePath);
    return null;
}
```

### 4. Consider Result Pattern (Future)

For expected errors, consider using Result<T> pattern:
```csharp
public record Result<T>
{
    public T? Value { get; init; }
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    
    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(string error) => new() { IsSuccess = false, Error = error };
}
```

## Files to Create/Modify

```
src/Shared/
└── Exceptions/
    ├── PhotosIndexException.cs (new)
    ├── FileHashException.cs (new)
    ├── ThumbnailGenerationException.cs (new)
    └── MetadataExtractionException.cs (new)

src/Api/Services/
└── IndexedFileService.cs (modify)

src/IndexingService/Services/
├── HashComputer.cs (modify)
├── MetadataExtractor.cs (modify)
└── FileScanner.cs (modify)

tests/ (update to test new exception types)
```

## Exception Handling Guidelines

Document in CONTRIBUTING.md:

1. **Catch Specific Exceptions**: Always catch the most specific exception type
2. **Use Custom Exceptions**: For domain errors, use custom exception classes
3. **Include Context**: Add relevant properties to custom exceptions
4. **Log Appropriately**: 
   - Warning for expected errors (file not found)
   - Error for unexpected issues (access denied)
5. **Don't Swallow**: If you can't handle it, let it bubble up
6. **Result Pattern**: Consider for expected failures in business logic

## Benefits

- **Better Debugging**: Specific exception types reveal root cause faster
- **Proper Handling**: Different exceptions can be handled differently
- **Context**: Custom exceptions carry domain-specific information
- **Logging**: More precise log levels and messages

## Related Tasks

- `13-code-quality/001-static-analysis-configuration.md` - Could add analyzer rules

## References

- [Exception Handling Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions)

## Completion Checklist

- [ ] Create custom exception classes
- [ ] Review all 19 generic catch blocks
- [ ] Replace with specific exceptions (file operations)
- [ ] Replace with specific exceptions (image processing)
- [ ] Replace with specific exceptions (API client)
- [ ] Improve exception messages
- [ ] Update tests to verify exception types
- [ ] Document exception handling guidelines
- [ ] All tests passing
- [ ] PR created and reviewed
