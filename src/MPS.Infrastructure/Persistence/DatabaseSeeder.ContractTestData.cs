using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Employee;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Test seed data cho module Contract — chuyển từ docs/Contract.sql sang C# để chạy
/// tự động cùng DatabaseSeeder (không cần chạy tay file SQL qua SSMS).
///
/// Cover các case: CRUD HĐ ở mọi trạng thái (Draft/Active/Cancelled/Expired/Terminated/Archived),
/// self-scope filter, Điều 20 k.2c (chặn HĐ Định hạn thứ 3), phụ lục Điều 22+33,
/// termination Điều 34/45, background jobs (auto-convert/expire/archive),
/// archive guard 409, multi-version file, soft-delete catalog, Guardian optional path.
///
/// Khác biệt so với file SQL (tránh đụng unique index với users do DatabaseSeeder tạo):
///   • EmployeeCode: HR001/EMP001..EMP003 đã bị chiếm bởi hr/employee/worker2/worker3
///     → dùng HRT001 + EMPT001..EMPT006.
///   • Email hr@mingo.local đã thuộc user 'hr' → hr.user dùng hr.user@mingo.local.
///   • Mật khẩu Test@1234 hash runtime qua IPasswordHasher (không hardcode BCrypt hash).
///
/// Idempotent: bỏ qua toàn bộ nếu bất kỳ ContractNo test nào đã tồn tại; các phase
/// catalog (Positions/Users/Grades/AllowanceTypes/...) chèn-nếu-thiếu.
///
/// Tài khoản test (mật khẩu Test@1234):
///   hr.user   HR — tạo HĐ, ký phụ lục, chấm dứt
///   employee1 Nam   — Draft + Active IND + 2 historical Definite  [có guardian]
///   employee2 Hoa   — Active Tổ trưởng + 1 phụ lục Draft          [KHÔNG guardian]
///   employee3 Cường — Cancelled + Terminated + Archived           [có guardian]
///   employee4 Linh  — Draft Probation + Active 24m                [có guardian]
///   employee5 Tuấn  — AutoConvert + Expire candidates             [KHÔNG guardian]
///   employee6 Mai   — Active 6m + Expired 10+ năm                 [guardian thiếu Address]
/// </summary>
public sealed partial class DatabaseSeeder
{
    private const string ContractTestPassword = "Test@1234";
    private const string ContractTestMinioBucket = "mps-storage-dev";

    private static readonly string[] ContractTestContractNos =
    {
        "HDLD-DRAFT-NAM-001", "HDLD-ACTIVE-NAM-IND", "HDLD-NAM-HIST-DEF-1", "HDLD-NAM-HIST-DEF-2",
        "HDLD-ACTIVE-HOA-LEAD", "HDLD-DRAFT-LINH-PROB", "HDLD-ACTIVE-LINH-24M", "HDLD-ACTIVE-MAI-6M",
        "HDLD-CANCEL-CUONG", "HDLD-TERM-CUONG", "HDLD-ARCH-CUONG-2014", "HDLD-EXP-MAI-OLD-2014",
        "HDLD-AUTOCVT-TUAN", "HDLD-EXPCAND-PARENT", "HDLD-EXPCAND-CHILD",
    };

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private async Task SeedContractTestDataAsync(CancellationToken ct)
    {
        // ⚠️ KHÔNG gate toàn bộ bằng contractsExist — các phase upstream (Users, EmployeeInformations)
        // phải luôn chạy self-idempotent để recover khi user manually xóa EmployeeInformation
        // nhưng contracts còn tồn tại (data drift).
        var positionIds = await EnsureContractTestPositionsAsync(ct);
        var userIds = await EnsureContractTestUsersAsync(ct);
        var employeeIds = await EnsureContractTestEmployeeInformationsAsync(userIds, positionIds, ct);
        // Bậc lương: dùng bảng bậc lương DÙNG CHUNG (đủ thang bậc cho mọi chức danh) —
        // xem DatabaseSeeder.SalaryGradesData.cs. Gọi ở đây vì cần chức danh contract-test đã có.
        var gradeIds = await EnsureSalaryGradeCatalogAsync(ct);
        var allowanceTypeIds = await EnsureContractTestAllowanceTypesAsync(ct);
        await EnsureContractTestTemplatesAsync(ct);

        // Gate riêng cho phase Contracts + downstream — chỉ chạy 1 lần / batch. Nếu bất kỳ
        // contract test nào đã tồn tại → coi như phase này đã hoàn tất, skip.
        var contractsExist = await _context.Contracts
            .AsNoTracking()
            .AnyAsync(c => ContractTestContractNos.Contains(c.ContractNo), ct);
        if (contractsExist)
        {
            _logger.LogDebug("Contract test contracts already seeded; only ensured upstream data.");
            return;
        }

        // Công ty chỉ có DUY NHẤT 1 pháp nhân đại diện (seed ở SeedDefaultContractPartyAsync) —
        // toàn bộ HĐ test đều ký dưới pháp nhân này.
        var party = await GetCompanyContractPartyAsync(ct);
        if (party is null)
        {
            _logger.LogWarning("SeedContractTestData: chưa có pháp nhân công ty; bỏ qua phần HĐ test.");
            return;
        }

        var hrUserId = userIds["hr.user"];
        var contracts = await SeedContractTestContractsAsync(
            hrUserId, employeeIds, party.Id, positionIds, gradeIds, ct);
        await SeedContractTestAllowancesAsync(contracts, allowanceTypeIds, ct);
        var terminationNoticeFile = await SeedContractTestFilesAsync(contracts, hrUserId, ct);
        await SeedContractTestAppendicesAsync(contracts, allowanceTypeIds, hrUserId, ct);
        await SeedContractTestTerminationsAsync(contracts, terminationNoticeFile, hrUserId, ct);

        _logger.LogInformation(
            "Seeded contract test data: 7 test users, 15 contracts, allowances, files, appendices, terminations.");
    }

