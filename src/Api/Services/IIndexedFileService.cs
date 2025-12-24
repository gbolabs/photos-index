using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Services;

/// <summary>
/// Service for managing indexed files.
/// </summary>
public interface IIndexedFileService
{
    Task<PagedResponse<IndexedFileDto>> QueryAsync(FileQueryParameters query, CancellationToken ct);
    Task<IndexedFileDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<byte[]?> GetThumbnailAsync(Guid id, CancellationToken ct);
    Task<(byte[] Content, string FileName, string ContentType)?> DownloadFileAsync(Guid id, CancellationToken ct);
    Task<BatchOperationResponse> BatchIngestAsync(BatchIngestFilesRequest request, CancellationToken ct);
    Task<FileStatisticsDto> GetStatisticsAsync(CancellationToken ct);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<FileNeedsReindexDto>> CheckNeedsReindexAsync(CheckFilesNeedReindexRequest request, CancellationToken ct);
    Task<IReadOnlyList<IndexedFileDto>> GetBatchMetadataAsync(IReadOnlyList<Guid> fileIds, CancellationToken ct);
}
