using Api.Services;
using Database.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services;

public class DuplicateGroupStatusTransitionValidatorTests
{
    private readonly DuplicateGroupStatusTransitionValidator _validator = new();

    #region Valid Transitions

    [Theory]
    [InlineData(DuplicateGroupStatus.Pending, DuplicateGroupStatus.AutoSelected)]
    [InlineData(DuplicateGroupStatus.Pending, DuplicateGroupStatus.Validated)]
    [InlineData(DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.AutoSelected)]
    [InlineData(DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.Validated)]
    [InlineData(DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.Pending)]
    [InlineData(DuplicateGroupStatus.Validated, DuplicateGroupStatus.Cleaning)]
    [InlineData(DuplicateGroupStatus.Validated, DuplicateGroupStatus.Pending)]
    [InlineData(DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.Cleaned)]
    [InlineData(DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.CleaningFailed)]
    [InlineData(DuplicateGroupStatus.CleaningFailed, DuplicateGroupStatus.Validated)]
    [InlineData(DuplicateGroupStatus.CleaningFailed, DuplicateGroupStatus.Pending)]
    [InlineData(DuplicateGroupStatus.Cleaned, DuplicateGroupStatus.Pending)]
    public void CanTransition_ValidTransitions_ReturnsTrue(DuplicateGroupStatus from, DuplicateGroupStatus to)
    {
        _validator.CanTransition(from, to).Should().BeTrue();
    }

    [Theory]
    [InlineData(DuplicateGroupStatus.Pending, DuplicateGroupStatus.AutoSelected)]
    [InlineData(DuplicateGroupStatus.Pending, DuplicateGroupStatus.Validated)]
    [InlineData(DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.AutoSelected)]
    [InlineData(DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.Validated)]
    [InlineData(DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.Pending)]
    [InlineData(DuplicateGroupStatus.Validated, DuplicateGroupStatus.Cleaning)]
    [InlineData(DuplicateGroupStatus.Validated, DuplicateGroupStatus.Pending)]
    [InlineData(DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.Cleaned)]
    [InlineData(DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.CleaningFailed)]
    [InlineData(DuplicateGroupStatus.CleaningFailed, DuplicateGroupStatus.Validated)]
    [InlineData(DuplicateGroupStatus.CleaningFailed, DuplicateGroupStatus.Pending)]
    [InlineData(DuplicateGroupStatus.Cleaned, DuplicateGroupStatus.Pending)]
    public void ValidateTransition_ValidTransitions_DoesNotThrow(DuplicateGroupStatus from, DuplicateGroupStatus to)
    {
        var action = () => _validator.ValidateTransition(from, to);
        action.Should().NotThrow();
    }

    #endregion

    #region Invalid Transitions

    [Theory]
    // Pending cannot go directly to Cleaning, CleaningFailed, or Cleaned
    [InlineData(DuplicateGroupStatus.Pending, DuplicateGroupStatus.Cleaning)]
    [InlineData(DuplicateGroupStatus.Pending, DuplicateGroupStatus.CleaningFailed)]
    [InlineData(DuplicateGroupStatus.Pending, DuplicateGroupStatus.Cleaned)]
    // AutoSelected cannot go directly to Cleaning, CleaningFailed, or Cleaned
    [InlineData(DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.Cleaning)]
    [InlineData(DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.CleaningFailed)]
    [InlineData(DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.Cleaned)]
    // Validated cannot go to AutoSelected or CleaningFailed or Cleaned directly
    [InlineData(DuplicateGroupStatus.Validated, DuplicateGroupStatus.AutoSelected)]
    [InlineData(DuplicateGroupStatus.Validated, DuplicateGroupStatus.CleaningFailed)]
    [InlineData(DuplicateGroupStatus.Validated, DuplicateGroupStatus.Cleaned)]
    // Cleaning cannot go back to Pending, AutoSelected, or Validated
    [InlineData(DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.Pending)]
    [InlineData(DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.AutoSelected)]
    [InlineData(DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.Validated)]
    // CleaningFailed cannot go to AutoSelected, Cleaning, or Cleaned
    [InlineData(DuplicateGroupStatus.CleaningFailed, DuplicateGroupStatus.AutoSelected)]
    [InlineData(DuplicateGroupStatus.CleaningFailed, DuplicateGroupStatus.Cleaning)]
    [InlineData(DuplicateGroupStatus.CleaningFailed, DuplicateGroupStatus.Cleaned)]
    // Cleaned cannot go to AutoSelected, Validated, Cleaning, or CleaningFailed
    [InlineData(DuplicateGroupStatus.Cleaned, DuplicateGroupStatus.AutoSelected)]
    [InlineData(DuplicateGroupStatus.Cleaned, DuplicateGroupStatus.Validated)]
    [InlineData(DuplicateGroupStatus.Cleaned, DuplicateGroupStatus.Cleaning)]
    [InlineData(DuplicateGroupStatus.Cleaned, DuplicateGroupStatus.CleaningFailed)]
    public void CanTransition_InvalidTransitions_ReturnsFalse(DuplicateGroupStatus from, DuplicateGroupStatus to)
    {
        _validator.CanTransition(from, to).Should().BeFalse();
    }

