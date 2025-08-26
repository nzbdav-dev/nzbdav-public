using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NzbWebDAV.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddIdPrefixToDavItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdPrefix",
                table: "DavItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE DavItems
                SET IdPrefix = lower(substr(Id, 1, 5));
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_DavItems_IdPrefix_Type",
                table: "DavItems",
                columns: new[] { "IdPrefix", "Type" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DavItems_IdPrefix_Type",
                table: "DavItems");

            migrationBuilder.DropColumn(
                name: "IdPrefix",
                table: "DavItems");
        }
    }
}
