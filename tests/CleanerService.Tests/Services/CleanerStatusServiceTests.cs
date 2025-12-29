using CleanerService.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Shared.Dtos;
using Xunit;

namespace CleanerService.Tests.Services;

public class CleanerStatusServiceTests
{
    private readonly CleanerServiceOptions _options = new()
    {
        ApiBaseUrl = "http://localhost:5000",
        DryRunEnabled = true
    };

    private CleanerStatusService CreateService(CleanerServiceOptions? options = null)
    {
        return new CleanerStatusService(Options.Create(options ?? _options));
    }

    [Fact]
    public void GetStatus_ReturnsInitialState()
    {
        // Arrange
        var service = CreateService();

        // Act
        var status = service.GetStatus();

        // Assert
        status.Should().NotBeNull();
        status.State.Should().Be(CleanerState.Idle);
        status.DryRunEnabled.Should().BeTrue();
        status.FilesProcessed.Should().Be(0);
        status.FilesDeleted.Should().Be(0);
        status.FilesFailed.Should().Be(0);
        status.FilesSkipped.Should().Be(0);
        status.BytesArchived.Should().Be(0);
        status.ErrorCount.Should().Be(0);
        status.Hostname.Should().NotBeNullOrEmpty();
        status.CleanerId.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(CleanerState.Idle)]
    [InlineData(CleanerState.Processing)]
    [InlineData(CleanerState.Uploading)]
    [InlineData(CleanerState.Deleting)]
    [InlineData(CleanerState.Error)]
    public void SetState_UpdatesState(CleanerState expectedState)
    {
        // Arrange
        var service = CreateService();

        // Act
        service.SetState(expectedState);
        var status = service.GetStatus();

        // Assert
        status.State.Should().Be(expectedState);
    }

    [Fact]
    public void SetState_Processing_SetsLastJobStarted()
    {
        // Arrange
        var service = CreateService();
        var beforeSet = DateTime.UtcNow;

        // Act
        service.SetState(CleanerState.Processing);
        var status = service.GetStatus();

        // Assert
        status.LastJobStarted.Should().NotBeNull();
        status.LastJobStarted!.Value.Should().BeOnOrAfter(beforeSet);
    }

    [Fact]
    public void SetState_Idle_AfterProcessing_SetsLastJobCompleted()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.SetState(CleanerState.Processing);
        Thread.Sleep(10); // Ensure some time passes
        service.SetState(CleanerState.Idle);
        var status = service.GetStatus();

