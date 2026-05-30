# Authentication & Authorization Module — Implementation Plan

**Status:** Confirmed by Product Owner — Ready for Phase 1
**Project:** mingo-payroll-api (.NET 8, Clean Architecture, EF Core Code-First, MediatR CQRS)
**Last updated:** 2026-05-30

---

## 1. Mục tiêu

Bổ sung module Authentication & Authorization vào API:

- Login bằng `Username`/`Password`, cấp **JWT access token** + **refresh token** (rotation, reuse detection).
- **Role-based authorization** — mỗi role có một tập permission cố định, gán cho user qua `RoleId`.
- **Custom role management** — `SystemAdmin` tạo/sửa/xóa role mới với permission set tùy chỉnh.
- **Scope-aware permission** (`Self` / `All`) — Employee chỉ thấy & sửa data của chính mình.
- **System role bảo vệ** — 7 role mặc định không sửa name/description/xóa được, nhưng vẫn cho phép sửa permission set.

**Out of scope (chưa làm ở phase này):**
- Self-service change password
- Account lockout / rate limit login
- Audit log riêng cho auth events
- Per-user permission override (đã quyết định bỏ — quyền chỉ qua role)

---

## 2. Quyết định đã chốt

| # | Hạng mục | Quyết định |
|---|---|---|
| 1 | Refresh token | CÓ — opaque random base64, lưu **hash SHA-256**, rotation mỗi lần refresh, reuse detection revoke cả chain |
| 2 | Roles default | 7 system roles: `SystemAdmin`, `HR`, `Accountant`, `Director`, `DepartmentManager`, `ProjectManager`, `Employee` (`IsSystem = true`) |
| 3 | Custom role | SysAdmin tạo role mới (`IsSystem = false`) với permission set tùy chỉnh; xóa được nếu không có user nào dùng |
| 4 | System role rules | KHÔNG xóa, KHÔNG sửa name/description; CÓ sửa permissions (linh hoạt vận hành) |
| 5 | PK `User` | `int Id IDENTITY` (đổi từ `EmployeeCode string`); `EmployeeCode` giữ thành unique column |
| 6 | Password hashing | `BCrypt.Net-Next` work factor 12, seed migrate plain text qua `PasswordSeederService` chạy startup |
| 7 | Scope `Self`/`All` | Encoding `Resource.Action.Scope` (vd `User.Read.Self`, `User.Read.All`); filter trong query handler |
| 8 | Employee gọi list endpoint | **403 Forbidden** (không return `[selfOnly]`) — list không có nghĩa khi chỉ có quyền self |
| 9 | Permission override per-user | KHÔNG có — quyền chỉ qua role |
| 10 | Migration strategy | **Option 1**: drop DB local + xóa migration `InitialCreate` cũ + tạo lại `InitialCreate` mới với schema hoàn chỉnh |
| 11 | JWT algorithm | HS256, secret ≥ 32 chars, qua user-secrets ở dev / env var ở prod |
| 12 | JWT TTL | Access token 60 phút; Refresh token 7 ngày |
| 13 | Permission caching | `IMemoryCache`, key `perm:user:{userId}`, TTL 5 phút sliding |

---

## 3. Cảnh báo: đổi PK `User` = breaking change lớn

Khảo sát: **35 file** đang reference `EmployeeCode` làm key. Sau khi đổi:

- `User.Id` (int identity) là PK mới; `EmployeeCode` (string) trở thành **unique not-null column** (vẫn dùng làm mã NV nghiệp vụ).
- `EmployeeInformation`: FK đổi từ `EmployeeCode → User.EmployeeCode` thành `UserId → User.Id`; bỏ field `EmployeeCode` (truy cập qua `User.EmployeeCode` nav nếu cần).
- JWT claim `sub` = `User.Id` (int dạng string).
- Tất cả route `/api/users/{employeeCode}` → `/api/users/{id:int}`. Thêm endpoint phụ `/api/users/by-code/{employeeCode}` để search theo mã NV.
- 16+ command/query/validator/handler của `User` & `EmployeeInformation` cần sửa signature.

**Migration (Option 1 — đã chốt):**
1. `dotnet ef database drop -f -p MPS.Infrastructure -s MPS.API` (drop DB local).
2. Xóa folder `src/MPS.Infrastructure/Persistence/Migrations/`.
3. Sửa entities + configurations theo schema mới.
4. `dotnet ef migrations add InitialCreate -p MPS.Infrastructure -s MPS.API`.
5. `dotnet ef database update -p MPS.Infrastructure -s MPS.API`.

---

## 4. Thiết kế chi tiết

### 4.1. Permission model

**Encoding:** `Resource.Action.Scope`

Ví dụ: `User.Read.Self`, `User.Read.All`, `Position.Create.All`, `Role.ManagePermissions.All`.

- `Resource`: tên entity hoặc nhóm chức năng (`User`, `EmployeeInformation`, `Position`, `PerformanceMetric`, `TypePerformanceMetric`, `Role`, `RefreshToken`).
- `Action`: `Read`, `Create`, `Update`, `Delete`, `Approve`, `ManagePermissions`, `Revoke`...
- `Scope`: `Self` (chỉ data của chính mình), `All` (toàn bộ).

Master list lưu trong bảng `Permissions`, seed cố định qua `HasData`.

### 4.2. Effective permission resolution

```
effective(user) = rolePermissions[user.RoleId]
```

- `IPermissionResolver.GetEffectivePermissionsAsync(int userId, CT)` → load `User.RoleId` → load `RolePermissions` → return `IReadOnlySet<string>` permission codes.
- Cache `IMemoryCache`:
  - Key: `perm:user:{userId}`
  - TTL: 5 phút sliding
