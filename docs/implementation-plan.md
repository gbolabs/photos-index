# Implementation Plan for Picture File Indexing and Deduplication

This document outlines the detailed implementation plan, task breakdown, and parallel development strategy for the Docker-based picture file indexing and deduplication application.

## Project Overview

### Objectives
- Create a containerized application to index picture files
- Identify and remove duplicate files efficiently
- Provide web interface for duplicate management
- Optimize for Synology NAS deployment

### Key Features
- File scanning with hash computation for deduplication
- REST API for data management
- PostgreSQL database for efficient storage
- Angular 21 web interface
- Cleaner service for safe file removal

## Project Structure

### Repository Organization

The project will follow a clear modular structure to support both Docker (Synology NAS) and Podman/Kubernetes (local development) environments:

```
photos-index/
├── .github/
│   └── workflows/          # GitHub CI/CD pipelines
├── docs/                   # Documentation
├── src/
│   ├── api/                # API Service (ASP.NET Core)
│   │   ├── controllers/
│   │   ├── models/
│   │   ├── services/
│   │   └── tests/
│   ├── indexing-service/   # Indexing Service (.NET Console)
│   │   ├── core/
│   │   ├── file-processors/
│   │   ├── hash-algorithms/
│   │   └── tests/
│   ├── database/           # Database Service (PostgreSQL)
│   │   ├── migrations/
│   │   ├── models/
│   │   └── tests/
│   ├── web/                # Web Interface (Angular 21)
│   │   ├── src/
│   │   └── tests/
│   ├── cleaner-service/    # Cleaner Service (.NET)
│   │   ├── core/
│   │   └── tests/
│   └── shared/             # Shared libraries and utilities
├── deploy/
│   ├── docker/             # Docker configurations (for Synology NAS)
│   │   ├── api/
│   │   ├── indexing-service/
│   │   ├── database/
│   │   ├── web/
│   │   ├── cleaner-service/
│   │   └── docker-compose.yml
│   └── kubernetes/         # Kubernetes configurations (for local dev)
│       ├── api/
│       ├── indexing-service/
│       ├── database/
│       ├── web/
│       ├── cleaner-service/
│       └── kustomization.yaml
├── tests/                  # Integration and E2E tests
│   ├── integration/
│   └── e2e/
└── .gitignore
```

### Environment-Specific Configurations

#### Docker (Synology NAS Production)
- Uses `deploy/docker/docker-compose.yml`
- Standard Docker networking
- Optimized for NAS resource constraints
- Persistent volume configuration

#### Podman/Kubernetes (Local Development)
- Uses `deploy/kubernetes/` configurations
- Podman-compatible container definitions
- Kubernetes deployment manifests
- Local development optimizations
- Ephemeral testing environments

## Implementation Phases

### Phase 1: Foundation and CI/CD Setup (Week 1-2)

#### Tasks
- [ ] Set up GitHub repository with proper structure
- [ ] Create initial project scaffolding according to above structure
- [ ] Implement GitHub Actions CI pipeline with multi-environment support
- [ ] Configure test coverage reporting for all components
- [ ] Set up Docker Compose for Synology NAS deployment
- [ ] Set up Kubernetes manifests for local Podman development
- [ ] Create basic test frameworks for all components
- [ ] Implement environment detection in CI pipeline

#### Deliverables
- Functional CI pipeline with quality gates
- Project structure with separate service folders as defined
- Basic test frameworks for unit and integration tests
- Docker Compose configuration for production (Synology NAS)
- Kubernetes manifests for local development (Podman)
- Environment-aware build and test scripts

### Phase 2: Core Services Development (Week 3-6)

#### Parallel Development Streams

**Stream A: Indexing Service (Backend Team)**
- [ ] Implement file system scanning in `src/indexing-service/core/`
- [ ] Develop hash computation algorithms in `src/indexing-service/hash-algorithms/`
- [ ] Create metadata extraction functionality in `src/indexing-service/file-processors/`
- [ ] Implement change detection logic with timestamp/hash comparison
- [ ] Build duplicate identification system using computed hashes
- [ ] Add progress reporting to API
- [ ] Write unit tests in `src/indexing-service/tests/`

**Stream B: API Service (Backend Team)**
- [ ] Design REST API endpoints in `src/api/controllers/`
- [ ] Implement data ingestion from indexing service in `src/api/services/`
- [ ] Create duplicate handling endpoints with proper error handling
- [ ] Develop directory configuration API with validation
- [ ] Add health check and monitoring endpoints
- [ ] Implement pagination support for large datasets
- [ ] Write unit and integration tests in `src/api/tests/`

**Stream C: Database Service (Backend Team)**
- [ ] Design PostgreSQL schema in `src/database/models/`
- [ ] Implement data access layer with repository pattern
- [ ] Create database migrations in `src/database/migrations/`
- [ ] Implement indexing for performance-critical queries
- [ ] Develop transaction management for batch operations
- [ ] Implement data integrity checks and constraints
- [ ] Write tests in `src/database/tests/`

