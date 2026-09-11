using OzelYetenekSinavSistemi.Application.DTOs.ExamResults;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class ExamResultWriteResultTests
{
    [Fact]
    public void Ok_PreservesPayload()
    {
        var id = Guid.NewGuid();
        var updated = DateTime.UtcNow;
        var result = ExamResultWriteResult.Ok(id, updated);

        Assert.True(result.Success);
        Assert.Equal(ExamResultWriteStatus.Success, result.Status);
        Assert.Equal(id, result.ResultId);
        Assert.Equal(updated, result.UpdatedDate);
    }

    [Theory]
    [InlineData(ExamResultWriteStatus.ApplicationNotFoundOrUnauthorized)]
    [InlineData(ExamResultWriteStatus.ConcurrencyConflict)]
    [InlineData(ExamResultWriteStatus.ScoreRequired)]
    public void Fail_HasNoPayload(ExamResultWriteStatus status)
    {
        var result = ExamResultWriteResult.Fail(status);
        Assert.False(result.Success);
        Assert.Null(result.ResultId);
        Assert.Null(result.UpdatedDate);
    }

    [Fact]
    public void WriteRequest_CarriesConcurrencyToken()
    {
        var expected = DateTime.UtcNow;
        var request = new ExamResultWriteRequest
        {
            ApplicationId = Guid.NewGuid(),
            EvaluatorUserId = Guid.NewGuid(),
            IsSuperAdmin = true,
            AttendanceStatus = AttendanceStatus.Attended,
            ExamScore = 10,
            ExpectedUpdatedDate = expected
        };

        Assert.Equal(expected, request.ExpectedUpdatedDate);
    }
}
