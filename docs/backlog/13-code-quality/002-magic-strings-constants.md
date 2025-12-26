# 002: Extract Magic Strings to Constants

**Status**: 🔲 Not Started
**Priority**: P1 (High Priority)
**Agent**: A1
**Branch**: `feature/code-quality-magic-strings`
**Estimated Complexity**: Medium

## Objective

Replace hard-coded string literals with named constants or enums to improve code maintainability, reduce typos, and enable compile-time checking.

## Dependencies

None

## Problem Statement

The codebase contains magic strings in multiple areas:

1. **Duplicate Group Status** - "pending", "validated", "resolved" (used in 10+ locations)
2. **API Error Codes** - "NOT_FOUND", "BAD_REQUEST", "CONFLICT", "INTERNAL_ERROR"
3. **Auto-Select Strategy** - String-based enum would be better
4. **File Extensions** - Repeated in multiple services

Examples:
```csharp
// Current - prone to typos
group.Status = "validated";
if (request.Scope == "pending") { ... }

// Proposed
group.Status = DuplicateGroupStatus.Validated;
if (request.Scope == DuplicateGroupStatus.Pending) { ... }
```

## Acceptance Criteria

- [ ] Create `DuplicateGroupStatus` constants class
- [ ] Create `ApiErrorCode` constants class  
- [ ] Convert `AutoSelectStrategy` to proper enum (if not already)
- [ ] Extract file extensions to configuration/constants
- [ ] Replace all magic string usages
- [ ] All tests still pass
- [ ] No new magic strings introduced

## Implementation Plan

### 1. Create Constants Classes

**src/Shared/Constants/DuplicateGroupStatus.cs:**
```csharp
namespace Shared.Constants;

/// <summary>
/// Status values for duplicate groups.
/// </summary>
public static class DuplicateGroupStatus
{
    public const string Pending = "pending";
    public const string AutoSelected = "auto-selected";
    public const string Conflict = "conflict";
    public const string Validated = "validated";
    public const string Cleaned = "cleaned";
}
```

**src/Shared/Constants/ApiErrorCode.cs:**
```csharp
namespace Shared.Constants;

/// <summary>
/// Standard API error codes.
/// </summary>
public static class ApiErrorCode
{
    public const string NotFound = "NOT_FOUND";
    public const string BadRequest = "BAD_REQUEST";
    public const string Conflict = "CONFLICT";
    public const string InternalError = "INTERNAL_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
}
```

**src/Shared/Constants/SupportedFileExtensions.cs:**
```csharp
namespace Shared.Constants;

public static class SupportedFileExtensions
{
    public static readonly HashSet<string> Images = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif",
        ".heic", ".heif", ".webp", ".avif"
    };
}
```

### 2. Replace Usages

Search and replace all occurrences:

**Database Layer:**
- `src/Database/Entities/DuplicateGroup.cs` - default value
- `src/Database/PhotosDbContext.cs` - default value configuration
- Migrations (leave as-is, they're historical)

**API Layer:**
- `src/Api/Services/DuplicateService.cs` - all status checks and assignments
- `src/Api/Services/OriginalSelectionService.cs` - scope checks
- `src/Shared/Responses/ApiErrorResponse.cs` - all factory methods
- `src/Shared/Dtos/DuplicateGroupDto.cs` - default value

**Tests:**
- Update all test assertions to use constants

### 3. Validation

Run full test suite and verify:
- All tests pass
- No string literal typos can occur
- IntelliSense works for all constants

## Files to Create/Modify

```
src/Shared/
└── Constants/
    ├── DuplicateGroupStatus.cs (new)
    ├── ApiErrorCode.cs (new)
    └── SupportedFileExtensions.cs (new)

src/Database/
├── Entities/DuplicateGroup.cs (modify)
└── PhotosDbContext.cs (modify)

src/Api/Services/
├── DuplicateService.cs (modify)
├── OriginalSelectionService.cs (modify)
└── (other services as needed)

src/Shared/
├── Responses/ApiErrorResponse.cs (modify)
└── Dtos/DuplicateGroupDto.cs (modify)

tests/ (multiple files - modify)
```

## Benefits

- **Type Safety**: Typos caught at compile time
- **Refactoring**: Easy to rename/change values
- **IntelliSense**: IDE autocomplete for valid values
- **Documentation**: Single source of truth
- **Maintenance**: Easy to find all usages

## Related Tasks

- `13-code-quality/001-static-analysis-configuration.md` - Will catch future magic strings
- `02-api-layer/*` - May need updates

## Completion Checklist

- [ ] Create all constants classes
- [ ] Replace DuplicateGroupStatus usages (10+ files)
- [ ] Replace ApiErrorCode usages (5+ files)
- [ ] Replace file extension lists
- [ ] Update tests to use constants
- [ ] Verify all tests pass
- [ ] Search for remaining magic strings
- [ ] PR created and reviewed
