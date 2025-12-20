using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Channels;
using IndexingService.Models;
using Microsoft.Extensions.Logging;

namespace IndexingService.Services;

/// <summary>
/// Computes SHA256 hashes of files using streaming for memory efficiency.
/// </summary>
public class HashComputer : IHashComputer
{
    private const int BufferSize = 81920; // 80KB - optimal for disk I/O
    private readonly ILogger<HashComputer> _logger;

    public HashComputer(ILogger<HashComputer> logger)
    {
        _logger = logger;
    }

    public async Task<HashResult> ComputeAsync(
        string filePath,
        CancellationToken cancellationToken,
        IProgress<HashProgress>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!File.Exists(filePath))
        {
            return HashResult.Failed(filePath, "File not found");
        }

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.SequentialScan | FileOptions.Asynchronous);

            var totalBytes = stream.Length;
            var bytesRead = 0L;

            using var sha256 = SHA256.Create();
            var buffer = new byte[BufferSize];
            int read;

            while ((read = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)) > 0)
            {
                sha256.TransformBlock(buffer, 0, read, null, 0);
                bytesRead += read;

                progress?.Report(new HashProgress
                {
                    BytesProcessed = bytesRead,
                    TotalBytes = totalBytes
                });
            }

            sha256.TransformFinalBlock([], 0, 0);
            stopwatch.Stop();

            var hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();

            _logger.LogDebug(
                "Computed hash for {FilePath}: {Hash} ({BytesProcessed} bytes in {Duration}ms)",
                filePath, hash, bytesRead, stopwatch.ElapsedMilliseconds);

            return new HashResult
            {
                FilePath = filePath,
                Hash = hash,
                BytesProcessed = bytesRead,
                Duration = stopwatch.Elapsed,
                Success = true
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to read file for hashing: {FilePath}", filePath);
            return HashResult.Failed(filePath, $"IO error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied when hashing file: {FilePath}", filePath);
            return HashResult.Failed(filePath, "Access denied");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error hashing file: {FilePath}", filePath);
            return HashResult.Failed(filePath, $"Unexpected error: {ex.Message}");
        }
    }

    public async IAsyncEnumerable<HashResult> ComputeBatchAsync(
        IEnumerable<string> filePaths,
        int maxParallelism,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (maxParallelism < 1)
            maxParallelism = 1;

        var channel = Channel.CreateBounded<HashResult>(new BoundedChannelOptions(maxParallelism * 2)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        var producerTask = Task.Run(async () =>
        {
            try
            {
                await Parallel.ForEachAsync(
                    filePaths,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = maxParallelism,
                        CancellationToken = cancellationToken
                    },
                    async (filePath, ct) =>
                    {
                        var result = await ComputeAsync(filePath, ct);
                        await channel.Writer.WriteAsync(result, ct);
                    });
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return result;
        }

        await producerTask;
    }
}
