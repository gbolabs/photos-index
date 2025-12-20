import { Injectable, inject } from '@angular/core';
import {
  HttpInterceptor,
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpResponse,
  HttpErrorResponse,
} from '@angular/common/http';
import { Observable } from 'rxjs';
import { tap, finalize } from 'rxjs/operators';
import { trace, context, SpanKind, SpanStatusCode, Span } from '@opentelemetry/api';
import { TelemetryService } from './telemetry.service';

/**
 * HTTP Interceptor that creates OpenTelemetry spans for all outgoing HTTP requests.
 *
 * Features:
 * - Creates a span for each HTTP request
 * - Adds W3C traceparent header for distributed tracing
 * - Records response status and any errors
 * - Extracts X-Trace-Id from response for error correlation
 */
@Injectable()
export class TelemetryInterceptor implements HttpInterceptor {
  private telemetryService = inject(TelemetryService);

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const tracer = this.telemetryService.getTracer();

    // If telemetry is not initialized, pass through
    if (!tracer) {
      return next.handle(request);
    }

    // Create span for this HTTP request
    const spanName = `HTTP ${request.method} ${this.getPathFromUrl(request.url)}`;
    const span = tracer.startSpan(spanName, {
      kind: SpanKind.CLIENT,
      attributes: {
        'http.method': request.method,
        'http.url': request.url,
        'http.target': this.getPathFromUrl(request.url),
        'http.host': this.getHostFromUrl(request.url),
      },
    });

    // Add traceparent header for distributed tracing
    const traceparent = this.getTraceParentFromSpan(span);
    let modifiedRequest = request;

    if (traceparent) {
      modifiedRequest = request.clone({
        setHeaders: {
          'traceparent': traceparent,
        },
      });
    }

    // Execute request within span context
    return context.with(trace.setSpan(context.active(), span), () => {
      const startTime = performance.now();

      return next.handle(modifiedRequest).pipe(
        tap({
          next: (event) => {
            if (event instanceof HttpResponse) {
              const duration = performance.now() - startTime;

              // Record response attributes
              span.setAttribute('http.status_code', event.status);
              span.setAttribute('http.response_content_length',
                event.headers.get('content-length') || 0);
              span.setAttribute('http.duration_ms', Math.round(duration));

              // Extract server trace ID for correlation
              const serverTraceId = event.headers.get('X-Trace-Id');
              if (serverTraceId) {
                span.setAttribute('http.server_trace_id', serverTraceId);
              }

              span.setStatus({ code: SpanStatusCode.OK });
            }
          },
          error: (error: HttpErrorResponse) => {
            const duration = performance.now() - startTime;

            // Record error attributes
            span.setAttribute('http.status_code', error.status);
            span.setAttribute('http.duration_ms', Math.round(duration));
            span.setAttribute('error', true);
            span.setAttribute('error.type', error.name);
            span.setAttribute('error.message', error.message);

            // Extract server trace ID from error response for correlation
            const serverTraceId = error.headers.get('X-Trace-Id');
            if (serverTraceId) {
              span.setAttribute('http.server_trace_id', serverTraceId);
            }

            span.setStatus({
              code: SpanStatusCode.ERROR,
              message: error.message,
            });
            span.recordException(error);
          },
        }),
        finalize(() => {
          span.end();
        })
      );
    });
  }

  /**
   * Generates W3C traceparent header from span.
   * Format: version-traceId-spanId-flags
   */
  private getTraceParentFromSpan(span: Span): string {
    const spanContext = span.spanContext();
    const flags = spanContext.traceFlags.toString(16).padStart(2, '0');
    return `00-${spanContext.traceId}-${spanContext.spanId}-${flags}`;
  }

  /**
   * Extracts path from URL for span naming.
   */
  private getPathFromUrl(url: string): string {
    try {
      // Handle relative URLs
      if (url.startsWith('/')) {
        return url.split('?')[0];
      }
      const urlObj = new URL(url);
      return urlObj.pathname;
    } catch {
      return url;
    }
  }

  /**
   * Extracts host from URL.
   */
  private getHostFromUrl(url: string): string {
    try {
      if (url.startsWith('/')) {
        return window.location.host;
      }
      const urlObj = new URL(url);
      return urlObj.host;
    } catch {
      return 'unknown';
    }
  }
}
