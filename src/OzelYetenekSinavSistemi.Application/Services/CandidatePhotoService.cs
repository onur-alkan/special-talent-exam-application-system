using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Domain.Common;

namespace OzelYetenekSinavSistemi.Application.Services;

public sealed class CandidatePhotoService : ICandidatePhotoService
{
    private const string GenericFailure = "Fotoğraf bulunamadı.";

    private readonly IUserRepository _userRepository;
    private readonly ICandidateApplicationRepository _applicationRepository;
    private readonly IExamPeriodManagerRepository _managerRepository;
    private readonly IPhotoUploadService _photoUploadService;

    public CandidatePhotoService(
        IUserRepository userRepository,
        ICandidateApplicationRepository applicationRepository,
        IExamPeriodManagerRepository managerRepository,
        IPhotoUploadService photoUploadService)
    {
        _userRepository = userRepository;
        _applicationRepository = applicationRepository;
        _managerRepository = managerRepository;
        _photoUploadService = photoUploadService;
    }

    public async Task<OperationResult<PhotoFileContent>> GetCurrentUserPhotoAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(currentUserId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.PhotoPath))
            return OperationResult<PhotoFileContent>.Fail(GenericFailure);

        var read = await _photoUploadService.ReadPhotoAsync(user.PhotoPath, cancellationToken).ConfigureAwait(false);
        return read.Success && read.Data is not null
            ? OperationResult<PhotoFileContent>.Ok(read.Data)
            : OperationResult<PhotoFileContent>.Fail(GenericFailure);
    }

    public async Task<OperationResult<PhotoFileContent>> GetApplicationPhotoAsync(
        Guid applicationId,
        Guid currentUserId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var detail = await _applicationRepository.GetDetailByIdAsync(applicationId, cancellationToken).ConfigureAwait(false);
        if (detail is null || string.IsNullOrWhiteSpace(detail.PhotoPath))
            return OperationResult<PhotoFileContent>.Fail(GenericFailure);

        var authorized = role switch
        {
            DomainConstants.RoleNames.SuperAdmin => true,
            DomainConstants.RoleNames.Candidate => detail.UserId == currentUserId,
            DomainConstants.RoleNames.ApplicationManager =>
                await _managerRepository.IsManagerOfExamPeriodAsync(currentUserId, detail.ExamPeriodId, cancellationToken)
                    .ConfigureAwait(false),
            _ => false
        };

        if (!authorized)
            return OperationResult<PhotoFileContent>.Fail(GenericFailure);

        var read = await _photoUploadService.ReadPhotoAsync(detail.PhotoPath, cancellationToken).ConfigureAwait(false);
        return read.Success && read.Data is not null
            ? OperationResult<PhotoFileContent>.Ok(read.Data)
            : OperationResult<PhotoFileContent>.Fail(GenericFailure);
    }
}
