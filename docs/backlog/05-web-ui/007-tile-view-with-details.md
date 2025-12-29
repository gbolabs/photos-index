# Tile View with Expandable Details

**Status**: Not Started
**Priority**: Medium
**Complexity**: Medium

## Description

Replace the current table-based file browser with a responsive tile/grid view. Clicking a tile expands an inline detail panel instead of navigating to a separate page.

## User Story

As a user browsing my photos, I want to see thumbnails in a grid layout and quickly view details without leaving the page, so I can efficiently browse large collections.

## Requirements

### Tile Grid View
- Responsive grid layout (auto-fit columns based on screen width)
- Thumbnail-first design with minimal text overlay (filename, date)
- Hover effect showing quick actions (view, delete, mark duplicate)
- Lazy loading for performance with large collections

### Expandable Detail Panel
- Clicking a tile opens an inline detail panel (slide-down or side panel)
- Shows full EXIF metadata, file info, and larger preview
- Close button or click-outside to collapse
- Keyboard navigation (arrow keys to move between tiles, Escape to close)

### Mobile Considerations
- Touch-friendly tile sizes (minimum 100px)
- Swipe gestures for navigation
- Detail panel as bottom sheet on mobile

## Technical Notes

- Use Angular CDK for virtual scrolling (performance with 72k+ files)
- Consider Masonry layout for mixed aspect ratios
- Preserve current table view as an option (toggle button)

## Mockup

```
┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐
│ IMG │ │ IMG │ │ IMG │ │ IMG │
│  1  │ │  2  │ │  3  │ │  4  │
└─────┘ └─────┘ └─────┘ └─────┘
┌─────────────────────────────────┐
│ ▼ IMG 2 Details                 │
│ ┌─────────┐ Filename: IMG_2.jpg │
│ │ Preview │ Size: 4.2 MB        │
│ │         │ Date: 2024-01-15    │
│ └─────────┘ Camera: Sony α7     │
│             Dimensions: 4000x3000│
└─────────────────────────────────┘
┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐
│ IMG │ │ IMG │ │ IMG │ │ IMG │
│  5  │ │  6  │ │  7  │ │  8  │
└─────┘ └─────┘ └─────┘ └─────┘
```

## Acceptance Criteria

- [ ] Grid view displays thumbnails responsively
- [ ] Clicking tile expands inline detail panel
- [ ] Virtual scrolling handles 10k+ files smoothly
- [ ] Toggle between grid and table view
- [ ] Works on mobile (touch, gestures)
- [ ] Keyboard accessible
