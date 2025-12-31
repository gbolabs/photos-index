# UX Implementation Plan

**Status**: Proposed
**Date**: 2025-12-31
**Target**: v0.11.0 - v0.14.0

## Roadmap Overview

```
v0.10.0 (Current)
    │
    ▼
v0.11.0 ─── Phase 1: Critical Accessibility & Core UX
    │       └── UX-001 to UX-005 (5 tasks)
    │
    ▼
v0.12.0 ─── Phase 2: Navigation & Consistency
    │       └── UX-006 to UX-010 (5 tasks)
    │
    ▼
v0.13.0 ─── Phase 3: Enhanced User Experience
    │       └── UX-011 to UX-016 (6 tasks)
    │
    ▼
v0.14.0 ─── Phase 4: Polish & Performance
            └── UX-017 to UX-025 (9 tasks)
```

---

## Phase 1: Critical Accessibility & Core UX

**Version**: v0.11.0
**Focus**: WCAG 2.1 Level AA compliance basics and core UX improvements
**Effort**: 15-22 hours

### Sprint Plan

| Task | Description | Effort | Assignee |
|------|-------------|--------|----------|
| UX-001 | ARIA labels for icon buttons | 1-2h | A4 |
| UX-002 | Status icons for color independence | 2-3h | A4 |
| UX-003 | Touch-friendly target sizes | 2-3h | A4 |
| UX-004 | Error message improvement | 4-6h | A4 |
| UX-005 | Skeleton loading states | 6-8h | A4 |

### Detailed Implementation

#### Week 1: Quick Wins (UX-001, UX-002, UX-003)

**Day 1-2: UX-001 - ARIA Labels**
```bash
# Branch
git checkout -b feature/ux-001-aria-labels

# Files to modify
src/app/features/duplicates/components/**/*.html
src/app/features/settings/**/*.html
src/app/app.component.html
```

Implementation checklist:
1. Search for all `mat-icon-button` in codebase
2. Add `aria-label` to each
3. Verify with Lighthouse accessibility audit
4. Test with screen reader (VoiceOver/NVDA)

**Day 2-3: UX-002 - Status Icons**
```typescript
// Add to shared constants
export const STATUS_ICONS: Record<string, string> = {
  'Pending': 'pending_actions',
  'AutoSelected': 'auto_awesome',
  'Validated': 'check_circle',
  'Cleaning': 'cleaning_services',
  'CleaningFailed': 'error',
  'Cleaned': 'done_all'
};

// Update template
<mat-chip [class]="'status-' + status.toLowerCase()">
  <mat-icon class="status-icon">{{ getStatusIcon(status) }}</mat-icon>
  {{ getStatusLabel(status) }}
</mat-chip>
```

**Day 3-4: UX-003 - Touch Targets**
```scss
// Global touch target mixin
@mixin touch-target($size: 44px) {
  min-width: $size;
  min-height: $size;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

// Apply to checkboxes
.mat-mdc-checkbox {
  @include touch-target;
}
```

#### Week 2: Core UX (UX-004, UX-005)

**Day 5-7: UX-004 - Error Messages**
```typescript
// Create api-error-handler.ts
export class ApiErrorHandler {
  static getMessage(error: HttpErrorResponse): string {
    // Network error
    if (error.status === 0) {
      return 'Unable to connect to the server. Please check your internet connection.';
    }

    // Parse API error response
    if (error.error?.message) {
      return error.error.message;
    }

    // HTTP status mapping
    const messages: Record<number, string> = {
      400: 'Invalid request. Please check your input.',
      401: 'Your session has expired. Please refresh the page.',
      403: 'You do not have permission to perform this action.',
      404: 'The requested resource was not found.',
      409: 'This action conflicts with the current state.',
      422: 'The provided data could not be processed.',
      429: 'Too many requests. Please wait a moment.',
      500: 'An internal server error occurred.',
      502: 'The server is temporarily unavailable.',
      503: 'The service is temporarily unavailable.',
    };

    return messages[error.status] || 'An unexpected error occurred.';
  }

  static isRetriable(error: HttpErrorResponse): boolean {
    return error.status === 0 || error.status >= 500;
  }
}
```

