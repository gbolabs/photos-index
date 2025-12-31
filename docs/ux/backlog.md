# UX Improvement Backlog

**Status**: Proposed
**Date**: 2025-12-31
**Based on**: [UX Analysis Report](./analysis.md)

## Overview

This backlog contains 25 prioritized UX improvements organized into 4 phases. Each task includes estimated complexity, affected components, and acceptance criteria.

---

## Phase 1: Critical Accessibility & Core UX (Priority: P0)

### UX-001: Add ARIA Labels to Icon Buttons
**Complexity**: Low (1-2 hours)
**Components**: All components with icon-only buttons

**Description**: All icon-only buttons must have `aria-label` attributes for screen reader accessibility.

**Affected Files**:
- `src/app/features/duplicates/components/duplicate-group-detail/duplicate-group-detail.component.html`
- `src/app/features/duplicates/components/bulk-actions-toolbar/bulk-actions-toolbar.component.html`
- `src/app/features/settings/settings.component.html`
- `src/app/app.component.html`

**Acceptance Criteria**:
- [ ] All `mat-icon-button` elements have `aria-label` attribute
- [ ] Labels are descriptive (e.g., "Delete non-original files" not just "Delete")
- [ ] Lighthouse accessibility audit passes

---

### UX-002: Status Icons for Color Independence
**Complexity**: Low (2-3 hours)
**Components**: Status chips

**Description**: Add icons to all status badges so users with color vision deficiencies can distinguish statuses.

**Affected Files**:
- `src/app/features/duplicates/components/duplicate-group-list/duplicate-group-list.component.ts`
- `src/app/features/duplicates/components/duplicate-group-list/duplicate-group-list.component.html`
- `src/app/features/duplicates/components/duplicate-table-view/duplicate-table-view.component.ts`

**Implementation**:
```typescript
getStatusIcon(status: string): string {
  const icons: Record<string, string> = {
    'Pending': 'pending_actions',
    'AutoSelected': 'auto_awesome',
    'Validated': 'check_circle',
    'Cleaning': 'cleaning_services',
    'CleaningFailed': 'error',
    'Cleaned': 'done_all'
  };
  return icons[status] || 'help';
}
```

**Acceptance Criteria**:
- [ ] All 6 status states have unique icons
- [ ] Icons are visible alongside status text
- [ ] Status is distinguishable without color

---

### UX-003: Touch-Friendly Target Sizes
**Complexity**: Low (2-3 hours)
**Components**: Checkboxes, small buttons, table actions

**Description**: Ensure all interactive elements meet 44x44px minimum touch target.

**Affected Files**:
- `src/app/features/duplicates/components/duplicate-group-list/duplicate-group-list.component.scss`
- `src/app/features/duplicates/components/duplicate-table-view/duplicate-table-view.component.scss`
- `src/styles.scss` (global styles)

**Acceptance Criteria**:
- [ ] All checkboxes have 44x44px clickable area
- [ ] All icon buttons have 44x44px clickable area
- [ ] Table row actions are easily tappable on mobile

---

### UX-004: Error Message Improvement
**Complexity**: Medium (4-6 hours)
**Components**: API error handling

**Description**: Replace generic error messages with specific, actionable messages.

**Affected Files**:
- `src/app/services/api-error-handler.ts` (create if not exists)
- `src/app/services/duplicate.service.ts`
- `src/app/services/indexed-file.service.ts`
- `src/app/services/scan-directory.service.ts`

**Implementation**:
```typescript
export function getErrorMessage(error: HttpErrorResponse): string {
  if (error.status === 0) return 'Unable to connect. Please check your network.';
  if (error.status === 401) return 'Your session has expired. Please refresh the page.';
  if (error.status === 403) return 'You do not have permission to perform this action.';
  if (error.status === 404) return 'The requested resource was not found.';
  if (error.status === 409) return 'This action conflicts with the current state.';
  if (error.status >= 500) return 'Server error. Please try again later.';
  return error.error?.message || error.error?.title || 'An unexpected error occurred.';
}
```

**Acceptance Criteria**:
- [ ] Network errors show connection-specific message
- [ ] HTTP status codes map to user-friendly messages
- [ ] API error details are parsed and displayed when available
- [ ] Retry button shown for retriable errors

---

