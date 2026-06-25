using MPS.Domain.Constants;
using MPS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MPS.Infrastructure.Persistence.Configurations;

public class RolePermissionEntityConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasKey(e => new { e.RoleId, e.PermissionId });

        builder.HasOne(e => e.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(e => e.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.PermissionId);

        builder.HasData(BuildSeed());
    }

    private static IEnumerable<RolePermission> BuildSeed()
    {
        var matrix = new (int RoleId, int[] PermissionIds)[]
        {
            (SystemRoles.SystemAdminId, AllPermissionIds()),

            (SystemRoles.HrId, new[]
            {
                PermissionIds.UserReadAll, PermissionIds.UserReadSelf, PermissionIds.UserCreateAll,
                PermissionIds.UserUpdateAll, PermissionIds.UserUpdateSelf, PermissionIds.UserDeleteAll,
                PermissionIds.EmployeeInformationReadAll, PermissionIds.EmployeeInformationReadSelf,
                PermissionIds.EmployeeInformationCreateAll, PermissionIds.EmployeeInformationUpdateAll,
                PermissionIds.EmployeeInformationUpdateSelf, PermissionIds.EmployeeInformationDeleteAll,
                PermissionIds.PositionReadAll, PermissionIds.PositionCreateAll,
                PermissionIds.PositionUpdateAll, PermissionIds.PositionDeleteAll,
                PermissionIds.DepartmentReadAll, PermissionIds.DepartmentCreateAll,
                PermissionIds.DepartmentUpdateAll, PermissionIds.DepartmentDeleteAll,
                PermissionIds.PerformanceMetricReadAll, PermissionIds.PerformanceMetricReadSelf,
                PermissionIds.TypePerformanceMetricReadAll,
                // Contract module (HRManager + HRStaff perms collapsed onto single HR role)
                PermissionIds.ContractReadAll, PermissionIds.ContractCreateAll, PermissionIds.ContractUpdateAll,
                PermissionIds.ContractDeleteAll, PermissionIds.ContractActivateAll, PermissionIds.ContractCancelAll,
                PermissionIds.ContractAppendixReadAll, PermissionIds.ContractAppendixCreateAll,
                PermissionIds.ContractAppendixActivateAll,
                PermissionIds.ContractTerminationReadAll, PermissionIds.ContractTerminationCreateAll,
                PermissionIds.ContractFileReadAll, PermissionIds.ContractFileUploadAll,
                PermissionIds.SalaryGradeReadAll, PermissionIds.SalaryGradeCreateAll,
                PermissionIds.SalaryGradeUpdateAll, PermissionIds.SalaryGradeDeleteAll,
                PermissionIds.AllowanceTypeReadAll, PermissionIds.AllowanceTypeCreateAll,
                PermissionIds.AllowanceTypeUpdateAll, PermissionIds.AllowanceTypeDeleteAll,
                PermissionIds.ContractTemplateReadAll, PermissionIds.ContractTemplateCreateAll,
                PermissionIds.ContractTemplateUpdateAll, PermissionIds.ContractTemplateDeleteAll,
            }),

            (SystemRoles.AccountantId, new[]
            {
                PermissionIds.UserReadSelf, PermissionIds.UserUpdateSelf,
                PermissionIds.EmployeeInformationReadAll, PermissionIds.EmployeeInformationReadSelf,
                PermissionIds.EmployeeInformationUpdateSelf,
                PermissionIds.PositionReadAll,
                PermissionIds.DepartmentReadAll,
                // Contract: read-only để chạy payroll
                PermissionIds.ContractReadAll,
                PermissionIds.ContractAppendixReadAll,
                PermissionIds.ContractTerminationReadAll,
                PermissionIds.ContractFileReadAll,
            }),

            (SystemRoles.DirectorId, new[]
            {
                PermissionIds.UserReadAll, PermissionIds.UserReadSelf, PermissionIds.UserUpdateSelf,
                PermissionIds.EmployeeInformationReadAll, PermissionIds.EmployeeInformationReadSelf,
                PermissionIds.EmployeeInformationUpdateSelf,
                PermissionIds.PositionReadAll, PermissionIds.PositionCreateAll,
                PermissionIds.PositionUpdateAll, PermissionIds.PositionDeleteAll,
                PermissionIds.DepartmentReadAll, PermissionIds.DepartmentCreateAll,
                PermissionIds.DepartmentUpdateAll, PermissionIds.DepartmentDeleteAll,
                PermissionIds.PerformanceMetricReadAll, PermissionIds.PerformanceMetricReadSelf,
                PermissionIds.PerformanceMetricApproveAll,
                PermissionIds.TypePerformanceMetricReadAll, PermissionIds.TypePerformanceMetricCreateAll,
                PermissionIds.TypePerformanceMetricUpdateAll, PermissionIds.TypePerformanceMetricDeleteAll,
            }),

            (SystemRoles.DepartmentManagerId, new[]
            {
                PermissionIds.UserReadSelf, PermissionIds.UserUpdateSelf,
                PermissionIds.EmployeeInformationReadDepartment, PermissionIds.EmployeeInformationReadSelf,
                PermissionIds.EmployeeInformationUpdateSelf,
                PermissionIds.PositionReadAll,
                PermissionIds.DepartmentReadAll,
                PermissionIds.PerformanceMetricReadAll, PermissionIds.PerformanceMetricReadSelf,
                PermissionIds.PerformanceMetricCreateAll, PermissionIds.PerformanceMetricUpdateAll,
                PermissionIds.PerformanceMetricDeleteAll, PermissionIds.PerformanceMetricApproveAll,
                PermissionIds.TypePerformanceMetricReadAll,
            }),

            (SystemRoles.ProjectManagerId, new[]
            {
                PermissionIds.UserReadSelf, PermissionIds.UserUpdateSelf,
                PermissionIds.EmployeeInformationReadSelf, PermissionIds.EmployeeInformationUpdateSelf,
                PermissionIds.PositionReadAll,
                PermissionIds.DepartmentReadAll,
                PermissionIds.PerformanceMetricReadAll, PermissionIds.PerformanceMetricReadSelf,
                PermissionIds.PerformanceMetricCreateAll, PermissionIds.PerformanceMetricUpdateAll,
                PermissionIds.PerformanceMetricDeleteAll,
                PermissionIds.TypePerformanceMetricReadAll,
            }),

            (SystemRoles.EmployeeId, new[]
            {
                PermissionIds.UserReadSelf, PermissionIds.UserUpdateSelf,
                PermissionIds.EmployeeInformationReadSelf, PermissionIds.EmployeeInformationUpdateSelf,
                PermissionIds.PositionReadAll,
                PermissionIds.DepartmentReadAll,
                PermissionIds.PerformanceMetricReadSelf,
                PermissionIds.TypePerformanceMetricReadAll,
                // Contract: chỉ xem HĐ của chính mình (self-scope)
                PermissionIds.ContractReadSelf,
                PermissionIds.ContractAppendixReadSelf,
                PermissionIds.ContractTerminationReadSelf,
                PermissionIds.ContractFileReadSelf,
            }),
        };

        return matrix.SelectMany(row => row.PermissionIds
            .Select(pid => new RolePermission { RoleId = row.RoleId, PermissionId = pid }));
    }

    private static int[] AllPermissionIds() => new[]
    {
        PermissionIds.UserReadAll, PermissionIds.UserReadSelf, PermissionIds.UserCreateAll,
        PermissionIds.UserUpdateAll, PermissionIds.UserUpdateSelf, PermissionIds.UserDeleteAll,
        PermissionIds.EmployeeInformationReadAll, PermissionIds.EmployeeInformationReadSelf,
        PermissionIds.EmployeeInformationCreateAll, PermissionIds.EmployeeInformationUpdateAll,
        PermissionIds.EmployeeInformationUpdateSelf, PermissionIds.EmployeeInformationDeleteAll,
        PermissionIds.PositionReadAll, PermissionIds.PositionCreateAll,
        PermissionIds.PositionUpdateAll, PermissionIds.PositionDeleteAll,
        PermissionIds.PerformanceMetricReadAll, PermissionIds.PerformanceMetricReadSelf,
        PermissionIds.PerformanceMetricCreateAll, PermissionIds.PerformanceMetricUpdateAll,
        PermissionIds.PerformanceMetricDeleteAll, PermissionIds.PerformanceMetricApproveAll,
        PermissionIds.TypePerformanceMetricReadAll, PermissionIds.TypePerformanceMetricCreateAll,
        PermissionIds.TypePerformanceMetricUpdateAll, PermissionIds.TypePerformanceMetricDeleteAll,
        PermissionIds.RoleReadAll, PermissionIds.RoleCreateAll, PermissionIds.RoleUpdateAll,
        PermissionIds.RoleDeleteAll, PermissionIds.RoleManagePermissionsAll,
        PermissionIds.RefreshTokenRevokeAll,
        // Contract module
        PermissionIds.ContractReadAll, PermissionIds.ContractReadSelf, PermissionIds.ContractCreateAll,
        PermissionIds.ContractUpdateAll, PermissionIds.ContractDeleteAll, PermissionIds.ContractActivateAll,
        PermissionIds.ContractCancelAll,
        PermissionIds.ContractAppendixReadAll, PermissionIds.ContractAppendixReadSelf,
        PermissionIds.ContractAppendixCreateAll, PermissionIds.ContractAppendixActivateAll,
        PermissionIds.ContractTerminationReadAll, PermissionIds.ContractTerminationReadSelf,
        PermissionIds.ContractTerminationCreateAll,
        PermissionIds.ContractFileReadAll, PermissionIds.ContractFileReadSelf,
        PermissionIds.ContractFileUploadAll, PermissionIds.ContractFileDeleteAll,
        PermissionIds.SalaryGradeReadAll, PermissionIds.SalaryGradeCreateAll,
        PermissionIds.SalaryGradeUpdateAll, PermissionIds.SalaryGradeDeleteAll,
        PermissionIds.AllowanceTypeReadAll, PermissionIds.AllowanceTypeCreateAll,
        PermissionIds.AllowanceTypeUpdateAll, PermissionIds.AllowanceTypeDeleteAll,
        PermissionIds.ContractTemplateReadAll, PermissionIds.ContractTemplateCreateAll,
        PermissionIds.ContractTemplateUpdateAll, PermissionIds.ContractTemplateDeleteAll,
        PermissionIds.ContractPartyReadAll, PermissionIds.ContractPartyCreateAll,
        // Department module
        PermissionIds.DepartmentReadAll, PermissionIds.DepartmentCreateAll,
        PermissionIds.DepartmentUpdateAll, PermissionIds.DepartmentDeleteAll,
        PermissionIds.EmployeeInformationReadDepartment,
    };
}
