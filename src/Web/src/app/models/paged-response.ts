/**
 * Generic paged response wrapper.
 * Matches backend: src/Shared/Responses/PagedResponse.cs
 */
export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}
