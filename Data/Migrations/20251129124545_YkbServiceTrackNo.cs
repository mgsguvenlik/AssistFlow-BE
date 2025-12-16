using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class YkbServiceTrackNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OracleNo",
                schema: "ykb",
                table: "YkbServicesRequest",
                newName: "YkbServiceTrackNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "YkbServiceTrackNo",
                schema: "ykb",
                table: "YkbServicesRequest",
                newName: "OracleNo");
        }
    }
}