- **Invalidation:**
  - User đổi `RoleId` → invalidate `perm:user:{userId}`.
  - Permission set của 1 role thay đổi → query toàn bộ user của role đó + loop invalidate.
  - Role bị xóa → không cần invalidate (xóa role đã chặn nếu còn user).

### 4.3. JWT & Refresh token

**Access token (JWT, HS256):**
- Claims: `sub` = UserId, `name` = Username, `email`, `employeeCode`, `role` = RoleName, `jti`, `iat`, `exp`.
- KHÔNG embed permissions (tránh stale + token phình to).
- TTL: 60 phút (config).

**Refresh token:**
- Opaque random 64-byte → base64.
- Lưu **hash SHA-256** trong DB (raw token chỉ trả về client 1 lần).
- TTL: 7 ngày (config).
- Mỗi lần `/refresh` → revoke token cũ + cấp pair mới; `ReplacedByTokenHash` link 2 token.
- **Reuse detection:** nếu refresh token đã revoked được dùng lại → revoke toàn bộ token chain của user đó (signal of theft).

**Endpoints:**
- `POST /api/auth/login` → `{ accessToken, refreshToken, accessExpiresAt, refreshExpiresAt }`
- `POST /api/auth/refresh` body `{ refreshToken }` → trả pair mới
- `POST /api/auth/logout` body `{ refreshToken }` → revoke refresh token
- `GET /api/auth/me` → trả user info + effective permissions

### 4.4. Password hashing

- `BCrypt.Net-Next` work factor 12 (~200-300ms/hash).
- `User.PasswordHash` cột `nvarchar(512)`.
- **Migrate plain text data hiện có:** `PasswordSeederService` chạy 1 lần startup (chỉ env `Development` hoặc trigger thủ công). Idempotent: detect plain text bằng heuristic `!hash.StartsWith("$2")` → hash lại.

### 4.5. Authorization enforcement

- Custom attribute `[HasPermission("User.Read.All")]` (kế thừa `AuthorizeAttribute`).
- Custom attribute `[HasAnyPermission("User.Read.Self", "User.Read.All")]` cho endpoint có scope.
- `PermissionPolicyProvider` (dynamic) — prefix `PERM:` cho single, `PERMANY:c1|c2|...` cho any-of, fallback to `DefaultAuthorizationPolicyProvider`.
- `PermissionAuthorizationHandler` + `AnyPermissionAuthorizationHandler`.

**Scope enforcement trong handler:**
- Inject `ICurrentUserService` (đọc UserId từ JWT) + `IPermissionResolver`.
- Helper `IScopeChecker`:
  - `bool CanAccess(IReadOnlySet<string> perms, string resource, string action, int targetUserId, int currentUserId)`
  - Trả `true` nếu có `Resource.Action.All`, hoặc có `Resource.Action.Self` và `targetUserId == currentUserId`.
- Quy tắc list endpoint: nếu user chỉ có `.Self` (không có `.All`) → **403 Forbidden** (đã chốt).
- Quy tắc detail/update endpoint: chỉ `.Self` + access ID khác mình → 403.

### 4.6. Role management

**`Role` entity bổ sung:**
- `bool IsSystem` (default false; 7 system roles = true)
- `string? Description`
- `DateTime CreatedAt`, `DateTime? UpdatedAt`

**Rules:**
| Action | `IsSystem = true` | `IsSystem = false` |
|---|---|---|
| Rename / sửa description | KHÔNG | CÓ |
| Xóa | KHÔNG | CÓ (chặn nếu còn user) |
| Sửa permission set | CÓ | CÓ |

**Endpoints:**
| Method | Route | Permission |
|---|---|---|
| GET | `/api/roles` | `Role.Read.All` |
| GET | `/api/roles/{id}` | `Role.Read.All` |
| POST | `/api/roles` | `Role.Create.All` |
| PUT | `/api/roles/{id}` | `Role.Update.All` (chặn nếu `IsSystem`) |
| DELETE | `/api/roles/{id}` | `Role.Delete.All` (chặn nếu `IsSystem` hoặc còn user) |
| GET | `/api/roles/{id}/permissions` | `Role.Read.All` |
| PUT | `/api/roles/{id}/permissions` | `Role.ManagePermissions.All` (invalidate cache toàn user của role) |
| GET | `/api/permissions` | `Role.Read.All` (list master) |
| GET | `/api/users/{id}/permissions` | `Role.Read.All` (effective permissions debugging) |
| POST | `/api/admin/users/{id}/revoke-tokens` | `RefreshToken.Revoke.All` (force logout) |

**Domain exceptions mới:**
- `SystemRoleModificationException` → 403/409 khi sửa/xóa system role.
- `RoleInUseException` → 409 khi xóa role còn user.

---

## 5. Permission seed matrix (default cho 7 system roles)

