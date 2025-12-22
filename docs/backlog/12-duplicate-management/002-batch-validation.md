# 002: Batch Validation & Undo

**Status**: 🔲 Not Started
**Priority**: P1
**Issue**: [#68](https://github.com/gbolabs/photos-index/issues/68)
**Branch**: `feature/batch-validation`
**Estimated Complexity**: Medium-High
**Target Release**: v0.2.0

## Objective

Enable users to validate duplicate selections in batches (selected, next N, all) and undo validated selections before cleanup.

## Dependencies

- `12-001` Table View (for checkbox selection)

## Acceptance Criteria

- [ ] "Validate Selected" button for checked groups
- [ ] "Validate Next N" dropdown (10/100/1000)
- [ ] "Validate All Visible" with confirmation dialog
- [ ] Status field on DuplicateGroup entity (pending/validated/cleaned)
- [ ] Validated groups shown in purple/violet
- [ ] "Undo Validation" for selected validated groups
- [ ] Filter by status (pending/validated/cleaned)
- [ ] "Hide validated" toggle to focus on pending work
- [ ] Validation count in header (e.g., "2,097 validated")

## Technical Design

### Database Migration

```sql
ALTER TABLE "DuplicateGroups" ADD COLUMN "Status" VARCHAR(20) DEFAULT 'pending';
ALTER TABLE "DuplicateGroups" ADD COLUMN "ValidatedAt" TIMESTAMP NULL;
ALTER TABLE "DuplicateGroups" ADD COLUMN "KeptFileId" UUID NULL;
CREATE INDEX idx_duplicate_groups_status ON "DuplicateGroups"("Status");
```

### New API Endpoints

```
POST /api/duplicate-groups/validate
Body: { groupIds: string[], keptFileId?: string }
Response: { validated: number }

POST /api/duplicate-groups/validate-batch
Body: { count: 10|100|1000, filter?: 'pending' }
Response: { validated: number, remaining: number }

POST /api/duplicate-groups/undo-validation
Body: { groupIds: string[] }
Response: { undone: number }

GET /api/duplicate-groups?status=pending&page=1&pageSize=50
```

### Entity Changes

```csharp
public class DuplicateGroup
{
    // ... existing fields
    public string Status { get; set; } = "pending"; // pending, validated, cleaned
    public DateTime? ValidatedAt { get; set; }
    public Guid? KeptFileId { get; set; }
}
```

### UI Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ Batch Actions                                                   │
├─────────────────────────────────────────────────────────────────┤
│ Selected: 15 groups                                             │
│ [Validate Selected] [Validate Next ▾] [Undo Selected]          │
│                      └─ 10 / 100 / 1000                        │
│                                                                 │
│ Filter: [All ▾] [x] Hide validated                             │
│          └─ All / Pending / Validated / Cleaned                │
└─────────────────────────────────────────────────────────────────┘
```

## Files to Create/Modify

### Backend
- `src/Database/Entities/DuplicateGroup.cs` - Add Status, ValidatedAt, KeptFileId
- `src/Database/Migrations/YYYYMMDD_AddDuplicateGroupStatus.cs` - Migration
- `src/Api/Controllers/DuplicateGroupsController.cs` - New endpoints
- `src/Shared/Dtos/ValidateDuplicatesRequest.cs` (new)

### Frontend
- `src/Web/src/app/features/duplicates/components/batch-actions-toolbar/` - Update
- `src/Web/src/app/features/duplicates/components/status-filter/` (new)
- `src/Web/src/app/services/duplicate.service.ts` - Add validation methods
- `src/Web/src/app/models/duplicate-group.dto.ts` - Add status fields

## Test Coverage

- API endpoint tests for validate/undo
- Migration tests
- Component tests for batch actions
- E2E test: validate → filter → undo flow
