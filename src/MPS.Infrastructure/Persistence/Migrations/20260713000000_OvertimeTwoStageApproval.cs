using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// OT workflow v3 (per docs/plans/overtime-request-workflow-plan.md):
    ///  - Đổi từ v1 (PM tạo batch, 1 tầng duyệt HR|DM) → v3 (NV tự đăng ký, 2 tầng: PM review → HR/DM approve).
    ///  - Enum OtRequestStatus thêm PendingApprover=5 (PM đã forward, chờ HR/DM).
    ///  - OvertimeRequests thêm 6 cột audit tách 2 tầng: PmReview* + ApproverReview*.
    ///  - Permissions:
    ///     * 112 Create.Self: gán cho Employee (RoleId=7)
    ///     * 113 Approve.All → rename Code = Approve.Department; giữ cho DM (5), bỏ HR (2)
    ///     * 178 CreateBatch.Department → rename Code = Review.Project; PM (6) giữ nguyên mapping
    ///     * 180 Approve.Hr: NEW cho HR (2)
    ///  - Legacy data: bulk reject records Status=Pending (=1) không có ProjectId — chuyển luồng mới.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260713000000_OvertimeTwoStageApproval")]
    public partial class OvertimeTwoStageApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================================
            // 1. Schema: 6 cột audit mới cho OvertimeRequests
            // =========================================================================
            migrationBuilder.AddColumn<int>(
                name: "PmReviewedByUserId",
                table: "OvertimeRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PmReviewedAt",
                table: "OvertimeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PmReviewNote",
                table: "OvertimeRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApproverReviewedByUserId",
                table: "OvertimeRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApproverReviewedAt",
                table: "OvertimeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApproverReviewNote",
                table: "OvertimeRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_PmReviewedByUserId",
                table: "OvertimeRequests",
                column: "PmReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_ApproverReviewedByUserId",
                table: "OvertimeRequests",
                column: "ApproverReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_OvertimeRequests_Users_PmReviewedByUserId",
                table: "OvertimeRequests",
                column: "PmReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OvertimeRequests_Users_ApproverReviewedByUserId",
                table: "OvertimeRequests",
                column: "ApproverReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // =========================================================================
            // 2. Permission metadata updates
            //    - 178: CreateBatch.Department → Review.Project  (PM giữ role mapping)
            //    - 113: Approve.All → Approve.Department        (DM giữ, bỏ HR)
            // =========================================================================
            migrationBuilder.Sql(@"
UPDATE [Permissions]
   SET [Action]      = N'Review',
       [Code]        = N'OvertimeRequest.Review.Project',
       [Scope]       = N'Project',
       [Description] = N'PM (tổ trưởng đơn = Project.ResponsibleUserId) review tầng đầu request OT — Forward chọn HR/DM hoặc Reject.'
 WHERE [Id] = 178;");

            migrationBuilder.Sql(@"
UPDATE [Permissions]
   SET [Code]        = N'OvertimeRequest.Approve.Department',
       [Scope]       = N'Department',
       [Description] = N'DM duyệt cuối request OT khi PM chọn ApproverRoute = DepartmentManager. Scope: cùng Dept với Project.'
 WHERE [Id] = 113;");

            // Bỏ HR (RoleId=2) khỏi permission 113 — HR không còn duyệt DM-route.
            migrationBuilder.Sql(
                "DELETE FROM [RolePermissions] WHERE [PermissionId] = 113 AND [RoleId] = 2;");

            // =========================================================================
            // 3. Permission mới: 180 = Approve.Hr → gán cho HR (2).
            // =========================================================================
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [Permissions] ON;
INSERT INTO [Permissions] ([Id], [Action], [Code], [Description], [Resource], [Scope]) VALUES
    (180, N'Approve', N'OvertimeRequest.Approve.Hr',
     N'HR duyệt cuối request OT khi PM chọn ApproverRoute = Hr.',
     N'OvertimeRequest', N'All');
SET IDENTITY_INSERT [Permissions] OFF;

INSERT INTO [RolePermissions] ([PermissionId], [RoleId]) VALUES (180, 2);");

            // =========================================================================
            // 4. Cấp lại 112 (Create.Self) cho Employee (RoleId=7).
            //    Idempotent — chỉ insert nếu chưa có.
            // =========================================================================
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [PermissionId] = 112 AND [RoleId] = 7)
    INSERT INTO [RolePermissions] ([PermissionId], [RoleId]) VALUES (112, 7);");

            // =========================================================================
            // 5. Legacy data — bulk reject Pending records không có ProjectId (Q8=(a)).
            //    Chỉ đụng record theo luồng v1 chưa xử — Approved/Rejected/Cancelled giữ nguyên.
            // =========================================================================
            migrationBuilder.Sql(@"
UPDATE [OvertimeRequests]
   SET [Status]     = 3,
       [ReviewNote] = CASE
                          WHEN [ReviewNote] IS NULL OR LEN([ReviewNote]) = 0
                              THEN N'Chuyển sang luồng OT mới (2-tầng PM→HR/DM). Vui lòng đăng ký lại.'
                          ELSE [ReviewNote] + N' | Chuyển sang luồng OT mới (2-tầng PM→HR/DM). Vui lòng đăng ký lại.'
                      END,
       [ReviewedAt] = SYSUTCDATETIME()
 WHERE [Status]    = 1
   AND [ProjectId] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Legacy data KHÔNG rollback (record đã bị reject không thể phân biệt so với reject thật).

            migrationBuilder.Sql("DELETE FROM [RolePermissions] WHERE [PermissionId] = 180;");
            migrationBuilder.Sql("DELETE FROM [Permissions] WHERE [Id] = 180;");

            // Không rollback grant (112, 7) vì có thể đã tồn tại trước migration.

            // Restore HR (RoleId=2) trên permission 113 để trở lại trạng thái v1.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [PermissionId] = 113 AND [RoleId] = 2)
    INSERT INTO [RolePermissions] ([PermissionId], [RoleId]) VALUES (113, 2);");

            migrationBuilder.Sql(@"
UPDATE [Permissions]
   SET [Code]        = N'OvertimeRequest.Approve.All',
       [Scope]       = N'All',
       [Description] = N'Approve or reject overtime requests.'
 WHERE [Id] = 113;");

            migrationBuilder.Sql(@"
UPDATE [Permissions]
   SET [Action]      = N'Create',
       [Code]        = N'OvertimeRequest.CreateBatch.Department',
       [Scope]       = N'Department',
       [Description] = N'PM tạo batch OT request cho NV cùng phòng ban.'
 WHERE [Id] = 178;");

            migrationBuilder.DropForeignKey(
                name: "FK_OvertimeRequests_Users_ApproverReviewedByUserId",
                table: "OvertimeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_OvertimeRequests_Users_PmReviewedByUserId",
                table: "OvertimeRequests");

            migrationBuilder.DropIndex(
                name: "IX_OvertimeRequests_ApproverReviewedByUserId",
                table: "OvertimeRequests");

            migrationBuilder.DropIndex(
                name: "IX_OvertimeRequests_PmReviewedByUserId",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(name: "ApproverReviewNote", table: "OvertimeRequests");
            migrationBuilder.DropColumn(name: "ApproverReviewedAt", table: "OvertimeRequests");
            migrationBuilder.DropColumn(name: "ApproverReviewedByUserId", table: "OvertimeRequests");
            migrationBuilder.DropColumn(name: "PmReviewNote", table: "OvertimeRequests");
            migrationBuilder.DropColumn(name: "PmReviewedAt", table: "OvertimeRequests");
            migrationBuilder.DropColumn(name: "PmReviewedByUserId", table: "OvertimeRequests");
        }
    }
}
