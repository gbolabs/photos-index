import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

declare global {
  interface Window {
    __env?: {
      apiUrl?: string;
      production?: boolean;
      version?: string;
    };
  }
}

@Injectable({
  providedIn: 'root',
})
export class VersionService {
  /**
   * Get the application version.
   * In production, this is injected at Docker runtime via env-config.js.
   * In development, it falls back to the environment file value.
   */
  getVersion(): string {
    // Check runtime config first (Docker deployment)
    if (typeof window !== 'undefined' && window.__env?.version) {
      return window.__env.version;
    }
    // Fall back to build-time environment
    return environment.version;
  }

  /**
   * Get a short version string for display (e.g., "v0.9.4")
   */
  getDisplayVersion(): string {
    const version = this.getVersion();
    if (version === 'unknown' || version === '__APP_VERSION__') {
      return 'dev';
    }
    return `v${version}`;
  }
}