    [Theory]
    [InlineData(DuplicateGroupStatus.Pending, DuplicateGroupStatus.Cleaning)]
    [InlineData(DuplicateGroupStatus.Validated, DuplicateGroupStatus.AutoSelected)]
    [InlineData(DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.Pending)]
    [InlineData(DuplicateGroupStatus.Cleaned, DuplicateGroupStatus.Validated)]
    public void ValidateTransition_InvalidTransitions_ThrowsInvalidOperationException(DuplicateGroupStatus from, DuplicateGroupStatus to)
    {
        var action = () => _validator.ValidateTransition(from, to);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*'{from}'*'{to}'*");
    }

    #endregion

    #region Algorithm Eligibility

    [Theory]
    [InlineData(DuplicateGroupStatus.Pending, true)]
    [InlineData(DuplicateGroupStatus.AutoSelected, true)]
    [InlineData(DuplicateGroupStatus.Validated, false)]
    [InlineData(DuplicateGroupStatus.Cleaning, false)]
    [InlineData(DuplicateGroupStatus.CleaningFailed, false)]
    [InlineData(DuplicateGroupStatus.Cleaned, false)]
    public void IsEligibleForAlgorithm_ReturnsCorrectResult(DuplicateGroupStatus status, bool expected)
    {
        _validator.IsEligibleForAlgorithm(status).Should().Be(expected);
    }

    [Fact]
    public void AlgorithmEligibleStatuses_ContainsOnlyPendingAndAutoSelected()
    {
        DuplicateGroupStatusTransitionValidator.AlgorithmEligibleStatuses
            .Should().BeEquivalentTo(new[] { DuplicateGroupStatus.Pending, DuplicateGroupStatus.AutoSelected });
    }

    #endregion

    #region GetValidTargetStatuses

    [Fact]
    public void GetValidTargetStatuses_FromPending_ReturnsPendingAndValidated()
    {
        var targets = _validator.GetValidTargetStatuses(DuplicateGroupStatus.Pending);

        targets.Should().BeEquivalentTo(new[] { DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.Validated });
    }

    [Fact]
    public void GetValidTargetStatuses_FromAutoSelected_ReturnsAutoSelectedValidatedAndPending()
    {
        var targets = _validator.GetValidTargetStatuses(DuplicateGroupStatus.AutoSelected);

        targets.Should().BeEquivalentTo(new[] { DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.Validated, DuplicateGroupStatus.Pending });
    }

    [Fact]
    public void GetValidTargetStatuses_FromValidated_ReturnsCleaningAndPending()
    {
        var targets = _validator.GetValidTargetStatuses(DuplicateGroupStatus.Validated);

        targets.Should().BeEquivalentTo(new[] { DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.Pending });
    }

    [Fact]
    public void GetValidTargetStatuses_FromCleaning_ReturnsCleanedAndCleaningFailed()
    {
        var targets = _validator.GetValidTargetStatuses(DuplicateGroupStatus.Cleaning);

        targets.Should().BeEquivalentTo(new[] { DuplicateGroupStatus.Cleaned, DuplicateGroupStatus.CleaningFailed });
    }

    [Fact]
    public void GetValidTargetStatuses_FromCleaningFailed_ReturnsValidatedAndPending()
    {
        var targets = _validator.GetValidTargetStatuses(DuplicateGroupStatus.CleaningFailed);

        targets.Should().BeEquivalentTo(new[] { DuplicateGroupStatus.Validated, DuplicateGroupStatus.Pending });
    }

    [Fact]
    public void GetValidTargetStatuses_FromCleaned_ReturnsPendingOnly()
    {
        var targets = _validator.GetValidTargetStatuses(DuplicateGroupStatus.Cleaned);

        targets.Should().BeEquivalentTo(new[] { DuplicateGroupStatus.Pending });
    }

    #endregion
}
