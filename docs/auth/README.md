# Authentication & Authorization Documentation

This directory contains the authentication and authorization design and implementation plan for Photos Index.

## Contents

| Document | Description |
|----------|-------------|
| [ADR-015](../adrs/015-authentication-authorization.md) | Architecture Decision Record for auth/authz |
| [implementation-plan.md](./implementation-plan.md) | Detailed implementation plan with 35 tasks |

## Quick Summary

### Overview

Photos Index will implement:
- **Authentication**: OpenID Connect (OIDC) with Infomaniak Login
- **Authorization**: Role-Based Access Control (RBAC)
- **Audit**: Complete audit trail of user actions

### External Identity Provider

- **Provider**: Infomaniak Login (https://login.infomaniak.com)
- **Protocol**: OAuth 2.0 / OpenID Connect
- **Enterprise**: isago.ch

### Authorization Model

```
User → UserGroup → Group → GroupRole → Role → RolePermission → Permission
```

### Proposed Groups

| Group | Description |
|-------|-------------|
| `admins` | Full system access |
| `managers` | Manage duplicate workflow |
| `operators` | Day-to-day duplicate processing |
| `viewers` | Read-only access |

### Proposed Permissions

| Category | Permissions |
|----------|-------------|
| Files | view, download, hide |
| Duplicates | view, select, validate, delete, undo |
| Settings | view, manage |
| Indexing | view, trigger, cancel |
| Users | view, manage |
| Groups | view, manage |
| Audit | view |

## Implementation Phases

| Phase | Version | Focus | Effort |
|-------|---------|-------|--------|
| 1 | v0.15.0 | Authentication Foundation | 30-40h |
| 2 | v0.16.0 | Authorization Core | 35-45h |
| 3 | v0.17.0 | Group Integration | 25-35h |
| 4 | v0.18.0 | Audit & Polish | 20-30h |

## Review Status

- [ ] UX Review: Pending
- [ ] Architecture Review: Pending
- [ ] Security Review: Pending
- [ ] Team Approval: Pending

## References

- [Infomaniak Developer Portal](https://developer.infomaniak.com/getting-started)
- [OpenID Connect Spec](https://openid.net/specs/openid-connect-core-1_0.html)
- [ASP.NET Core Security](https://learn.microsoft.com/en-us/aspnet/core/security/)
