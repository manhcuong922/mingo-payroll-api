namespace MPS.Application.Features.Notifications.Recipients;

/// <summary>
/// Mô tả NGƯỜI NHẬN của một thông báo theo kiểu khai báo (declarative), CHƯA phải danh sách user id cụ thể.
/// IRecipientResolver sẽ nở (expand) spec này thành danh sách user id thực tế.
///
/// Hỗ trợ 4 kiểu (kết hợp được):
///   • Users        — người nhận cụ thể (vd người được giao việc).
///   • Roles        — mọi user thuộc các role.
///   • Departments  — mọi user thuộc các phòng ban/xưởng.
///   • Permissions  — mọi user có (bất kỳ) permission code nào trong tập (kênh CHUẨN cho fan-out
///     nghiệp vụ vì permission ổn định hơn role; xem plan §2.3).
///   • AllUsers     — toàn bộ nhân viên đang hoạt động (broadcast).
/// Có thể loại trừ (Exclude) — thường dùng để KHÔNG báo cho chính người tạo sự kiện.
///
/// Bất biến (immutable) + fluent: mỗi phương thức trả về spec mới đã gộp.
/// </summary>
public sealed class RecipientSpec
{
    private readonly HashSet<int> _userIds;
    private readonly HashSet<int> _roleIds;
    private readonly HashSet<int> _departmentIds;
    private readonly HashSet<string> _permissionCodes;
    private readonly HashSet<int> _excludeUserIds;

    private RecipientSpec(
        HashSet<int> userIds,
        HashSet<int> roleIds,
        HashSet<int> departmentIds,
        HashSet<string> permissionCodes,
        HashSet<int> excludeUserIds,
        bool allUsers)
    {
        _userIds = userIds;
        _roleIds = roleIds;
        _departmentIds = departmentIds;
        _permissionCodes = permissionCodes;
        _excludeUserIds = excludeUserIds;
        AllUsers = allUsers;
    }

    public IReadOnlyCollection<int> UserIds => _userIds;
    public IReadOnlyCollection<int> RoleIds => _roleIds;
    public IReadOnlyCollection<int> DepartmentIds => _departmentIds;
    public IReadOnlyCollection<string> PermissionCodes => _permissionCodes;
    public IReadOnlyCollection<int> ExcludeUserIds => _excludeUserIds;
    public bool AllUsers { get; }

    /// <summary>Spec rỗng — không ai nhận. Dùng làm điểm bắt đầu để .To*(...) gộp dần.</summary>
    public static RecipientSpec None { get; } = new(new(), new(), new(), new(), new(), false);

    /// <summary>Gửi tới các user cụ thể.</summary>
    public static RecipientSpec Users(params int[] userIds) => None.ToUsers(userIds);

    /// <summary>Gửi tới các user cụ thể.</summary>
    public static RecipientSpec Users(IEnumerable<int> userIds) => None.ToUsers(userIds);

    /// <summary>Gửi tới mọi user thuộc các role.</summary>
    public static RecipientSpec Roles(params int[] roleIds) => None.ToRoles(roleIds);

    /// <summary>Gửi tới mọi user thuộc các role.</summary>
    public static RecipientSpec Roles(IEnumerable<int> roleIds) => None.ToRoles(roleIds);

    /// <summary>Gửi tới mọi user thuộc các phòng ban/xưởng.</summary>
    public static RecipientSpec Departments(params int[] departmentIds) => None.ToDepartments(departmentIds);

    /// <summary>Gửi tới mọi user thuộc các phòng ban/xưởng.</summary>
    public static RecipientSpec Departments(IEnumerable<int> departmentIds) => None.ToDepartments(departmentIds);

    /// <summary>Gửi tới mọi user active có (bất kỳ) permission code nào trong tập.</summary>
    public static RecipientSpec Permissions(params string[] permissionCodes) => None.ToPermissions(permissionCodes);

    /// <summary>Gửi tới mọi user active có (bất kỳ) permission code nào trong tập.</summary>
    public static RecipientSpec Permissions(IEnumerable<string> permissionCodes) => None.ToPermissions(permissionCodes);

    /// <summary>Broadcast tới toàn bộ nhân viên đang hoạt động.</summary>
    public static RecipientSpec Everyone() => new(new(), new(), new(), new(), new(), allUsers: true);

    public RecipientSpec ToUsers(IEnumerable<int> userIds) =>
        With(userIds: MergeInt(_userIds, userIds));

    public RecipientSpec ToRoles(IEnumerable<int> roleIds) =>
        With(roleIds: MergeInt(_roleIds, roleIds));

    public RecipientSpec ToDepartments(IEnumerable<int> departmentIds) =>
        With(departmentIds: MergeInt(_departmentIds, departmentIds));

    public RecipientSpec ToPermissions(IEnumerable<string> permissionCodes) =>
        With(permissionCodes: MergeStr(_permissionCodes, permissionCodes.Where(c => !string.IsNullOrWhiteSpace(c))));

    /// <summary>Loại trừ các user (vd người tạo sự kiện) khỏi kết quả cuối.</summary>
    public RecipientSpec Excluding(IEnumerable<int?> userIds) =>
        With(excludeUserIds: MergeInt(_excludeUserIds, userIds.Where(x => x.HasValue).Select(x => x!.Value)));

    /// <summary>Loại trừ 1 user (bỏ qua nếu null).</summary>
    public RecipientSpec Excluding(int? userId) => Excluding(new[] { userId });

    public bool IsEmpty =>
        !AllUsers
        && _userIds.Count == 0
        && _roleIds.Count == 0
        && _departmentIds.Count == 0
        && _permissionCodes.Count == 0;

    private RecipientSpec With(
        HashSet<int>? userIds = null,
        HashSet<int>? roleIds = null,
        HashSet<int>? departmentIds = null,
        HashSet<string>? permissionCodes = null,
        HashSet<int>? excludeUserIds = null) =>
        new(
            userIds ?? new(_userIds),
            roleIds ?? new(_roleIds),
            departmentIds ?? new(_departmentIds),
            permissionCodes ?? new(_permissionCodes, StringComparer.Ordinal),
            excludeUserIds ?? new(_excludeUserIds),
            AllUsers);

    private static HashSet<int> MergeInt(HashSet<int> current, IEnumerable<int> extra)
    {
        var result = new HashSet<int>(current);
        foreach (var x in extra) result.Add(x);
        return result;
    }

    private static HashSet<string> MergeStr(HashSet<string> current, IEnumerable<string> extra)
    {
        var result = new HashSet<string>(current, StringComparer.Ordinal);
        foreach (var x in extra) result.Add(x);
        return result;
    }
}
