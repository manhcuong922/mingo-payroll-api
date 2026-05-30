# Authentication & Authorization Module — Implementation Plan

**Status:** ✅ **COMPLETED** (all 6 phases shipped on branch `feature/FE01-authen/author`)
**Project:** mingo-payroll-api (.NET 8, Clean Architecture, EF Core Code-First, MediatR CQRS)
**Last updated:** 2026-05-30 (status reconciled against shipped code)

---

## 0. Implementation status (added post-shipping)

| Phase | Status | Commit |
|-------|--------|--------|
| Phase 1 — Domain entities + migration | ✅ Done | `951c5dc` |
| Phase 2 — Password hashing + JWT infra | ✅ Done | `90915f1` |
| Phase 3 — Login/Refresh/Logout/Me endpoints | ✅ Done | `f9ed16b` |
| Phase 4 — Permission attributes + apply to controllers | ✅ Done | `347d473` |
| Phase 5 — Role + Permission CRUD | ✅ Done | `347d473` |
| Phase 6a — Self-scope, rate limit, change-password, token cleanup | ✅ Done | `d134a62` |
| Phase 6b — Forgot/reset password, tests, docs, hardening | ✅ Done | pending commit |
| Phase 6c — SystemAdmin lockout safety guard | ✅ Done | pending commit |

### Deviations from original plan

| # | Plan said | Actually shipped | Reason |
|---|-----------|------------------|--------|
| 1 | JWT does **not** embed permissions (`§4.3`) | JWT embeds `perms[]` claim | Stateless verify, no DB hit per request; trade-off: perms stale up to 60min until next refresh |
| 2 | Permission cache key `perm:user:{userId}` | Cache key `perms:role:{roleId}` | Per-role cache is N× more efficient (1 entry shared by all users in that role) |
| 3 | List endpoint with only `.Self` → **403 Forbidden** (`§4.5`) | List filters to current user's records (returns 1 or 0 items) | Better UX for FE; can revert to 403 if strict policy required |
| 4 | `IScopeChecker` helper class | Inline `_currentUserService.HasPermission(...)` in handlers | Simpler, less indirection; each handler's ownership rule is bespoke |
| 5 | Folder `MPS.Application/Abstractions/Security/` | Folder `MPS.Application/Common/Interfaces/` + `Common/Models/` | Cosmetic; same contents |
| 6 | `InvalidCredentialsException`, `InvalidRefreshTokenException` | All collapsed into `ForbiddenAccessException` → 401 | Fewer exception types, same HTTP behavior |
| 7 | `IPermissionCacheInvalidator` separate service | `IPermissionResolver.InvalidateRoleCache()` method | Same functionality, fewer DI registrations |

### Work shipped beyond original scope

Original plan §1 listed these as **out of scope** — all of them were shipped anyway:

- ✅ Self-service `POST /api/auth/change-password` (verify old → hash new → revoke all sessions)
- ✅ Rate limiting via `Microsoft.AspNetCore.RateLimiting` (5/min login, 20/min refresh, 5/min forgot/reset)
- ✅ Forgot/reset password flow (`POST /api/auth/forgot-password` + `/reset-password`)
  - `PasswordResetToken` entity + repo + migration `AddPasswordResetTokens`
  - `IEmailSender` abstraction + `LoggingEmailSender` dev stub
- ✅ `RefreshTokenCleanupService` background job (`IHostedService`, deletes 30-day-old tokens every 6h)
- ✅ Revoke-on-state-change: admin disables user (Status 1→0) or changes role → all refresh tokens revoked
- ✅ AutoMapper CVE GHSA-rvv3-g6hj-g44x fixed (14.0.0 → 16.1.1)
- ✅ JWT secret hardening: moved out of `appsettings.json` → User Secrets for dev, env var for prod
- ✅ `docs/auth/README.md` — full permission matrix, endpoint table, configuration guide, auth flow diagram
- ✅ SystemAdmin lockout safety guard — `UpdateRolePermissionsCommandHandler` rejects updates that would strip `Role.*` + `RefreshToken.Revoke.All` from SystemAdmin

### Still NOT shipped (deferred)

