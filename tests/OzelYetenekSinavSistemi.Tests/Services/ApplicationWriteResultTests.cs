using OzelYetenekSinavSistemi.Application.DTOs.Applications;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class ApplicationWriteResultTests
{
    [Fact]
    public void Ok_PreservesSuccessPayload()
    {
        var id = Guid.NewGuid();
        var result = ApplicationWriteResult.Ok(id, 42, "VERIFY");

        Assert.True(result.Success);
        Assert.Equal(ApplicationWriteStatus.Success, result.Status);
        Assert.Equal(id, result.ApplicationId);
        Assert.Equal(42, result.CandidateNo);
        Assert.Equal("VERIFY", result.VerificationCode);
    }

    [Fact]
    public void OkUpdated_HasApplicationIdWithoutCandidateNo()
    {
        var id = Guid.NewGuid();
        var result = ApplicationWriteResult.OkUpdated(id);

        Assert.True(result.Success);
        Assert.Equal(id, result.ApplicationId);
        Assert.Null(result.CandidateNo);
        Assert.Null(result.VerificationCode);
    }

    [Theory]
    [InlineData(ApplicationWriteStatus.ExamNotOpen)]
    [InlineData(ApplicationWriteStatus.AlreadyApplied)]
    [InlineData(ApplicationWriteStatus.ApplicationNotFoundOrUnauthorized)]
    [InlineData(ApplicationWriteStatus.NoPreferenceSelected)]
    [InlineData(ApplicationWriteStatus.PreferenceLimitExceeded)]
    [InlineData(ApplicationWriteStatus.DuplicatePreference)]
    [InlineData(ApplicationWriteStatus.InvalidOrInactivePreference)]
    [InlineData(ApplicationWriteStatus.Conflict)]
    [InlineData(ApplicationWriteStatus.Failed)]
    public void Fail_DoesNotExposeSuccessPayload(ApplicationWriteStatus status)
    {
        var result = ApplicationWriteResult.Fail(status);

        Assert.False(result.Success);
        Assert.Equal(status, result.Status);
        Assert.Null(result.ApplicationId);
        Assert.Null(result.CandidateNo);
        Assert.Null(result.VerificationCode);
    }
}
