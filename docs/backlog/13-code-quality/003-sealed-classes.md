# 003: Seal Service Implementations

**Status**: 🔲 Not Started
**Priority**: P2 (Medium Priority)
**Agent**: A1
**Branch**: `feature/code-quality-sealed-classes`
**Estimated Complexity**: Low

## Objective

Add `sealed` keyword to service implementations and DTOs that are not designed for inheritance to improve performance and clarify design intent.

## Dependencies

None

## Problem Statement

Currently, 6+ service implementations and multiple DTOs are not sealed:
- `IndexedFileService`
- `DuplicateService`
- `ScanDirectoryService`
- `OriginalSelectionService`
- `BuildInfoService`
- `IndexingStatusService`

Benefits of sealing:
- **Performance**: Compiler can devirtualize calls and inline methods
- **Design Intent**: Clearly communicates "not for inheritance"
- **Security**: Prevents unauthorized extension
- **Simpler Code**: Removes inheritance considerations

## Acceptance Criteria

- [ ] Seal all service implementations in `src/Api/Services/`
- [ ] Seal all service implementations in `src/IndexingService/Services/`
- [ ] Seal DTOs and response types where appropriate
- [ ] All tests still pass (no mocking issues)
- [ ] Document any classes that should NOT be sealed and why

## Implementation Plan

### 1. Identify Sealable Classes

**Service Implementations:**
```csharp
// src/Api/Services/
public sealed class IndexedFileService : IIndexedFileService { ... }
public sealed class DuplicateService : IDuplicateService { ... }
public sealed class ScanDirectoryService : IScanDirectoryService { ... }
public sealed class OriginalSelectionService : IOriginalSelectionService { ... }
public sealed class BuildInfoService : IBuildInfoService { ... }
public sealed class IndexingStatusService : IIndexingStatusService { ... }
```

**IndexingService:**
```csharp
// src/IndexingService/Services/
public sealed class FileScanner : IFileScanner { ... }
public sealed class HashComputer : IHashComputer { ... }
public sealed class MetadataExtractor : IMetadataExtractor { ... }
public sealed class IndexingOrchestrator : IIndexingOrchestrator { ... }
```

**API Client:**
```csharp
// src/IndexingService/ApiClient/
public sealed class PhotosApiClient : IPhotosApiClient { ... }
```

**DTOs and Records:**
```csharp
// src/Shared/Dtos/
public sealed record IndexedFileDto { ... }
public sealed record DuplicateGroupDto { ... }
public sealed record ScanDirectoryDto { ... }
// ... etc
```

### 2. Classes to NOT Seal

**Controllers**: Keep controllers unsealed for testing frameworks
```csharp
// Controllers often extended by test frameworks
public class IndexedFilesController : ControllerBase { ... }
```

**Base Classes**: Any actual base classes
```csharp
// If any exist, document why they're unsealed
```

**Test Fixtures**: Test classes don't need sealing
```csharp
// All test classes can remain unsealed
```

### 3. Update Tests

Verify tests still work:
- Moq should work fine (mocks interfaces, not implementations)
- No inheritance-based test patterns broken

## Files to Modify

```
src/Api/Services/
├── IndexedFileService.cs
├── DuplicateService.cs
├── ScanDirectoryService.cs
├── OriginalSelectionService.cs
├── BuildInfoService.cs
└── IndexingStatusService.cs

src/IndexingService/
├── Services/
│   ├── FileScanner.cs
│   ├── HashComputer.cs
│   ├── MetadataExtractor.cs
│   └── IndexingOrchestrator.cs
└── ApiClient/
    └── PhotosApiClient.cs

src/Shared/Dtos/
├── IndexedFileDto.cs
├── DuplicateGroupDto.cs
├── ScanDirectoryDto.cs
├── FileStatisticsDto.cs
└── (all other DTOs)

src/Shared/Responses/
├── ApiErrorResponse.cs
├── PagedResponse.cs
└── (all other responses)
```

## Validation

1. Run `dotnet build` - should succeed
2. Run all tests - should pass
3. Performance benchmarks (optional) - measure improvement

## Performance Impact

Sealing classes enables:
- **Call Devirtualization**: Non-virtual method calls (faster)
- **Inlining**: Compiler can inline sealed class methods
- **Memory**: Slightly better memory layout
- **Impact**: Small but measurable for hot paths

Estimated: 1-5% performance improvement on service layer calls.

## Benefits

- **Performance**: Faster method calls
- **Clarity**: Design intent explicit
- **Safety**: Prevents unintended inheritance
- **Best Practice**: Recommended by .NET team

## Related Tasks

- `13-code-quality/001-static-analysis-configuration.md` - Could add analyzer for unsealed classes

## References

- [CA1852: Seal internal types](https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1852)
- [Performance benefits of sealed](https://devblogs.microsoft.com/premier-developer/the-cost-of-c-virtual-methods/)

## Completion Checklist

- [ ] Identify all sealable service classes
- [ ] Add sealed keyword to all services
- [ ] Add sealed keyword to DTOs/records
- [ ] Document any intentionally unsealed classes
- [ ] Run all tests and verify passing
- [ ] Check for any mocking issues
- [ ] PR created and reviewed
