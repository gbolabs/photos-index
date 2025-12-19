# Create a docker application to index picture files of a/some directory/ies

This document describes how to create a Docker application that indexes picture files from specified directories. The application will scan the directories for image files and create an index that can be used for quick access and retrieval.

## Objectives

### Deployment
- Create a containerized application hosting an image indexing service.
- Create a containerized api to receive indexed data from the indexing service.
- Create a containerized database to store the indexed data.
- Create a web interface to interact with the indexed data.

### Features
#### General
- Use Docker Compose to orchestrate multiple containers (indexing service, api, database, web interface).
- Ensure services can communicate with each other within the Docker network.
- Use environment variables for configuration (e.g., database connection strings, api endpoints).
- Implement logging for monitoring and debugging purposes.
- Use Aspire to manage dependencies and build the application.
- Use .NET 10 wherever applicable.
- Use Angular 21 for the web interface.
- Memory usage should be optimized to handle large directories with many picture files.
- Will be hosted on a Synology NAS (implies resource constraints and compatibility considerations).
- This solution aims to be used for home-use, not enterprise-grade.

#### Indexing Service (console application)
- Retrieves api host and port from environment variables.
- Retrieves directories to scan from the api upon startup.
- It can restart if it been stopped or crashed.
- Already indexed files should not be re-indexed unless they have been modified.
  Identifyed by comparing file modification timestamps or hashes.
- Scan specified directories for picture files (e.g., .jpg, .png, .gif, .heic).
  variants of the extensions should be considered (e.g., .JPG, .JPEG).
- Extract metadata from picture files (e.g., file name, size, creation date, dimensions).
- Generate thumbnails for quick preview.
- Compute some unique hash for each picture file for deduplication purposes.
- Store indexed data in a structured format (e.g., JSON, XML).
- Duplicate entries should be indexed and marked as duplicates based on computed hashes (api logic?)
- Expose progress to the api
##### Optional Features
- Create a vector representation of images for similarity search using llama hosted model on a remote server.

#### API Service
- Expose endpoints to receive indexed data from the indexing service.
- Provide endpoints to query indexed data based on various criteria (e.g., file name, date range, dimensions).
- Support pagination for large datasets.
- Features a health check endpoint to monitor service status.
- Features api to handle found duplicates based on computed hashes.
- Implement API to configure directories to scan for the indexing service.
- Monitor indexing service status and provide restart functionality if needed.

#### Database Service
- Use PostgreSQL
- Design a schema to store indexed picture data efficiently.
- Implement indexing on frequently queried fields to optimize performance.
- Ensure data integrity and support transactions for batch inserts/updates.

#### Web Interface
- Create a user-friendly interface using Angular 21.
- Allow users to search and filter indexed picture files based on various criteria.
- Display picture thumbnails and metadata in a grid or list view.
- Implement pagination for browsing large sets of indexed data.
- Provide functionality to view detailed information about each picture file.
- Provide visualization of indexing progress and statistics (e.g., number of files indexed, duplicates found
- Duplicate management interface to review and handle duplicate entries.
#### Optional Features
- Implement responsive design for usability on various devices (desktop, tablet, mobile).
- Allow users to manage indexed data (e.g., delete entries, mark favorites).

#### Optional Features
- Implement a cleaner service to remove duplicate files from the filesystem based on the indexed data.