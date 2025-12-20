/**
 * Standard API error response.
 * Matches backend: src/Shared/Responses/ApiErrorResponse.cs
 */
export interface ApiErrorResponse {
  message: string;
  code?: string;
  errors?: { [key: string]: string[] };
  traceId?: string;
}