**Stream D: Web Interface (Frontend Team)**
- [ ] Set up Angular 21 project structure in `src/web/`
- [ ] Create basic UI components with Angular Material
- [ ] Implement search and filter functionality with reactive forms
- [ ] Develop duplicate management interface with confirmation dialogs
- [ ] Build progress visualization with real-time updates
- [ ] Create statistics dashboard with charts and metrics
- [ ] Write component tests in `src/web/tests/`

### Phase 3: Advanced Features (Week 7-8)

#### Tasks
- [ ] Implement cleaner service with safety mechanisms in `src/cleaner-service/core/`
- [ ] Develop file removal confirmation workflow with multi-step verification
- [ ] Create backup and recovery system with transaction logging
- [ ] Implement similarity search using vector representations (optional)
- [ ] Add vector representation for images in `src/indexing-service/file-processors/`
- [ ] Develop performance optimization for large directories
- [ ] Implement memory management with streaming processing
- [ ] Write comprehensive tests for cleaner service in `src/cleaner-service/tests/`

### Phase 4: Integration and Testing (Week 9-10)

#### Tasks
- [ ] Integrate all services using shared libraries in `src/shared/`
- [ ] Develop end-to-end tests in `tests/e2e/`
- [ ] Perform integration testing between services
- [ ] Optimize Docker configurations in `deploy/docker/`
- [ ] Optimize Kubernetes manifests in `deploy/kubernetes/`
- [ ] Fix bugs and issues identified in testing
- [ ] Write comprehensive documentation in `docs/`
- [ ] Create user guides and technical documentation
- [ ] Finalize CI/CD pipeline with multi-environment support

## Parallel Development Strategy

### Team Structure

**Backend Team (3 agents)**
- Agent 1: Indexing Service + File System Operations
- Agent 2: API Service + REST Endpoints
- Agent 3: Database Service + Schema Design

**Frontend Team (2 agents)**
- Agent 1: Angular Application Structure
- Agent 2: UI Components and Duplicate Management

**DevOps Team (1 agent)**
- CI/CD Pipeline Setup
- Docker Configuration
- Infrastructure Management

**QA Team (2 agents)**
- Test Framework Development
- Automated Testing
- Performance Testing

### Communication Protocol
- Daily stand-up reports in project management system
- Weekly integration meetings
- Shared documentation updates
- Continuous code reviews

## Testing Strategy

### Test Coverage Requirements
- **Core Services**: 80% minimum coverage (enforced in CI)
- **API Service**: 85% minimum coverage (enforced in CI)
- **Web Interface**: 70% minimum coverage (enforced in CI)
- **Integration Tests**: 90% coverage of critical paths
- **E2E Tests**: Full workflow validation

### Test Types and Locations
1. **Unit Tests**: Individual functions and components
   - Location: `src/*/tests/` directories
   - Framework: xUnit for .NET components
   - Execution: Part of CI pipeline
   - Coverage: Minimum 80% for backend, 70% for frontend

2. **Integration Tests**: Service-to-service communication
   - Location: `tests/integration/`
   - Framework: xUnit with TestContainers for containerized services
   - Execution: Nightly CI runs
   - Scope: API-Database, API-Indexing Service, Web-API interactions
   - Key Scenarios:
     - Indexing service uploading data to API
     - API querying database for duplicates
     - Web interface calling API endpoints
     - Cleaner service coordinating with database

3. **End-to-End Tests**: Complete workflow validation
   - Location: `tests/e2e/`
   - Framework: Playwright for web UI, xUnit for backend workflows
   - Execution: Pre-deployment gate and scheduled runs
   - Key Workflows:
     - Full indexing cycle: Scan → Hash → Store → Identify Duplicates
     - Duplicate management: Detection → Review → Removal
     - Configuration: Directory setup → Indexing → Results viewing

4. **Performance Tests**: Memory and speed optimization
   - Location: `tests/performance/`
   - Framework: BenchmarkDotNet for .NET, custom scripts for system-level
   - Execution: Weekly performance runs
   - Key Metrics:
     - Indexing speed (files/second)
     - Memory usage with large directories
     - Database query performance
     - API response times

5. **Regression Tests**: Prevent reintroduced bugs
   - Location: Integrated with unit tests
   - Framework: xUnit with historical test data
   - Execution: Every CI run
   - Focus Areas:
     - Hash computation consistency
     - Duplicate detection accuracy
     - File system operations reliability

### CI Pipeline Components
- **Build Verification**: Compile all services and components using .NET CLI and Angular CLI
- **Unit Test Execution**: Run all xUnit tests with coverage reporting
- **Integration Test Execution**: Run xUnit integration tests with TestContainers
- **E2E Test Execution**: Run Playwright tests for web interface
- **Test Coverage Reporting**: Generate HTML/XML reports, enforce minimums (80%+)
- **Container Image Building**: Build Docker images for production, Podman images for development
- **Multi-Environment Testing**: Test both Docker Compose and Kubernetes configurations
- **Security Scanning**: Vulnerability detection using Trivy or Snyk
- **Quality Gate Enforcement**: Block merges on test failures or coverage below thresholds

### Integration Test Implementation Ideas

