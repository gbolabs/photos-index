# Authentication & Authorization Implementation Plan

**Status**: Proposed
**Date**: 2025-12-31
**ADR**: [ADR-015](../adrs/015-authentication-authorization.md)

## Roadmap Overview

```
v0.10.0 (Current - No Auth)
    │
    ▼
v0.15.0 ─── Phase 1: Authentication Foundation
    │       └── OIDC integration, user sync, token management
    │
    ▼
v0.16.0 ─── Phase 2: Authorization Core
    │       └── RBAC schema, permission service, API protection
    │
    ▼
v0.17.0 ─── Phase 3: Group Integration
    │       └── Group management, role assignment, admin UI
    │
    ▼
v0.18.0 ─── Phase 4: Audit & Polish
            └── Audit logging, security hardening, documentation
```

---

## Phase 1: Authentication Foundation

**Version**: v0.15.0
**Effort**: 30-40 hours
**Dependencies**: Infomaniak OAuth2 app configuration

### Tasks

| ID | Task | Effort | Component |
|----|------|--------|-----------|
| AUTH-001 | Configure Infomaniak OAuth2 application | 2h | DevOps |
| AUTH-002 | Add OIDC packages to .NET API | 2h | Backend |
| AUTH-003 | Implement authentication middleware | 8h | Backend |
| AUTH-004 | Create User entity and migration | 4h | Backend |
| AUTH-005 | Implement user sync from OIDC claims | 4h | Backend |
| AUTH-006 | Add angular-oauth2-oidc to frontend | 2h | Frontend |
| AUTH-007 | Implement AuthService | 6h | Frontend |
| AUTH-008 | Create login/logout UI | 4h | Frontend |
| AUTH-009 | Add HTTP interceptor for Bearer token | 2h | Frontend |
| AUTH-010 | Implement token refresh flow | 4h | Both |
| AUTH-011 | Add authentication tests | 4h | Both |

### Detailed Implementation

#### AUTH-001: Configure Infomaniak OAuth2 Application

