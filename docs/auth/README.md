# Authentication & Authorization

Mingo Payroll API uses JWT access tokens (HS256, 60-minute TTL) with opaque refresh tokens (rotated on each use, with reuse detection).

## Configuration

### JWT secret (required)

The JWT signing secret must be at least 32 bytes (256 bits). It is **never** committed to source control.

**Local development** — use .NET User Secrets:

```bash
dotnet user-secrets set "Jwt:SecretKey" "your-local-secret-at-least-32-bytes-long" \
  --project src/MPS.API/MPS.API.csproj
```

**Production** — use environment variables:

```bash
export Jwt__SecretKey="$(openssl rand -base64 48)"
export Jwt__Issuer="MPS.Api"
export Jwt__Audience="MPS.Client"
```

The `__` (double underscore) is the .NET convention for nested config keys on platforms that don't allow `:` in env var names.

Startup fails fast if `Jwt:SecretKey` is missing or shorter than 32 bytes.

### Default admin

On first startup, `DatabaseSeeder` creates a system administrator from the `DefaultAdmin` config section if no admin exists. Override the password before deploying.

```json
"DefaultAdmin": {
  "Username": "admin",
  "Email": "admin@mingo.local",
  "Password": "ChangeMe!2026"
}
```

### Rate limiting

`/api/auth/login` and `/api/auth/forgot-password` and `/api/auth/reset-password` — 5 req/min per IP.
`/api/auth/refresh` — 20 req/min per IP.

Returns `429 Too Many Requests` on excess.

## Auth flow

```
Client                              API                                DB
  │                                  │                                  │
  │ POST /api/auth/login             │                                  │
  │ { username, password }           │                                  │
  ├─────────────────────────────────►│                                  │
  │                                  │ Verify BCrypt hash               │
  │                                  ├─────────────────────────────────►│
  │                                  │ Generate JWT (sub, role, perms)  │
  │                                  │ Issue refresh token (SHA-256)    │
  │                                  ├─────────────────────────────────►│
  │ { accessToken, refreshToken,     │                                  │
  │   accessTokenExpiresAt,          │                                  │
  │   refreshTokenExpiresAt }        │                                  │
  │◄─────────────────────────────────┤                                  │
  │                                  │                                  │
  │ GET /api/whatever                │                                  │
  │ Authorization: Bearer <jwt>      │                                  │
  ├─────────────────────────────────►│ Validate JWT, check perms claim  │
  │                                  │ Run handler                      │
  │◄─────────────────────────────────┤                                  │
  │                                  │                                  │
  │ POST /api/auth/refresh           │                                  │
  │ { refreshToken }                 │                                  │
  ├─────────────────────────────────►│ Hash + lookup; if revoked,       │
  │                                  │ revoke entire chain (reuse!)     │
  │                                  │ Otherwise rotate + reissue       │
  │ { new tokens }                   │                                  │
  │◄─────────────────────────────────┤                                  │
```

## Endpoints

### Auth endpoints

| Method | Route | Auth | Rate-limited | Purpose |
|--------|-------|------|--------------|---------|
| POST | `/api/auth/login` | Anonymous | `auth-login` | Username + password → tokens |
| POST | `/api/auth/refresh` | Anonymous | `auth-refresh` | Refresh → rotated tokens |
| POST | `/api/auth/logout` | Bearer | – | Revoke specific refresh token |
| POST | `/api/auth/change-password` | Bearer | – | Verify old, hash new, revoke all sessions |
| POST | `/api/auth/forgot-password` | Anonymous | `auth-login` | Email reset link (always 202) |
| POST | `/api/auth/reset-password` | Anonymous | `auth-login` | Token + new password |
| GET | `/api/auth/me` | Bearer | – | Current user profile + permissions |

### Resource endpoints — required permissions

| Resource | Endpoint | Permission |
|----------|----------|------------|
| User | `GET /api/user` | `User.Read.All` |
| | `GET /api/user/{id}` | `User.Read.All` OR `User.Read.Self` (id must equal current user) |
| | `POST /api/user` | `User.Create.All` |
| | `PUT /api/user/{id}` | `User.Update.All` OR `User.Update.Self` (cannot change own role/status) |
| | `DELETE /api/user/{id}` | `User.Delete.All` |
| EmployeeInformation | `GET /api/employeeinformation` | `EmployeeInformation.Read.All` OR `.Self` (returns only own record) |
| | `GET /api/employeeinformation/{id}` | `EmployeeInformation.Read.All` OR `.Self` (owner check) |
| | `POST /api/employeeinformation` | `EmployeeInformation.Create.All` |
| | `PUT /api/employeeinformation/{id}` | `EmployeeInformation.Update.All` OR `.Self` (owner check) |
| | `DELETE /api/employeeinformation/{id}` | `EmployeeInformation.Delete.All` |
| Position | `GET /POST /PUT /DELETE` | `Position.{Read,Create,Update,Delete}.All` |
| PerformanceMetric | `GET ` | `PerformanceMetric.Read.All` OR `.Self` |
| | `POST /PUT /DELETE` | corresponding `.All` permission |
| TypePerformanceMetric | `GET /POST /PUT /DELETE` | `TypePerformanceMetric.{Read,Create,Update,Delete}.All` |
| Role | `GET /api/role` | `Role.Read.All` |
| | `POST /api/role` | `Role.Create.All` |
| | `PUT /api/role/{id}` | `Role.Update.All` (blocked on system roles) |
| | `DELETE /api/role/{id}` | `Role.Delete.All` (blocked on system roles or roles with users) |
| | `PUT /api/role/{id}/permissions` | `Role.ManagePermissions.All` |
| Permission | `GET /api/permission` | `Role.Read.All` OR `Role.ManagePermissions.All` |

