using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MPS.Infrastructure.Persistence;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260706103000_RemoveAttendanceSettingTimezone")]
    public partial class RemoveAttendanceSettingTimezone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.AttendanceSettings', 'TimezoneId') IS NOT NULL
                BEGIN
                    DECLARE @constraintName sysname;

                    SELECT @constraintName = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c
                        ON c.default_object_id = dc.object_id
                    INNER JOIN sys.tables t
                        ON t.object_id = c.object_id
                    WHERE t.name = N'AttendanceSettings'
                        AND c.name = N'TimezoneId';

                    IF @constraintName IS NOT NULL
                    BEGIN
                        DECLARE @sql nvarchar(max);
                        SET @sql = N'ALTER TABLE [AttendanceSettings] DROP CONSTRAINT ' + QUOTENAME(@constraintName);
                        EXEC sp_executesql @sql;
                    END

                    ALTER TABLE [AttendanceSettings] DROP COLUMN [TimezoneId];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.AttendanceSettings', 'Notes') IS NOT NULL
                BEGIN
                    ALTER TABLE [AttendanceSettings] DROP COLUMN [Notes];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.AttendanceSettings', 'TimezoneId') IS NULL
                BEGIN
                    ALTER TABLE [AttendanceSettings]
                    ADD [TimezoneId] nvarchar(80) NOT NULL
                        CONSTRAINT [DF_AttendanceSettings_TimezoneId] DEFAULT N'Asia/Ho_Chi_Minh';
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.AttendanceSettings', 'Notes') IS NULL
                BEGIN
                    ALTER TABLE [AttendanceSettings]
                    ADD [Notes] nvarchar(500) NULL;
                END
                """);
        }
    }
}
