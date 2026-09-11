using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.ViewModels.UserManagement;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public interface IUserManagementService
{
    Task<IReadOnlyList<StaffUserListItemViewModel>> GetStaffUsersAsync(
        Guid currentUserId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<OperationResult<EditStaffUserViewModel>> GetStaffUserForEditAsync(
        Guid targetUserId, Guid currentUserId, CancellationToken cancellationToken = default);

    Task<OperationResult<Guid>> CreateStaffUserAsync(
        CreateStaffUserViewModel model,
        Guid actorUserId,
        string? ipAddress,
        string? correlationId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateStaffUserAsync(
        EditStaffUserViewModel model,
        Guid actorUserId,
        string? ipAddress,
        string? correlationId,
        CancellationToken cancellationToken = default);
}
