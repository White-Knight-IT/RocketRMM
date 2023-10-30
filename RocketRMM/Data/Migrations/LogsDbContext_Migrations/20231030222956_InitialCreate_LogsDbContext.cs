using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RocketRMM.Data.Migrations.LogsDbContext_Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_LogsDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "_logEntries",
                columns: table => new
                {
                    RowKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    API = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SentAsAlert = table.Column<bool>(type: "bit", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__logEntries", x => x.RowKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "_logEntries");
        }
    }
}