**Day 8-10: UX-005 - Skeleton Loading**
```typescript
// Create skeleton-card.component.ts
@Component({
  selector: 'app-skeleton-card',
  template: `
    <div class="skeleton-card" *ngFor="let _ of Array(count)">
      <div class="skeleton skeleton-image"></div>
      <div class="skeleton skeleton-title"></div>
      <div class="skeleton skeleton-text"></div>
    </div>
  `,
  styles: [`
    .skeleton {
      background: linear-gradient(90deg, #f0f0f0 25%, #e0e0e0 50%, #f0f0f0 75%);
      background-size: 200% 100%;
      animation: shimmer 1.5s infinite;
      border-radius: 4px;
    }
    .skeleton-image { height: 120px; margin-bottom: 8px; }
    .skeleton-title { height: 20px; width: 70%; margin-bottom: 8px; }
    .skeleton-text { height: 14px; width: 90%; }
    @keyframes shimmer {
      0% { background-position: 200% 0; }
      100% { background-position: -200% 0; }
    }
  `]
})
export class SkeletonCardComponent {
  @Input() count = 6;
  Array = Array;
}
```

### Phase 1 Definition of Done

- [ ] All icon buttons have aria-label attributes
- [ ] All status badges have icons alongside color
- [ ] All interactive elements have 44x44px minimum touch target
- [ ] Error messages are specific and actionable
- [ ] Skeleton loading replaces spinner in list views
- [ ] Lighthouse accessibility score > 90
- [ ] All tests pass
- [ ] PR approved and merged

---

## Phase 2: Navigation & Consistency

**Version**: v0.12.0
**Focus**: Navigation patterns and design system foundations
**Effort**: 19-27 hours

### Sprint Plan

| Task | Description | Effort | Assignee |
|------|-------------|--------|----------|
| UX-006 | Breadcrumb navigation | 4-6h | A4 |
| UX-007 | Back navigation state | 6-8h | A4 |
| UX-008 | Spacing scale | 2-3h | A4 |
| UX-009 | Button hierarchy | 3-4h | A4 |
| UX-010 | Connection status | 4-6h | A4 |

### Detailed Implementation

#### Breadcrumb Component (UX-006)
```typescript
// breadcrumb.component.ts
interface BreadcrumbItem {
  label: string;
  route?: string;
  icon?: string;
}

@Component({
  selector: 'app-breadcrumb',
  template: `
    <nav aria-label="Breadcrumb" class="breadcrumb">
      @for (item of items(); track item.label; let last = $last) {
        @if (!last) {
          <a [routerLink]="item.route" class="breadcrumb-item">
            @if (item.icon) {
              <mat-icon>{{ item.icon }}</mat-icon>
            }
            {{ item.label }}
          </a>
          <mat-icon class="separator">chevron_right</mat-icon>
        } @else {
          <span class="breadcrumb-item current" aria-current="page">
            {{ item.label }}
          </span>
        }
      }
    </nav>
  `
})
export class BreadcrumbComponent {
  items = input.required<BreadcrumbItem[]>();
}
```

#### Design System Variables (UX-008)
```scss
// _variables.scss
:root {
  // Spacing scale (8px base)
  --spacing-0: 0;
  --spacing-1: 4px;
  --spacing-2: 8px;
  --spacing-3: 12px;
  --spacing-4: 16px;
  --spacing-5: 20px;
  --spacing-6: 24px;
  --spacing-8: 32px;
  --spacing-10: 40px;
  --spacing-12: 48px;
  --spacing-16: 64px;

  // Border radius
  --radius-sm: 4px;
  --radius-md: 8px;
  --radius-lg: 12px;
  --radius-full: 9999px;

  // Transitions
  --transition-fast: 150ms ease;
  --transition-normal: 250ms ease;
  --transition-slow: 400ms ease;
}
```

### Phase 2 Definition of Done

- [ ] Breadcrumb component in detail views
- [ ] Scroll/selection state preserved on back navigation
- [ ] CSS custom properties for spacing used throughout
- [ ] Button hierarchy consistent across all views
- [ ] Real-time connection status visible in header
- [ ] All tests pass
- [ ] PR approved and merged

---

## Phase 3: Enhanced User Experience

**Version**: v0.13.0
**Focus**: User delight and productivity features
**Effort**: 22-32 hours

### Sprint Plan

| Task | Description | Effort | Assignee |
|------|-------------|--------|----------|
| UX-011 | Undo in snackbar | 4-6h | A4 |
| UX-012 | Empty state improvements | 3-4h | A4 |
| UX-013 | Gallery keyboard navigation | 6-8h | A4 |
| UX-014 | Mobile-optimized tables | 6-8h | A4 |
| UX-015 | Search help tooltip | 2-3h | A4 |
| UX-016 | Processing animations | 1-2h | A4 |

### Key Implementation: Undo Pattern (UX-011)
```typescript
// notification.service.ts
interface UndoableAction {
  message: string;
  undoFn: () => Observable<unknown>;
}

export class NotificationService {
  constructor(private snackBar: MatSnackBar) {}

  showUndoable(action: UndoableAction): void {
    const ref = this.snackBar.open(action.message, 'Undo', {
      duration: 10000,
      panelClass: 'undoable-snackbar'
    });

    ref.onAction().subscribe(() => {
      action.undoFn().subscribe({
        next: () => this.snackBar.open('Action undone', '', { duration: 3000 }),
        error: () => this.snackBar.open('Failed to undo', '', { duration: 3000 })
      });
    });
  }
}

// Usage in component
this.notificationService.showUndoable({
  message: '2 files queued for deletion',
  undoFn: () => this.duplicateService.undoDelete(groupId)
});
```

### Phase 3 Definition of Done

- [ ] Undo action available for 10s after destructive operations
- [ ] Empty states have clear CTAs
- [ ] Gallery navigable with keyboard
- [ ] Tables usable on mobile
- [ ] Search syntax documented in UI
- [ ] Processing states have subtle animations
- [ ] All tests pass
- [ ] PR approved and merged

---

## Phase 4: Polish & Performance

**Version**: v0.14.0
**Focus**: Performance optimizations and final polish
**Effort**: 42-62 hours

### Sprint Plan

| Task | Description | Effort | Assignee |
|------|-------------|--------|----------|
| UX-017 | Server-side sorting | 8-12h | A1/A4 |
| UX-018 | LQIP image loading | 6-8h | A4 |
| UX-019 | Form accessibility audit | 4-6h | A4 |
| UX-020 | Typography scale | 2-3h | A4 |
| UX-021 | Retry with backoff | 4-6h | A4 |
| UX-022 | Gallery multi-select | 8-12h | A4 |
| UX-023 | Icon constant map | 2-3h | A4 |
| UX-024 | Swipe gestures | 6-8h | A4 |
| UX-025 | Dialog full-screen mobile | 2-3h | A4 |

### API Change for Server-Side Sorting (UX-017)
```csharp
// DuplicatesController.cs
[HttpGet]
public async Task<ActionResult<PagedResponse<DuplicateGroupDto>>> GetAll(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? status = null,
    [FromQuery] string sortBy = "totalSize",
    [FromQuery] string sortDir = "desc")
{
    var query = _dbContext.DuplicateGroups.AsQueryable();

    // Apply sorting
    query = (sortBy, sortDir) switch
    {
        ("totalSize", "asc") => query.OrderBy(g => g.TotalSize),
        ("totalSize", "desc") => query.OrderByDescending(g => g.TotalSize),
        ("fileCount", "asc") => query.OrderBy(g => g.FileCount),
        ("fileCount", "desc") => query.OrderByDescending(g => g.FileCount),
        ("createdAt", "asc") => query.OrderBy(g => g.CreatedAt),
        ("createdAt", "desc") => query.OrderByDescending(g => g.CreatedAt),
        _ => query.OrderByDescending(g => g.TotalSize)
    };

    // ... rest of pagination
}
```

### Phase 4 Definition of Done

- [ ] Sorting works across full dataset
- [ ] Image loading is smooth with placeholders
- [ ] All forms pass accessibility audit
- [ ] Typography consistent throughout
- [ ] Transient errors auto-retry
- [ ] Gallery supports multi-select
- [ ] Icons centralized and consistent
- [ ] Swipe navigation works on touch devices
- [ ] Dialogs full-screen on mobile
- [ ] All tests pass
- [ ] PR approved and merged

---

## Testing Strategy

### Accessibility Testing

1. **Automated Testing**
   - Lighthouse accessibility audit (target: >90)
   - axe-core integration in E2E tests
   - ESLint a11y plugin

2. **Manual Testing**
   - Keyboard-only navigation
   - Screen reader testing (VoiceOver, NVDA)
   - High contrast mode
   - Reduced motion preference

### Cross-Browser Testing

| Browser | Desktop | Mobile |
|---------|---------|--------|
| Chrome | Yes | Yes (Android) |
| Firefox | Yes | Yes (Android) |
| Safari | Yes | Yes (iOS) |
| Edge | Yes | - |

### Device Testing

| Category | Devices |
|----------|---------|
| Mobile | iPhone SE, iPhone 14, Pixel 5 |
| Tablet | iPad, Galaxy Tab |
| Desktop | 1920x1080, 2560x1440 |

---

## Success Metrics

| Metric | Current | Phase 1 | Phase 4 |
|--------|---------|---------|---------|
| Lighthouse Accessibility | ~70 | >90 | >95 |
| Lighthouse Performance | ~80 | >80 | >90 |
| Mobile Usability | Fair | Good | Excellent |
| Keyboard Navigable | Partial | Core views | Full |
| Screen Reader Support | Limited | Basic | Full |

---

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Breaking changes | Medium | High | Feature flags, gradual rollout |
| Performance regression | Low | Medium | Performance budget in CI |
| Browser compatibility | Low | Medium | Cross-browser testing matrix |
| Accessibility regressions | Medium | High | Automated a11y testing |

---

## Review Checkpoints

After each phase:
1. Code review by team
2. Accessibility audit (Lighthouse + manual)
3. User testing with 2-3 stakeholders
4. Performance benchmark comparison
5. Sign-off before merge to main

---

## Appendix: File Changes Summary

### Phase 1 Files
```
src/app/features/duplicates/components/**/*.html     [Modified]
src/app/features/settings/**/*.html                  [Modified]
src/app/app.component.html                           [Modified]
src/app/shared/constants/status-icons.ts             [Created]
src/app/services/api-error-handler.ts                [Created]
src/app/shared/components/skeleton-card/             [Created]
src/styles.scss                                      [Modified]
```

### Phase 2 Files
```
src/app/shared/components/breadcrumb/                [Created]
src/app/features/duplicates/duplicates.component.ts  [Modified]
src/app/services/duplicate-list-state.service.ts     [Created]
src/styles/_variables.scss                           [Created]
src/app/services/connection-status.service.ts        [Created]
```

### Phase 3 Files
```
src/app/services/notification.service.ts             [Modified]
src/app/features/gallery/components/**/*.ts          [Modified]
src/app/features/duplicates/**/*.scss                [Modified]
src/app/features/files/files.component.html          [Modified]
```

### Phase 4 Files
```
Backend: src/Api/Controllers/DuplicatesController.cs [Modified]
Backend: src/Api/Services/DuplicateService.cs        [Modified]
src/app/shared/directives/lqip.directive.ts          [Created]
src/app/shared/directives/swipe.directive.ts         [Created]
src/app/shared/constants/icons.ts                    [Created]
src/app/core/retry.interceptor.ts                    [Created]
```
