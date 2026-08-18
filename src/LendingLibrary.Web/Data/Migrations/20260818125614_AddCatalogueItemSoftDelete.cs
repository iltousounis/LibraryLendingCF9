using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LendingLibrary.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogueItemSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogueItems_Isbn",
                table: "CatalogueItems");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "CatalogueItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueItems_Isbn",
                table: "CatalogueItems",
                column: "Isbn",
                unique: true,
                filter: "\"Isbn\" IS NOT NULL AND \"DeletedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogueItems_Isbn",
                table: "CatalogueItems");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "CatalogueItems");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueItems_Isbn",
                table: "CatalogueItems",
                column: "Isbn",
                unique: true,
                filter: "\"Isbn\" IS NOT NULL");
        }
    }
}
