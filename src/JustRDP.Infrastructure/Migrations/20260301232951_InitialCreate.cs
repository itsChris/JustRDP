using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JustRDP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "TreeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EntryType = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    ConnectionType = table.Column<int>(type: "INTEGER", nullable: true, defaultValue: 0),
                    HostName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Port = table.Column<int>(type: "INTEGER", nullable: true, defaultValue: 3389),
                    ConnectionEntry_CredentialUsername = table.Column<string>(type: "TEXT", nullable: true),
                    ConnectionEntry_CredentialDomain = table.Column<string>(type: "TEXT", nullable: true),
                    ConnectionEntry_CredentialPasswordEncrypted = table.Column<byte[]>(type: "BLOB", nullable: true),
                    DesktopWidth = table.Column<int>(type: "INTEGER", nullable: true),
                    DesktopHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    ColorDepth = table.Column<int>(type: "INTEGER", nullable: true, defaultValue: 32),
                    ResizeBehavior = table.Column<int>(type: "INTEGER", nullable: true),
                    AutoReconnect = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: true),
                    NetworkLevelAuthentication = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: true),
                    Compression = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: true),
                    RedirectClipboard = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: true),
                    RedirectPrinters = table.Column<bool>(type: "INTEGER", nullable: true),
                    RedirectDrives = table.Column<bool>(type: "INTEGER", nullable: true),
                    RedirectSmartCards = table.Column<bool>(type: "INTEGER", nullable: true),
                    RedirectPorts = table.Column<bool>(type: "INTEGER", nullable: true),
                    AudioRedirectionMode = table.Column<int>(type: "INTEGER", nullable: true),
                    GatewayHostName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    GatewayUsageMethod = table.Column<int>(type: "INTEGER", nullable: true),
                    GatewayUsername = table.Column<string>(type: "TEXT", nullable: true),
                    GatewayDomain = table.Column<string>(type: "TEXT", nullable: true),
                    GatewayPasswordEncrypted = table.Column<byte[]>(type: "BLOB", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    SshPrivateKeyPath = table.Column<string>(type: "TEXT", nullable: true),
                    SshPrivateKeyPassphraseEncrypted = table.Column<byte[]>(type: "BLOB", nullable: true),
                    SshTerminalFontFamily = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    SshTerminalFontSize = table.Column<double>(type: "REAL", nullable: true),
                    IsExpanded = table.Column<bool>(type: "INTEGER", nullable: true),
                    CredentialUsername = table.Column<string>(type: "TEXT", nullable: true),
                    CredentialDomain = table.Column<string>(type: "TEXT", nullable: true),
                    CredentialPasswordEncrypted = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreeEntries_TreeEntries_ParentId",
                        column: x => x.ParentId,
                        principalTable: "TreeEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreeEntries_ParentId",
                table: "TreeEntries",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_TreeEntries_ParentId_SortOrder",
                table: "TreeEntries",
                columns: new[] { "ParentId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "TreeEntries");
        }
    }
}