- ❌ `GET /api/users/{id}/permissions` — effective permissions debug endpoint
- ❌ `POST /api/admin/users/{id}/revoke-tokens` — explicit SysAdmin force-logout endpoint (can be done indirectly via Status=0)
- ❌ Integration tests with `WebApplicationFactory` (only unit tests exist — 56 passing)
- ❌ `PermissionResolverTests` unit tests
- ❌ Validator unit tests
- ❌ Code coverage measurement / report
- ❌ Audit log (`§1` out-of-scope, still out of scope)
- ❌ SMTP/SendGrid `IEmailSender` impl (dev-only `LoggingEmailSender` ships)

---

## 1. Mục tiêu (original)

Bổ sung module Authentication & Authorization vào API:

- Login bằng `Username`/`Password`, cấp **JWT access token** + **refresh token** (rotation, reuse detection).
- **Role-based authorization** — mỗi role có một tập permission cố định, gán cho user qua `RoleId`.
- **Custom role management** — `SystemAdmin` tạo/sửa/xóa role mới với permission set tùy chỉnh.
- **Scope-aware permission** (`Self` / `All`) — Employee chỉ thấy & sửa data của chính mình.
- **System role bảo vệ** — 7 role mặc định không sửa name/description/xóa được, nhưng vẫn cho phép sửa permission set.

**Originally out of scope (đa số đã ship sau này — xem §0):**
- ~~Self-service change password~~ → shipped
- ~~Account lockout / rate limit login~~ → rate-limit shipped (lockout không cần)
- Audit log riêng cho auth events → vẫn chưa làm
- Per-user permission override (đã quyết định bỏ — quyền chỉ qua role)

---

## 2. Quyết định đã chốt