## Permission matrix

The 7 system roles map to permission sets at seed time. **System admins can edit these mappings via `PUT /api/role/{id}/permissions`.** Changes take effect on next user login/refresh (current JWTs keep old perms until they expire — 60 min max).

### System roles

| Id | Name | IsSystem | Default |
|----|------|----------|---------|
| 1 | SystemAdmin | ✓ | All 32 permissions |
| 2 | HR | ✓ | User + EmployeeInformation full; Position read; Role read |
| 3 | Accountant | ✓ | EmployeeInformation read; PerformanceMetric read |
| 4 | Director | ✓ | All read perms + PerformanceMetric approve |
| 5 | DepartmentManager | ✓ | EmployeeInformation/PerformanceMetric read; Self perms |
| 6 | ProjectManager | ✓ | PerformanceMetric read/update; Self perms |
| 7 | Employee | ✓ | All `.Self` perms only |

For exact mappings, see `RolePermissionEntityConfiguration.HasData(...)`.

### Permission naming

```
Resource.Action.Scope
```

- **Resource**: `User`, `EmployeeInformation`, `Position`, `PerformanceMetric`, `TypePerformanceMetric`, `Role`, `RefreshToken`
- **Action**: `Read`, `Create`, `Update`, `Delete`, `Approve`, `ManagePermissions`, `Revoke`
- **Scope**: `All` (operates on any record) or `Self` (operates only on records owned by current user)

Constants are defined in `MPS.Domain.Constants.Permissions`.

### Self-scope semantics

Handlers enforce `.Self` at the application layer:

- **GetById**: if user only has `.Self`, throw 401 unless `resource.OwnerUserId == currentUserId`
- **GetAll**: if user only has `.Self`, filter results to records owned by current user
- **Update**: ownership check + block reassignment of ownership
- **Self update of User**: cannot change own `RoleId` or `Status` (anti-privilege-escalation)

## Securing controllers

```csharp
using MPS.API.Authorization;
using MPS.Domain.Constants;

[HasPermission(Permissions.UserReadAll)]
public IActionResult GetAll() { ... }

[HasAnyPermission(Permissions.UserReadAll, Permissions.UserReadSelf)]
public IActionResult GetById(int id) { ... }

[HasAllPermissions(Permissions.RoleManagePermissionsAll, Permissions.RoleUpdateAll)]
public IActionResult SomeOp() { ... }
```

Behind the scenes, `PermissionPolicyProvider` dynamically materializes policies named `Permission:Any:p1,p2` or `Permission:All:p1,p2`, and `PermissionAuthorizationHandler` checks the JWT's `perms` claim.

## Session lifecycle

| Event | Effect |
|-------|--------|
| Login | New refresh token issued; `LastLoginAt` updated |
| Refresh | Old token revoked + replaced (`ReplacedByTokenHash` tracked) |
| Refresh-token reuse | Entire token chain for that user revoked |
| Logout | One refresh token revoked |
| Change password | All refresh tokens for user revoked |
| Reset password (forgot flow) | All refresh tokens for user revoked |
| Admin disables user (Status 1→0) | All refresh tokens for user revoked |
| Admin changes user's role | All refresh tokens for user revoked |
| Background job (every 6h) | Delete refresh tokens with `ExpiresAt < UtcNow - 30 days` |

## Forgot-password flow

```
1. POST /api/auth/forgot-password { email }
   → 202 Accepted (always — does not leak whether email exists)
   → If email matches an active user: generate 48-byte token, store SHA-256 hash,
     1-hour expiry; invalidate prior tokens; "send" reset email
2. POST /api/auth/reset-password { token, newPassword }
   → Verify token hash, check expiry + not-used, hash new password,
     mark token used, revoke all refresh tokens
   → 204 No Content on success; 401 on invalid/expired/used token
```

**Dev mode**: `LoggingEmailSender` logs the reset token to the console instead of sending email. Wire `SmtpEmailSender` or `SendGridEmailSender` (implementing `IEmailSender`) in production.

## Refresh token internals

- 64 random bytes, base64url-encoded
- Stored as SHA-256 hex hash (never the raw token)
- Rotated on every refresh: the presented token is revoked, a new one issued, the `ReplacedByTokenHash` links the chain
- **Reuse detection**: if a revoked token is presented, the entire user's token chain is revoked (signals compromised token)
- Audit columns: `CreatedAt`, `CreatedByIp`, `RevokedAt`, `RevokedReason`, `RevokedByIp`
- Background cleanup: `RefreshTokenCleanupService` deletes tokens 30+ days past expiry every 6 hours

## Testing

Test users for manual API testing:

```bash
# Login as admin
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"ChangeMe!2026"}'

# Use access token
curl http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer <accessToken>"

# Refresh
curl -X POST http://localhost:5000/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refreshToken>"}'
```
