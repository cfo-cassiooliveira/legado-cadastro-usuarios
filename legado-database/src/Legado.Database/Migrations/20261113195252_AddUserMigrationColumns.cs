using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Legado.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMigrationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "KeycloakUserId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MigratedAt",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "KeycloakUserIdIndex",
                table: "AspNetUsers",
                column: "KeycloakUserId",
                unique: true,
                filter: "[KeycloakUserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "KeycloakUserIdIndex",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "KeycloakUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MigratedAt",
                table: "AspNetUsers");
        }
    }
}