#### TestContainers Approach
```csharp
// Example integration test using TestContainers and xUnit
public class ApiDatabaseIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlContainer();
    private HttpClient _apiClient;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // Initialize API with test database connection
        _apiClient = new HttpClient() { BaseAddress = new Uri("http://localhost:5000") };
    }

    [Fact]
    public async Task IndexingService_CanUploadData_ToApiAndDatabase()
    {
        // Arrange
        var testFile = new FileData { 
            Name = "test.jpg", 
            Hash = "abc123",
            Size = 1024
        };

        // Act
        var response = await _apiClient.PostAsJsonAsync("/api/files", testFile);

        // Assert
        response.EnsureSuccessStatusCode();
        var storedFile = await _apiClient.GetFromJsonAsync<FileData>("/api/files/abc123");
        Assert.Equal("test.jpg", storedFile.Name);
    }

    public async Task DisposeAsync()
    {
        await _postgres.StopAsync();
    }
}
```

#### Key Integration Test Scenarios
1. **Indexing → API → Database Flow**
   - Test file data upload from indexing service to API
   - Verify database persistence
   - Test duplicate detection logic

2. **API → Web Interface Communication**
   - Test REST API endpoints
   - Verify data serialization/deserialization
   - Test error handling and validation

3. **Cleaner Service Coordination**
   - Test file removal workflows
   - Verify database consistency after cleanup
   - Test safety mechanisms and rollback

4. **Cross-Service Duplicate Detection**
   - Test hash computation consistency
   - Verify duplicate identification across services
   - Test conflict resolution

### Playwright E2E Test Examples

```csharp
// Example Playwright test for duplicate management workflow
[Fact]
public async Task DuplicateManagement_CompleteWorkflow()
{
    // Launch browser
    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync();
    var page = await browser.NewPageAsync();

    // Login and navigate to duplicates page
    await page.GotoAsync("http://localhost:4200/login");
    await page.FillAsync("#username", "admin");
    await page.FillAsync("#password", "password");
    await page.ClickAsync("#login-button");
    await page.WaitForURLAsync("http://localhost:4200/dashboard");
    await page.ClickAsync("#duplicates-link");

    // Verify duplicate detection
    await page.WaitForSelectorAsync(".duplicate-group");
    var duplicateCount = await page.Locator(".duplicate-group").CountAsync();
    Assert.True(duplicateCount > 0, "Should find duplicates");

    // Test duplicate resolution
    await page.ClickAsync(".duplicate-group:first-child .keep-button");
    await page.ClickAsync(".duplicate-group:first-child .remove-button");
    await page.ClickAsync("#confirm-removal");

    // Verify results
    await page.WaitForSelectorAsync(".success-message");
    var remainingDuplicates = await page.Locator(".duplicate-group").CountAsync();
    Assert.True(remainingDuplicates < duplicateCount, "Should have fewer duplicates after resolution");
}
```

### Environment-Specific Testing
- **Docker Tests**: Run using `deploy/docker/docker-compose.yml`
- **Kubernetes Tests**: Run using `deploy/kubernetes/` manifests
- **Cross-Environment Validation**: Ensure compatibility

## Task Breakdown and Timeline

### Week 1-2: Foundation
- Day 1-2: Project structure setup
- Day 3-5: CI pipeline implementation
- Day 6-7: Docker Compose configuration
- Day 8-10: Test framework development

### Week 3-4: Core Services (Parallel)
- **Indexing Service**: File scanning, hash computation
- **API Service**: Basic endpoints, data models
- **Database**: Schema design, basic queries
- **Web Interface**: Angular setup, basic components

### Week 5-6: Feature Completion
- **Indexing Service**: Metadata extraction, duplicate detection
- **API Service**: Advanced endpoints, error handling
- **Database**: Performance optimization, transactions
- **Web Interface**: Duplicate management, search filters

### Week 7-8: Advanced Features
- Cleaner service implementation
- Safety mechanisms for file removal
- Optional features (similarity search)
- Performance optimization

### Week 9-10: Integration and Testing
- Service integration
- End-to-end testing
- Bug fixing
- Documentation
- Final quality assurance

## Risk Management

### Potential Risks
1. **Integration Issues**: Services not communicating properly
2. **Performance Problems**: Memory usage with large datasets
3. **Test Coverage Gaps**: Critical paths not adequately tested
4. **Dependency Conflicts**: Version mismatches between services

### Mitigation Strategies
1. Early integration testing
2. Performance testing from day one
3. Comprehensive test planning
4. Dependency management system

## Success Criteria

### Technical Success
- All services containerized and functional
- 80%+ test coverage across all components
- CI pipeline with quality gates
- Successful duplicate identification and removal
- Memory-optimized for large datasets

### Project Success
- On-time delivery according to timeline
- All mandatory features implemented
- Comprehensive documentation
- Ready for Synology NAS deployment
- User-friendly interface for duplicate management

## Next Steps

1. Assign tasks to development teams
2. Set up project management tools
3. Begin Phase 1 implementation
4. Establish daily stand-up routine
5. Monitor progress and adjust timeline as needed