    // ===== Phase 1: Positions (BE/FE/LEAD/HR) — chèn-nếu-thiếu theo Code =====
    private async Task<Dictionary<string, int>> EnsureContractTestPositionsAsync(CancellationToken ct)
    {
        var seeds = new[]
        {
            (Code: "MOC", Name: "Thợ mộc khung tranh"),
            (Code: "SON", Name: "Thợ sơn hoàn thiện"),
            (Code: "TOTR", Name: "Tổ trưởng sản xuất"),
            (Code: "HR", Name: "Nhân viên nhân sự"),
        };
        var codes = seeds.Select(s => s.Code).ToArray();

        var existing = await _context.Positions
            .Where(p => codes.Contains(p.Code))
            .Select(p => new { p.Id, p.Code })
            .ToListAsync(ct);
        var byCode = existing.ToDictionary(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase);

        var toAdd = seeds
            .Where(s => !byCode.ContainsKey(s.Code))
            .Select(s => new Position { Code = s.Code, Name = s.Name })
            .ToList();
        if (toAdd.Count > 0)
        {
            await _context.Positions.AddRangeAsync(toAdd, ct);
            await _context.SaveChangesAsync(ct);
            foreach (var p in toAdd) byCode[p.Code] = p.Id;
            _logger.LogInformation("Seeded {Count} contract-test Position(s).", toAdd.Count);
        }

        return byCode;
    }

