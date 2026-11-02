using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YTStudio.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Agenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "publications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChannelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ScriptId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ScheduledAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    ReminderMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    RemindedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_publications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_publications_channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_publications_scripts_ScriptId",
                        column: x => x.ScriptId,
                        principalTable: "scripts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_publications_ChannelId",
                table: "publications",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_publications_ScheduledAt",
                table: "publications",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_publications_ScriptId",
                table: "publications",
                column: "ScriptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "publications");
        }
    }
}
