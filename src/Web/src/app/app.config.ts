import { ApplicationConfig, provideBrowserGlobalErrorListeners, APP_INITIALIZER } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withInterceptorsFromDi, HTTP_INTERCEPTORS } from '@angular/common/http';

import { routes } from './app.routes';
import { TelemetryService } from './core/telemetry.service';
import { TelemetryInterceptor } from './core/telemetry.interceptor';

/**
 * Factory function to initialize telemetry during app startup.
 */
function initializeTelemetry(telemetryService: TelemetryService) {
  return () => {
    // Get OTLP endpoint from environment (injected at runtime via env.js)
    const otlpEndpoint = (window as Record<string, unknown>)['__OTEL_ENDPOINT__'] as string | undefined;

    telemetryService.initialize({
      serviceName: 'photos-index-web',
      serviceVersion: '1.0.0',
      otlpEndpoint: otlpEndpoint || undefined,
      debug: false, // Set to true for console logging of spans
    });
  };
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideAnimationsAsync(),
    provideHttpClient(withInterceptorsFromDi()),
    // Initialize telemetry on app startup
    {
      provide: APP_INITIALIZER,
      useFactory: initializeTelemetry,
      deps: [TelemetryService],
      multi: true,
    },
    // Register telemetry interceptor for HTTP requests
    {
      provide: HTTP_INTERCEPTORS,
      useClass: TelemetryInterceptor,
      multi: true,
    },
  ]
};
