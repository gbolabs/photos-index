# Critical Review: UX and Auth/Authz Plans

**Date**: 2025-12-31
**Reviewer**: Claude Code
**Plans Reviewed**:
- UX Improvement Plan (PR #176)
- Auth/Authz Plan (PR #177)

---

## Executive Summary

Both plans are well-structured and comprehensive. However, there are areas requiring attention before implementation begins. This review identifies potential issues, gaps, and recommendations from both UX and Architecture perspectives.

---

## UX Plan Review (PR #176)

### Strengths

1. **Comprehensive Analysis**: 35 findings across 10 categories provide thorough coverage
2. **Prioritization**: Clear P0-P3 priority levels with effort estimates
3. **Phased Approach**: 4 phases allow incremental improvement
4. **Acceptance Criteria**: Each task has measurable criteria

### UX Concerns

#### 1. Missing Mobile-First Strategy
**Issue**: The plan addresses mobile issues reactively rather than with a mobile-first mindset.

**Recommendation**: Consider a mobile-first redesign for key flows:
- Duplicate review workflow on mobile (< 768px)
- Gallery navigation on touch devices
- Settings management on tablets

**Impact**: Without mobile-first thinking, fixes may feel like patches.

#### 2. No User Research Foundation
**Issue**: All recommendations are based on heuristic analysis, not actual user feedback.

**Recommendation**:
- Conduct 3-5 user interviews before Phase 1
- Add analytics to measure current pain points
- Create user personas for different user types (admin, operator, viewer)

**Impact**: May solve wrong problems or miss critical issues.

#### 3. Accessibility Gaps
**Issue**: WCAG 2.1 Level AA is mentioned but not systematically addressed.

**Specific Gaps**:
- No mention of color contrast audit (4.5:1 for text)
- No mention of reduced motion support
- Focus visible states only briefly mentioned
- No consideration for screen magnification users

**Recommendation**: Add dedicated accessibility audit task before Phase 1.

#### 4. Design System Incomplete
**Issue**: Spacing and typography mentioned but no complete design system.

**Missing Elements**:
- Color palette documentation
- Component library
- Animation guidelines
- Responsive breakpoints

**Recommendation**: Create design tokens file and component documentation as Phase 0.

### Architecture Concerns (UX Plan)

#### 1. State Management for Complex Flows
**Issue**: Scroll position preservation (UX-007) requires state management not currently in place.

**Technical Risk**:
- Route state may not persist through page reloads
- Service-based state may leak memory

**Recommendation**: Evaluate NgRx or signal-based store before implementing.

#### 2. Skeleton Loading Performance
**Issue**: Skeleton components (UX-005) must be SSR-compatible if SSR is ever added.

**Recommendation**: Design skeletons as pure CSS where possible.

#### 3. Server-Side Sorting Scope
**Issue**: UX-017 (server-side sorting) requires API changes but scope unclear.

**Questions**:
- Which endpoints need sorting?
- How does this affect existing pagination?
- Should we add filtering at the same time?

**Recommendation**: Create separate API enhancement task before UX-017.

---

## Auth/Authz Plan Review (PR #177)

### Strengths

1. **Industry Standards**: OIDC with established provider
2. **Comprehensive RBAC**: Well-designed permission hierarchy
3. **Audit Trail**: Built-in from the start
4. **Phased Rollout**: Authentication before authorization

### UX Concerns (Auth Plan)

#### 1. Login Flow UX Not Specified
**Issue**: No mockups or flow diagrams for login experience.

**Missing Details**:
- What does the login button look like?
- Where is it placed?
- What happens during redirect?
- Error states for failed login?
- Session timeout behavior?

**Recommendation**: Add UX specifications before Phase 1 implementation.

#### 2. Permission Denied Experience
**Issue**: What do users see when they lack permission?

**Scenarios Not Addressed**:
- Clicking a button without permission
- Navigating to a protected route
- API returns 403

**Recommendation**: Design unified "access denied" experience.

#### 3. Role Understanding
**Issue**: Will users understand why they can/can't do things?

**Missing**:
- No "My Permissions" page
- No help text explaining roles
- No admin visibility into who has what

**Recommendation**: Add user-facing permission documentation.

#### 4. First-Time User Experience
**Issue**: What happens when a new user logs in?

**Questions**:
- Are they assigned a default group?
- Do they see an empty dashboard?
- Who notifies admins of new users?

**Recommendation**: Design onboarding flow for new users.

### Architecture Concerns (Auth Plan)

#### 1. Token Storage Security
**Issue**: Plan mentions tokens but not secure storage strategy.

**Risks**:
- LocalStorage vulnerable to XSS
- SessionStorage doesn't persist across tabs
- Cookies need proper flags

**Recommendation**: Use HttpOnly cookies for refresh tokens, memory for access tokens.

#### 2. Permission Caching Strategy
**Issue**: 5-minute cache may be too aggressive.

**Scenarios**:
- Admin adds user to group → user must wait 5 min
- Permission removed → security delay

**Recommendation**:
- Add cache invalidation on role changes
- Consider WebSocket/SignalR for real-time permission updates
- Allow admin to force-refresh user sessions

#### 3. IDP Dependency
**Issue**: Infomaniak availability affects entire application.

**Single Point of Failure**:
- IDP down = no logins
- IDP token validation = every API call

**Recommendation**:
- Implement token caching on API
- Consider offline mode with cached session
- Add health check for IDP status

#### 4. Group Sync Complexity
**Issue**: Phase 3 mentions "sync groups from Infomaniak (if available)" - unclear.

**Questions**:
- Does Infomaniak expose groups via claims?
- Is bidirectional sync needed?
- How to handle group name conflicts?

**Recommendation**: Investigate Infomaniak group support before Phase 2 finishes.

#### 5. Migration Path Undefined
**Issue**: How do existing deployments migrate?

**Current State**:
- No authentication = all users are admin
- Files may have been created without user context

**Recommendation**:
- Create migration script to assign files to "system" user
- Document downtime requirements
- Create rollback procedure

#### 6. Multi-Tenancy Implications
**Issue**: Plan doesn't address multi-organization scenarios.

**If different families share instance**:
- Should they see each other's files?
- Separate directories per org?
- Org-level admins?

**Recommendation**: Clarify single-tenant vs multi-tenant scope.

#### 7. Service Account Authentication
**Issue**: How do IndexingService and CleanerService authenticate?

**Current**: Services call API without auth
**After**: API requires authentication

**Recommendation**: Add section for service-to-service authentication (client credentials flow or shared secrets).

---

## Cross-Cutting Concerns

### 1. Dependency Between Plans
**Issue**: Auth plan (v0.15.0+) should start after UX Phase 1 (v0.11.0) or in parallel?

**Recommendation**:
- UX Phase 1 can proceed independently
- Auth Phase 1 can proceed in parallel
- Auth Phase 2+ should wait for UX Phase 1 (need consistent UI patterns)

### 2. Testing Strategy Gaps

**UX Plan**:
- No visual regression testing mentioned
- No cross-browser testing specified

**Auth Plan**:
- Mock IDP for testing not specified
- No security penetration testing

**Recommendation**: Add testing infrastructure tasks to both plans.

### 3. Documentation Needs

Both plans lack:
- End-user documentation updates
- Admin guide updates
- API documentation updates

**Recommendation**: Add documentation tasks to each phase.

---

## Priority Recommendations

### Before Starting Either Plan

1. **User Research** (1-2 weeks)
   - Interview 3-5 users
   - Analyze current usage patterns
   - Identify most critical pain points

2. **Design Tokens** (2-3 days)
   - Create centralized design variables
   - Document color, spacing, typography

3. **Investigate Infomaniak** (1 week)
   - Verify OIDC configuration options
   - Test group claim support
   - Document available scopes

### High-Priority Fixes to Plans

| Issue | Plan | Action |
|-------|------|--------|
| Login UX not specified | Auth | Add login flow mockups |
| Token storage not defined | Auth | Specify secure storage strategy |
| Service-to-service auth | Auth | Add client credentials section |
| Accessibility audit | UX | Add as pre-Phase 1 task |
| Permission denied UX | Auth | Design denied states |
| Migration path | Auth | Document migration procedure |

---

## Conclusion

Both plans provide solid foundations but require refinement before implementation:

**UX Plan**: Ready to start after adding accessibility audit and design tokens. Consider user research to validate priorities.

**Auth Plan**: Needs clarification on token storage, service auth, and migration before Phase 1. UX for auth flows needs design.

**Recommendation**: Approve both plans conditionally, with the understanding that gaps identified will be addressed in the first sprint of each phase.

---

## Appendix: Review Checklist

### UX Review Criteria
- [x] Accessibility considered
- [ ] Mobile-first approach (partial)
- [x] Loading states defined
- [x] Error states defined
- [ ] User research basis (missing)
- [x] Phased implementation
- [x] Measurable criteria

### Architecture Review Criteria
- [x] Scalability considered
- [x] Security addressed
- [ ] Migration path defined (partial)
- [x] Testing strategy mentioned
- [ ] Service-to-service auth (missing)
- [x] Industry standards used
- [ ] Failure modes addressed (partial)
