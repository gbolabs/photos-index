# ADR-015: Authentication and Authorization with External IDP

**Status**: Proposed
**Date**: 2025-12-31
**Author**: Claude Code

## Context

Photos Index is currently a single-user application without authentication. As the application matures and is deployed for organization use (isago.ch enterprise), we need to implement:

1. **Authentication**: Verify user identity via external Identity Provider (IDP)
2. **Authorization**: Control access to resources based on user roles and permissions
3. **Multi-tenancy**: Support multiple users with different access levels

The organization uses Infomaniak as their primary IT provider, making Infomaniak Login a natural choice for the Identity Provider.

### Requirements

1. **Single Sign-On (SSO)**: Users should log in once with their Infomaniak credentials
2. **Group-based access**: Access control based on organization groups
3. **Permission granularity**: Fine-grained permissions for different operations
4. **Audit trail**: Track who performed what actions
5. **API authentication**: Secure API endpoints with bearer tokens
6. **Session management**: Handle token refresh and session expiry

## Decision

We will implement OpenID Connect (OIDC) authentication with Infomaniak Login and a custom Role-Based Access Control (RBAC) system for authorization.

### Architecture Overview

```
┌─────────────────┐     ┌──────────────────────────────────────────────┐
│                 │     │              Photos Index                     │
│   Infomaniak    │     │                                              │
│     Login       │◄────┤  ┌──────────┐  ┌────────────┐  ┌──────────┐ │
│                 │     │  │ Angular  │  │   .NET     │  │PostgreSQL│ │
│  (OAuth2/OIDC)  │     │  │  (SPA)   │◄─┤   API      │◄─┤   DB     │ │
│                 │     │  └──────────┘  └────────────┘  └──────────┘ │
└─────────────────┘     │        │             │              ▲       │
        ▲               │        ▼             ▼              │       │
        │               │  ┌──────────────────────────────────┴──┐    │
        └───────────────┤  │         RBAC Authorization          │    │
                        │  │  (Groups, Roles, Permissions)       │    │
                        │  └─────────────────────────────────────┘    │
                        └──────────────────────────────────────────────┘
```

### Authentication Flow

```
┌──────┐          ┌─────────┐          ┌────────────┐          ┌───────────┐
│ User │          │ Angular │          │  .NET API  │          │ Infomaniak│
└──┬───┘          └────┬────┘          └─────┬──────┘          └─────┬─────┘
   │                   │                     │                       │
   │  1. Access App    │                     │                       │
   ├──────────────────►│                     │                       │
   │                   │                     │                       │
   │  2. Redirect to   │                     │                       │
   │◄──────────────────┤                     │                       │
   │  Login           │                     │                       │
   │                   │                     │                       │
   │  3. Authenticate  │                     │                       │
   ├─────────────────────────────────────────────────────────────────►
   │                   │                     │                       │
   │  4. Auth Code     │                     │                       │
   │◄─────────────────────────────────────────────────────────────────
   │                   │                     │                       │
   │  5. Auth Code     │                     │                       │
   ├──────────────────►│                     │                       │
   │                   │                     │                       │
   │                   │  6. Exchange Code   │                       │
   │                   ├─────────────────────►                       │
   │                   │                     │  7. Exchange Code    │
   │                   │                     ├───────────────────────►
   │                   │                     │                       │
   │                   │                     │  8. ID + Access Token │
   │                   │                     │◄───────────────────────
   │                   │                     │                       │
   │                   │  9. Session Token   │                       │
   │                   │◄────────────────────┤                       │
   │                   │                     │                       │
   │  10. Logged In    │                     │                       │
   │◄──────────────────┤                     │                       │
```

### Authorization Model

#### Entity Relationship

