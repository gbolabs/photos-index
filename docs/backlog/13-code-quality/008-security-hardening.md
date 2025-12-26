# 008: Security Hardening

**Status**: 🔲 Not Started
**Priority**: P1 (High Priority)
**Agent**: A1/A4
**Branch**: `feature/code-quality-security`
**Estimated Complexity**: High

## Objective

Implement comprehensive security measures including authentication, authorization, input validation, and protection against common vulnerabilities.

## Dependencies

None - critical security improvement

## Problem Statement

Current security gaps:
- No authentication/authorization implemented
- Limited input validation on DTOs/requests
- No rate limiting for batch operations
- File path validation could be strengthened (directory traversal)
- No file size limits for uploads

## Acceptance Criteria

### Authentication & Authorization
- [ ] Implement JWT or API key authentication
- [ ] Add authorization policies (admin vs. user)
- [ ] Protect sensitive endpoints
- [ ] Configure CORS properly

### Input Validation
- [ ] Add DataAnnotations to all DTOs/requests
- [ ] Validate file paths (no directory traversal)
- [ ] Validate file sizes (max limits)
- [ ] Sanitize user inputs

### Rate Limiting
- [ ] Add rate limiting middleware
- [ ] Limit batch operations (max batch size)
- [ ] Throttle expensive operations

### Additional Security
- [ ] Add security headers (HSTS, CSP, X-Frame-Options)
- [ ] Implement request size limits
- [ ] Add API versioning with deprecation
- [ ] Secure sensitive configuration data

## Implementation Plan

### 1. Authentication (JWT)

**appsettings.json:**
```json
{
  "Authentication": {
    "Jwt": {
      "SecretKey": "${JWT_SECRET_KEY}",
      "Issuer": "photos-index-api",
      "Audience": "photos-index-web",
      "ExpirationMinutes": 60
    }
  }
}
```

**Program.cs:**
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Authentication:Jwt:Issuer"],
            ValidAudience = builder.Configuration["Authentication:Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Authentication:Jwt:SecretKey"]))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
});

// ...

app.UseAuthentication();
app.UseAuthorization();
```

**Controller Protection:**
```csharp
[Authorize]
[ApiController]
[Route("api/files")]
public class IndexedFilesController : ControllerBase
{
    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        // ...
    }
}
```

### 2. Input Validation

**Add DataAnnotations:**
```csharp
public record CreateScanDirectoryRequest
{
    [Required]
    [MaxLength(1000)]
    [RegularExpression(@"^[a-zA-Z]:\\[\w\s\-\\]+$|^/[\w\s\-/]+$", 
        ErrorMessage = "Invalid path format")]
    public required string Path { get; init; }
    
    public bool IncludeSubdirectories { get; init; } = true;
}
```

**Path Validation Service:**
```csharp
public class PathValidator : IPathValidator
{
    public bool IsValidPath(string path)
    {
        // Check for directory traversal
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(path, StringComparison.OrdinalIgnoreCase))
            return false;
            
        // Check for invalid characters
        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return false;
            
        return true;
    }
}
```

### 3. Rate Limiting

**Install Package:**
```bash
dotnet add package AspNetCoreRateLimit
```

**Configure:**
```csharp
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "POST:/api/files/batch",
            Limit = 10,
            Period = "1m"
        },
        new RateLimitRule
        {
            Endpoint = "*",
            Limit = 100,
            Period = "1m"
        }
    };
});
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

app.UseIpRateLimiting();
```

### 4. Security Headers

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "no-referrer");
    
    if (context.Request.IsHttps)
    {
        context.Response.Headers.Add("Strict-Transport-Security", 
            "max-age=31536000; includeSubDomains");
    }
    
    await next();
});
```

### 5. File Size Limits

```csharp
public record BatchIngestFilesRequest
{
    [Required]
    [MaxLength(1000, ErrorMessage = "Maximum 1000 files per batch")]
    [MinLength(1, ErrorMessage = "At least one file required")]
    public required List<IngestFileRequest> Files { get; init; }
}

public record IngestFileRequest
{
    [Range(1, 5L * 1024 * 1024 * 1024, 
        ErrorMessage = "File size must be between 1 byte and 5GB")]
    public long FileSize { get; init; }
}
```

## Files to Create/Modify

```
src/Api/
├── Authentication/
│   ├── JwtService.cs (new)
│   └── IAuthenticationService.cs (new)
├── Middleware/
│   └── SecurityHeadersMiddleware.cs (new)
├── Validators/
│   ├── IPathValidator.cs (new)
│   └── PathValidator.cs (new)
└── Program.cs (modify)

src/Shared/
├── Requests/ (add DataAnnotations to all)
└── Dtos/ (add DataAnnotations to all)

appsettings.json (add security config)
```

## Security Checklist

Reference: [OWASP Top 10](https://owasp.org/www-project-top-ten/)

- [ ] A01: Broken Access Control → Authentication & Authorization
- [ ] A02: Cryptographic Failures → HTTPS, secure secrets
- [ ] A03: Injection → Input validation, parameterized queries (already done with EF)
- [ ] A04: Insecure Design → Security by design patterns
- [ ] A05: Security Misconfiguration → Security headers, proper config
- [ ] A06: Vulnerable Components → Keep dependencies updated
- [ ] A07: Authentication Failures → Strong JWT implementation
- [ ] A08: Software and Data Integrity → File hash validation (already done)
- [ ] A09: Logging Failures → Structured logging (already done)
- [ ] A10: SSRF → Validate external URLs if any

## Testing

Create security tests:
```csharp
public class SecurityTests
{
    [Fact]
    public async Task UnauthorizedRequest_Returns401()
    {
        // Test without auth token
    }
    
    [Fact]
    public async Task DirectoryTraversal_IsBlocked()
    {
        // Test path like "../../../etc/passwd"
    }
    
    [Fact]
    public async Task RateLimit_IsEnforced()
    {
        // Test exceeding rate limit
    }
}
```

## Documentation

Update README.md:
- How to configure authentication
- How to generate JWT tokens
- Environment variables for secrets

## Environment Variables

```bash
# Required for production
JWT_SECRET_KEY=<generate-strong-key>
ASPNETCORE_URLS=https://+:443;http://+:80
ASPNETCORE_Kestrel__Certificates__Default__Path=/app/cert.pfx
ASPNETCORE_Kestrel__Certificates__Default__Password=${CERT_PASSWORD}
```

## Benefits

- **Protection**: Guards against common attacks
- **Compliance**: Meets basic security requirements
- **Trust**: Users can trust the application
- **Audit**: Authentication enables tracking

## Related Tasks

- `02-api-layer/004-api-versioning.md` - API versioning
- `13-code-quality/001-static-analysis-configuration.md` - Could add security analyzers

## References

- [ASP.NET Core Security](https://docs.microsoft.com/en-us/aspnet/core/security/)
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [JWT Best Practices](https://tools.ietf.org/html/rfc8725)

## Completion Checklist

- [ ] Implement JWT authentication
- [ ] Add authorization policies
- [ ] Protect all endpoints appropriately
- [ ] Add input validation to all DTOs
- [ ] Implement path validation
- [ ] Add rate limiting
- [ ] Add security headers middleware
- [ ] Add file size limits
- [ ] Configure CORS properly
- [ ] Create security tests
- [ ] Document authentication setup
- [ ] Security audit/review
- [ ] All tests passing
- [ ] PR created and reviewed