    // ===== Phase 2: Users (1 HR + 6 Employees) — chèn-nếu-thiếu theo Username =====
    private async Task<Dictionary<string, int>> EnsureContractTestUsersAsync(CancellationToken ct)
    {
        var seeds = new[]
        {
            (Username: "hr.user", Code: "HRT001", RoleId: SystemRoles.HrId, FullName: "Phạm Thị Lan", Email: "hr.user@mingo.local", Dob: Utc(1988, 5, 20), Phone: "0901111000"),
            (Username: "employee1", Code: "EMPT001", RoleId: SystemRoles.EmployeeId, FullName: "Nguyễn Văn Nam", Email: "nam@mingo.local", Dob: Utc(1995, 3, 15), Phone: "0902222000"),
            (Username: "employee2", Code: "EMPT002", RoleId: SystemRoles.EmployeeId, FullName: "Trần Thị Hoa", Email: "hoa@mingo.local", Dob: Utc(1993, 8, 25), Phone: "0903333000"),
            (Username: "employee3", Code: "EMPT003", RoleId: SystemRoles.EmployeeId, FullName: "Lê Văn Cường", Email: "cuong@mingo.local", Dob: Utc(1990, 11, 10), Phone: "0904444000"),
            (Username: "employee4", Code: "EMPT004", RoleId: SystemRoles.EmployeeId, FullName: "Phạm Thị Linh", Email: "linh@mingo.local", Dob: Utc(1997, 7, 8), Phone: "0905555000"),
            (Username: "employee5", Code: "EMPT005", RoleId: SystemRoles.EmployeeId, FullName: "Hoàng Văn Tuấn", Email: "tuan@mingo.local", Dob: Utc(1992, 2, 14), Phone: "0906666000"),
            (Username: "employee6", Code: "EMPT006", RoleId: SystemRoles.EmployeeId, FullName: "Vũ Thị Mai", Email: "mai@mingo.local", Dob: Utc(1996, 9, 30), Phone: "0907777000"),
        };
        var usernames = seeds.Select(s => s.Username).ToArray();

        var existing = await _context.Users
            .Where(u => usernames.Contains(u.Username))
            .Select(u => new { u.Id, u.Username })
            .ToListAsync(ct);
        var byUsername = existing.ToDictionary(u => u.Username, u => u.Id, StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var toAdd = seeds
            .Where(s => !byUsername.ContainsKey(s.Username))
            .Select(s => new User
            {
                EmployeeCode = s.Code,
                FullName = s.FullName,
                RoleId = s.RoleId,
                Username = s.Username,
                PasswordHash = _passwordHasher.Hash(ContractTestPassword),
                Email = s.Email,
                Dob = s.Dob,
                PhoneNo = s.Phone,
                Status = 1,
                PasswordChangedAt = now,
            })
            .ToList();
        if (toAdd.Count > 0)
        {
            await _context.Users.AddRangeAsync(toAdd, ct);
            await _context.SaveChangesAsync(ct);
            foreach (var u in toAdd) byUsername[u.Username] = u.Id;
            _logger.LogInformation("Seeded {Count} contract-test User(s): {Names}.",
                toAdd.Count, string.Join(", ", toAdd.Select(u => u.Username)));
        }

        return byUsername;
    }

    // ===== Phase 3: EmployeeInformations (kèm Guardian mix có/không) — idempotent theo CitizenId =====
    private async Task<Dictionary<string, int>> EnsureContractTestEmployeeInformationsAsync(
        IReadOnlyDictionary<string, int> userIds,
        IReadOnlyDictionary<string, int> positionIds,
        CancellationToken ct)
    {
        var seeds = new[]
        {
            // Lan (HR) — có guardian: chồng
            (Username: "hr.user", FullName: "Phạm Thị Lan", Dob: Utc(1988, 5, 20), Gender: Gender.Female,
             CitizenId: "012345678901", Address: "Quận Đống Đa, Hà Nội", Email: "hr.user@mingo.local", Phone: "0901111000",
             Pos: "HR", Dependents: 0, TaxNo: "8800001111", SocialNo: "BHXH001", Bank: "VCB001",
             GName: (string?)"Nguyễn Văn Lân", GRel: (string?)"Chồng", GPhone: (string?)"0911111000", GAddr: (string?)"Quận Đống Đa, Hà Nội"),
            // Nam — có guardian: mẹ
            (Username: "employee1", FullName: "Nguyễn Văn Nam", Dob: Utc(1995, 3, 15), Gender: Gender.Male,
             CitizenId: "012345678902", Address: "Quận Cầu Giấy, Hà Nội", Email: "nam@mingo.local", Phone: "0902222000",
             Pos: "MOC", Dependents: 1, TaxNo: "8800002222", SocialNo: "BHXH002", Bank: "VCB002",
             GName: (string?)"Nguyễn Thị Bích", GRel: (string?)"Mẹ", GPhone: (string?)"0922222111", GAddr: (string?)"Quận Cầu Giấy, Hà Nội"),
            // Hoa — KHÔNG có guardian (test optional path)
            (Username: "employee2", FullName: "Trần Thị Hoa", Dob: Utc(1993, 8, 25), Gender: Gender.Female,
             CitizenId: "012345678903", Address: "Quận 1, TP.HCM", Email: "hoa@mingo.local", Phone: "0903333000",
             Pos: "TOTR", Dependents: 2, TaxNo: "8800003333", SocialNo: "BHXH003", Bank: "VCB003",
             GName: (string?)null, GRel: (string?)null, GPhone: (string?)null, GAddr: (string?)null),
            // Cường — có guardian: bố
            (Username: "employee3", FullName: "Lê Văn Cường", Dob: Utc(1990, 11, 10), Gender: Gender.Male,
             CitizenId: "012345678904", Address: "Quận Hai Bà Trưng, Hà Nội", Email: "cuong@mingo.local", Phone: "0904444000",
             Pos: "SON", Dependents: 0, TaxNo: "8800004444", SocialNo: "BHXH004", Bank: "VCB004",
             GName: (string?)"Lê Văn Hùng", GRel: (string?)"Bố", GPhone: (string?)"0944444111", GAddr: (string?)"Quận Hai Bà Trưng, Hà Nội"),
            // Linh — có guardian: bố
            (Username: "employee4", FullName: "Phạm Thị Linh", Dob: Utc(1997, 7, 8), Gender: Gender.Female,
             CitizenId: "012345678905", Address: "Quận Ba Đình, Hà Nội", Email: "linh@mingo.local", Phone: "0905555000",
             Pos: "MOC", Dependents: 0, TaxNo: "8800005555", SocialNo: "BHXH005", Bank: "VCB005",
             GName: (string?)"Phạm Văn Mạnh", GRel: (string?)"Bố", GPhone: (string?)"0955555111", GAddr: (string?)"Quận Ba Đình, Hà Nội"),
            // Tuấn — KHÔNG có guardian (test optional path)
            (Username: "employee5", FullName: "Hoàng Văn Tuấn", Dob: Utc(1992, 2, 14), Gender: Gender.Male,
             CitizenId: "012345678906", Address: "Quận 3, TP.HCM", Email: "tuan@mingo.local", Phone: "0906666000",
             Pos: "MOC", Dependents: 1, TaxNo: "8800006666", SocialNo: "BHXH006", Bank: "VCB006",
             GName: (string?)null, GRel: (string?)null, GPhone: (string?)null, GAddr: (string?)null),
            // Mai — guardian partial (thiếu Address) — test partial path
            (Username: "employee6", FullName: "Vũ Thị Mai", Dob: Utc(1996, 9, 30), Gender: Gender.Female,
             CitizenId: "012345678907", Address: "Quận Long Biên, Hà Nội", Email: "mai@mingo.local", Phone: "0907777000",
             Pos: "SON", Dependents: 0, TaxNo: "8800007777", SocialNo: "BHXH007", Bank: "VCB007",
             GName: (string?)"Vũ Văn Hải", GRel: (string?)"Anh", GPhone: (string?)"0977777111", GAddr: (string?)null),
        };
        var citizenIds = seeds.Select(s => s.CitizenId).ToArray();

        // Load tracked (không AsNoTracking) để có thể update UserId khi lệch — data drift
        // xảy ra khi lần trước tạo EI nhưng UserId bị null hoặc trỏ tới user đã xóa.
        var existing = await _context.EmployeeInformations
            .Where(e => citizenIds.Contains(e.CitizenId))
            .ToListAsync(ct);
        var byCitizenId = existing.ToDictionary(e => e.CitizenId, e => e, StringComparer.Ordinal);
        var idByCitizenId = existing.ToDictionary(e => e.CitizenId, e => e.Id, StringComparer.Ordinal);

        // Reconcile: nếu EI đã tồn tại nhưng UserId null hoặc lệch → cập nhật.
        var reconciled = 0;
        foreach (var s in seeds)
        {
            if (!byCitizenId.TryGetValue(s.CitizenId, out var ei)) continue;
            var expectedUserId = userIds[s.Username];
            if (ei.UserId != expectedUserId)
            {
                ei.UserId = expectedUserId;
                reconciled++;
            }
        }
        if (reconciled > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Reconciled UserId on {Count} existing contract-test EmployeeInformation(s).", reconciled);
        }

        var toAdd = new List<(string Username, EmployeeInformation Entity)>();
        foreach (var s in seeds)
        {
            if (idByCitizenId.ContainsKey(s.CitizenId)) continue;

            toAdd.Add((s.Username, new EmployeeInformation
            {
                UserId = userIds[s.Username],
                FullName = s.FullName,
                Dob = s.Dob,
                Gender = s.Gender,
                CitizenId = s.CitizenId,
                PermanentAddress = s.Address,
                Email = s.Email,
                PhoneNo = s.Phone,
                PositionId = positionIds[s.Pos],
                DependentsNo = s.Dependents,
                TaxNo = s.TaxNo,
                SocialNo = s.SocialNo,
                BankNumber = s.Bank,
                GuardianFullName = s.GName,
                GuardianRelationship = s.GRel,
                GuardianPhoneNo = s.GPhone,
                GuardianAddress = s.GAddr,
            }));
        }

        if (toAdd.Count > 0)
        {
            await _context.EmployeeInformations.AddRangeAsync(toAdd.Select(t => t.Entity), ct);
            await _context.SaveChangesAsync(ct);
            foreach (var (_, e) in toAdd) idByCitizenId[e.CitizenId] = e.Id;
            _logger.LogInformation("Seeded {Count} contract-test EmployeeInformation(s).", toAdd.Count);
        }

        // Map username → EmployeeInformationId (Contracts FK vào EmployeeInformations, không phải Users)
        return seeds.ToDictionary(
            s => s.Username, s => idByCitizenId[s.CitizenId], StringComparer.OrdinalIgnoreCase);
    }

    // ===== Phase 4: AllowanceTypes (3 active + 1 inactive) — PHONE tái sử dụng từ seed mặc định =====
    private async Task<Dictionary<string, int>> EnsureContractTestAllowanceTypesAsync(CancellationToken ct)
    {
        var seeds = new[]
        {
            (Code: "LUNCH", Name: "Phụ cấp ăn trưa", Desc: (string?)"Theo TT 26/2016/TT-BLDTBXH", Amount: (decimal?)730_000m, IsBhxh: false, IsTaxable: false, IsActive: true),
            (Code: "TRANSPORT", Name: "Phụ cấp đi lại", Desc: (string?)null, Amount: (decimal?)500_000m, IsBhxh: false, IsTaxable: true, IsActive: true),
            (Code: "RESPONSIBILITY", Name: "Phụ cấp trách nhiệm", Desc: (string?)"Áp dụng Tổ trưởng / Quản lý", Amount: (decimal?)2_000_000m, IsBhxh: true, IsTaxable: true, IsActive: true),
            (Code: "LEGACY", Name: "Phụ cấp cũ (ngừng)", Desc: (string?)"Demo inactive — UI grey", Amount: (decimal?)100_000m, IsBhxh: false, IsTaxable: false, IsActive: false),
            // PHONE thường đã được SeedDefaultAllowanceTypesAsync tạo trước — chỉ chèn nếu thiếu.
            (Code: "PHONE", Name: "Phụ cấp điện thoại", Desc: (string?)null, Amount: (decimal?)300_000m, IsBhxh: false, IsTaxable: true, IsActive: true),
        };
        var codes = seeds.Select(s => s.Code).ToArray();

        var existing = await _context.AllowanceTypes
            .Where(a => codes.Contains(a.Code))
            .Select(a => new { a.Id, a.Code })
            .ToListAsync(ct);
        var byCode = existing.ToDictionary(a => a.Code, a => a.Id, StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var toAdd = seeds
            .Where(s => !byCode.ContainsKey(s.Code))
            .Select(s => new AllowanceType
            {
                Code = s.Code,
                Name = s.Name,
                Description = s.Desc,
                DefaultAmount = s.Amount,
                IsBhxhBase = s.IsBhxh,
                IsTaxable = s.IsTaxable,
                IsActive = s.IsActive,
                CreatedAt = now,
                UpdatedAt = s.IsActive ? null : now,
            })
            .ToList();
        if (toAdd.Count > 0)
        {
            await _context.AllowanceTypes.AddRangeAsync(toAdd, ct);
            await _context.SaveChangesAsync(ct);
            foreach (var a in toAdd) byCode[a.Code] = a.Id;
            _logger.LogInformation("Seeded {Count} contract-test AllowanceType(s).", toAdd.Count);
        }

        return byCode;
    }

    // ===== Phase 5: ContractTemplates (2 active + 1 inactive) — idempotent theo Name =====
    private async Task EnsureContractTestTemplatesAsync(CancellationToken ct)
    {
        var seeds = new[]
        {
            (Name: "HĐ Định hạn Chính thức v1", Category: ContractEnums.ContractCategory.Definite, IsActive: true),
            (Name: "HĐ Vô thời hạn v1", Category: ContractEnums.ContractCategory.Indefinite, IsActive: true),
            (Name: "Template cũ (inactive)", Category: ContractEnums.ContractCategory.Definite, IsActive: false),
        };
        var names = seeds.Select(s => s.Name).ToArray();

        var existingNames = await _context.ContractTemplates
            .AsNoTracking()
            .Where(t => names.Contains(t.Name))
            .Select(t => t.Name)
            .ToListAsync(ct);
        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var toAdd = seeds
            .Where(s => !existing.Contains(s.Name))
            .Select(s => new ContractTemplate
            {
                Name = s.Name,
                ContractCategory = s.Category,
                ContractSubtype = ContractEnums.ContractSubtype.Official,
                TemplateFileId = null, // seed không upload file thật
                Version = 1,
                IsActive = s.IsActive,
                CreatedAt = now,
            })
            .ToList();
        if (toAdd.Count > 0)
        {
            await _context.ContractTemplates.AddRangeAsync(toAdd, ct);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded {Count} contract-test ContractTemplate(s).", toAdd.Count);
        }
    }

    // ===== Phase 6: 15 Contracts cover mọi logic case =====
    private async Task<Dictionary<string, Contract>> SeedContractTestContractsAsync(
        int hrUserId,
        IReadOnlyDictionary<string, int> employeeIds,
        int partyId,
        IReadOnlyDictionary<string, int> positionIds,
        IReadOnlyDictionary<string, int> gradeIds,
        CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var eiNam = employeeIds["employee1"];
        var eiHoa = employeeIds["employee2"];
        var eiCuong = employeeIds["employee3"];
        var eiLinh = employeeIds["employee4"];
        var eiTuan = employeeIds["employee5"];
        var eiMai = employeeIds["employee6"];
        var posBe = positionIds["MOC"];
        var posFe = positionIds["SON"];
        var posLead = positionIds["TOTR"];

        // #1 DRAFT cho Nam — test edit/cancel/activate
        var draft = NewTestContract("HDLD-DRAFT-NAM-001",
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Draft,
            eiNam, partyId, hrUserId, posBe,
            "Thợ mộc — gia công, lắp ráp khung tranh gỗ", "Hà Nội",
            today.AddDays(30), today.AddDays(30).AddYears(1),
            gradeIds["MOC-2"], 7_200_000m);

        // #2 ACTIVE Indefinite cho Nam (chính) — test phụ lục + termination
        var activeNamInd = NewTestContract("HDLD-ACTIVE-NAM-IND",
            ContractEnums.ContractCategory.Indefinite, ContractEnums.ContractStatus.Active,
            eiNam, partyId, hrUserId, posBe,
            "Thợ mộc bậc cao — khung tranh cao cấp", "Hà Nội",
            Utc(2024, 1, 1), null,
            gradeIds["MOC-3"], 8_000_000m,
            signedAt: Utc(2024, 1, 1), signedPlace: "Hà Nội",
            createdAt: Utc(2023, 12, 25), updatedAt: Utc(2024, 1, 1));

        // #3 + #4 Hai HĐ Định hạn cũ của Nam — chứng minh Điều 20 k.2c: cùng pháp nhân NSDLĐ nên
        // rule "không cho ký HĐ Định hạn thứ 3" trigger đúng khi HR test tạo HĐ Định hạn mới cho Nam.
        var namHist1 = NewTestContract("HDLD-NAM-HIST-DEF-1",
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Expired,
            eiNam, partyId, hrUserId, posBe,
            "Thợ mộc Bậc 1", "Hà Nội",
            Utc(2022, 1, 1), Utc(2022, 12, 31),
            gradeIds["MOC-1"], 6_500_000m,
            signedAt: Utc(2022, 1, 1), signedPlace: "Hà Nội",
            createdAt: Utc(2021, 12, 15), updatedAt: Utc(2022, 12, 31));
        var namHist2 = NewTestContract("HDLD-NAM-HIST-DEF-2",
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Expired,
            eiNam, partyId, hrUserId, posBe,
            "Thợ mộc Bậc 2", "Hà Nội",
            Utc(2023, 1, 1), Utc(2023, 12, 31),
            gradeIds["MOC-2"], 7_200_000m,
            signedAt: Utc(2023, 1, 1), signedPlace: "Hà Nội",
            createdAt: Utc(2022, 12, 15), updatedAt: Utc(2023, 12, 31));

        // #5 ACTIVE cho Hoa — self-scope test
        var activeHoa = NewTestContract("HDLD-ACTIVE-HOA-LEAD",
            ContractEnums.ContractCategory.Indefinite, ContractEnums.ContractStatus.Active,
            eiHoa, partyId, hrUserId, posLead,
            "Tổ trưởng — quản lý tổ sản xuất 5 người", "TP.HCM",
            Utc(2023, 6, 1), null,
            gradeIds["TOTR-1"], 7_200_000m,
            signedAt: Utc(2023, 6, 1), signedPlace: "TP.HCM",
            createdAt: Utc(2023, 5, 28));

        // #6 DRAFT có Probation cho Linh — test probation flow
        var draftLinhProb = NewTestContract("HDLD-DRAFT-LINH-PROB",
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Draft,
            eiLinh, partyId, hrUserId, posBe,
            "Thợ mộc — thử việc 2 tháng", "Hà Nội",
            today.AddDays(15), today.AddMonths(3),
            gradeIds["MOC-1"], 6_500_000m,
            subtype: ContractEnums.ContractSubtype.Probation);
        draftLinhProb.HasProbation = true;
        draftLinhProb.ProbationStartDate = today.AddDays(15);
        draftLinhProb.ProbationEndDate = today.AddDays(75);
        draftLinhProb.ProbationSalary = 5_700_000m; // ~88% của 6,5tr ≥ 85% (Điều 26) và ≥ LTT vùng I
        draftLinhProb.ProbationResultStatus = ContractEnums.ProbationResultStatus.InProgress;

        // #7 ACTIVE Definite 24m cho Linh — test termination 30-day notice rule
        var activeLinh24M = NewTestContract("HDLD-ACTIVE-LINH-24M",
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Active,
            eiLinh, partyId, hrUserId, posBe,
            "Thợ mộc Bậc 2", "Hà Nội",
            today.AddDays(-180), today.AddDays(-180).AddYears(2),
            gradeIds["MOC-2"], 7_200_000m,
            signedAt: today.AddDays(-180), signedPlace: "Hà Nội",
            createdAt: today.AddDays(-185));

        // #8 ACTIVE Definite 6m cho Mai — test 3-working-day notice rule
        var activeMai6M = NewTestContract("HDLD-ACTIVE-MAI-6M",
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Active,
            eiMai, partyId, hrUserId, posFe,
            "Thợ sơn Bậc 2 — đơn ngắn hạn", "Hà Nội",
            today.AddMonths(-2), today.AddMonths(4),
            gradeIds["SON-2"], 7_300_000m,
            signedAt: today.AddMonths(-2), signedPlace: "Hà Nội",
            createdAt: today.AddMonths(-2).AddDays(-5));

        // #9 CANCELLED cho Cường — status guard test
        var cancelCuong = NewTestContract("HDLD-CANCEL-CUONG",
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Cancelled,
            eiCuong, partyId, hrUserId, posFe,
            "Thợ sơn (HĐ bị hủy do Cường rút lời)", "Hà Nội",
            Utc(2026, 1, 1), Utc(2026, 12, 31),
            gradeIds["SON-2"], 7_300_000m,
            createdAt: Utc(2025, 12, 20), updatedAt: Utc(2025, 12, 22));

        // #10 TERMINATED cho Cường — view termination flow
        var termCuong = NewTestContract("HDLD-TERM-CUONG",
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Terminated,
            eiCuong, partyId, hrUserId, posFe,
            "Thợ sơn (đã chấm dứt theo Điều 34 k.1)", "Hà Nội",
            Utc(2024, 1, 1), Utc(2025, 12, 31),
            gradeIds["SON-2"], 7_300_000m,
            signedAt: Utc(2024, 1, 1), signedPlace: "Hà Nội",
            createdAt: Utc(2023, 12, 20), updatedAt: Utc(2025, 12, 31));

        // #11 ARCHIVED cho Cường — test 409 CONTRACT_ARCHIVED (dùng bậc lương inactive BE-99)
        var archCuong = NewTestContract("HDLD-ARCH-CUONG-2014",
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Terminated,
            eiCuong, partyId, hrUserId, posFe,
            "Thợ sơn(HĐ cũ 2014 — đã archive sau 10 năm chấm dứt)", "Hà Nội",
            Utc(2014, 1, 1), Utc(2015, 12, 31),
            gradeIds["MOC-99"], 6_000_000m,
            workingTimeJson: "{}",
            signedAt: Utc(2014, 1, 1), signedPlace: "Hà Nội",
            createdAt: Utc(2013, 12, 20), updatedAt: Utc(2015, 12, 31));
        archCuong.ArchivedAt = today.AddDays(-90); // archive 90 ngày trước

        // #12 EXPIRED > 10 năm cho Mai — archive job candidate (Expired path)
        var expMaiOld = NewTestContract("HDLD-EXP-MAI-OLD-2014",
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Expired,
            eiMai, partyId, hrUserId, posFe,
            "Thợ sơn(HĐ Expired 2014 — chờ archive job)", "Hà Nội",
            today.AddYears(-12), today.AddYears(-11),
            gradeIds["SON-2"], 6_000_000m,
            workingTimeJson: "{}",
            signedAt: today.AddYears(-12), signedPlace: "Hà Nội",
            createdAt: today.AddYears(-12), updatedAt: today.AddYears(-11));

        // #13 AUTO-CONVERT CANDIDATE cho Tuấn (Definite Active, EffectiveTo + 30d < NOW, no child)
        var autoConvertTuan = NewTestContract("HDLD-AUTOCVT-TUAN",
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Active,
            eiTuan, partyId, hrUserId, posBe,
            "Thợ mộc Bậc 2 (HĐ Định hạn hết hạn 45 ngày trước, vẫn Active → AutoConvertJob sẽ chuyển thành Indefinite)",
            "TP.HCM",
            today.AddYears(-2), today.AddDays(-45),
            gradeIds["MOC-2"], 7_200_000m,
            signedAt: today.AddYears(-2), signedPlace: "TP.HCM",
            createdAt: today.AddYears(-2).AddDays(-5));

        // #14 EXPIRE CANDIDATE (parent) — Tuấn, có HĐ con
        var expireParent = NewTestContract("HDLD-EXPCAND-PARENT",
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Active,
            eiTuan, partyId, hrUserId, posBe,
            "Thợ mộc Bậc 1 (HĐ Định hạn parent — hết hạn 2 ngày trước, đã có HĐ con)", "TP.HCM",
            today.AddYears(-1), today.AddDays(-2),
            gradeIds["MOC-1"], 6_500_000m,
            signedAt: today.AddYears(-1), signedPlace: "TP.HCM",
            createdAt: today.AddYears(-1).AddDays(-3));

        // #15 CHILD của #14 (đã ký HĐ mới tiếp nối) — hiệu lực 15/8/2024 – 15/8/2026
        var expireChild = NewTestContract("HDLD-EXPCAND-CHILD",
            ContractEnums.ContractCategory.Definite, ContractEnums.ContractStatus.Active,
            eiTuan, partyId, hrUserId, posBe,
            "Thợ mộc Bậc 2 (HĐ mới sau khi #14 hết hạn)", "TP.HCM",
            Utc(2024, 8, 15), Utc(2026, 8, 15),
            gradeIds["MOC-2"], 7_200_000m,
            signedAt: Utc(2024, 8, 15), signedPlace: "TP.HCM",
            createdAt: Utc(2024, 8, 10));
        expireChild.ParentContract = expireParent; // EF tự resolve ParentContractId khi save

        var contracts = new List<Contract>
        {
            draft, activeNamInd, namHist1, namHist2, activeHoa, draftLinhProb, activeLinh24M,
            activeMai6M, cancelCuong, termCuong, archCuong, expMaiOld, autoConvertTuan,
            expireParent, expireChild,
        };
        await _context.Contracts.AddRangeAsync(contracts, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} contract-test Contract(s).", contracts.Count);

        return contracts.ToDictionary(c => c.ContractNo, StringComparer.OrdinalIgnoreCase);
    }

    private static Contract NewTestContract(
        string contractNo,
        ContractEnums.ContractCategory category,
        ContractEnums.ContractStatus status,
        int employeeInformationId,
        int employerPartyId,
        int signerUserId,
        int positionId,
        string jobDescription,
        string workLocation,
        DateTime effectiveFrom,
        DateTime? effectiveTo,
        int salaryGradeId,
        decimal salaryAmount,
        ContractEnums.ContractSubtype subtype = ContractEnums.ContractSubtype.Official,
        string workingTimeJson = @"{""hoursPerDay"":8,""daysPerWeek"":5}",
        DateTime? signedAt = null,
        string? signedPlace = null,
        DateTime? createdAt = null,
        DateTime? updatedAt = null) =>
        new()
        {
            ContractNo = contractNo,
            ContractCategory = category,
            ContractSubtype = subtype,
            Form = ContractEnums.ContractForm.Written,
            EmployeeInformationId = employeeInformationId,
            EmployerPartyId = employerPartyId,
            EmployerSignerUserId = signerUserId,
            PositionId = positionId,
            JobDescription = jobDescription,
            WorkLocation = workLocation,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            SalaryGradeId = salaryGradeId,
            SalaryAmountSnapshot = salaryAmount,
            SalaryCurrencySnapshot = "VND",
            SalaryFormType = ContractEnums.SalaryFormType.Time,
            SalaryPayCycle = ContractEnums.SalaryPayCycle.Monthly,
            WorkingTimeJson = workingTimeJson,
            SocialInsuranceFlag = true,
            HealthInsuranceFlag = true,
            UnemploymentInsuranceFlag = true,
            Status = status,
            ConvertedFromDefiniteFlag = false,
            HasProbation = false,
            SignedAt = signedAt,
            SignedPlace = signedPlace,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            CreatedByUserId = signerUserId,
            UpdatedAt = updatedAt,
        };

    // ===== Phase 7: ContractAllowances — gắn phụ cấp vào các HĐ Active =====
    private async Task SeedContractTestAllowancesAsync(
        IReadOnlyDictionary<string, Contract> contracts,
        IReadOnlyDictionary<string, int> allowanceTypeIds,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var seeds = new (string ContractNo, string TypeCode, decimal Amount, string? Note)[]
        {
            // Nam Active Indefinite (#2): ăn trưa + đi lại
            ("HDLD-ACTIVE-NAM-IND", "LUNCH", 730_000m, null),
            ("HDLD-ACTIVE-NAM-IND", "TRANSPORT", 500_000m, null),
            // Hoa Tổ trưởng (#5): full bộ
            ("HDLD-ACTIVE-HOA-LEAD", "LUNCH", 730_000m, null),
            ("HDLD-ACTIVE-HOA-LEAD", "TRANSPORT", 1_500_000m, "Đi công tác nhiều"),
            ("HDLD-ACTIVE-HOA-LEAD", "PHONE", 500_000m, null),
            ("HDLD-ACTIVE-HOA-LEAD", "RESPONSIBILITY", 5_000_000m, "Phụ cấp Tổ trưởng"),
            // Linh 24m (#7): ăn trưa
            ("HDLD-ACTIVE-LINH-24M", "LUNCH", 730_000m, null),
            // Mai 6m (#8): ăn trưa + điện thoại
            ("HDLD-ACTIVE-MAI-6M", "LUNCH", 730_000m, null),
            ("HDLD-ACTIVE-MAI-6M", "PHONE", 300_000m, null),
        };

        var allowances = seeds.Select(s => new ContractAllowance
        {
            ContractId = contracts[s.ContractNo].Id,
            AllowanceTypeId = allowanceTypeIds[s.TypeCode],
            Amount = s.Amount,
            Currency = "VND",
            Note = s.Note,
            CreatedAt = now,
        }).ToList();

        await _context.ContractAllowances.AddRangeAsync(allowances, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} contract-test ContractAllowance(s).", allowances.Count);
    }

    // ===== Phase 8: ContractFiles (metadata — MinIO blob KHÔNG có thật, download sẽ 404) =====
    // Multi-version cho HĐ #2 (test replace flow + sort Version DESC + IsCurrent).
    // Trả về file notice Termination — Phase 10 cần FK WrittenNoticeFileId.
    private async Task<ContractFile> SeedContractTestFilesAsync(
        IReadOnlyDictionary<string, Contract> contracts, int hrUserId, CancellationToken ct)
    {
        var files = new List<ContractFile>
        {
            // HĐ #2 Nam — 2 version: v1 không còn current, v2 IsCurrent
            NewTestFile(ContractEnums.FileTargetType.Contract, contracts["HDLD-ACTIVE-NAM-IND"].Id,
                "HDLD-ACTIVE-NAM-IND-v1.pdf", "contracts/2024/01/HDLD-ACTIVE-NAM-IND/v1.pdf",
                524_288, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa0001",
                version: 1, isCurrent: false, Utc(2023, 12, 25, 10, 0), hrUserId),
            NewTestFile(ContractEnums.FileTargetType.Contract, contracts["HDLD-ACTIVE-NAM-IND"].Id,
                "HDLD-ACTIVE-NAM-IND-v2.pdf", "contracts/2024/02/HDLD-ACTIVE-NAM-IND/v2.pdf",
                530_000, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa0002",
                version: 2, isCurrent: true, Utc(2024, 2, 15, 14, 0), hrUserId),
            // HĐ #5 Hoa
            NewTestFile(ContractEnums.FileTargetType.Contract, contracts["HDLD-ACTIVE-HOA-LEAD"].Id,
                "HDLD-ACTIVE-HOA-LEAD-v1.pdf", "contracts/2023/06/HDLD-ACTIVE-HOA-LEAD/v1.pdf",
                612_345, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb0002",
                version: 1, isCurrent: true, Utc(2023, 5, 28, 14, 30), hrUserId),
            // HĐ #7 Linh
            NewTestFile(ContractEnums.FileTargetType.Contract, contracts["HDLD-ACTIVE-LINH-24M"].Id,
                "HDLD-ACTIVE-LINH-24M-v1.pdf", "contracts/2025/12/HDLD-ACTIVE-LINH-24M/v1.pdf",
                489_000, "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc0007",
                version: 1, isCurrent: true, Utc(2025, 12, 1, 9, 0), hrUserId),
            // HĐ #8 Mai
            NewTestFile(ContractEnums.FileTargetType.Contract, contracts["HDLD-ACTIVE-MAI-6M"].Id,
                "HDLD-ACTIVE-MAI-6M-v1.pdf", "contracts/2026/04/HDLD-ACTIVE-MAI-6M/v1.pdf",
                411_000, "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd0008",
                version: 1, isCurrent: true, Utc(2026, 3, 30, 11, 0), hrUserId),
            // HĐ #10 Cường Terminated — file HĐ
            NewTestFile(ContractEnums.FileTargetType.Contract, contracts["HDLD-TERM-CUONG"].Id,
                "HDLD-TERM-CUONG-v1.pdf", "contracts/2024/01/HDLD-TERM-CUONG/v1.pdf",
                489_000, "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee0010",
                version: 1, isCurrent: true, Utc(2023, 12, 20, 9, 0), hrUserId),
        };

        // File notice cho termination HĐ #10 — trả về cho Phase 10
        var terminationNotice = NewTestFile(
            ContractEnums.FileTargetType.Termination, contracts["HDLD-TERM-CUONG"].Id,
            "termination-notice-2025-12.pdf", "terminations/2025/12/HDLD-TERM-CUONG/notice-v1.pdf",
            102_400, "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff0011",
            version: 1, isCurrent: true, Utc(2025, 12, 1, 11, 0), hrUserId);
        files.Add(terminationNotice);

        await _context.ContractFiles.AddRangeAsync(files, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} contract-test ContractFile(s).", files.Count);

        return terminationNotice;
    }

    private static ContractFile NewTestFile(
        ContractEnums.FileTargetType targetType,
        int targetId,
        string fileName,
        string objectKey,
        long fileSizeBytes,
        string sha256,
        int version,
        bool isCurrent,
        DateTime uploadedAt,
        int uploadedByUserId) =>
        new()
        {
            TargetType = targetType,
            TargetId = targetId,
            FileName = fileName,
            MinioBucket = ContractTestMinioBucket,
            MinioObjectKey = objectKey,
            CdnUrl = string.Empty,
            MimeType = "application/pdf",
            FileSizeBytes = fileSizeBytes,
            Sha256 = sha256,
            Version = version,
            IsCurrent = isCurrent,
            SignedBySource = "mingo-signing",
            UploadedAt = uploadedAt,
            UploadedByUserId = uploadedByUserId,
        };

    // ===== Phase 9: ContractAppendices (2 Active + 1 Draft) + file cho phụ lục đầu =====
    private async Task SeedContractTestAppendicesAsync(
        IReadOnlyDictionary<string, Contract> contracts,
        IReadOnlyDictionary<string, int> allowanceTypeIds,
        int hrUserId,
        CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var contractNamId = contracts["HDLD-ACTIVE-NAM-IND"].Id;
        var transportId = allowanceTypeIds["TRANSPORT"];
        var phoneId = allowanceTypeIds["PHONE"];

        // Phụ lục Active cho Nam — đổi 1 phụ cấp (simple change)
        var appendix1 = new ContractAppendix
        {
            ContractId = contractNamId,
            AppendixNo = "PL-2025-001",
            AppendixType = ContractEnums.AppendixType.Amendment,
            Title = "Điều chỉnh phụ cấp đi lại",
            ChangedClausesJson =
                $@"{{""updateAllowances"":[{{""allowanceTypeId"":{transportId},""amount"":800000,""note"":""Tăng từ 500k lên 800k""}}]}}",
            AppendixText = "Hai bên thỏa thuận điều chỉnh phụ cấp đi lại của NLĐ Nam từ 500,000 VND lên 800,000 VND/tháng.",
            NoticeSentAt = Utc(2025, 5, 15),
            EffectiveFrom = Utc(2025, 6, 1),
            SignedAt = Utc(2025, 5, 25),
            Status = ContractEnums.AppendixStatus.Active,
            CreatedAt = Utc(2025, 5, 15),
            CreatedByUserId = hrUserId,
        };

        // Phụ lục Active cho Nam — multi-change (đổi job + thêm allowance)
        var appendix2 = new ContractAppendix
        {
            ContractId = contractNamId,
            AppendixNo = "PL-2025-002",
            AppendixType = ContractEnums.AppendixType.Amendment,
            Title = "Thăng bậc thợ cả + tăng lương + thêm phụ cấp điện thoại",
            ChangedClausesJson =
                $@"{{""jobDescription"":""Thợ mộc bậc cao (thăng bậc thợ cả)"",""addAllowances"":[{{""allowanceTypeId"":{phoneId},""amount"":300000,""currency"":""VND"",""note"":""Cấp mới sau thăng bậc""}}]}}",
            AppendixText = "Hai bên thỏa thuận thăng Nam lên thợ cả + bổ sung phụ cấp điện thoại.",
            NoticeSentAt = Utc(2025, 10, 1),
            EffectiveFrom = Utc(2025, 10, 15),
            SignedAt = Utc(2025, 10, 10),
            Status = ContractEnums.AppendixStatus.Active,
            CreatedAt = Utc(2025, 10, 1),
            CreatedByUserId = hrUserId,
        };

        // Phụ lục DRAFT cho Hoa — chờ activate (FE test activate flow), chưa ký
        var appendix3 = new ContractAppendix
        {
            ContractId = contracts["HDLD-ACTIVE-HOA-LEAD"].Id,
            AppendixNo = "PL-2026-001",
            AppendixType = ContractEnums.AppendixType.Amendment,
            Title = "Đổi địa điểm làm việc TP.HCM → Hà Nội",
            ChangedClausesJson =
                @"{""workLocation"":""Hà Nội"",""jobDescription"":""Tổ trưởng — chuyển ra HN làm việc tại trụ sở chính""}",
            AppendixText = "Hai bên thỏa thuận chuyển NLĐ Hoa từ chi nhánh TP.HCM ra trụ sở Hà Nội.",
            NoticeSentAt = today.AddDays(-2),
            EffectiveFrom = today.AddDays(10),
            SignedAt = null,
            Status = ContractEnums.AppendixStatus.Draft,
            CreatedAt = today.AddDays(-2),
            CreatedByUserId = hrUserId,
        };

        await _context.ContractAppendices.AddRangeAsync(new[] { appendix1, appendix2, appendix3 }, ct);
        await _context.SaveChangesAsync(ct);

        // File cho phụ lục Active đầu (PL-2025-001)
        var appendixFile = NewTestFile(
            ContractEnums.FileTargetType.Appendix, appendix1.Id,
            "PL-2025-001.pdf", "appendices/2025/05/HDLD-ACTIVE-NAM-IND/PL-2025-001-v1.pdf",
            92_000, "11111111111111111111111111111111111111111111111111111111aaa0001",
            version: 1, isCurrent: true, Utc(2025, 5, 25, 10, 0), hrUserId);
        await _context.ContractFiles.AddAsync(appendixFile, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded 3 contract-test ContractAppendix(es) (2 Active + 1 Draft).");
    }

    // ===== Phase 10: ContractTerminations (2 — HĐ #10 có notice file, HĐ #11 archived không cần) =====
    private async Task SeedContractTestTerminationsAsync(
        IReadOnlyDictionary<string, Contract> contracts,
        ContractFile terminationNoticeFile,
        int hrUserId,
        CancellationToken ct)
    {
        var reasonExpiryId = await _context.ContractTerminationReasonRefs
            .AsNoTracking()
            .Where(r => r.Code == "ART34_C1_EXPIRY")
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (reasonExpiryId == 0)
        {
            _logger.LogWarning("Termination reason ART34_C1_EXPIRY not found; skipping contract-test terminations.");
            return;
        }

        var terminations = new[]
        {
            // Termination cho HĐ Cường — C1 AutoExpiry, cần WrittenNoticeFile
            new ContractTermination
            {
                ContractId = contracts["HDLD-TERM-CUONG"].Id,
                TerminationDate = Utc(2025, 12, 31),
                ReasonRefId = reasonExpiryId,
                TerminationParty = ContractEnums.TerminationParty.AutoExpiry,
                LegalBasis = "Điều 34 k.1 BLLĐ 2019",
                NoticeSentAt = Utc(2025, 12, 1),
                NoticeDaysAhead = 30,
                NoNoticeRequired = false,
                WrittenNoticeFileId = terminationNoticeFile.Id,
                DecidedByUserId = hrUserId,
                Notes = "HĐ hết hạn tự nhiên, hai bên không tiếp tục ký mới.",
                CreatedAt = Utc(2025, 12, 15),
            },
            // Termination cho HĐ Archived Cường — C1 AutoExpiry (không cần notice file)
            new ContractTermination
            {
                ContractId = contracts["HDLD-ARCH-CUONG-2014"].Id,
                TerminationDate = Utc(2015, 12, 31),
                ReasonRefId = reasonExpiryId,
                TerminationParty = ContractEnums.TerminationParty.AutoExpiry,
                LegalBasis = "Điều 34 k.1 BLLĐ 2019",
                NoticeSentAt = Utc(2015, 12, 1),
                NoticeDaysAhead = 30,
                NoNoticeRequired = false,
                WrittenNoticeFileId = null,
                DecidedByUserId = hrUserId,
                Notes = "HĐ cũ 2014-2015 đã chấm dứt do hết hạn — sau 10+ năm đã được archive.",
                CreatedAt = Utc(2015, 12, 15),
            },
        };

        await _context.ContractTerminations.AddRangeAsync(terminations, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded 2 contract-test ContractTermination(s).");
    }
}