```
┌─────────────┐     ┌─────────────────┐     ┌──────────────┐
│    User     │     │   UserGroup     │     │    Group     │
├─────────────┤     ├─────────────────┤     ├──────────────┤
│ Id          │◄────┤ UserId          │     │ Id           │
│ ExternalId  │     │ GroupId         ├────►│ Name         │
│ Email       │     │ AssignedAt      │     │ Description  │
│ DisplayName │     │ AssignedBy      │     │ IsExternal   │
│ LastLoginAt │     └─────────────────┘     │ ExternalId   │
└─────────────┘                             └──────┬───────┘
                                                   │
                    ┌─────────────────┐            │
                    │  GroupRole      │◄───────────┘
                    ├─────────────────┤
                    │ GroupId         │
                    │ RoleId          ├────────────┐
                    └─────────────────┘            │
                                                   ▼
┌─────────────┐     ┌─────────────────┐     ┌──────────────┐
│ Permission  │     │ RolePermission  │     │    Role      │
├─────────────┤     ├─────────────────┤     ├──────────────┤
│ Id          │◄────┤ PermissionId    │     │ Id           │
│ Name        │     │ RoleId          ├────►│ Name         │
│ Resource    │     └─────────────────┘     │ Description  │
│ Action      │                             │ Priority     │
│ Description │                             └──────────────┘
└─────────────┘
```

#### Proposed Groups

| Group | Description | Use Case |
|-------|-------------|----------|
| `admins` | Full system administrators | System configuration, user management |
| `managers` | Duplicate management leads | Review, validate, approve deletions |
| `operators` | Day-to-day operators | View, select originals, queue cleanup |
| `viewers` | Read-only access | View files, statistics |

#### Proposed Roles

| Role | Description | Assignable To |
|------|-------------|---------------|
| `System.Admin` | Full system access | admins group |
| `Duplicates.Manager` | Manage duplicate workflow | managers group |
| `Duplicates.Operator` | Process duplicates | operators group |
| `Files.Viewer` | View files only | viewers group |
| `Settings.Admin` | Configure directories | admins, managers |
| `Indexing.Admin` | Control indexing | admins |

#### Proposed Permissions

| Permission | Resource | Action | Description |
|------------|----------|--------|-------------|
| `files.view` | Files | Read | View file list and details |
| `files.download` | Files | Read | Download original files |
| `files.hide` | Files | Write | Hide files from view |
| `duplicates.view` | Duplicates | Read | View duplicate groups |
| `duplicates.select` | Duplicates | Write | Select original file |
| `duplicates.validate` | Duplicates | Write | Validate selections |
| `duplicates.delete` | Duplicates | Delete | Queue files for deletion |
| `duplicates.undo` | Duplicates | Write | Undo deletions |
| `settings.view` | Settings | Read | View directory config |
| `settings.manage` | Settings | Write | Add/edit directories |
| `indexing.view` | Indexing | Read | View scan status |
| `indexing.trigger` | Indexing | Write | Start scans |
| `indexing.cancel` | Indexing | Write | Cancel scans |
| `users.view` | Users | Read | View user list |
| `users.manage` | Users | Write | Manage users |
| `groups.view` | Groups | Read | View groups |
| `groups.manage` | Groups | Write | Manage groups |
| `audit.view` | Audit | Read | View audit logs |

#### Role-Permission Mapping

```
System.Admin
├── files.*
├── duplicates.*
├── settings.*
├── indexing.*
├── users.*
├── groups.*
└── audit.*

Duplicates.Manager
├── files.view
├── files.download
├── duplicates.*
├── settings.view
└── indexing.view

Duplicates.Operator
├── files.view
├── files.download
├── duplicates.view
├── duplicates.select
├── duplicates.validate
└── indexing.view

Files.Viewer
├── files.view
├── files.download
├── duplicates.view
└── indexing.view

Settings.Admin
├── files.view
├── settings.*
└── indexing.*

Indexing.Admin
├── files.view
├── indexing.*
└── settings.view
```

### Implementation Approach

#### Phase 1: Authentication Foundation
1. Configure Infomaniak OAuth2/OIDC application
2. Implement ASP.NET Core authentication middleware
3. Implement Angular OIDC client (angular-oauth2-oidc)
4. Create user sync from OIDC claims
5. Implement token storage and refresh

#### Phase 2: Authorization Core
1. Create database schema for RBAC
2. Implement authorization service
3. Create policy-based authorization handlers
4. Add permission-based endpoint protection
5. Implement authorization middleware

#### Phase 3: Group Integration
1. Sync groups from Infomaniak (if available)
2. Implement manual group management
3. Create group membership UI
4. Add group-based role assignment

