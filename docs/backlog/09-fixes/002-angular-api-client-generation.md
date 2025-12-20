# 002: Angular API Client Generation

**Status**: 🔲 Not Started
**Priority**: P2
**Agent**: A4 (Web)
**Estimated Effort**: Medium

## Objective

Replace manually maintained TypeScript models and API service with auto-generated client from OpenAPI/Swagger specification.

## Background

Currently, Angular API services and DTOs are manually maintained:
- `src/Web/src/app/core/models.ts` - Hand-coded DTOs
- `src/Web/src/app/models/*.ts` - More hand-coded models
- `src/Web/src/app/core/api.service.ts` - Manual HTTP calls
- `src/Web/src/app/services/*.service.ts` - More manual HTTP calls

This leads to:
1. Type mismatches between API and client (e.g., `PagedResponse` issues)
2. Route mismatches (e.g., `/api/directories` vs `/api/scan-directories`)
3. Manual effort to keep types in sync
4. Potential for runtime errors from contract drift

## Proposed Solution

Use **NSwag** or **OpenAPI Generator** to auto-generate Angular client from API Swagger spec.

### Option A: NSwag (Recommended)
- .NET native, integrates well with ASP.NET Core
- Generates TypeScript/Angular clients
- Can run at build time or as npm script

### Option B: OpenAPI Generator
- Multi-language support
- Large community
- More configuration options

## Implementation Steps

### 1. Configure API for Swagger JSON export
```bash
# API already exposes: http://localhost:5080/swagger/v1/swagger.json
```

### 2. Add NSwag npm package
```bash
cd src/Web
npm install nswag --save-dev
```

### 3. Create NSwag configuration
```json
// src/Web/nswag.json
{
  "runtime": "Net80",
  "documentGenerator": {
    "fromDocument": {
      "url": "http://localhost:5080/swagger/v1/swagger.json"
    }
  },
  "codeGenerators": {
    "openApiToTypeScriptClient": {
      "className": "ApiClient",
      "moduleName": "",
      "namespace": "",
      "typeScriptVersion": 5.0,
      "template": "Angular",
      "promiseType": "Promise",
      "httpClass": "HttpClient",
      "useSingletonProvider": true,
      "injectionTokenType": "InjectionToken",
      "output": "src/app/generated/api-client.ts"
    }
  }
}
```

### 4. Add npm script
```json
// package.json
"scripts": {
  "generate-api": "nswag run nswag.json"
}
```

### 5. Update imports
- Replace manual services with generated client
- Remove duplicate model files
- Keep only app-specific interfaces

## Acceptance Criteria

- [ ] API client auto-generated from Swagger spec
- [ ] All DTOs match API exactly (no manual sync)
- [ ] npm script to regenerate client
- [ ] CI/CD verifies generated code is up-to-date
- [ ] Existing components work with generated client

## Files to Create

| File | Purpose |
|------|---------|
| `src/Web/nswag.json` | NSwag configuration |
| `src/Web/src/app/generated/api-client.ts` | Generated client |

## Files to Modify

| File | Changes |
|------|---------|
| `src/Web/package.json` | Add nswag dependency and script |
| `src/Web/src/app/core/api.service.ts` | Replace with generated or wrap generated |
| `src/Web/src/app/services/*.service.ts` | Use generated client |

## Files to Delete (after migration)

| File | Reason |
|------|--------|
| `src/Web/src/app/core/models.ts` | Replaced by generated types |
| `src/Web/src/app/models/*.ts` | Replaced by generated types |

## References

- [NSwag GitHub](https://github.com/RicoSuter/NSwag)
- [OpenAPI Generator](https://openapi-generator.tech/)
- [Angular HTTP Client](https://angular.dev/guide/http)
