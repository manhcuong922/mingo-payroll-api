namespace MPS.Domain.Constants;

public static class Permissions
{
    public const string UserReadAll = "User.Read.All";
    public const string UserReadSelf = "User.Read.Self";
    public const string UserCreateAll = "User.Create.All";
    public const string UserUpdateAll = "User.Update.All";
    public const string UserUpdateSelf = "User.Update.Self";
    public const string UserDeleteAll = "User.Delete.All";

    public const string EmployeeInformationReadAll = "EmployeeInformation.Read.All";
    public const string EmployeeInformationReadSelf = "EmployeeInformation.Read.Self";
    public const string EmployeeInformationCreateAll = "EmployeeInformation.Create.All";
    public const string EmployeeInformationUpdateAll = "EmployeeInformation.Update.All";
    public const string EmployeeInformationUpdateSelf = "EmployeeInformation.Update.Self";
    public const string EmployeeInformationDeleteAll = "EmployeeInformation.Delete.All";

    public const string PositionReadAll = "Position.Read.All";
    public const string PositionCreateAll = "Position.Create.All";
    public const string PositionUpdateAll = "Position.Update.All";
    public const string PositionDeleteAll = "Position.Delete.All";

    public const string PerformanceMetricReadAll = "PerformanceMetric.Read.All";
    public const string PerformanceMetricReadSelf = "PerformanceMetric.Read.Self";
    public const string PerformanceMetricCreateAll = "PerformanceMetric.Create.All";
    public const string PerformanceMetricUpdateAll = "PerformanceMetric.Update.All";
    public const string PerformanceMetricDeleteAll = "PerformanceMetric.Delete.All";
    public const string PerformanceMetricApproveAll = "PerformanceMetric.Approve.All";

    public const string TypePerformanceMetricReadAll = "TypePerformanceMetric.Read.All";
    public const string TypePerformanceMetricCreateAll = "TypePerformanceMetric.Create.All";
    public const string TypePerformanceMetricUpdateAll = "TypePerformanceMetric.Update.All";
    public const string TypePerformanceMetricDeleteAll = "TypePerformanceMetric.Delete.All";

    public const string RoleReadAll = "Role.Read.All";
    public const string RoleCreateAll = "Role.Create.All";
    public const string RoleUpdateAll = "Role.Update.All";
    public const string RoleDeleteAll = "Role.Delete.All";
    public const string RoleManagePermissionsAll = "Role.ManagePermissions.All";

    public const string RefreshTokenRevokeAll = "RefreshToken.Revoke.All";
}

public static class PermissionIds
{
    public const int UserReadAll = 1;
    public const int UserReadSelf = 2;
    public const int UserCreateAll = 3;
    public const int UserUpdateAll = 4;
    public const int UserUpdateSelf = 5;
    public const int UserDeleteAll = 6;

    public const int EmployeeInformationReadAll = 7;
    public const int EmployeeInformationReadSelf = 8;
    public const int EmployeeInformationCreateAll = 9;
    public const int EmployeeInformationUpdateAll = 10;
    public const int EmployeeInformationUpdateSelf = 11;
    public const int EmployeeInformationDeleteAll = 12;

    public const int PositionReadAll = 13;
    public const int PositionCreateAll = 14;
    public const int PositionUpdateAll = 15;
    public const int PositionDeleteAll = 16;

    public const int PerformanceMetricReadAll = 17;
    public const int PerformanceMetricReadSelf = 18;
    public const int PerformanceMetricCreateAll = 19;
    public const int PerformanceMetricUpdateAll = 20;
    public const int PerformanceMetricDeleteAll = 21;
    public const int PerformanceMetricApproveAll = 22;

    public const int TypePerformanceMetricReadAll = 23;
    public const int TypePerformanceMetricCreateAll = 24;
    public const int TypePerformanceMetricUpdateAll = 25;
    public const int TypePerformanceMetricDeleteAll = 26;

    public const int RoleReadAll = 27;
    public const int RoleCreateAll = 28;
    public const int RoleUpdateAll = 29;
    public const int RoleDeleteAll = 30;
    public const int RoleManagePermissionsAll = 31;

    public const int RefreshTokenRevokeAll = 32;
}