| Permission | SysAdmin | HR | Accountant | Director | DeptMgr | ProjMgr | Employee |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| `User.Read.All` | ✓ | ✓ | | ✓ | ✓ | | |
| `User.Read.Self` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `User.Create.All` | ✓ | ✓ | | | | | |
| `User.Update.All` | ✓ | ✓ | | | | | |
| `User.Update.Self` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `User.Delete.All` | ✓ | ✓ | | | | | |
| `EmployeeInformation.Read.All` | ✓ | ✓ | ✓ | ✓ | ✓ | | |
| `EmployeeInformation.Read.Self` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `EmployeeInformation.Create.All` | ✓ | ✓ | | | | | |
| `EmployeeInformation.Update.All` | ✓ | ✓ | | | | | |
| `EmployeeInformation.Update.Self` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `EmployeeInformation.Delete.All` | ✓ | ✓ | | | | | |
| `Position.Read.All` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Position.Create.All` | ✓ | ✓ | | ✓ | | | |
| `Position.Update.All` | ✓ | ✓ | | ✓ | | | |
| `Position.Delete.All` | ✓ | ✓ | | ✓ | | | |
| `PerformanceMetric.Read.All` | ✓ | ✓ | | ✓ | ✓ | ✓ | |
| `PerformanceMetric.Read.Self` | ✓ | ✓ | | ✓ | ✓ | ✓ | ✓ |
| `PerformanceMetric.Create.All` | ✓ | | | | ✓ | ✓ | |
| `PerformanceMetric.Update.All` | ✓ | | | | ✓ | ✓ | |
| `PerformanceMetric.Delete.All` | ✓ | | | | ✓ | ✓ | |
| `PerformanceMetric.Approve.All` | ✓ | | | ✓ | ✓ | | |
| `TypePerformanceMetric.Read.All` | ✓ | ✓ | | ✓ | ✓ | ✓ | ✓ |
| `TypePerformanceMetric.Create.All` | ✓ | | | ✓ | | | |
| `TypePerformanceMetric.Update.All` | ✓ | | | ✓ | | | |
| `TypePerformanceMetric.Delete.All` | ✓ | | | ✓ | | | |
| `Role.Read.All` | ✓ | | | | | | |
| `Role.Create.All` | ✓ | | | | | | |
| `Role.Update.All` | ✓ | | | | | | |
| `Role.Delete.All` | ✓ | | | | | | |
| `Role.ManagePermissions.All` | ✓ | | | | | | |
| `RefreshToken.Revoke.All` | ✓ | | | | | | |

---

## 6. Domain model

### 6.1. Entities mới

**`Permission`**
- `int Id` (identity PK)
- `string Code` (unique, nvarchar(100))
- `string Resource` (nvarchar(50))
- `string Action` (nvarchar(50))
- `string Scope` (nvarchar(20))
- `string Description` (nvarchar(255))
- Nav: `ICollection<RolePermission> RolePermissions`

**`RolePermission`** (join table)
- `int RoleId` + `int PermissionId` (composite PK)
- Nav: `Role`, `Permission`
- Cascade delete on Role + Permission.

**`RefreshToken`**
- `int Id` (identity PK)
- `int UserId` (FK → User.Id, cascade delete)
- `string TokenHash` (unique, nvarchar(128))
- `DateTime ExpiresAt`
- `DateTime CreatedAt`
- `DateTime? RevokedAt`
- `string? RevokedReason` (nvarchar(255))
- `string? ReplacedByTokenHash` (nvarchar(128))
- `string? CreatedByIp` (nvarchar(64))
- `string? RevokedByIp` (nvarchar(64))
- Index: `(UserId, ExpiresAt)`
- Nav: `User`

### 6.2. Entities sửa

**`User`**
- THÊM: `int Id` (PK identity), `string PasswordHash` (thay `Password`, nvarchar(512)), `DateTime? LastLoginAt`, `DateTime? PasswordChangedAt`
- GIỮ: `string EmployeeCode` (unique, not-null, nvarchar(50)), `FullName`, `RoleId`, `Username` (unique), `Email` (unique), `Dob`, `PhoneNo`, `Status`
- Nav: `Role`, `EmployeeInformation`, `ICollection<RefreshToken> RefreshTokens`

**`EmployeeInformation`**
- THÊM: `int UserId` (FK → User.Id)
- BỎ: field `EmployeeCode` (truy cập qua `User.EmployeeCode` nav)
- PK vẫn `int Id`
- Unique index `(UserId)` (one-to-one)

**`Role`**
- THÊM: `bool IsSystem`, `string? Description` (nvarchar(255)), `DateTime CreatedAt`, `DateTime? UpdatedAt`
- Nav: `ICollection<RolePermission> RolePermissions`, `ICollection<User> Users`

### 6.3. Quan hệ

```
Role 1 ─< RolePermission >─ 1 Permission
User * ── 1 Role
User 1 ── 1 EmployeeInformation (FK: UserId)
User 1 ─< RefreshToken
```

---

## 7. Implementation Phases

### Phase 1 — Restructure `User` PK + Domain entities + Migration

**Mục tiêu:** Schema mới, build OK, CRUD User & EmployeeInformation hoạt động với int Id.

**Bước:**
1. Sửa `User.cs`: thêm `Id int`, rename `Password` → `PasswordHash`, thêm fields + nav `RefreshTokens`.
2. Sửa `EmployeeInformation.cs`: bỏ `EmployeeCode` field, thêm `int UserId`.
3. Sửa `Role.cs`: thêm `IsSystem`, `Description`, `CreatedAt`, `UpdatedAt`, nav `RolePermissions`, `Users`.
4. Tạo entities mới: `Permission`, `RolePermission`, `RefreshToken`.
5. Tạo `src/MPS.Domain/Constants/Permissions.cs` (const all permission codes).
6. Tạo `src/MPS.Domain/Constants/SystemRoles.cs` (const role names + IDs cho 7 system roles).
7. Tạo `src/MPS.Domain/Enums/PermissionScope.cs` (`Self`, `All`).
8. Interfaces: `IPermissionRepository`, `IRolePermissionRepository`, `IRoleRepository`, `IRefreshTokenRepository`.
9. Sửa `IUserRepository`: đổi key string → int, thêm `GetByIdAsync`, `GetByUsernameAsync`, giữ `GetByEmployeeCodeAsync`.
10. Sửa `IEmployeeInformationRepository`: đổi key string → int, thêm `GetByUserIdAsync`.
11. Sửa entity configurations:
    - `UserEntityConfiguration`: PK Id identity, unique `EmployeeCode`/`Username`/`Email`.
    - `EmployeeInformationEntityConfiguration`: FK `UserId`, unique `UserId`.
    - `RoleEntityConfiguration`: thêm field config + `HasData` 7 system roles với Id cố định (1-7), `IsSystem=true`.
12. Tạo configurations mới: `PermissionEntityConfiguration` (+ `HasData` master), `RolePermissionEntityConfiguration` (+ `HasData` matrix mục 5), `RefreshTokenEntityConfiguration`.
13. Update `AppDbContext`: thêm `DbSet<Permission>`, `DbSet<RolePermission>`, `DbSet<RefreshToken>`.
14. Implement repositories mới + sửa `UserRepository`, `EmployeeInformationRepository`.
15. Sửa toàn bộ Application layer cho User & EmployeeInformation (Commands/Queries/Validators/Handlers/DTOs) đổi sang `int Id`.
16. Sửa Controllers + Requests cho User & EmployeeInformation.
17. Đăng ký DI 4 repo mới.
18. `dotnet ef database drop -f` → xóa folder `Migrations/` → `dotnet ef migrations add InitialCreate` → `dotnet ef database update`.

**Risk:** HIGH — đụng 35 file.

---

### Phase 2 — Password hashing + JWT + Refresh token infrastructure

**Mục tiêu:** services sẵn sàng, chưa wire vào endpoint.

**Bước:**
1. NuGet vào `MPS.Infrastructure`:
   - `BCrypt.Net-Next` (v4.x)
   - `Microsoft.AspNetCore.Authentication.JwtBearer` (v8.0.x)
2. Abstractions ở `MPS.Application/Abstractions/Security/`:
   - `IPasswordHasher` — `string Hash(string)`, `bool Verify(string, string)`
   - `IJwtTokenService` — `(string token, DateTime expiresAt) GenerateAccessToken(User user)`
   - `IRefreshTokenService` — `Task<(string raw, RefreshToken entity)> GenerateAsync(int userId, string? ip, CT)`, `Task<RefreshToken?> ValidateAsync(string raw, CT)`, `Task RevokeAsync(string raw, string reason, string? ip, CT)`, `Task<(string newRaw, RefreshToken entity)> RotateAsync(string oldRaw, string? ip, CT)`, `Task RevokeAllForUserAsync(int userId, string reason, CT)`
   - `ICurrentUserService` — `int? UserId`, `string? EmployeeCode`, `string? Username`, `string? Role`, `bool IsAuthenticated`
3. Implementations ở `MPS.Infrastructure/Security/`:
   - `BCryptPasswordHasher`
   - `JwtSettings` (Options pattern: `Issuer`, `Audience`, `SecretKey`, `AccessTokenMinutes`, `RefreshTokenDays`)
   - `JwtTokenService`
   - `RefreshTokenService` (raw = `Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))`; hash = SHA-256)
   - `CurrentUserService` (đọc `IHttpContextAccessor.HttpContext.User.Claims`)
   - `PasswordSeederService` (idempotent migrate plain text)
4. `appsettings.json` thêm section `JwtSettings` (placeholder). `appsettings.Development.json` cho dev key.
5. DI registration trong `InfrastructureServiceRegistration` + `Configure<JwtSettings>` từ `IConfiguration`.

**Risk:** Low.

---

### Phase 3 — Login + Refresh + Logout + Auth middleware

**Mục tiêu:** Login flow đầy đủ, có Bearer auth.

**Bước:**
1. Features `MPS.Application/Features/Auth/`:
   - `Commands/LoginCommand` + handler (find user by username, verify password, gen access + refresh, update `LastLoginAt`)
   - `Commands/RefreshTokenCommand` + handler (validate, rotate, reuse detection → revoke chain)
   - `Commands/LogoutCommand` + handler (revoke refresh)
   - `Queries/GetCurrentUserQuery` + handler (return user info + effective permissions)
   - Validators + DTOs (`LoginResponseDto`, `RefreshResponseDto`, `CurrentUserDto`)
2. Domain exceptions: `InvalidCredentialsException`, `InvalidRefreshTokenException`.
3. Sửa `ExceptionHandlingMiddleware` → map 401 cho auth exceptions, 403 cho `ForbiddenAccessException`.
4. Controller `AuthController` — `POST login`, `POST refresh`, `POST logout`, `GET me`.
5. Request DTOs ở `MPS.API/Requests/Auth/`.
6. `Program.cs`:
   - `AddHttpContextAccessor`
   - `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` đọc `JwtSettings`
   - `app.UseAuthentication()` **trước** `app.UseAuthorization()`
   - Swagger `AddSecurityDefinition("Bearer", ...)` + `AddSecurityRequirement`
7. Chạy `PasswordSeederService` 1 lần để hash data plain text hiện có (qua startup hook env Dev).

**Risk:** Medium — middleware order, refresh token reuse detection cần test kỹ.

---

### Phase 4 — Permission resolver + Authorization handler + Apply vào controllers cũ

**Mục tiêu:** enforce permission + scope trên CRUD hiện có.

**Bước:**
1. Abstractions:
   - `IPermissionResolver` — `Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(int userId, CT)`
   - `IPermissionCacheInvalidator` — `Task InvalidateUserAsync(int userId)`, `Task InvalidateUsersByRoleAsync(int roleId, CT)`
   - `IScopeChecker` — `bool CanAccessTarget(IReadOnlySet<string> perms, string resource, string action, int targetUserId, int currentUserId)`, `bool HasAnyScope(IReadOnlySet<string> perms, string resource, string action)`
   - `IForbiddenAccessGuard` hoặc throw trực tiếp từ handler.
2. Implementations:
   - `PermissionResolver` (query Role + RolePermissions, cache `IMemoryCache`)
   - `PermissionCacheInvalidator`
   - `ScopeChecker`
3. Authorization infra ở `MPS.API/Authorization/`:
   - `PermissionRequirement(string code)`
   - `AnyPermissionRequirement(string[] codes)`
   - `PermissionAuthorizationHandler`
   - `AnyPermissionAuthorizationHandler`
   - `PermissionPolicyProvider` (dynamic, prefix `PERM:` và `PERMANY:c1|c2|...`)
   - `HasPermissionAttribute(string code)`
   - `HasAnyPermissionAttribute(params string[] codes)`
4. DI registration trong `Program.cs`: `AddMemoryCache`, register handler/provider/resolver/invalidator/scopechecker.
5. Apply attributes lên controllers hiện có (theo matrix):
   - `UserController`:
     - `GetAll` → `[HasPermission("User.Read.All")]`
     - `GetById/{id}` → `[HasAnyPermission("User.Read.Self", "User.Read.All")]` + scope check trong handler
     - `Create` → `[HasPermission("User.Create.All")]`
     - `Update/{id}` → `[HasAnyPermission("User.Update.Self", "User.Update.All")]` + scope check
     - `Delete/{id}` → `[HasPermission("User.Delete.All")]`
   - `EmployeeInformationController` — tương tự với `EmployeeInformation.*`
   - `PositionController`, `PerformanceMetricController`, `TypePerformanceMetricController` — apply theo matrix
6. Scope enforcement trong handler:
   - Inject `ICurrentUserService` + `IPermissionResolver` + `IScopeChecker`.
   - Detail/Update/Delete: nếu chỉ có `.Self` và `targetId != currentUserId` → throw `ForbiddenAccessException`.
   - List: nếu không có `.All` → throw `ForbiddenAccessException` (đã chốt: 403).
7. Domain exception `ForbiddenAccessException` map 403.

**Risk:** Medium-High — logic scope dễ sót case. Test matrix per role kỹ.

---

### Phase 5 — Role CRUD + Permission viewer + Force logout

**Mục tiêu:** SysAdmin quản lý role qua API.

**Bước:**
1. Features `MPS.Application/Features/Role/`:
   - `Queries/GetAllRolesQuery` + handler
   - `Queries/GetRoleByIdQuery` + handler
   - `Queries/GetRolePermissionsQuery` + handler
   - `Commands/CreateRoleCommand` + handler (IsSystem mặc định false)
   - `Commands/UpdateRoleCommand` + handler (throw `SystemRoleModificationException` nếu IsSystem; cho phép sửa name/description)
   - `Commands/DeleteRoleCommand` + handler (throw `SystemRoleModificationException` nếu IsSystem; throw `RoleInUseException` nếu còn user)
   - `Commands/UpdateRolePermissionsCommand` + handler (replace full set; invalidate cache toàn user của role; **cho phép sửa cả system role**)
   - Validators + DTOs (`RoleDto`, `RoleDetailDto` chứa permissions list)
2. Features `MPS.Application/Features/Permission/`:
   - `Queries/GetAllPermissionsQuery` + handler (list master, group theo Resource)
   - `Queries/GetUserEffectivePermissionsQuery` + handler (debug / UI hiển thị)
   - DTO `PermissionDto`
3. Features `MPS.Application/Features/RefreshToken/`:
   - `Commands/RevokeAllUserTokensCommand` + handler (SysAdmin force logout)
4. Controllers:
   - `RoleController` — full CRUD + `GET /api/roles/{id}/permissions` + `PUT /api/roles/{id}/permissions`
   - `PermissionController` — `GET /api/permissions`, `GET /api/users/{id}/permissions`
   - `SecurityAdminController` — `POST /api/admin/users/{id}/revoke-tokens`
5. Requests `MPS.API/Requests/Role/`:
   - `CreateRoleRequest`, `UpdateRoleRequest`, `UpdateRolePermissionsRequest` (chứa `int[] PermissionIds`)
6. Mỗi mutation invalidate cache phù hợp.
7. Audit field placeholder: ghi `UpdatedAt = DateTime.UtcNow` trên Role khi sửa.

**Risk:** Medium.

---

### Phase 6 — Tests (Unit + Integration)

**Mục tiêu:** coverage ≥ 80% cho code mới.

**Bước:**
1. Tạo test projects (chưa có):
   - `tests/MPS.Application.Tests/` (xUnit + FluentAssertions + NSubstitute)
   - `tests/MPS.Infrastructure.Tests/` (xUnit + EF Core SQLite in-memory)
   - `tests/MPS.API.IntegrationTests/` (xUnit + `WebApplicationFactory` + SQLite hoặc Testcontainers SQL Server)
   - Update `MPS.sln` thêm 3 test projects.
2. Unit tests:
   - `BCryptPasswordHasherTests` — hash khác mỗi lần, verify đúng/sai
   - `JwtTokenServiceTests` — claims đầy đủ, signature đúng, expiry
   - `RefreshTokenServiceTests` — generate hash đúng, rotation, reuse detection revoke chain, expiry validation
   - `PermissionResolverTests` — role only, cache hit/miss, invalidation
   - `ScopeCheckerTests` — `.Self` + same user → true, `.Self` + other user → false, `.All` → true
   - `LoginCommandHandlerTests`, `RefreshTokenCommandHandlerTests`, `LogoutCommandHandlerTests`, `GetCurrentUserQueryHandlerTests`
   - `CreateRoleCommandHandlerTests`, `UpdateRoleCommandHandlerTests` (chặn IsSystem), `DeleteRoleCommandHandlerTests` (chặn IsSystem + còn user), `UpdateRolePermissionsCommandHandlerTests` (cho phép sửa system role + invalidate cache)
   - All FluentValidation validators
3. Integration tests với `WebApplicationFactory`:
   - Auth flow: login → me → refresh → logout → reuse old refresh (revoke chain)
   - Permission enforcement: Employee role gọi `GET /api/users` → **403**; `GET /api/users/{ownId}` → 200; `GET /api/users/{otherId}` → 403
   - Role CRUD: SysAdmin tạo custom role + gán user + user có đúng quyền sau invalidate
   - System role: rename → 403/409; delete → 403/409; update permissions → 200
   - Delete role còn user → 409
   - Force logout: revoke-all-tokens → user gọi refresh failed
4. CI: `dotnet test --collect:"XPlat Code Coverage"` + report.

**Risk:** Low-Medium — setup `WebApplicationFactory` lần đầu tốn thời gian.

---

## 8. File tree dự kiến

### Phase 1

```
src/MPS.Domain/
  Entities/
    User.cs                                             [MODIFY]
    EmployeeInformation.cs                              [MODIFY]
    Role.cs                                             [MODIFY]
    Permission.cs                                       [NEW]
    RolePermission.cs                                   [NEW]
    RefreshToken.cs                                     [NEW]
  Constants/
    Permissions.cs                                      [NEW]
    SystemRoles.cs                                      [NEW]
  Enums/
    PermissionScope.cs                                  [NEW]
  Interfaces/
    IUserRepository.cs                                  [MODIFY]
    IEmployeeInformationRepository.cs                   [MODIFY]
    IRoleRepository.cs                                  [NEW]
    IPermissionRepository.cs                            [NEW]
    IRolePermissionRepository.cs                        [NEW]
    IRefreshTokenRepository.cs                          [NEW]
  Exceptions/
    ForbiddenAccessException.cs                         [NEW]
    SystemRoleModificationException.cs                  [NEW]
    RoleInUseException.cs                               [NEW]