### UX-005: Skeleton Loading States
**Complexity**: Medium (6-8 hours)
**Components**: List views, detail views

**Description**: Replace full-page spinners with skeleton loading components to prevent content reflow.

**Affected Files**:
- `src/app/shared/components/skeleton-card/` (create)
- `src/app/shared/components/skeleton-table/` (create)
- `src/app/features/duplicates/components/duplicate-group-list/duplicate-group-list.component.html`
- `src/app/features/duplicates/components/duplicate-group-detail/duplicate-group-detail.component.html`

**Acceptance Criteria**:
- [ ] Skeleton card component for grid loading
- [ ] Skeleton table component for table loading
- [ ] No content reflow when data loads
- [ ] Smooth transition from skeleton to content

---

## Phase 2: Navigation & Consistency (Priority: P1)

### UX-006: Breadcrumb Navigation
**Complexity**: Medium (4-6 hours)
**Components**: Detail views

**Description**: Add breadcrumb navigation to provide context and easy back navigation.

**Affected Files**:
- `src/app/shared/components/breadcrumb/` (create)
- `src/app/features/duplicates/components/duplicate-group-detail/duplicate-group-detail.component.html`
- `src/app/features/files/file-detail/file-detail.component.html`

**Acceptance Criteria**:
- [ ] Breadcrumb component created with accessible markup
- [ ] Shows path: Home > Duplicates > Group #X
- [ ] Each breadcrumb item is clickable
- [ ] Screen readers announce breadcrumb navigation

---

### UX-007: Back Navigation State Preservation
**Complexity**: Medium (6-8 hours)
**Components**: List views

**Description**: Preserve scroll position and selections when returning to list views.

**Affected Files**:
- `src/app/features/duplicates/duplicates.component.ts`
- `src/app/features/duplicates/services/duplicate-list-state.service.ts` (create)
- `src/app/app.routes.ts`

**Implementation**:
- Store scroll position in service before navigation
- Restore position after navigation back
- Preserve filter/sort state
- Use `scrollPositionRestoration: 'enabled'` in router config

**Acceptance Criteria**:
- [ ] Scroll position restored when returning to list
- [ ] Selected items preserved during navigation
- [ ] Filter and sort state preserved
- [ ] Works with browser back button

---

### UX-008: Design System - Spacing Scale
**Complexity**: Low (2-3 hours)
**Components**: All SCSS files

**Description**: Implement consistent spacing scale across the application.

**Affected Files**:
- `src/styles.scss` or `src/styles/_variables.scss` (create)
- All component SCSS files (audit and update)

**Implementation**:
```scss
:root {
  --spacing-xs: 4px;
  --spacing-sm: 8px;
  --spacing-md: 16px;
  --spacing-lg: 24px;
  --spacing-xl: 32px;
  --spacing-2xl: 48px;
}
```

**Acceptance Criteria**:
- [ ] CSS custom properties defined for spacing
- [ ] All components use spacing variables
- [ ] No hardcoded pixel values for spacing

---

### UX-009: Button Hierarchy Guidelines
**Complexity**: Low (3-4 hours)
**Components**: All components with buttons

**Description**: Establish and implement consistent button hierarchy.

**Guidelines**:
- Primary action: `mat-raised-button color="primary"`
- Secondary action: `mat-stroked-button`
- Tertiary action: `mat-button`
- Destructive action: `mat-raised-button color="warn"`

**Affected Files**:
- All component HTML files with buttons
- Create `docs/ux/button-guidelines.md`

**Acceptance Criteria**:
- [ ] Button guidelines documented
- [ ] All primary actions use raised primary buttons
- [ ] All destructive actions use warn color
- [ ] Consistent hierarchy across all views

---

### UX-010: Connection Status Indicator
**Complexity**: Medium (4-6 hours)
**Components**: App header, SignalR services

**Description**: Show real-time connection status for SignalR services.

**Affected Files**:
- `src/app/services/connection-status.service.ts` (create)
- `src/app/app.component.html`
- `src/app/app.component.ts`
- `src/app/services/signalr.service.ts`

**Implementation**:
```html
<button mat-icon-button [matTooltip]="connectionTooltip()">
  <mat-icon [class.connected]="connected()" [class.disconnected]="!connected()">
    {{ connected() ? 'cloud_done' : 'cloud_off' }}
  </mat-icon>
</button>
```