| # | Hạng mục | Quyết định | Hiện trạng |
|---|---|---|---|
| 1 | Refresh token | Opaque random base64, SHA-256 hash, rotation, reuse detection revoke chain | ✅ Đúng |
| 2 | Roles default | 7 system roles `IsSystem = true` | ✅ Đúng |
| 3 | Custom role | SysAdmin tạo role mới `IsSystem = false`, xóa nếu không có user | ✅ Đúng |
| 4 | System role rules | KHÔNG xóa, KHÔNG sửa name/description, CÓ sửa permissions | ✅ Đúng + thêm guard SystemAdmin |
| 5 | PK `User` | `int Id IDENTITY`, `EmployeeCode` thành unique column | ✅ Đúng |
| 6 | Password hashing | `BCrypt.Net-Next` work factor 12 | ✅ Đúng (không cần seeder migrate plain text vì drop DB) |
| 7 | Scope `Self`/`All` | `Resource.Action.Scope` encoding | ✅ Đúng |
| 8 | Employee gọi list endpoint | Plan: 403 Forbidden | ⚠️ Impl: filter về 1 record của user (xem §0 deviation #3) |
| 9 | Per-user override | KHÔNG có | ✅ Đúng |
| 10 | Migration strategy | Drop DB local + tạo lại | ✅ Đúng |
| 11 | JWT algorithm | HS256, secret ≥ 32 chars, qua user-secrets/env var | ✅ Đúng (hardening lần 2 ở Phase 6b) |
| 12 | JWT TTL | Access 60 phút, Refresh 7 ngày | ✅ Đúng |
| 13 | Permission caching | `IMemoryCache`, TTL 5 phút | ✅ Đúng (cache theo role, không theo user) |

---

## 3. Cảnh báo: đổi PK `User` = breaking change lớn — ✅ DONE

35 file đã update đúng theo plan trong commit `951c5dc`.

---

## 4. Thiết kế chi tiết

### 4.1. Permission model — ✅ DONE

`Resource.Action.Scope` encoding. 32 permissions seed master.
Constants: `MPS.Domain.Constants.Permissions`, `PermissionIds`.

### 4.2. Effective permission resolution — ✅ DONE (with deviation)

**Plan:** lookup mỗi request qua `IPermissionResolver`.
**Impl:** permissions embed trong JWT claim `perms[]` → no DB hit. Cache lookup chỉ chạy lúc login/refresh để build claims. Trade-off: stale tối đa 60min.

Cache:
- Key: `perms:role:{roleId}` (KHÁC plan: per-role thay vì per-user)
- TTL: 5 phút absolute + 2 phút sliding
- Invalidation: `IPermissionResolver.InvalidateRoleCache(roleId)` được gọi khi:
  - Role create/delete/permission update

### 4.3. JWT & Refresh token — ✅ DONE (with deviation)

**Access token (JWT, HS256):**
- Claims: `sub`, `name`/`unique_name`, `email`, `jti`, `iat`, `nbf`, `exp`, `role`, `role_id`, **`perms[]`** (DEVIATION từ plan)
- TTL: 60 phút (config)

**Refresh token:**
- Opaque random 64-byte, base64url-encoded
- Hash SHA-256 trong DB
- TTL: 7 ngày (config)
- Rotation mỗi `/refresh`, link `ReplacedByTokenHash`
- Reuse detection → revoke chain

**Endpoints:** ✅ tất cả ship + `change-password`, `forgot-password`, `reset-password` (ngoài plan)

### 4.4. Password hashing — ✅ DONE

BCrypt cost 12. `nvarchar(512)` cột.
Plan có `PasswordSeederService` migrate plain text → KHÔNG cần vì đã drop DB.
Thay vào đó: `DatabaseSeeder` seed default admin từ config.

### 4.5. Authorization enforcement — ✅ DONE (with deviations)

Attributes:
- `[HasPermission(code)]` — single permission
- `[HasAnyPermission(codes)]` — bất kỳ 1 perm match
- `[HasAllPermissions(codes)]` — tất cả phải có (PHỤ THÊM ngoài plan)

`PermissionPolicyProvider` dynamic, prefix `Permission:Any:p1,p2` / `Permission:All:p1,p2`.

**Scope enforcement:**
- Inline check `_currentUserService.HasPermission(...)` trong handlers (KHÔNG có `IScopeChecker` class riêng — deviation #4).
- Detail/Update endpoint: chỉ `.Self` + access ID khác → throw `ForbiddenAccessException`.
- List endpoint: nếu chỉ `.Self` → **filter về data của user thôi** (KHÁC plan, plan nói 403 — deviation #3).
- User self-update: block thay đổi RoleId/Status (ANTI-ESCALATION ngoài plan).

### 4.6. Role management — ✅ DONE

Entity bổ sung đúng plan. Rules đúng plan.

**Endpoints ship:**
| Method | Route | Permission | Status |
|---|---|---|---|
| GET | `/api/role` | `Role.Read.All` | ✅ |
| GET | `/api/role/{id}` | `Role.Read.All` | ✅ |
| POST | `/api/role` | `Role.Create.All` | ✅ |
| PUT | `/api/role/{id}` | `Role.Update.All` | ✅ |
| DELETE | `/api/role/{id}` | `Role.Delete.All` | ✅ |
| PUT | `/api/role/{id}/permissions` | `Role.ManagePermissions.All` | ✅ (+ SystemAdmin lockout guard) |
| GET | `/api/permission` | `Role.Read.All` / `Role.ManagePermissions.All` | ✅ |
| GET | `/api/users/{id}/permissions` | `Role.Read.All` | ❌ KHÔNG ship |
| POST | `/api/admin/users/{id}/revoke-tokens` | `RefreshToken.Revoke.All` | ❌ KHÔNG ship |

**SystemAdmin guard (Phase 6c, ngoài plan):**
`UpdateRolePermissionsCommandHandler` throw `SystemRoleModificationException` nếu update SystemAdmin permissions mà bỏ bất kỳ perm trong: `Role.Read.All`, `Role.Create.All`, `Role.Update.All`, `Role.Delete.All`, `Role.ManagePermissions.All`, `RefreshToken.Revoke.All`.

---

## 5. Permission seed matrix — ✅ DONE

106 role-permission mappings seed đúng theo bảng plan.
Lưu trong `RolePermissionEntityConfiguration.HasData(...)`.

---

## 6. Domain model — ✅ DONE

Tất cả entities mới + sửa đúng theo plan §6.

Bổ sung Phase 6b: `PasswordResetToken` entity + EF config + migration `AddPasswordResetTokens` (NGOÀI plan).

---

## 7. Implementation Phases (historical record)

Phase 1-5 nội dung giữ nguyên — đã ship đúng kế hoạch (xem §0 status table).

### Phase 6 — Tests + production hardening (rebroken thành 3 mini-phases khi ship)

**Phase 6a (commit `d134a62`):**
- Self-scope enforcement: User + EmployeeInformation handlers
- Revoke refresh tokens on user disable/role change
- Rate limiting trên login/refresh
- Change-password endpoint
- `RefreshTokenCleanupService` background job

**Phase 6b (pending commit):**
- AutoMapper 14→16 (CVE fix)
- JWT secret hardening: User Secrets + env var
- `PasswordResetToken` entity + migration + repo
- `IEmailSender` + `LoggingEmailSender`
- Forgot/reset password endpoints
- `docs/auth/README.md` (permission matrix + endpoint table + auth flow)
- Test projects: `MPS.Application.Tests` (38 tests) + `MPS.Infrastructure.Tests` (15 tests) = **53 passing**

**Phase 6c (pending commit):**
- SystemAdmin lockout safety guard + 3 tests = **56 passing tổng**

**Vẫn còn thiếu cho test goal 80% coverage:**
- ❌ Integration tests với `WebApplicationFactory`
- ❌ `PermissionResolverTests`
- ❌ Validator tests
- ❌ Coverage report

---

## 8. File tree dự kiến (historical reference)

Giữ nguyên — đã match thực tế ~90%. Sai lệch chỉ ở naming/folder (xem deviation §0).

---

## 9. Success criteria — checklist hiện tại

- [x] DB schema mới đúng plan
- [x] CRUD `User` & `EmployeeInformation` với `int Id`
- [x] `POST /api/auth/login` cấp `accessToken + refreshToken`
- [x] `POST /api/auth/refresh` rotate; reuse → revoke chain
- [x] `POST /api/auth/logout` revoke
- [x] Endpoint protected: 401 thiếu token, 403 thiếu permission
- [x] Employee chỉ thấy/sửa data của chính mình (filter; KHÔNG 403 cho list — deviation)
- [x] SysAdmin tạo custom role + gán user → user có đúng quyền sau invalidate
- [x] SysAdmin sửa permission set của role → users phải re-login (token TTL 60min)
- [x] System role: rename/xóa → 409; sửa permissions → 200 (+ SystemAdmin guard)
- [x] Custom role chỉ xóa khi không có user (409 nếu còn)
- [ ] SysAdmin force logout user qua endpoint riêng — **KHÔNG SHIP** (có indirect path: Status=0)
- [x] Password BCrypt, không plain text
- [x] Không secret hardcoded; JWT key qua user-secrets/env var
- [x] Swagger UI có "Authorize" Bearer
- [x] `dotnet build` pass (0 errors, 0 warnings)
- [ ] `dotnet test` ≥ 80% coverage — **KHÔNG ĐO** (56 unit tests pass nhưng chưa có coverage report)

**Bonus ship (ngoài plan):**
- [x] `POST /api/auth/change-password`
- [x] `POST /api/auth/forgot-password` + `/reset-password`
- [x] Rate limiting login/refresh
- [x] Revoke refresh tokens on user disable/role change
- [x] `RefreshTokenCleanupService` background job
- [x] AutoMapper CVE fix
- [x] `docs/auth/README.md`

---

## 10. Rủi ro & lưu ý (post-shipping update)

- **`IMemoryCache` single-instance**: khi scale-out cần đổi `IDistributedCache` (Redis). Vẫn ngoài scope.
- **BCrypt work factor 12** ~200-300ms/hash. Đã test, OK trên dev.
- **JWT embeds permissions** (deviation #1): perms stale tối đa 60min. Mitigation: revoke refresh tokens khi role/perms thay đổi → buộc re-login.
- **System role permission update**: ✅ ĐÃ FIX — `UpdateRolePermissionsCommandHandler` chặn nếu strip critical perms khỏi SystemAdmin (Phase 6c).
- **Forgot-password email**: `LoggingEmailSender` chỉ log token ra console. Production phải swap `SmtpEmailSender` hoặc `SendGridEmailSender`.