src/MPS.Infrastructure/
  Persistence/
    AppDbContext.cs                                     [MODIFY: +3 DbSet]
    Configurations/
      UserEntityConfiguration.cs                        [MODIFY]
      EmployeeInformationEntityConfiguration.cs         [MODIFY]
      RoleEntityConfiguration.cs                        [MODIFY: +HasData 7 system roles]
      PermissionEntityConfiguration.cs                  [NEW + HasData master]
      RolePermissionEntityConfiguration.cs              [NEW + HasData matrix]
      RefreshTokenEntityConfiguration.cs                [NEW]
    Migrations/                                         [DELETE old → regenerate]
  Repositories/
    UserRepository.cs                                   [MODIFY]
    EmployeeInformationRepository.cs                    [MODIFY]
    RoleRepository.cs                                   [NEW]
    PermissionRepository.cs                             [NEW]
    RolePermissionRepository.cs                         [NEW]
    RefreshTokenRepository.cs                           [NEW]
  DependencyInjection/
    InfrastructureServiceRegistration.cs                [MODIFY]

src/MPS.Application/
  Features/User/
    Commands/UserCreateCommand.cs                       [MODIFY]
    Commands/UserCreateCommandHandler.cs                [MODIFY]
    Commands/UserUpdateCommand.cs                       [MODIFY: id int]
    Commands/UserUpdateCommandHandler.cs                [MODIFY]
    Commands/UserDeleteCommand.cs                       [MODIFY: id int]
    Commands/UserDeleteCommandHandler.cs                [MODIFY]
    Queries/UserGetByIdQuery.cs                         [NEW]
    Queries/UserGetByIdQueryHandler.cs                  [NEW]
    Queries/UserGetByEmployeeCodeQuery.cs               [MODIFY: secondary lookup]
    Queries/UserGetByEmployeeCodeQueryHandler.cs        [MODIFY]
    Queries/UserGetAllQueryHandler.cs                   [MODIFY: return Id]
    Validators/UserCreateCommandValidator.cs            [MODIFY]
    Validators/UserUpdateCommandValidator.cs            [MODIFY]
    Validators/UserDeleteCommandValidator.cs            [MODIFY]
  Features/EmployeeInformation/
    Commands/EmployeeInformationCreateCommand.cs        [MODIFY: UserId int]
    Commands/EmployeeInformationCreateCommandHandler.cs [MODIFY]
    Commands/EmployeeInformationUpdateCommand.cs        [MODIFY]
    Commands/EmployeeInformationUpdateCommandHandler.cs [MODIFY]
    Commands/EmployeeInformationDeleteCommand.cs        [MODIFY]
    Commands/EmployeeInformationDeleteCommandHandler.cs [MODIFY]
    Queries/EmployeeInformationGetByIdQuery.cs          [MODIFY]
    Queries/EmployeeInformationGetByIdQueryHandler.cs   [MODIFY]
    Queries/EmployeeInformationGetAllQueryHandler.cs    [MODIFY]
    Validators/*.cs                                     [MODIFY]
  DTOs/
    UserDto.cs                                          [MODIFY: +Id int]
    EmployeeInformationDto.cs                           [MODIFY: +UserId]

src/MPS.API/
  Controllers/
    UserController.cs                                   [MODIFY: route {id:int} + /by-code/{code}]
    EmployeeInformationController.cs                    [MODIFY]
  Requests/User/
    CreateUserRequest.cs                                [MODIFY]
    UpdateUserRequest.cs                                [MODIFY]
  Requests/EmployeeInformation/
    CreateEmployeeInformationRequest.cs                 [MODIFY]
    UpdateEmployeeInformationRequest.cs                 [MODIFY]
```

### Phase 2

```
src/MPS.Application/Abstractions/Security/
  IPasswordHasher.cs                                    [NEW]
  IJwtTokenService.cs                                   [NEW]
  IRefreshTokenService.cs                               [NEW]
  ICurrentUserService.cs                                [NEW]

src/MPS.Infrastructure/Security/
  BCryptPasswordHasher.cs                               [NEW]
  JwtSettings.cs                                        [NEW]
  JwtTokenService.cs                                    [NEW]
  RefreshTokenService.cs                                [NEW]
  CurrentUserService.cs                                 [NEW]
  PasswordSeederService.cs                              [NEW]
src/MPS.Infrastructure/MPS.Infrastructure.csproj        [MODIFY: +BCrypt.Net-Next, +JwtBearer]
src/MPS.Infrastructure/DependencyInjection/InfrastructureServiceRegistration.cs  [MODIFY]
src/MPS.API/appsettings.json                            [MODIFY: +JwtSettings]
src/MPS.API/appsettings.Development.json                [NEW or MODIFY]
```

### Phase 3

```
src/MPS.Application/Features/Auth/
  Commands/LoginCommand.cs                              [NEW]
  Commands/LoginCommandHandler.cs                       [NEW]
  Commands/RefreshTokenCommand.cs                       [NEW]
  Commands/RefreshTokenCommandHandler.cs                [NEW]
  Commands/LogoutCommand.cs                             [NEW]
  Commands/LogoutCommandHandler.cs                      [NEW]
  Queries/GetCurrentUserQuery.cs                        [NEW]
  Queries/GetCurrentUserQueryHandler.cs                 [NEW]
  Validators/LoginCommandValidator.cs                   [NEW]
  Validators/RefreshTokenCommandValidator.cs            [NEW]
  Validators/LogoutCommandValidator.cs                  [NEW]
  DTOs/LoginResponseDto.cs                              [NEW]
  DTOs/RefreshResponseDto.cs                            [NEW]
  DTOs/CurrentUserDto.cs                                [NEW]

src/MPS.Domain/Exceptions/
  InvalidCredentialsException.cs                        [NEW]
  InvalidRefreshTokenException.cs                       [NEW]

src/MPS.API/
  Controllers/AuthController.cs                         [NEW]
  Requests/Auth/LoginRequest.cs                         [NEW]
  Requests/Auth/RefreshTokenRequest.cs                  [NEW]
  Requests/Auth/LogoutRequest.cs                        [NEW]
  Middleware/ExceptionHandlingMiddleware.cs             [MODIFY: 401/403 map]
  Program.cs                                            [MODIFY: AddAuthentication+JwtBearer, Swagger Bearer, HttpContextAccessor]
```

### Phase 4

```
src/MPS.Application/Abstractions/Security/
  IPermissionResolver.cs                                [NEW]
  IPermissionCacheInvalidator.cs                        [NEW]
  IScopeChecker.cs                                      [NEW]

src/MPS.Infrastructure/Security/
  PermissionResolver.cs                                 [NEW]
  PermissionCacheInvalidator.cs                         [NEW]
  ScopeChecker.cs                                       [NEW]

src/MPS.API/Authorization/
  PermissionRequirement.cs                              [NEW]
  AnyPermissionRequirement.cs                           [NEW]
  PermissionAuthorizationHandler.cs                     [NEW]
  AnyPermissionAuthorizationHandler.cs                  [NEW]
  PermissionPolicyProvider.cs                           [NEW]
  HasPermissionAttribute.cs                             [NEW]
  HasAnyPermissionAttribute.cs                          [NEW]

src/MPS.API/
  Program.cs                                            [MODIFY: AddMemoryCache + register handlers]
  Controllers/UserController.cs                         [MODIFY: +attributes]
  Controllers/EmployeeInformationController.cs          [MODIFY]
  Controllers/PositionController.cs                     [MODIFY]
  Controllers/PerformanceMetricController.cs            [MODIFY]
  Controllers/TypePerformanceMetricController.cs        [MODIFY]

src/MPS.Application/Features/User/Queries/UserGetByIdQueryHandler.cs        [MODIFY: scope check]
src/MPS.Application/Features/User/Queries/UserGetAllQueryHandler.cs         [MODIFY: scope]
src/MPS.Application/Features/User/Commands/UserUpdateCommandHandler.cs      [MODIFY: scope]
src/MPS.Application/Features/EmployeeInformation/Queries/*.cs               [MODIFY: scope]
src/MPS.Application/Features/EmployeeInformation/Commands/*UpdateHandler.cs [MODIFY: scope]
src/MPS.Application/Features/PerformanceMetric/Queries/*.cs                 [MODIFY: scope]
```

### Phase 5

```
src/MPS.Application/Features/Role/
  Queries/GetAllRolesQuery.cs                           [NEW]
  Queries/GetAllRolesQueryHandler.cs                    [NEW]
  Queries/GetRoleByIdQuery.cs                           [NEW]
  Queries/GetRoleByIdQueryHandler.cs                    [NEW]
  Queries/GetRolePermissionsQuery.cs                    [NEW]
  Queries/GetRolePermissionsQueryHandler.cs             [NEW]
  Commands/CreateRoleCommand.cs                         [NEW]
  Commands/CreateRoleCommandHandler.cs                  [NEW]
  Commands/UpdateRoleCommand.cs                         [NEW]
  Commands/UpdateRoleCommandHandler.cs                  [NEW: chặn IsSystem]
  Commands/DeleteRoleCommand.cs                         [NEW]
  Commands/DeleteRoleCommandHandler.cs                  [NEW: chặn IsSystem + còn user]
  Commands/UpdateRolePermissionsCommand.cs              [NEW]
  Commands/UpdateRolePermissionsCommandHandler.cs       [NEW: cho phép sửa system + invalidate cache]
  Validators/CreateRoleCommandValidator.cs              [NEW]
  Validators/UpdateRoleCommandValidator.cs              [NEW]
  Validators/DeleteRoleCommandValidator.cs              [NEW]
  Validators/UpdateRolePermissionsCommandValidator.cs   [NEW]
  DTOs/RoleDto.cs                                       [NEW]
  DTOs/RoleDetailDto.cs                                 [NEW: +permissions]

src/MPS.Application/Features/Permission/
  Queries/GetAllPermissionsQuery.cs                     [NEW]
  Queries/GetAllPermissionsQueryHandler.cs              [NEW]
  Queries/GetUserEffectivePermissionsQuery.cs           [NEW]
  Queries/GetUserEffectivePermissionsQueryHandler.cs    [NEW]
  DTOs/PermissionDto.cs                                 [NEW]
  DTOs/EffectivePermissionsDto.cs                       [NEW]

src/MPS.Application/Features/RefreshToken/
  Commands/RevokeAllUserTokensCommand.cs                [NEW]
  Commands/RevokeAllUserTokensCommandHandler.cs         [NEW]
  Validators/RevokeAllUserTokensCommandValidator.cs     [NEW]

src/MPS.API/Controllers/
  RoleController.cs                                     [NEW]
  PermissionController.cs                               [NEW]
  SecurityAdminController.cs                            [NEW]

src/MPS.API/Requests/Role/
  CreateRoleRequest.cs                                  [NEW]
  UpdateRoleRequest.cs                                  [NEW]
  UpdateRolePermissionsRequest.cs                       [NEW]
```

### Phase 6

```
tests/
  MPS.Application.Tests/MPS.Application.Tests.csproj            [NEW]
  MPS.Application.Tests/Features/Auth/*Tests.cs                 [NEW]
  MPS.Application.Tests/Features/Role/*Tests.cs                 [NEW]
  MPS.Application.Tests/Features/Permission/*Tests.cs           [NEW]
  MPS.Infrastructure.Tests/MPS.Infrastructure.Tests.csproj      [NEW]
  MPS.Infrastructure.Tests/Security/BCryptPasswordHasherTests.cs [NEW]
  MPS.Infrastructure.Tests/Security/JwtTokenServiceTests.cs     [NEW]
  MPS.Infrastructure.Tests/Security/RefreshTokenServiceTests.cs [NEW]
  MPS.Infrastructure.Tests/Security/PermissionResolverTests.cs  [NEW]
  MPS.Infrastructure.Tests/Security/ScopeCheckerTests.cs        [NEW]
  MPS.API.IntegrationTests/MPS.API.IntegrationTests.csproj      [NEW]
  MPS.API.IntegrationTests/Auth/LoginEndpointTests.cs           [NEW]
  MPS.API.IntegrationTests/Auth/RefreshFlowTests.cs             [NEW]
  MPS.API.IntegrationTests/Authorization/PermissionEnforcementTests.cs [NEW]
  MPS.API.IntegrationTests/Authorization/ScopeBehaviorTests.cs  [NEW]
  MPS.API.IntegrationTests/Role/RoleCrudTests.cs                [NEW]
  MPS.API.IntegrationTests/Role/SystemRoleProtectionTests.cs    [NEW]
  MPS.API.IntegrationTests/Fixtures/TestWebApplicationFactory.cs [NEW]
  MPS.API.IntegrationTests/Fixtures/TestDataSeeder.cs           [NEW]

MPS.sln                                                          [MODIFY: +3 test projects]
```

---

## 9. Success criteria

- [ ] DB tạo mới với schema: `User` int PK, `Role` có `IsSystem`, `Permissions` seed master, `RolePermissions` seed matrix, `RefreshTokens`.
- [ ] CRUD `User` & `EmployeeInformation` hoạt động với `int Id`.
- [ ] `POST /api/auth/login` trả `accessToken + refreshToken`.
- [ ] `POST /api/auth/refresh` rotate token; reuse old token → revoke toàn chain.
- [ ] `POST /api/auth/logout` revoke refresh token.
- [ ] Endpoint protected: 401 thiếu token, 403 thiếu permission.
- [ ] Employee chỉ thấy/sửa data của chính mình; gọi `GET /api/users` (list) → **403**; gọi data người khác → 403.
- [ ] SysAdmin tạo custom role + gán user → user có đúng quyền sau invalidate.
- [ ] SysAdmin sửa permission set của role hiện có → toàn bộ user mang role nhận thay đổi.
- [ ] System role: rename/xóa → 403/409; sửa permissions → 200.
- [ ] Custom role chỉ xóa được khi không có user nào dùng (chặn 409).
- [ ] SysAdmin force logout user qua revoke all refresh tokens.
- [ ] Password BCrypt hash, không còn plain text.
- [ ] Không có secret hardcoded; `JwtSettings:SecretKey` qua user-secrets/env var.
- [ ] Swagger UI có "Authorize" Bearer.
- [ ] `dotnet build` pass, `dotnet test` ≥ 80% coverage cho code mới.

---

## 10. Rủi ro & lưu ý

- **Rename column `Password` → `PasswordHash`** + đổi PK: theo migration Option 1 đã chốt, drop DB local → không lo về data migration cũ.
- **`IMemoryCache`** chỉ hoạt động trong 1 instance. Nếu scale-out → cần đổi sang `IDistributedCache` (Redis). Ngoài scope hiện tại.
- **BCrypt work factor 12** ~200-300ms/hash trên máy dev → login async, không block thread pool. Test response time login phải < 500ms.
- **Migration rollback**: Option 1 không có path rollback ngoài việc backup migration cũ trước khi xóa. Lưu file `InitialCreate.cs` cũ vào branch riêng phòng trường hợp cần tham khảo.
- **System role permission update có thể vô tình mất quyền SysAdmin**. Cần validation: không cho remove `Role.*` permissions khỏi role `SystemAdmin` (hardcoded safety check trong `UpdateRolePermissionsCommandHandler`).