        // Assert
        status.LastJobCompleted.Should().NotBeNull();
        status.LastJobStarted.Should().NotBeNull();
        status.LastJobCompleted.Should().BeOnOrAfter(status.LastJobStarted!.Value);
    }

    [Fact]
    public void SetActivity_UpdatesCurrentActivity()
    {
        // Arrange
        var service = CreateService();
        const string activity = "Processing file: test.jpg";

        // Act
        service.SetActivity(activity);
        var status = service.GetStatus();

        // Assert
        status.CurrentActivity.Should().Be(activity);
    }

    [Fact]
    public void SetActivity_Null_ClearsActivity()
    {
        // Arrange
        var service = CreateService();
        service.SetActivity("Some activity");

        // Act
        service.SetActivity(null);
        var status = service.GetStatus();

        // Assert
        status.CurrentActivity.Should().BeNull();
    }

    [Fact]
    public void SetCurrentJob_UpdatesCurrentJobId()
    {
        // Arrange
        var service = CreateService();
        var jobId = Guid.NewGuid();

        // Act
        service.SetCurrentJob(jobId);
        var status = service.GetStatus();

        // Assert
        status.CurrentJobId.Should().Be(jobId);
    }

    [Fact]
    public void IncrementFilesProcessed_IncreasesCount()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.IncrementFilesProcessed();
        service.IncrementFilesProcessed();
        service.IncrementFilesProcessed();
        var status = service.GetStatus();

        // Assert
        status.FilesProcessed.Should().Be(3);
    }

    [Fact]
    public void IncrementFilesDeleted_IncreasesCount()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.IncrementFilesDeleted();
        service.IncrementFilesDeleted();
        var status = service.GetStatus();

        // Assert
        status.FilesDeleted.Should().Be(2);
    }

    [Fact]
    public void IncrementFilesFailed_IncreasesCount()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.IncrementFilesFailed();
        var status = service.GetStatus();

        // Assert
        status.FilesFailed.Should().Be(1);
    }

    [Fact]
    public void IncrementFilesSkipped_IncreasesCount()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.IncrementFilesSkipped();
        service.IncrementFilesSkipped();
        service.IncrementFilesSkipped();
        service.IncrementFilesSkipped();
        var status = service.GetStatus();

        // Assert
        status.FilesSkipped.Should().Be(4);
    }

    [Fact]
    public void AddBytesArchived_AccumulatesBytes()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.AddBytesArchived(1000);
        service.AddBytesArchived(2500);
        service.AddBytesArchived(500);
        var status = service.GetStatus();

        // Assert
        status.BytesArchived.Should().Be(4000);
    }

    [Fact]
    public void RecordError_IncrementsErrorCountAndSetsLastError()
    {
        // Arrange
        var service = CreateService();
        const string error1 = "First error";
        const string error2 = "Second error";

        // Act
        service.RecordError(error1);
        service.RecordError(error2);
        var status = service.GetStatus();

        // Assert
        status.ErrorCount.Should().Be(2);
        status.LastError.Should().Be(error2);
    }

    [Fact]
    public void Reset_ClearsCountersAndState()
    {
        // Arrange
        var service = CreateService();
        var jobId = Guid.NewGuid();

        service.SetCurrentJob(jobId);
        service.SetActivity("Some activity");
        service.IncrementFilesProcessed();
        service.IncrementFilesDeleted();
        service.IncrementFilesFailed();
        service.IncrementFilesSkipped();
        service.AddBytesArchived(5000);

        // Act
        service.Reset();
        var status = service.GetStatus();

        // Assert
        status.FilesProcessed.Should().Be(0);
        status.FilesDeleted.Should().Be(0);
        status.FilesFailed.Should().Be(0);
        status.FilesSkipped.Should().Be(0);
        status.BytesArchived.Should().Be(0);
        status.CurrentJobId.Should().BeNull();
        status.CurrentActivity.Should().BeNull();
    }

    [Fact]
    public void Reset_DoesNotClearErrorCount()
    {
        // Arrange
        var service = CreateService();
        service.RecordError("Some error");
        service.RecordError("Another error");

        // Act
        service.Reset();
        var status = service.GetStatus();

        // Assert
        // Error count persists across resets (it's a cumulative metric)
        status.ErrorCount.Should().Be(2);
        status.LastError.Should().Be("Another error");
    }

    [Fact]
    public void GetStatus_UpdatesLastHeartbeat()
    {
        // Arrange
        var service = CreateService();
        var firstStatus = service.GetStatus();
        var firstHeartbeat = firstStatus.LastHeartbeat;

        Thread.Sleep(50); // Ensure time passes

        // Act
        var secondStatus = service.GetStatus();

        // Assert
        secondStatus.LastHeartbeat.Should().BeOnOrAfter(firstHeartbeat);
    }

    [Fact]
    public void GetStatus_CalculatesUptime()
    {
        // Arrange
        var service = CreateService();
        Thread.Sleep(100); // Let some time pass

        // Act
        var status = service.GetStatus();

        // Assert
        status.Uptime.Should().BeGreaterThan(TimeSpan.Zero);
        status.Uptime.TotalMilliseconds.Should().BeGreaterThan(50);
    }

    [Fact]
    public void DryRunEnabled_ReflectsConfiguration()
    {
        // Arrange
        var optionsDryRunDisabled = new CleanerServiceOptions
        {
            ApiBaseUrl = "http://localhost:5000",
            DryRunEnabled = false
        };
        var service = CreateService(optionsDryRunDisabled);

        // Act
        var status = service.GetStatus();

        // Assert
        status.DryRunEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentAccess_IsThreadSafe()
    {
        // Arrange
        var service = CreateService();
        var tasks = new List<Task>();

        // Act - Simulate concurrent access from multiple threads
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => service.IncrementFilesProcessed()));
            tasks.Add(Task.Run(() => service.IncrementFilesDeleted()));
            tasks.Add(Task.Run(() => service.GetStatus()));
        }

        await Task.WhenAll(tasks);
        var status = service.GetStatus();

        // Assert
        status.FilesProcessed.Should().Be(100);
        status.FilesDeleted.Should().Be(100);
    }
}
