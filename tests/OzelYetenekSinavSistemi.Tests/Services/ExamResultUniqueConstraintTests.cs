using OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class ExamResultUniqueConstraintTests
{
    [Fact]
    public void IsApplicationIdUniqueViolation_RecognizesConstraint()
    {
        Assert.True(CandidateExamResultRepository.IsApplicationIdUniqueViolation(
            2627,
            "Violation of UNIQUE KEY constraint 'UQ_CandidateExamResults_ApplicationId'."));
    }

    [Fact]
    public void IsApplicationIdUniqueViolation_IgnoresOtherUnique()
    {
        Assert.False(CandidateExamResultRepository.IsApplicationIdUniqueViolation(
            2627,
            "Violation of UNIQUE KEY constraint 'UQ_Other'."));
        Assert.True(CandidateExamResultRepository.IsUniqueConstraintViolation(2627));
    }

    [Fact]
    public void IsUniqueConstraintViolation_IgnoresNonUnique()
    {
        Assert.False(CandidateExamResultRepository.IsUniqueConstraintViolation(547));
    }
}