**Acceptance Criteria**:
- [ ] Connection status visible in header
- [ ] Status updates in real-time
- [ ] Tooltip shows detailed status
- [ ] Different icon/color for connected vs disconnected

---

## Phase 3: Enhanced User Experience (Priority: P2)

### UX-011: Undo in Snackbar
**Complexity**: Medium (4-6 hours)
**Components**: Deletion workflows

**Description**: Show undo action in snackbar after destructive operations.

**Affected Files**:
- `src/app/features/duplicates/components/duplicate-group-detail/duplicate-group-detail.component.ts`
- `src/app/services/duplicate.service.ts`

**Implementation**:
```typescript
this.snackBar.open('2 files queued for deletion', 'Undo', { duration: 10000 })
  .onAction().subscribe(() => {
    this.duplicateService.undoDelete(groupId).subscribe();
  });
```

**Acceptance Criteria**:
- [ ] Undo button shown in snackbar after delete
- [ ] 10-second window to undo
- [ ] Undo restores previous state
- [ ] Success message shown after undo

---

### UX-012: Empty State Improvements
**Complexity**: Low (3-4 hours)
**Components**: All list views

**Description**: Add call-to-action buttons in empty states.

**Affected Files**:
- `src/app/features/duplicates/components/duplicate-group-list/duplicate-group-list.component.html`
- `src/app/features/settings/settings.component.html`
- `src/app/features/gallery/gallery.component.html`

**Acceptance Criteria**:
- [ ] Empty states have descriptive message
- [ ] Primary action button to resolve empty state
- [ ] Appropriate icon for context

---

### UX-013: Keyboard Navigation for Gallery
**Complexity**: Medium (6-8 hours)
**Components**: Gallery grid

**Description**: Implement arrow key navigation in gallery view.

**Affected Files**:
- `src/app/features/gallery/components/gallery-grid/gallery-grid.component.ts`
- `src/app/features/gallery/components/gallery-grid/gallery-grid.component.html`

**Implementation**:
- Arrow keys move focus between tiles
- Enter/Space opens selected image
- Escape closes preview
- Focus visible on selected tile

**Acceptance Criteria**:
- [ ] Arrow keys navigate between tiles
- [ ] Focus ring visible on current tile
- [ ] Enter opens image preview
- [ ] Escape returns to gallery

---

### UX-014: Mobile-Optimized Tables
**Complexity**: Medium (6-8 hours)
**Components**: Table views

**Description**: Convert tables to card layout on mobile or add scroll indicators.

**Affected Files**:
- `src/app/features/duplicates/components/duplicate-table-view/duplicate-table-view.component.html`
- `src/app/features/duplicates/components/duplicate-table-view/duplicate-table-view.component.scss`
- `src/app/features/files/files.component.html`

**Options**:
1. Card layout for mobile (< 768px)
2. Horizontal scroll with shadow indicators
3. Column prioritization (hide less important on mobile)

**Acceptance Criteria**:
- [ ] Tables usable on mobile devices
- [ ] Critical information visible without scrolling
- [ ] Touch-friendly interactions
- [ ] No horizontal scroll on < 768px (if using cards)

---

### UX-015: Search Help Tooltip
**Complexity**: Low (2-3 hours)
**Components**: Files search

**Description**: Document search syntax in UI tooltip.

**Affected Files**:
- `src/app/features/files/files.component.html`
- `src/app/features/files/files.component.ts`

**Content**:
```
Search syntax:
- date:2024-01-15 - files from specific date
- date:>2024-01-01 - files after date
- path:/photos - files in path
- hash:abc123 - files with hash
- camera:iPhone - files from camera
```

**Acceptance Criteria**:
- [ ] Help icon next to search input
- [ ] Tooltip/popover with syntax examples
- [ ] Examples are clickable to populate search

---

### UX-016: Processing State Animation
**Complexity**: Low (1-2 hours)
**Components**: Status chips

**Description**: Add subtle animation for in-progress states.

**Affected Files**:
- `src/app/features/duplicates/components/duplicate-group-list/duplicate-group-list.component.scss`

**Implementation**:
```scss
.status-cleaning {
  animation: pulse 1.5s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.6; }
}
```

