using OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class CandidateApplicationUniqueConstraintTests
{
    [Fact]
    public void IsUserExamUniqueViolation_RecognizesConstraintName()
    {
        Assert.True(CandidateApplicationRepository.IsUserExamUniqueViolation(
            2627,
            "Violation of UNIQUE KEY constraint 'UQ_CandidateApplications_User_Exam'. Cannot insert duplicate key."));
    }

    [Fact]
    public void IsUserExamUniqueViolation_AlsoAcceptsDuplicateKeyNumber()
    {
        Assert.True(CandidateApplicationRepository.IsUserExamUniqueViolation(
            2601,
            "Cannot insert duplicate key row in object 'dbo.CandidateApplications' with unique index 'UQ_CandidateApplications_User_Exam'."));
    }

    [Fact]
    public void IsUserExamUniqueViolation_IgnoresOtherUniqueConstraints()
    {
        Assert.False(CandidateApplicationRepository.IsUserExamUniqueViolation(
            2627,
            "Violation of UNIQUE KEY constraint 'UQ_CandidateApplications_VerificationCode'."));

        Assert.True(CandidateApplicationRepository.IsUniqueConstraintViolation(2627));
    }

    [Fact]
    public void IsUniqueConstraintViolation_IgnoresNonUniqueErrors()
    {
        Assert.False(CandidateApplicationRepository.IsUniqueConstraintViolation(547));
        Assert.False(CandidateApplicationRepository.IsUserExamUniqueViolation(547, "FK conflict UQ_CandidateApplications_User_Exam"));
    }
}
