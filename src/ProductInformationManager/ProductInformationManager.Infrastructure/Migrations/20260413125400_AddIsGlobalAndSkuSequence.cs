using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductInformationManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsGlobalAndSkuSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "product_sku_seq");

            migrationBuilder.AlterColumn<string>(
                name: "Sku",
                table: "Products",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<bool>(
                name: "IsGlobal",
                table: "DescriptionTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGlobal",
                table: "DescriptionTypes");

            migrationBuilder.DropSequence(
                name: "product_sku_seq");

            migrationBuilder.AlterColumn<string>(
                name: "Sku",
                table: "Products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);
        }
    }
}
