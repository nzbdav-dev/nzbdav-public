using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NzbWebDAV.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadDirIdToHistoryItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DownloadDirId",
                table: "HistoryItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoryItems_Category_DownloadDirId",
                table: "HistoryItems",
                columns: new[] { "Category", "DownloadDirId" });

            // Populate DownloadDirId for completed history items based on
            // DavItems hierarchy under /content/{Category}/{JobName}
            migrationBuilder.Sql(
                """
                UPDATE HistoryItems
                SET DownloadDirId = (
                  SELECT child.Id
                  FROM DavItems AS child
                  JOIN DavItems AS parent ON child.ParentId = parent.Id
                  WHERE parent.ParentId = '00000000-0000-0000-0000-000000000002' -- DavItem.ContentFolder.Id
                    AND parent.Name = HistoryItems.Category
                    AND child.Name = HistoryItems.JobName
                    AND child.Type = 1 -- ItemType.Directory
                )
                WHERE DownloadStatus = 1; -- HistoryItem.DownloadStatusOption.Completed
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HistoryItems_Category_DownloadDirId",
                table: "HistoryItems");

            migrationBuilder.DropColumn(
                name: "DownloadDirId",
                table: "HistoryItems");
        }
    }
}