#### Phase 4: UI Integration
1. Add login/logout UI
2. Implement permission-based UI rendering
3. Add user profile page
4. Create admin dashboard for user/group management

### Infomaniak OIDC Configuration

Based on [Infomaniak Developer Portal](https://developer.infomaniak.com/getting-started) and [infomaniak-connect-openid](https://github.com/Infomaniak/infomaniak-connect-openid):

```json
{
  "Authority": "https://login.infomaniak.com",
  "ClientId": "<from-infomaniak-manager>",
  "ClientSecret": "<from-infomaniak-manager>",
  "ResponseType": "code",
  "Scopes": ["openid", "profile", "email"],
  "CallbackPath": "/signin-oidc",
  "SignedOutCallbackPath": "/signout-callback-oidc"
}
```

#### Required Configuration Steps

1. Access [Infomaniak Manager](https://manager.infomaniak.com/v3/ng/products/cloud/ik-auth)
2. Create OAuth2 application
3. Configure redirect URIs:
   - `https://photos.isago.ch/signin-oidc` (production)
   - `http://localhost:8080/signin-oidc` (development)
4. Note Client ID and Client Secret
5. Configure in application settings

### API Authorization

#### Controller-Level Authorization

```csharp
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DuplicatesController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.DuplicatesView)]
    public async Task<ActionResult<PagedResponse<DuplicateGroupDto>>> GetAll() { }

    [HttpPost("{id}/validate")]
    [Authorize(Policy = Permissions.DuplicatesValidate)]
    public async Task<IActionResult> Validate(Guid id) { }

    [HttpDelete("{id}/non-originals")]
    [Authorize(Policy = Permissions.DuplicatesDelete)]
    public async Task<IActionResult> DeleteNonOriginals(Guid id) { }
}
```

#### Policy Configuration

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy(Permissions.DuplicatesView, policy =>
        policy.RequireClaim("permission", "duplicates.view"));

    options.AddPolicy(Permissions.DuplicatesValidate, policy =>
        policy.RequireClaim("permission", "duplicates.validate"));

    options.AddPolicy(Permissions.DuplicatesDelete, policy =>
        policy.RequireClaim("permission", "duplicates.delete"));
});
```

### Angular Integration

```typescript
// auth.config.ts
export const authConfig: AuthConfig = {
  issuer: 'https://login.infomaniak.com',
  clientId: environment.oidcClientId,
  redirectUri: window.location.origin + '/signin-oidc',
  postLogoutRedirectUri: window.location.origin,
  scope: 'openid profile email',
  responseType: 'code',
  showDebugInformation: !environment.production,
};

// auth.service.ts
@Injectable({ providedIn: 'root' })
export class AuthService {
  private oauthService = inject(OAuthService);

  readonly isAuthenticated = signal(false);
  readonly user = signal<User | null>(null);
  readonly permissions = signal<string[]>([]);

  async initAuth(): Promise<void> {
    this.oauthService.configure(authConfig);
    await this.oauthService.loadDiscoveryDocumentAndTryLogin();
    this.isAuthenticated.set(this.oauthService.hasValidAccessToken());
  }

  hasPermission(permission: string): boolean {
    return this.permissions().includes(permission);
  }
}
```

### UI Permission Guards

```typescript
// permission.guard.ts
export const permissionGuard = (permission: string): CanActivateFn => {
  return () => {
    const authService = inject(AuthService);
    if (authService.hasPermission(permission)) {
      return true;
    }
    return inject(Router).createUrlTree(['/unauthorized']);
  };
};

// Usage in routes
{
  path: 'settings',
  loadComponent: () => import('./settings.component'),
  canActivate: [permissionGuard('settings.view')]
}
```

### Permission-Based UI Rendering

```html
<!-- In templates -->
@if (authService.hasPermission('duplicates.delete')) {
  <button mat-raised-button color="warn" (click)="deleteNonOriginals()">
    Delete Duplicates
  </button>
}

<!-- Directive approach -->
<button mat-raised-button *hasPermission="'duplicates.delete'" (click)="delete()">
  Delete
