# 001: Compact Table View for Duplicates

**Status**: 🔲 Not Started
**Priority**: P1
**Issue**: [#68](https://github.com/gbolabs/photos-index/issues/68)
**Branch**: `feature/duplicate-table-view`
**Estimated Complexity**: Medium
**Target Release**: v0.2.0

## Objective

Create a compact table view optimized for reviewing 30k+ duplicate groups, replacing the current card-based grid that doesn't scale.

## Dependencies

- None (standalone UI improvement)

## Acceptance Criteria

- [ ] New table view component for duplicates page
- [ ] Toggle between Grid/Table view modes
- [ ] Columns: Checkbox, Original (Keep), Size, Date, Duplicates
- [ ] Color coding: Green = auto-selected, Purple = validated, Orange = conflict (needs manual selection)
- [ ] Pagination with configurable page sizes (50/100/500)
- [ ] Responsive design for various screen widths
- [ ] Sorting by size, date, file count
- [ ] Row click expands to show full paths

## Technical Design

### Component Structure
```
duplicates/
├── components/
│   ├── duplicate-table-view/
│   │   ├── duplicate-table-view.component.ts
│   │   ├── duplicate-table-view.component.html
│   │   └── duplicate-table-view.component.scss
│   └── view-mode-toggle/
│       └── view-mode-toggle.component.ts
```

### Table Layout
```
┌────┬──────────────────┬────────────┬────────────┬──────────────────────┐
│ □  │ Original (Keep)  │ Size       │ Date       │ Duplicates           │
├────┼──────────────────┼────────────┼────────────┼──────────────────────┤
│ ☑  │ 🟢 IMG_001.jpg   │ 4.2 MB     │ 2024-01-15 │ 📁 /backup/IMG_001.. │
│    │ /photos/2024/    │            │            │ 📁 /cloud-sync/IM... │
└────┴──────────────────┴────────────┴────────────┴──────────────────────┘
```

### API Changes

None required - uses existing `GET /api/duplicate-groups` with pagination.

## Files to Create/Modify

- `src/Web/src/app/features/duplicates/components/duplicate-table-view/` (new)
- `src/Web/src/app/features/duplicates/components/view-mode-toggle/` (new)
- `src/Web/src/app/features/duplicates/duplicates.ts` (add view mode signal)
- `src/Web/src/app/features/duplicates/duplicates.html` (conditional rendering)

## Test Coverage

- Unit tests for table view component
- Unit tests for view mode toggle
- Test pagination controls
- Test sorting functionality
