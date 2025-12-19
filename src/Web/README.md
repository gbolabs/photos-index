# Photos Index - Angular Web Application

This is an Angular 21 application for the Photos Index project.

## Features

- Angular 21 with standalone components
- Angular Material UI (Indigo/Blue theme)
- Routing configured for multiple views
- Responsive sidenav layout
- Environment-based configuration

## Project Structure

```
src/
├── app/
│   ├── core/              # Core services and utilities
│   │   └── api.service.ts # HTTP API service
│   ├── shared/            # Shared components and modules
│   ├── features/          # Feature modules
│   │   ├── dashboard/     # Dashboard component
│   │   ├── duplicates/    # Duplicates management
│   │   └── settings/      # Application settings
│   ├── app.ts            # Root component
│   ├── app.html          # Root template with Material sidenav
│   ├── app.scss          # Root styles
│   ├── app.config.ts     # Application configuration
│   └── app.routes.ts     # Routing configuration
└── environments/         # Environment configurations
    ├── environment.ts                # Base environment
    ├── environment.development.ts    # Development settings
    └── environment.production.ts     # Production settings
```

## Routes

- `/` - Dashboard (home page)
- `/duplicates` - Duplicate photos management
- `/settings` - Application settings

## Development

### Prerequisites

- Node.js 22.x or higher
- npm 10.x or higher

### Install Dependencies

```bash
npm install
```

### Run Development Server

```bash
ng serve
```

Navigate to `http://localhost:4200/`. The application will automatically reload if you change any of the source files.

### Build

```bash
ng build
```

Build artifacts will be stored in the `dist/Web/` directory.

### Build for Production

```bash
ng build --configuration production
```

## Environment Configuration

The application uses different environment files for development and production:

- **Development**: API URL defaults to `http://localhost:5000`
- **Production**: API URL defaults to `/api` (relative path)

To change the API URL, edit the appropriate environment file in `src/environments/`.

## Angular Material Theme

The application uses a custom Angular Material theme based on Indigo (primary) and Blue (accent) colors. The theme configuration can be found in `src/styles.scss`.

## API Service

A basic API service is provided in `src/app/core/api.service.ts` that:
- Automatically uses the configured API URL from environment files
- Provides methods for HTTP GET, POST, PUT, and DELETE operations
- Uses RxJS Observables for asynchronous operations

Example usage:
```typescript
constructor(private apiService: ApiService) {}

getData() {
  this.apiService.get<MyDataType>('endpoint').subscribe(data => {
    // Handle data
  });
}
```

## Further Development

- Add authentication/authorization
- Implement photo gallery components
- Add duplicate detection UI
- Integrate with backend API
- Add unit and integration tests
