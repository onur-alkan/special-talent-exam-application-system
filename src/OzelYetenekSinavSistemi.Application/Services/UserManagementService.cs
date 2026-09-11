using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Users;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.UserManagement;
using OzelYetenekSinavSistemi.Domain.Common;

namespace OzelYetenekSinavSistemi.Application.Services;

public sealed class UserManagementService : IUserManagementService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly ITurkishIdentityNumberValidator _tcValidation;
    private readonly ISensitiveDataMaskingService _masking;

    public UserManagementService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        ITurkishIdentityNumberValidator tcValidation,
        ISensitiveDataMaskingService masking)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _tcValidation = tcValidation;
        _masking = masking;
    }

    public async Task<IReadOnlyList<StaffUserListItemViewModel>> GetStaffUsersAsync(
        Guid currentUserId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var skip = (page - 1) * pageSize;

        var users = await _userRepository.GetStaffUsersAsync(pageSize, skip, cancellationToken);
        return users.Select(u => new StaffUserListItemViewModel
        {
            Id = u.Id,
            FullName = u.FullName,
            MaskedTcNo = _masking.MaskTcNo(u.TcNo),
            MaskedEmail = _masking.MaskEmail(u.Email),
            RoleName = DomainConstants.GetRoleName(u.RoleId) ?? "-",
            IsActive = u.IsActive,
            CreatedDate = u.CreatedDate,
            IsSelf = u.Id == currentUserId
        }).ToList();
    }

    public async Task<OperationResult<EditStaffUserViewModel>> GetStaffUserForEditAsync(
        Guid targetUserId, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (user is null || !DomainConstants.IsStaffRoleId(user.RoleId))
            return OperationResult<EditStaffUserViewModel>.Fail("Personel kullanıcısı bulunamadı.");

        return OperationResult<EditStaffUserViewModel>.Ok(new EditStaffUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            MaskedTcNo = _masking.MaskTcNo(user.TcNo),
            MaskedEmail = _masking.MaskEmail(user.Email),
            RoleId = user.RoleId,
            IsActive = user.IsActive,
            IsSelf = user.Id == currentUserId
        });
    }

    public async Task<OperationResult<Guid>> CreateStaffUserAsync(
        CreateStaffUserViewModel model,
        Guid actorUserId,
        string? ipAddress,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!DomainConstants.IsStaffRoleId(model.RoleId) || model.RoleId == Guid.Empty)
            return OperationResult<Guid>.Fail("Geçerli bir personel rolü seçiniz.");

        if (!InputTextRules.IsValidHumanName(model.FirstName, out _))
            return OperationResult<Guid>.Fail(InputTextRules.FormatHumanNameMessage("Ad"));

        if (!InputTextRules.IsValidHumanName(model.LastName, out _))
            return OperationResult<Guid>.Fail(InputTextRules.FormatHumanNameMessage("Soyad"));

        if (!_tcValidation.IsValid(model.TcNo))
            return OperationResult<Guid>.Fail(TurkishIdentityNumber.InvalidMessage);

        if (await _userRepository.TcNoExistsAsync(model.TcNo.Trim(), cancellationToken))
            return OperationResult<Guid>.Fail("Bu T.C. kimlik numarasıyla kayıtlı bir hesap bulunmaktadır.");

        if (await _userRepository.EmailExistsAsync(model.Email.Trim(), cancellationToken))
            return OperationResult<Guid>.Fail("Bu e-posta adresiyle kayıtlı bir hesap bulunmaktadır.");

        var write = await _userRepository.CreateStaffUserAtomicAsync(new CreateStaffUserRequest
        {
            ActorUserId = actorUserId,
            TcNo = model.TcNo.Trim(),
            Email = model.Email.Trim(),
            FirstName = InputTextRules.NormalizeHumanName(model.FirstName) ?? string.Empty,
            LastName = InputTextRules.NormalizeHumanName(model.LastName) ?? string.Empty,
            RoleId = model.RoleId,
            PasswordHash = _passwordService.Hash(model.Password),
            IsActive = model.IsActive,
            SecurityStamp = Guid.NewGuid(),
            IpAddress = ipAddress,
            CorrelationId = correlationId
        }, cancellationToken);

        if (!write.Success)
            return OperationResult<Guid>.Fail(MapError(write.Status));

        return OperationResult<Guid>.Ok(write.UserId!.Value);
    }

    public async Task<OperationResult> UpdateStaffUserAsync(
        EditStaffUserViewModel model,
        Guid actorUserId,
        string? ipAddress,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!DomainConstants.IsStaffRoleId(model.RoleId) || model.RoleId == Guid.Empty)
            return OperationResult.Fail("Geçerli bir personel rolü seçiniz.");

        var write = await _userRepository.UpdateStaffUserAtomicAsync(new UpdateStaffUserRequest
        {
            ActorUserId = actorUserId,
            TargetUserId = model.Id,
            NewRoleId = model.RoleId,
            IsActive = model.IsActive,
            IpAddress = ipAddress,
            CorrelationId = correlationId
        }, cancellationToken);

        if (!write.Success)
            return OperationResult.Fail(MapError(write.Status));

        return OperationResult.Ok();
    }

    private static string MapError(UserManagementWriteStatus status) =>
        status switch
        {
            UserManagementWriteStatus.UserNotFound => "Personel kullanıcısı bulunamadı.",
            UserManagementWriteStatus.ActorNotAuthorized => "Bu işlem için yetkiniz yok.",
            UserManagementWriteStatus.InvalidRole => "Geçerli bir personel rolü seçiniz.",
            UserManagementWriteStatus.CandidateAccountCannotBePromoted =>
                "Aday hesapları personel yönetiminden yükseltilmez.",
            UserManagementWriteStatus.DuplicateTcNo => "Bu T.C. kimlik numarasıyla kayıtlı bir hesap bulunmaktadır.",
            UserManagementWriteStatus.DuplicateEmail => "Bu e-posta adresiyle kayıtlı bir hesap bulunmaktadır.",
            UserManagementWriteStatus.CannotModifyOwnAccount =>
                "Kendi hesabınızı pasifleştiremez veya rolünüzü düşüremezsiniz.",
            UserManagementWriteStatus.LastActiveSuperAdmin =>
                "Son aktif SuperAdmin pasifleştirilemez veya rolü düşürülemez.",
            UserManagementWriteStatus.Conflict =>
                "İşlem başka bir değişiklikle çakıştı. Yeniden deneyiniz.",
            _ => "İşlem tamamlanamadı."
        };
}
