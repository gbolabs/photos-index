# TODO: Convert to Central Package Management + CI Improvements

## Current State
- **CI**: All passing (Backend, Frontend, Docker)
- **Package management**: Using `Directory.Build.props` with MSBuild properties
- **Workflows**: Single `ci.yml` handles both PRs and main pushes

---

## Task 1: Convert to NuGet Central Package Management (CPM)

### Create `Directory.Packages.props` (root level)

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Microsoft -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />

    <!-- Database -->
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />

    <!-- OpenTelemetry -->
    <PackageVersion Include="OpenTelemetry.Exporter.Console" Version="1.11.2" />
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.11.2" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.11.2" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.11.1" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.11.1" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Runtime" Version="1.11.1" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Process" Version="1.14.0-beta.2" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.14.0-beta.2" />

    <!-- Other -->
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="10.0.0" />
    <PackageVersion Include="SixLabors.ImageSharp" Version="3.1.6" />

    <!-- Testing -->
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
    <PackageVersion Include="FluentAssertions" Version="7.0.0" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="Testcontainers" Version="4.3.0" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.3.0" />
  </ItemGroup>
</Project>
```

### Simplify `Directory.Build.props`

```xml
<Project>
  <PropertyGroup>
    <NoWarn>$(NoWarn);NU1902;NU1903</NoWarn>
  </PropertyGroup>
</Project>
```

### Update all .csproj files

Remove `Version` attributes from all `<PackageReference>` elements.

### Update Dockerfiles

Add `COPY Directory.Packages.props ./` alongside `Directory.Build.props` in:
- `deploy/docker/api/Dockerfile`
- `deploy/docker/indexing-service/Dockerfile`
- `deploy/docker/cleaner-service/Dockerfile`

---

## Task 2: Split CI Workflows

### Create `.github/workflows/pr.yml`

Fast checks for PRs:
- Trigger: `pull_request` to main
- Jobs: Backend tests, Frontend tests (parallel)
- No Docker builds

### Rename `ci.yml` → `.github/workflows/main.yml`

Full build on main:
- Trigger: `push` to main only
- Jobs: Backend tests, Frontend tests, Docker builds
- Remove PR trigger and conditional logic

### Update branch protection

Update required status checks to match new workflow job names.

---

## Files to Modify

| File | Action |
|------|--------|
| `Directory.Packages.props` | CREATE |
| `Directory.Build.props` | SIMPLIFY |
| `src/Api/Api.csproj` | Remove versions |
| `src/Database/Database.csproj` | Remove versions |
| `src/Shared/Shared.csproj` | Remove versions |
| `src/IndexingService/IndexingService.csproj` | Remove versions |
| `src/CleanerService/CleanerService.csproj` | Remove versions |
| `tests/Api.Tests/Api.Tests.csproj` | Remove versions |
| `tests/Database.Tests/Database.Tests.csproj` | Remove versions |
| `tests/IndexingService.Tests/IndexingService.Tests.csproj` | Remove versions |
| `tests/CleanerService.Tests/CleanerService.Tests.csproj` | Remove versions |
| `tests/Integration.Tests/Integration.Tests.csproj` | Remove versions |
| `deploy/docker/api/Dockerfile` | Add Directory.Packages.props |
| `deploy/docker/indexing-service/Dockerfile` | Add Directory.Packages.props |
| `deploy/docker/cleaner-service/Dockerfile` | Add Directory.Packages.props |
| `.github/workflows/pr.yml` | CREATE |
| `.github/workflows/main.yml` | RENAME from ci.yml |

---

## Execution Steps

1. Create `Directory.Packages.props` with all package versions
2. Simplify `Directory.Build.props` (remove version properties)
3. Update each csproj to remove Version attributes
4. Update Dockerfiles to copy `Directory.Packages.props`
5. Create `.github/workflows/pr.yml` (PR checks only)
6. Rename `ci.yml` → `main.yml` and simplify (push to main only)
7. Update branch protection to use new workflow job names
8. Test build locally: `dotnet build src/PhotosIndex.sln`
9. Commit and push
10. Verify CI passes
