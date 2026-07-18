using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskV7PauseCancelSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE [Projects] ADD [DeletedAt] datetime2 NULL;
ALTER TABLE [Projects] ADD [StatusBeforePause] nvarchar(20) NULL;

ALTER TABLE [ProjectTasks] ADD [AssignedPmUserId] int NULL;
ALTER TABLE [ProjectTasks] ADD [PausedByCascadeFromId] int NULL;
ALTER TABLE [ProjectTasks] ADD [StatusBeforePause] nvarchar(20) NULL;
ALTER TABLE [ProjectTasks] ADD [DeletedAt] datetime2 NULL;

CREATE INDEX [IX_Projects_DeletedAt] ON [Projects] ([DeletedAt]);

CREATE INDEX [IX_ProjectTasks_AssignedPmUserId] ON [ProjectTasks] ([AssignedPmUserId]);
CREATE INDEX [IX_ProjectTasks_DeletedAt] ON [ProjectTasks] ([DeletedAt]);

ALTER TABLE [ProjectTasks]
    ADD CONSTRAINT [FK_ProjectTasks_Users_AssignedPmUserId]
    FOREIGN KEY ([AssignedPmUserId]) REFERENCES [Users] ([Id])
    ON DELETE NO ACTION;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE [ProjectTasks] DROP CONSTRAINT [FK_ProjectTasks_Users_AssignedPmUserId];

DROP INDEX [IX_ProjectTasks_DeletedAt] ON [ProjectTasks];
DROP INDEX [IX_ProjectTasks_AssignedPmUserId] ON [ProjectTasks];
DROP INDEX [IX_Projects_DeletedAt] ON [Projects];

ALTER TABLE [ProjectTasks] DROP COLUMN [DeletedAt];
ALTER TABLE [ProjectTasks] DROP COLUMN [StatusBeforePause];
ALTER TABLE [ProjectTasks] DROP COLUMN [PausedByCascadeFromId];
ALTER TABLE [ProjectTasks] DROP COLUMN [AssignedPmUserId];

ALTER TABLE [Projects] DROP COLUMN [StatusBeforePause];
ALTER TABLE [Projects] DROP COLUMN [DeletedAt];
");
        }
    }
}
