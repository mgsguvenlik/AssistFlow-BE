using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    public partial class MoveQnbServicesRequestWorkOrderTypeToQnbSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.schemas
                    WHERE name = 'qnb'
                )
                BEGIN
                    EXEC('CREATE SCHEMA qnb')
                END

                IF OBJECT_ID('dbo.QnbServicesRequestWorkOrderTypes', 'U') IS NOT NULL
                   AND OBJECT_ID('qnb.QnbServicesRequestWorkOrderTypes', 'U') IS NULL
                BEGIN
                    ALTER SCHEMA qnb
                    TRANSFER dbo.QnbServicesRequestWorkOrderTypes;
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('qnb.QnbServicesRequestWorkOrderTypes', 'U') IS NOT NULL
                   AND OBJECT_ID('dbo.QnbServicesRequestWorkOrderTypes', 'U') IS NULL
                BEGIN
                    ALTER SCHEMA dbo
                    TRANSFER qnb.QnbServicesRequestWorkOrderTypes;
                END
            ");
        }
    }
}