1. Access [Infomaniak Manager](https://manager.infomaniak.com/v3/ng/products/cloud/ik-auth)
2. Create new OAuth2 application:
   - Name: `Photos Index`
   - Description: `Photo deduplication application`
   - Redirect URIs:
     - `https://photos.isago.ch/signin-oidc`
     - `http://localhost:8080/signin-oidc`
     - `http://localhost:4200/signin-oidc`
   - Post-logout URIs:
     - `https://photos.isago.ch`
     - `http://localhost:8080`
     - `http://localhost:4200`
3. Note Client ID and Client Secret
4. Store in secrets management (Kubernetes secrets / environment variables)

#### AUTH-002 & AUTH-003: .NET OIDC Integration

```csharp
// Program.cs
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(options =>
{
    options.Authority = builder.Configuration["Oidc:Authority"];
    options.ClientId = builder.Configuration["Oidc:ClientId"];
    options.ClientSecret = builder.Configuration["Oidc:ClientSecret"];
    options.ResponseType = "code";
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.Scope.Add("email");
    options.Scope.Add("profile");

    options.Events = new OpenIdConnectEvents
    {
        OnTokenValidated = async context =>
        {
            var userService = context.HttpContext.RequestServices
                .GetRequiredService<IUserSyncService>();
            await userService.SyncUserAsync(context.Principal!);
        }
    };
});

// For API (JWT Bearer)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Oidc:Authority"];
        options.Audience = builder.Configuration["Oidc:ClientId"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true
        };
    });
```

#### AUTH-004: User Entity

```csharp
// Database/Entities/User.cs
public class User
{
    public Guid Id { get; set; }
    public required string ExternalId { get; set; }
    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();
}

// Migration
migrationBuilder.CreateTable(
    name: "Users",
    columns: table => new
    {
        Id = table.Column<Guid>(nullable: false),
        ExternalId = table.Column<string>(maxLength: 255, nullable: false),
        Email = table.Column<string>(maxLength: 255, nullable: false),
        DisplayName = table.Column<string>(maxLength: 255, nullable: true),
        AvatarUrl = table.Column<string>(maxLength: 500, nullable: true),
        IsActive = table.Column<bool>(nullable: false, defaultValue: true),
        LastLoginAt = table.Column<DateTime>(nullable: true),
        CreatedAt = table.Column<DateTime>(nullable: false),
        UpdatedAt = table.Column<DateTime>(nullable: false)
    },
    constraints: table => table.PrimaryKey("PK_Users", x => x.Id));

migrationBuilder.CreateIndex("IX_Users_ExternalId", "Users", "ExternalId", unique: true);
migrationBuilder.CreateIndex("IX_Users_Email", "Users", "Email");
```

#### AUTH-005: User Sync Service

```csharp
public interface IUserSyncService
{
    Task<User> SyncUserAsync(ClaimsPrincipal principal);
}

public class UserSyncService : IUserSyncService
{
    private readonly PhotosDbContext _context;

    public UserSyncService(PhotosDbContext context)
    {
        _context = context;
    }

    public async Task<User> SyncUserAsync(ClaimsPrincipal principal)
    {
        var externalId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("No subject claim");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.ExternalId == externalId);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                ExternalId = externalId,
                Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? "",
                DisplayName = principal.FindFirst("name")?.Value,
                AvatarUrl = principal.FindFirst("picture")?.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
        }
        else
        {
            user.Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? user.Email;
            user.DisplayName = principal.FindFirst("name")?.Value ?? user.DisplayName;
            user.AvatarUrl = principal.FindFirst("picture")?.Value ?? user.AvatarUrl;
            user.UpdatedAt = DateTime.UtcNow;
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return user;
    }
}
```

#### AUTH-006 to AUTH-009: Angular Implementation

```typescript
// environment.ts
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000',
  oidc: {
    issuer: 'https://login.infomaniak.com',
    clientId: 'your-client-id',
    redirectUri: 'http://localhost:4200/signin-oidc',
    postLogoutRedirectUri: 'http://localhost:4200',
    scope: 'openid profile email',
  }
};

// auth.config.ts
import { AuthConfig } from 'angular-oauth2-oidc';
import { environment } from '../environments/environment';

export const authConfig: AuthConfig = {
  issuer: environment.oidc.issuer,
  clientId: environment.oidc.clientId,
  redirectUri: environment.oidc.redirectUri,
  postLogoutRedirectUri: environment.oidc.postLogoutRedirectUri,
  scope: environment.oidc.scope,
  responseType: 'code',
  showDebugInformation: !environment.production,
  requireHttps: environment.production,
};

// auth.service.ts
@Injectable({ providedIn: 'root' })
export class AuthService {
  private oauthService = inject(OAuthService);
  private router = inject(Router);

  readonly isAuthenticated = signal(false);
  readonly user = signal<UserProfile | null>(null);
  readonly loading = signal(true);

  async initAuth(): Promise<void> {
    this.oauthService.configure(authConfig);

    try {
      await this.oauthService.loadDiscoveryDocumentAndTryLogin();
      this.updateAuthState();

      // Handle token refresh
      this.oauthService.events.subscribe(event => {
        if (event.type === 'token_received') {
          this.updateAuthState();
        }
      });
    } finally {
      this.loading.set(false);
    }
  }

  private updateAuthState(): void {
    const hasToken = this.oauthService.hasValidAccessToken();
    this.isAuthenticated.set(hasToken);

    if (hasToken) {
      const claims = this.oauthService.getIdentityClaims();
      this.user.set({
        id: claims['sub'],
        email: claims['email'],
        name: claims['name'],
        picture: claims['picture'],
      });
    } else {
      this.user.set(null);
    }
  }

  login(): void {
    this.oauthService.initLoginFlow();
  }

  logout(): void {
    this.oauthService.logOut();
    this.router.navigate(['/']);
  }

  getAccessToken(): string | null {
    return this.oauthService.getAccessToken();
  }
}

// auth.interceptor.ts
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getAccessToken();

  if (token && !req.url.includes('login.infomaniak.com')) {
    req = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  return next(req);
};

// app.config.ts
export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(withInterceptors([authInterceptor])),
    provideOAuthClient(),
    // ...
  ]
};
```

### Phase 1 Definition of Done

- [ ] Users can log in via Infomaniak
- [ ] Users are created/synced in database on first login
- [ ] API endpoints require authentication
- [ ] Tokens are refreshed automatically
- [ ] Logout works correctly
- [ ] All tests pass

---

## Phase 2: Authorization Core

**Version**: v0.16.0
**Effort**: 35-45 hours
**Dependencies**: Phase 1 complete

### Tasks

| ID | Task | Effort | Component |
|----|------|--------|-----------|
| AUTHZ-001 | Create RBAC database schema | 6h | Backend |
| AUTHZ-002 | Seed default roles and permissions | 2h | Backend |
| AUTHZ-003 | Implement PermissionService | 6h | Backend |
| AUTHZ-004 | Create authorization policy handlers | 6h | Backend |
| AUTHZ-005 | Add [Authorize] attributes to controllers | 4h | Backend |
| AUTHZ-006 | Create permissions endpoint | 2h | Backend |
| AUTHZ-007 | Implement frontend PermissionService | 4h | Frontend |
| AUTHZ-008 | Create hasPermission directive | 4h | Frontend |
| AUTHZ-009 | Add permission guards to routes | 4h | Frontend |
| AUTHZ-010 | Update UI with permission checks | 6h | Frontend |
| AUTHZ-011 | Add authorization tests | 4h | Both |

### Detailed Implementation

#### AUTHZ-001: RBAC Schema

```csharp
// Database/Entities/Group.cs
public class Group
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsExternal { get; set; }
    public string? ExternalId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<UserGroup> UserGroups { get; set; } = [];
    public ICollection<GroupRole> GroupRoles { get; set; } = [];
}

// Database/Entities/Role.cs
public class Role
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<GroupRole> GroupRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}

// Database/Entities/Permission.cs
public class Permission
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Resource { get; set; }
    public required string Action { get; set; }
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}

// Junction tables
public class UserGroup
{
    public Guid UserId { get; set; }
    public Guid GroupId { get; set; }
    public DateTime AssignedAt { get; set; }
    public Guid? AssignedBy { get; set; }

    public User User { get; set; } = null!;
    public Group Group { get; set; } = null!;
    public User? AssignedByUser { get; set; }
}

public class GroupRole
{
    public Guid GroupId { get; set; }
    public Guid RoleId { get; set; }

    public Group Group { get; set; } = null!;
    public Role Role { get; set; } = null!;
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
```

#### AUTHZ-002: Seed Data

```csharp
public static class AuthorizationSeeder
{
    public static async Task SeedAsync(PhotosDbContext context)
    {
        if (await context.Permissions.AnyAsync()) return;

        // Permissions
        var permissions = new[]
        {
            new Permission { Name = "files.view", Resource = "Files", Action = "Read" },
            new Permission { Name = "files.download", Resource = "Files", Action = "Read" },
            new Permission { Name = "files.hide", Resource = "Files", Action = "Write" },
            new Permission { Name = "duplicates.view", Resource = "Duplicates", Action = "Read" },
            new Permission { Name = "duplicates.select", Resource = "Duplicates", Action = "Write" },
            new Permission { Name = "duplicates.validate", Resource = "Duplicates", Action = "Write" },
            new Permission { Name = "duplicates.delete", Resource = "Duplicates", Action = "Delete" },
            new Permission { Name = "duplicates.undo", Resource = "Duplicates", Action = "Write" },
            new Permission { Name = "settings.view", Resource = "Settings", Action = "Read" },
            new Permission { Name = "settings.manage", Resource = "Settings", Action = "Write" },
            new Permission { Name = "indexing.view", Resource = "Indexing", Action = "Read" },
            new Permission { Name = "indexing.trigger", Resource = "Indexing", Action = "Write" },
            new Permission { Name = "indexing.cancel", Resource = "Indexing", Action = "Write" },
            new Permission { Name = "users.view", Resource = "Users", Action = "Read" },
            new Permission { Name = "users.manage", Resource = "Users", Action = "Write" },
            new Permission { Name = "groups.view", Resource = "Groups", Action = "Read" },
            new Permission { Name = "groups.manage", Resource = "Groups", Action = "Write" },
            new Permission { Name = "audit.view", Resource = "Audit", Action = "Read" },
        };

        context.Permissions.AddRange(permissions);

        // Roles with permission mappings
        var adminRole = new Role { Name = "System.Admin", Description = "Full access", Priority = 100 };
        var managerRole = new Role { Name = "Duplicates.Manager", Description = "Manage duplicates", Priority = 50 };
        var operatorRole = new Role { Name = "Duplicates.Operator", Description = "Process duplicates", Priority = 25 };
        var viewerRole = new Role { Name = "Files.Viewer", Description = "View only", Priority = 10 };

        context.Roles.AddRange(adminRole, managerRole, operatorRole, viewerRole);

        // Groups
        var groups = new[]
        {
            new Group { Name = "admins", Description = "System administrators" },
            new Group { Name = "managers", Description = "Duplicate management leads" },
            new Group { Name = "operators", Description = "Day-to-day operators" },
            new Group { Name = "viewers", Description = "Read-only access" },
        };

        context.Groups.AddRange(groups);
        await context.SaveChangesAsync();

        // Assign roles to groups
        context.GroupRoles.AddRange(
            new GroupRole { GroupId = groups[0].Id, RoleId = adminRole.Id },
            new GroupRole { GroupId = groups[1].Id, RoleId = managerRole.Id },
            new GroupRole { GroupId = groups[2].Id, RoleId = operatorRole.Id },
            new GroupRole { GroupId = groups[3].Id, RoleId = viewerRole.Id }
        );

        // Assign permissions to roles (see ADR for full mapping)
        // Admin gets all
        var allPermissionIds = permissions.Select(p => p.Id);
        context.RolePermissions.AddRange(
            allPermissionIds.Select(pid => new RolePermission { RoleId = adminRole.Id, PermissionId = pid })
        );

        // Manager gets specific permissions
        var managerPerms = permissions
            .Where(p => p.Name.StartsWith("files.") || p.Name.StartsWith("duplicates.") ||
                        p.Name == "settings.view" || p.Name == "indexing.view")
            .Select(p => new RolePermission { RoleId = managerRole.Id, PermissionId = p.Id });
        context.RolePermissions.AddRange(managerPerms);

        // ... similar for operator and viewer

        await context.SaveChangesAsync();
    }
}
```

#### AUTHZ-003: Permission Service

```csharp
public interface IPermissionService
{
    Task<IReadOnlyList<string>> GetUserPermissionsAsync(Guid userId);
    Task<bool> HasPermissionAsync(Guid userId, string permission);
}

public class PermissionService : IPermissionService
{
    private readonly PhotosDbContext _context;
    private readonly IMemoryCache _cache;

    public async Task<IReadOnlyList<string>> GetUserPermissionsAsync(Guid userId)
    {
        var cacheKey = $"permissions:{userId}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached))
            return cached!;

        var permissions = await _context.Users
            .Where(u => u.Id == userId)
            .SelectMany(u => u.UserGroups)
            .SelectMany(ug => ug.Group.GroupRoles)
            .SelectMany(gr => gr.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync();

        _cache.Set(cacheKey, permissions, TimeSpan.FromMinutes(5));
        return permissions;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permission)
    {
        var permissions = await GetUserPermissionsAsync(userId);
        return permissions.Contains(permission);
    }
}
```

#### AUTHZ-004: Policy Handlers

```csharp
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) => Permission = permission;
}

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null) return;

        var userId = await GetUserIdFromExternalId(userIdClaim.Value);
        if (userId == null) return;

        if (await _permissionService.HasPermissionAsync(userId.Value, requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}

// Registration
services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(permission, policy =>
            policy.Requirements.Add(new PermissionRequirement(permission)));
    }
});
services.AddScoped<IAuthorizationHandler, PermissionHandler>();
```

### Phase 2 Definition of Done

- [ ] RBAC schema created and migrated
- [ ] Default roles and permissions seeded
- [ ] API endpoints protected by permissions
- [ ] Frontend shows/hides elements based on permissions
- [ ] Route guards enforce permissions
- [ ] All tests pass

---

## Phase 3: Group Integration

**Version**: v0.17.0
**Effort**: 25-35 hours
**Dependencies**: Phase 2 complete

### Tasks

| ID | Task | Effort | Component |
|----|------|--------|-----------|
| GROUP-001 | Create group management API | 6h | Backend |
| GROUP-002 | Create user management API | 6h | Backend |
| GROUP-003 | Implement group sync from IDP | 4h | Backend |
| GROUP-004 | Create admin UI for users | 8h | Frontend |
| GROUP-005 | Create admin UI for groups | 8h | Frontend |
| GROUP-006 | Add role assignment UI | 4h | Frontend |
| GROUP-007 | Integration tests | 4h | Both |

### API Endpoints

```
GET    /api/admin/users               # List users
GET    /api/admin/users/{id}          # Get user details
PATCH  /api/admin/users/{id}          # Update user
DELETE /api/admin/users/{id}          # Deactivate user

GET    /api/admin/groups              # List groups
POST   /api/admin/groups              # Create group
GET    /api/admin/groups/{id}         # Get group details
PUT    /api/admin/groups/{id}         # Update group
DELETE /api/admin/groups/{id}         # Delete group

POST   /api/admin/groups/{id}/members       # Add user to group
DELETE /api/admin/groups/{id}/members/{uid} # Remove user from group

GET    /api/admin/groups/{id}/roles         # Get group roles
PUT    /api/admin/groups/{id}/roles         # Set group roles

GET    /api/admin/roles               # List roles
GET    /api/admin/permissions         # List permissions
```

---

## Phase 4: Audit & Polish

**Version**: v0.18.0
**Effort**: 20-30 hours
**Dependencies**: Phase 3 complete

### Tasks

| ID | Task | Effort | Component |
|----|------|--------|-----------|
| AUDIT-001 | Create audit log entity and API | 4h | Backend |
| AUDIT-002 | Implement audit logging interceptor | 6h | Backend |
| AUDIT-003 | Create audit log viewer UI | 6h | Frontend |
| AUDIT-004 | Security hardening review | 4h | Both |
| AUDIT-005 | Documentation | 4h | Docs |
| AUDIT-006 | Performance testing | 4h | Both |

### Audit Log Implementation

```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public required string Action { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public JsonDocument? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }

    public User? User { get; set; }
}

public interface IAuditService
{
    Task LogAsync(string action, string? resourceType = null, string? resourceId = null, object? details = null);
}

public class AuditService : IAuditService
{
    private readonly PhotosDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task LogAsync(string action, string? resourceType, string? resourceId, object? details)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var userId = GetCurrentUserId();

        var entry = new AuditLog
        {
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Details = details != null ? JsonDocument.Parse(JsonSerializer.Serialize(details)) : null,
            UserId = userId,
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow
        };

        _context.AuditLogs.Add(entry);
        await _context.SaveChangesAsync();
    }
}
```

---

## Security Considerations

### Checklist

- [ ] All API endpoints require authentication
- [ ] Tokens have appropriate lifetime (1 hour access, 24 hour refresh)
- [ ] HTTPS enforced in production
- [ ] CORS configured correctly
- [ ] CSP headers set
- [ ] Secure cookie settings (HttpOnly, Secure, SameSite)
- [ ] Rate limiting on auth endpoints
- [ ] No sensitive data in logs
- [ ] SQL injection prevention (parameterized queries)
- [ ] XSS prevention (output encoding)

### Token Configuration

```csharp
// Recommended settings
services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero, // No tolerance for expired tokens
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };
    });
```

---

## Testing Strategy

### Unit Tests

```csharp
public class PermissionServiceTests
{
    [Fact]
    public async Task GetUserPermissions_ReturnsAllPermissionsFromAllGroups()
    {
        // Arrange
        var user = CreateTestUser();
        user.UserGroups.Add(new UserGroup { Group = CreateGroupWithPermissions("files.view", "files.download") });
        user.UserGroups.Add(new UserGroup { Group = CreateGroupWithPermissions("duplicates.view") });

        // Act
        var permissions = await _service.GetUserPermissionsAsync(user.Id);

        // Assert
        permissions.Should().BeEquivalentTo(new[] { "files.view", "files.download", "duplicates.view" });
    }

    [Fact]
    public async Task HasPermission_ReturnsFalse_WhenUserNotInGroup()
    {
        var user = CreateTestUser(); // No groups

        var result = await _service.HasPermissionAsync(user.Id, "files.view");

        result.Should().BeFalse();
    }
}
```

### Integration Tests

```csharp
public class AuthorizationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetDuplicates_RequiresAuthentication()
    {
        var response = await _client.GetAsync("/api/duplicates");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteDuplicates_RequiresDeletePermission()
    {
        var token = await GetTokenForUserWithPermissions("duplicates.view"); // No delete

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.DeleteAsync("/api/duplicates/123/non-originals");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
```

---

## Migration Strategy

### For Existing Deployments

1. **Pre-migration**:
   - Backup database
   - Notify users of upcoming auth requirement
   - Create Infomaniak OAuth app

2. **Migration**:
   - Deploy new version with auth enabled
   - Run database migrations (creates auth tables)
   - Seed default roles and permissions
   - Create initial admin user manually

3. **Post-migration**:
   - Verify admin can log in
   - Add users to appropriate groups
   - Test permissions work correctly

### First Admin Setup

```sql
-- Manual SQL to bootstrap first admin
INSERT INTO "UserGroups" ("UserId", "GroupId", "AssignedAt")
SELECT u."Id", g."Id", NOW()
FROM "Users" u, "Groups" g
WHERE u."Email" = 'admin@isago.ch' AND g."Name" = 'admins';
```

---

## Summary

| Phase | Version | Tasks | Effort |
|-------|---------|-------|--------|
| 1 - Authentication | v0.15.0 | 11 | 30-40h |
| 2 - Authorization | v0.16.0 | 11 | 35-45h |
| 3 - Groups | v0.17.0 | 7 | 25-35h |
| 4 - Audit | v0.18.0 | 6 | 20-30h |
| **Total** | | **35** | **110-150h** |
