using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amrod.OrderManagement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderStatusIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastStatusIdempotencyKey",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusUpdatedAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_LastStatusIdempotencyKey",
                table: "Orders",
                column: "LastStatusIdempotencyKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_LastStatusIdempotencyKey",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "LastStatusIdempotencyKey",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StatusUpdatedAt",
                table: "Orders");
        }
    }
}