**Acceptance Criteria**:
- [ ] Cleaning status has pulse animation
- [ ] Animation is subtle, not distracting
- [ ] Respects `prefers-reduced-motion`

---

## Phase 4: Polish & Performance (Priority: P3)

### UX-017: Server-Side Sorting
**Complexity**: High (8-12 hours)
**Components**: API and Frontend

**Description**: Implement server-side sorting for proper pagination.

**Affected Files**:
- Backend: `src/Api/Controllers/DuplicatesController.cs`
- Backend: `src/Api/Services/DuplicateService.cs`
- Frontend: `src/app/services/duplicate.service.ts`
- Frontend: `src/app/features/duplicates/components/duplicate-group-list/duplicate-group-list.component.ts`

**API Change**:
```
GET /api/duplicates?page=1&pageSize=20&sortBy=totalSize&sortDir=desc
```

**Acceptance Criteria**:
- [ ] API accepts sortBy and sortDir parameters
- [ ] Sorting works across full dataset
- [ ] Frontend removes client-side sorting
- [ ] Sort state preserved in URL

---

### UX-018: LQIP Image Loading
**Complexity**: Medium (6-8 hours)
**Components**: Thumbnails

**Description**: Implement Low-Quality Image Placeholder for smoother loading.

**Affected Files**:
- `src/app/shared/directives/lqip.directive.ts` (create)
- Thumbnail components

**Implementation**:
- Load tiny placeholder (< 1KB) immediately
- Blur placeholder while loading
- Fade to full image when loaded

**Acceptance Criteria**:
- [ ] Placeholder visible immediately
- [ ] Blur effect applied to placeholder
- [ ] Smooth fade transition
- [ ] Works with lazy loading

---

### UX-019: Form Accessibility Audit
**Complexity**: Medium (4-6 hours)
**Components**: All forms

**Description**: Comprehensive accessibility audit of all form fields.

**Affected Files**:
- `src/app/features/settings/components/directory-form-dialog/`
- All form components

**Checklist**:
- [ ] All inputs have associated labels
- [ ] Required fields marked with `aria-required`
- [ ] Error messages linked with `aria-describedby`
- [ ] Focus order is logical
- [ ] Form groups use fieldset/legend where appropriate

---

### UX-020: Typography Scale
**Complexity**: Low (2-3 hours)
**Components**: All components

**Description**: Implement consistent typography scale using Material.

**Affected Files**:
- `src/styles.scss`
- All component SCSS files (audit)

**Implementation**:
```scss
@use '@angular/material' as mat;

.headline-large { @include mat.typography-level($theme, 'headline-large'); }
.headline-medium { @include mat.typography-level($theme, 'headline-medium'); }
// etc.
```

**Acceptance Criteria**:
- [ ] Typography classes defined
- [ ] All headings use consistent sizes
- [ ] Body text consistent across views

---

### UX-021: Retry with Exponential Backoff
**Complexity**: Medium (4-6 hours)
**Components**: HTTP interceptor

**Description**: Implement automatic retry for transient failures.

**Affected Files**:
- `src/app/core/retry.interceptor.ts` (create)
- `src/app/app.config.ts`

**Implementation**:
```typescript
const maxRetries = 3;
const initialDelay = 1000;

return next(req).pipe(
  retryWhen(errors => errors.pipe(
    concatMap((error, i) => {
      if (i >= maxRetries || !isRetriable(error)) {
        return throwError(() => error);
      }
      return timer(initialDelay * Math.pow(2, i));
    })
  ))
);
```

**Acceptance Criteria**:
- [ ] 503, 0, ETIMEDOUT errors automatically retried
- [ ] Maximum 3 retries
- [ ] Exponential backoff (1s, 2s, 4s)
- [ ] User sees retry attempts in loading state

---

### UX-022: Gallery Multi-Select
**Complexity**: High (8-12 hours)
**Components**: Gallery

**Description**: Implement checkbox selection for bulk operations.

**Affected Files**:
- `src/app/features/gallery/components/gallery-tile/gallery-tile.component.ts`
- `src/app/features/gallery/components/gallery-grid/gallery-grid.component.ts`
- `src/app/features/gallery/gallery.component.html`

**Features**:
- Checkbox overlay on tiles
- Select all/none
- Bulk actions toolbar (hide, delete)
- Keyboard support (Shift+click for range)