</button>
```

### Database Schema

```sql
-- Users table
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    external_id VARCHAR(255) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL,
    display_name VARCHAR(255),
    avatar_url VARCHAR(500),
    is_active BOOLEAN NOT NULL DEFAULT true,
    last_login_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Groups table
CREATE TABLE groups (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL UNIQUE,
    description VARCHAR(500),
    is_external BOOLEAN NOT NULL DEFAULT false,
    external_id VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Roles table
CREATE TABLE roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL UNIQUE,
    description VARCHAR(500),
    priority INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Permissions table
CREATE TABLE permissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL UNIQUE,
    resource VARCHAR(50) NOT NULL,
    action VARCHAR(50) NOT NULL,
    description VARCHAR(500)
);

-- Junction tables
CREATE TABLE user_groups (
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    group_id UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    assigned_by UUID REFERENCES users(id),
    PRIMARY KEY (user_id, group_id)
);

CREATE TABLE group_roles (
    group_id UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    PRIMARY KEY (group_id, role_id)
);

CREATE TABLE role_permissions (
    role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permission_id UUID NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

-- Audit log
CREATE TABLE audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES users(id),
    action VARCHAR(100) NOT NULL,
    resource_type VARCHAR(100),
    resource_id VARCHAR(255),
    details JSONB,
    ip_address INET,
    user_agent VARCHAR(500),
    timestamp TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Indexes
CREATE INDEX idx_users_external_id ON users(external_id);
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_audit_log_user_id ON audit_log(user_id);
CREATE INDEX idx_audit_log_timestamp ON audit_log(timestamp DESC);
CREATE INDEX idx_audit_log_action ON audit_log(action);
```

### Audit Logging

Every significant action should be logged:

```csharp
public interface IAuditService
{
    Task LogAsync(AuditEntry entry);
}

public record AuditEntry(
    string Action,
    string? ResourceType = null,
    string? ResourceId = null,
    object? Details = null
);

// Usage
await _auditService.LogAsync(new AuditEntry(
    Action: "duplicates.delete",
    ResourceType: "DuplicateGroup",
    ResourceId: groupId.ToString(),
    Details: new { FilesDeleted = 3, SavedBytes = 1024000 }
));
```

## Consequences

### Positive

1. **Security**: Proper authentication and authorization protects sensitive data
2. **Auditability**: Complete audit trail of all actions
3. **Scalability**: Multi-user support enables team collaboration
4. **Flexibility**: RBAC allows fine-grained permission control
5. **SSO**: Single sign-on improves user experience
6. **Standards**: OIDC is a proven, secure standard

### Negative

1. **Complexity**: Significant implementation effort
2. **Dependencies**: Relies on Infomaniak availability
3. **Migration**: Existing deployments need migration strategy
4. **Testing**: Requires mock IDP for testing
5. **UX overhead**: Login flow adds friction

### Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| IDP unavailable | Implement token caching, graceful degradation |
| Token theft | Short token lifetime, secure storage, HTTPS only |
| Permission creep | Regular permission audits, least privilege |
| Over-complexity | Start with basic roles, expand as needed |

## Alternatives Considered

### 1. Built-in Authentication (ASP.NET Identity)

**Rejected because:**
- Requires managing passwords
- No SSO with existing enterprise identity
- More security responsibility

### 2. Other IDPs (Auth0, Okta, Keycloak)

**Rejected because:**
- Additional cost
- Not aligned with existing Infomaniak infrastructure
- More moving parts to maintain

### 3. Simple API Key Authentication

**Rejected because:**
- No user identity
- No fine-grained permissions
- Not suitable for multi-user scenarios

### 4. Basic HTTP Authentication

**Rejected because:**
- Credentials sent with every request
- No token-based session management
- Poor UX

## References

- [Infomaniak Developer Portal](https://developer.infomaniak.com/getting-started)
- [Infomaniak Connect for OpenID](https://github.com/Infomaniak/infomaniak-connect-openid)
- [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html)
- [ASP.NET Core Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)
- [angular-oauth2-oidc](https://github.com/manfredsteyer/angular-oauth2-oidc)
- [OWASP Authorization Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html)
