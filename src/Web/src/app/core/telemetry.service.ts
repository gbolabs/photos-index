import { Injectable, InjectionToken, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import { SimpleSpanProcessor, BatchSpanProcessor } from '@opentelemetry/sdk-trace-base';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { ZoneContextManager } from '@opentelemetry/context-zone';
import { Resource } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION } from '@opentelemetry/semantic-conventions';
import { trace, context, SpanStatusCode, Span, Tracer } from '@opentelemetry/api';

/**
 * Configuration for OpenTelemetry in Angular.
 */
export interface TelemetryConfig {
  serviceName: string;
  serviceVersion: string;
  /** OTLP endpoint URL, e.g., 'http://localhost:4318/v1/traces' */
  otlpEndpoint?: string;
  /** Enable console exporter for debugging */
  debug?: boolean;
}

/**
 * Injection token for telemetry configuration.
 */
export const TELEMETRY_CONFIG = new InjectionToken<TelemetryConfig>('TELEMETRY_CONFIG');

/**
 * Default telemetry configuration.
 */
export const DEFAULT_TELEMETRY_CONFIG: TelemetryConfig = {
  serviceName: 'photos-index-web',
  serviceVersion: '1.0.0',
  otlpEndpoint: undefined, // Will be set from environment
  debug: false
};

/**
 * Service for managing OpenTelemetry tracing in Angular.
 * Initializes the tracer provider and provides utilities for creating spans.
 */
@Injectable({
  providedIn: 'root'
})
export class TelemetryService {
  private tracer: Tracer | null = null;
  private provider: WebTracerProvider | null = null;
  private initialized = false;
  private platformId = inject(PLATFORM_ID);

  /**
   * Initializes the OpenTelemetry SDK.
   * Should be called once during application bootstrap.
   */
  initialize(config: TelemetryConfig = DEFAULT_TELEMETRY_CONFIG): void {
    // Only initialize in browser environment
    if (!isPlatformBrowser(this.platformId)) {
      console.log('[Telemetry] Skipping initialization - not in browser');
      return;
    }

    if (this.initialized) {
      console.warn('[Telemetry] Already initialized');
      return;
    }

    try {
      // Create resource with service information
      const resource = new Resource({
        [ATTR_SERVICE_NAME]: config.serviceName,
        [ATTR_SERVICE_VERSION]: config.serviceVersion,
      });

      // Create the tracer provider
      this.provider = new WebTracerProvider({
        resource,
      });

      // Add OTLP exporter if endpoint is configured
      if (config.otlpEndpoint) {
        const otlpExporter = new OTLPTraceExporter({
          url: config.otlpEndpoint,
        });
        this.provider.addSpanProcessor(new BatchSpanProcessor(otlpExporter));
        console.log(`[Telemetry] OTLP exporter configured: ${config.otlpEndpoint}`);
      }

      // Add console exporter for debugging
      if (config.debug) {
        const { ConsoleSpanExporter } = require('@opentelemetry/sdk-trace-base');
        this.provider.addSpanProcessor(new SimpleSpanProcessor(new ConsoleSpanExporter()));
        console.log('[Telemetry] Debug mode enabled - logging spans to console');
      }

      // Register the provider with Zone context manager for Angular
      this.provider.register({
        contextManager: new ZoneContextManager(),
      });

      // Get the tracer
      this.tracer = trace.getTracer(config.serviceName, config.serviceVersion);
      this.initialized = true;

      console.log(`[Telemetry] Initialized: ${config.serviceName} v${config.serviceVersion}`);
    } catch (error) {
      console.error('[Telemetry] Initialization failed:', error);
    }
  }

  /**
   * Gets the tracer instance.
   */
  getTracer(): Tracer | null {
    return this.tracer;
  }

  /**
   * Starts a new span with the given name.
   */
  startSpan(name: string, attributes?: Record<string, string | number | boolean>): Span | null {
    if (!this.tracer) {
      return null;
    }

    const span = this.tracer.startSpan(name);
    if (attributes) {
      Object.entries(attributes).forEach(([key, value]) => {
        span.setAttribute(key, value);
      });
    }
    return span;
  }

  /**
   * Gets the current trace ID from the active span.
   */
  getCurrentTraceId(): string | null {
    const activeSpan = trace.getActiveSpan();
    if (activeSpan) {
      return activeSpan.spanContext().traceId;
    }
    return null;
  }

  /**
   * Gets the current span context for propagation.
   */
  getTraceParentHeader(): string | null {
    const activeSpan = trace.getActiveSpan();
    if (activeSpan) {
      const spanContext = activeSpan.spanContext();
      // W3C Trace Context format: version-traceId-spanId-flags
      const flags = spanContext.traceFlags.toString(16).padStart(2, '0');
      return `00-${spanContext.traceId}-${spanContext.spanId}-${flags}`;
    }
    return null;
  }

  /**
   * Ends a span with optional error status.
   */
  endSpan(span: Span, error?: Error): void {
    if (error) {
      span.setStatus({
        code: SpanStatusCode.ERROR,
        message: error.message,
      });
      span.recordException(error);
    } else {
      span.setStatus({ code: SpanStatusCode.OK });
    }
    span.end();
  }

  /**
   * Shuts down the tracer provider.
   */
  async shutdown(): Promise<void> {
    if (this.provider) {
      await this.provider.shutdown();
      this.initialized = false;
      console.log('[Telemetry] Shutdown complete');
    }
  }
}
