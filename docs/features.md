# Docker Application for Picture File Indexing and Deduplication

This document describes how to create a Docker application that indexes picture files from specified directories, with a primary focus on identifying and removing duplicate files. The application will scan directories for image files, create an index for quick access, and provide tools for duplicate management and removal.

## Objectives

### Implementation Strategy

This project will follow a test-first approach with parallel development by coding agents. The implementation will be structured in phases:

#### Phase 1: Foundation and CI/CD Setup
- Set up GitHub CI pipeline with test coverage reporting
- Create project structure with separate folders for each service
- Implement Docker Compose for local development and testing
- Write comprehensive test suites for all components

#### Phase 2: Core Services (Parallel Development)
- **Indexing Service**: File scanning, hash computation, metadata extraction
- **API Service**: Endpoints for data ingestion and duplicate handling
- **Database Service**: PostgreSQL schema and integration
- **Web Interface**: Basic UI with duplicate management

#### Phase 3: Advanced Features
- Cleaner service with safety mechanisms for file removal
- Similarity search using vector representations
- Performance optimization and memory management

#### Phase 4: Deployment Preparation
- Containerization of all services
- Integration testing
- Documentation and user guides

### Testing Strategy

#### Test-First Approach
- Write unit tests before implementation for all critical components
- Test coverage minimum: 80% for core services, 70% for UI
- Automated testing in CI pipeline

#### Test Types
- **Unit Tests**: Individual components and functions
- **Integration Tests**: Service-to-service communication
- **End-to-End Tests**: Complete workflow from indexing to duplicate removal
- **Performance Tests**: Memory usage and processing speed

#### CI Pipeline Components
- Build verification
- Unit test execution
- Integration test execution
- Test coverage reporting
- Container image building
- Security scanning

### Parallel Development Plan

The project will be developed in parallel by multiple coding agents:

1. **Backend Team** (Indexing Service + API Service)
   - File system scanning and hash computation
   - Database integration and schema design
   - REST API endpoints for data management

2. **Frontend Team** (Web Interface)
   - Angular 21 application structure
   - Duplicate management UI components
   - Progress visualization and statistics

3. **DevOps Team** (CI/CD + Infrastructure)
   - GitHub Actions pipeline setup
   - Docker Compose configuration
   - Test coverage and quality gates

4. **QA Team** (Testing)
   - Comprehensive test suite development
   - Test automation and reporting
   - Performance and memory testing

## Deployment

### Deployment Strategy

#### Initial Phase (CI Focus)
- Set up GitHub CI pipeline for continuous integration
- Implement automated testing and quality gates
- Containerize individual services for isolated testing
- Focus on test coverage and code quality

#### Future Phase (CD to Synology NAS)
- Research Synology NAS deployment options
- Implement continuous deployment pipeline
- Configure NAS-specific settings and optimizations
- Set up monitoring and logging for production

### Deployment Components
- Create containerized application hosting an image indexing service.
- Create containerized API to receive indexed data from the indexing service.
- Create containerized database to store the indexed data.
- Create web interface to interact with the indexed data.

### Features
#### General
- Use Docker Compose to orchestrate multiple containers (indexing service, API, database, web interface).
- Ensure services can communicate with each other within the Docker network.
- Use environment variables for configuration (e.g., database connection strings, API endpoints).
- Implement logging for monitoring and debugging purposes.
- Use Aspire to manage dependencies and build the application.
- Use .NET 10 wherever applicable.
- Use Angular 21 for the web interface.
- Memory usage should be optimized to handle large directories with many picture files.
- Will be hosted on a Synology NAS (implies resource constraints and compatibility considerations).
- This solution aims to be used for home-use, not enterprise-grade.

#### Indexing Service (console application)
- Retrieves API host and port from environment variables.
- Retrieves directories to scan from the API upon startup.
- Can restart automatically if stopped or crashed.
- Already indexed files should not be re-indexed unless modified (identified by comparing file modification timestamps or hashes).
- Scan specified directories for picture files (e.g., .jpg, .png, .gif, .heic, including case-insensitive variants like .JPG, .JPEG).
- Extract metadata from picture files (e.g., file name, size, creation date, dimensions).
- Generate thumbnails for quick preview.
- Compute unique hash for each picture file for deduplication purposes.
- Store indexed data in a structured format (e.g., JSON, XML).
- Identify and mark duplicate entries based on computed hashes.
- Expose progress to the API.

##### Optional Features
- Create vector representation of images for similarity search using llama hosted model on a remote server.

#### API Service
- Expose endpoints to receive indexed data from the indexing service.
- Provide endpoints to query indexed data based on various criteria (e.g., file name, date range, dimensions).
- Support pagination for large datasets.
- Feature a health check endpoint to monitor service status.
- Feature API to handle found duplicates based on computed hashes.
- Implement API to configure directories to scan for the indexing service.
- Monitor indexing service status and provide restart functionality if needed.

#### Database Service
- Use PostgreSQL.
- Design a schema to store indexed picture data efficiently.
- Implement indexing on frequently queried fields to optimize performance.
- Ensure data integrity and support transactions for batch inserts/updates.

#### Web Interface
- Create a user-friendly interface using Angular 21.
- Allow users to search and filter indexed picture files based on various criteria.
- Display picture thumbnails and metadata in a grid or list view.
- Implement pagination for browsing large sets of indexed data.
- Provide functionality to view detailed information about each picture file.
- Provide visualization of indexing progress and statistics (e.g., number of files indexed, duplicates found).
- Duplicate management interface to review and handle duplicate entries.

##### Optional Features
- Implement responsive design for usability on various devices (desktop, tablet, mobile).
- Allow users to manage indexed data (e.g., delete entries, mark favorites).

#### Cleaner Service (Mandatory for Deduplication)
- Implement a cleaner service to remove duplicate files from the filesystem based on the indexed data.
- Provide safety mechanisms to prevent accidental deletion of important files.
- Allow users to review and confirm deletions before execution.
- Maintain a backup or log of deleted files for recovery purposes.