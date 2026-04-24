using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductInformationManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProductModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryDescriptionRules");

            migrationBuilder.DropColumn(
                name: "IsGlobal",
                table: "DescriptionTypes");

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "DescriptionTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMandatory",
                table: "DescriptionTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Categories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DescriptionTypes_CategoryId",
                table: "DescriptionTypes",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_DescriptionTypes_Categories_CategoryId",
                table: "DescriptionTypes",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DescriptionTypes_Categories_CategoryId",
                table: "DescriptionTypes");

            migrationBuilder.DropIndex(
                name: "IX_DescriptionTypes_CategoryId",
                table: "DescriptionTypes");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "DescriptionTypes");

            migrationBuilder.DropColumn(
                name: "IsMandatory",
                table: "DescriptionTypes");

            migrationBuilder.AddColumn<bool>(
                name: "IsGlobal",
                table: "DescriptionTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Categories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.CreateTable(
                name: "CategoryDescriptionRules",
                columns: table => new
                {
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DescriptionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryDescriptionRules", x => new { x.CategoryId, x.DescriptionTypeId });
                    table.ForeignKey(
                        name: "FK_CategoryDescriptionRules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryDescriptionRules_DescriptionTypes_DescriptionTypeId",
                        column: x => x.DescriptionTypeId,
                        principalTable: "DescriptionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryDescriptionRules_DescriptionTypeId",
                table: "CategoryDescriptionRules",
                column: "DescriptionTypeId");
        }
    }
}
