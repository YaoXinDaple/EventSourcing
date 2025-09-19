using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventSourcingBankAccount.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, comment: "账户ID"),
                    AccountHolder = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, comment: "账户持有人姓名"),
                    Balance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, comment: "账户余额"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AggregateId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, comment: "聚合根ID"),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, comment: "事件类型"),
                    EventData = table.Column<string>(type: "TEXT", nullable: false, comment: "事件数据（JSON格式）"),
                    Version = table.Column<int>(type: "INTEGER", nullable: false, comment: "事件版本号"),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false, comment: "事件时间戳")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AggregateId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, comment: "聚合根ID"),
                    SnapshotType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, comment: "快照类型"),
                    SnapshotData = table.Column<string>(type: "TEXT", nullable: false, comment: "快照数据（JSON格式）"),
                    Version = table.Column<int>(type: "INTEGER", nullable: false, comment: "快照版本号"),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false, comment: "快照时间戳")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_AggregateId_Version",
                table: "Events",
                columns: new[] { "AggregateId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventType",
                table: "Events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Timestamp",
                table: "Events",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_AggregateId_Type_Timestamp",
                table: "Snapshots",
                columns: new[] { "AggregateId", "SnapshotType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_AggregateId_Type_Version",
                table: "Snapshots",
                columns: new[] { "AggregateId", "SnapshotType", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_AggregateId_Version",
                table: "Snapshots",
                columns: new[] { "AggregateId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_Timestamp",
                table: "Snapshots",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Snapshots");
        }
    }
}