**Acceptance Criteria**:
- [ ] Checkbox visible on hover/touch
- [ ] Multiple tiles selectable
- [ ] Bulk actions toolbar appears with selection
- [ ] Range selection with Shift+click

---

### UX-023: Icon Constant Map
**Complexity**: Low (2-3 hours)
**Components**: All

**Description**: Create centralized icon definitions for consistency.

**Affected Files**:
- `src/app/shared/constants/icons.ts` (create)
- All components using icons

**Implementation**:
```typescript
export const ICONS = {
  DELETE: 'delete_outline',
  EDIT: 'edit',
  VIEW: 'visibility',
  DOWNLOAD: 'download',
  REFRESH: 'refresh',
  // Actions
  VALIDATE: 'check_circle',
  SKIP: 'skip_next',
  UNDO: 'undo',
  // Status
  PENDING: 'pending_actions',
  PROCESSING: 'autorenew',
  SUCCESS: 'done',
  ERROR: 'error_outline',
};
```

**Acceptance Criteria**:
- [ ] Icon constants exported
- [ ] All components use constants
- [ ] No duplicate icon definitions

---

### UX-024: Swipe Gestures
**Complexity**: Medium (6-8 hours)
**Components**: Gallery, Duplicate detail

**Description**: Implement swipe navigation for mobile.

**Affected Files**:
- `src/app/shared/directives/swipe.directive.ts` (create)
- `src/app/features/gallery/components/gallery-tile/`
- `src/app/features/duplicates/components/duplicate-group-detail/`

**Implementation**:
- Swipe left/right for next/previous
- Visual feedback during swipe
- Threshold for activation (50px)

**Acceptance Criteria**:
- [ ] Swipe gestures work on touch devices
- [ ] Visual feedback during swipe
- [ ] Configurable swipe threshold
- [ ] Works alongside other touch interactions

---

### UX-025: Dialog Full-Screen on Mobile
**Complexity**: Low (2-3 hours)
**Components**: All dialogs

**Description**: Make dialogs full-screen on mobile devices.

**Affected Files**:
- `src/app/shared/services/dialog.service.ts` (create or enhance)
- Dialog config across components

**Implementation**:
```typescript
const isMobile = window.innerWidth < 600;
return this.dialog.open(Component, {
  ...config,
  width: isMobile ? '100vw' : config.width,
  maxWidth: isMobile ? '100vw' : config.maxWidth,
  height: isMobile ? '100vh' : 'auto',
  panelClass: isMobile ? 'full-screen-dialog' : '',
});
```

**Acceptance Criteria**:
- [ ] Dialogs full-screen on < 600px width
- [ ] Close button accessible on full-screen
- [ ] Keyboard still works (Escape to close)
- [ ] Form inputs not obscured by keyboard

---

## Summary

| Phase | Tasks | Estimated Effort | Priority |
|-------|-------|-----------------|----------|
| Phase 1 | UX-001 to UX-005 | 15-22 hours | P0 - Critical |
| Phase 2 | UX-006 to UX-010 | 19-27 hours | P1 - High |
| Phase 3 | UX-011 to UX-016 | 22-32 hours | P2 - Medium |
| Phase 4 | UX-017 to UX-025 | 42-62 hours | P3 - Low |
| **Total** | **25 tasks** | **98-143 hours** | |

---

## Dependencies

```
UX-001 ──┐
UX-002 ──┼── Can run in parallel (no dependencies)
UX-003 ──┤
UX-004 ──┤
UX-005 ──┘

UX-006 ──┬── Can run in parallel
UX-007 ──┤
UX-008 ──┤
UX-009 ──┤
UX-010 ──┘

UX-011 ──> Requires UX-004 (error handling)
UX-013 ──> Can run independently
UX-014 ──> Can run independently
UX-017 ──> Requires backend changes (separate PR)

UX-018 ──> Requires thumbnail system understanding
UX-022 ──> Requires state management design
```

---

## Next Steps

1. **Review and Approve**: Team review of priorities
2. **Create GitHub Issues**: Convert tasks to GitHub issues
3. **Sprint Planning**: Assign tasks to sprints
4. **Implementation**: Follow acceptance criteria
5. **Testing**: Accessibility audit after each phase
