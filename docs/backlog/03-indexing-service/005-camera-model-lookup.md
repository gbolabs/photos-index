# Camera Model Commercial Name Lookup

**Status**: Not Started
**Priority**: Low
**Complexity**: Medium

## Description

Transform technical camera/phone model identifiers from EXIF data into user-friendly commercial names. For example, convert "H8324" to "Sony Xperia XZ3".

## User Story

As a user viewing my photo metadata, I want to see friendly camera names like "Sony Xperia XZ3" instead of cryptic model codes like "H8324", so I can easily identify which device took each photo.

## Problem

EXIF data often contains internal model codes:
- `H8324` → Sony Xperia XZ3
- `SM-G998B` → Samsung Galaxy S21 Ultra
- `iPhone14,5` → iPhone 13 Pro
- `Canon EOS R5` → (already friendly)

## Requirements

### Model Lookup Service
- Database/dictionary mapping model codes to commercial names
- Fallback to original value if not found
- Support for camera manufacturers and phone brands

### Data Sources (options)
1. **Static JSON file** - Simple, versioned with code
2. **External API** - e.g., gsmarena, devicespecifications.com
3. **Community database** - exiftool's camera database

### Integration Points
- MetadataService: Transform during extraction
- API: Add `cameraModelFriendly` field to response
- Web UI: Display friendly name with tooltip for original

## Technical Notes

```csharp
public class CameraModelLookup
{
    private readonly Dictionary<string, string> _models = new()
    {
        ["H8324"] = "Sony Xperia XZ3",
        ["H8216"] = "Sony Xperia XZ2",
        ["SM-G998B"] = "Samsung Galaxy S21 Ultra",
        ["iPhone14,5"] = "iPhone 13 Pro",
        // ... more mappings
    };

    public string GetFriendlyName(string? make, string? model)
    {
        if (string.IsNullOrEmpty(model)) return "Unknown";

        // Try direct lookup
        if (_models.TryGetValue(model, out var friendly))
            return friendly;

        // Return original if already friendly or unknown
        return $"{make} {model}".Trim();
    }
}
```

## Data Model Changes

```csharp
public class IndexedFile
{
    // Existing
    public string? CameraMake { get; set; }
    public string? CameraModel { get; set; }

    // New
    public string? CameraModelFriendly { get; set; }
}
```

## Acceptance Criteria

- [ ] Common phone models mapped (Sony Xperia, Samsung Galaxy, iPhone)
- [ ] Common camera models mapped (Canon, Nikon, Sony Alpha)
- [ ] Fallback to original value if not in lookup
- [ ] Web UI displays friendly name
- [ ] Lookup is extensible (easy to add new mappings)
- [ ] Performance: Lookup is O(1) hash lookup

## Future Enhancements

- Auto-update lookup table from external source
- User-contributed mappings
- Device icons based on manufacturer
