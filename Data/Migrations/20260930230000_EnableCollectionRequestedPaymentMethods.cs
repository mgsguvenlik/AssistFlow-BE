using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations;

[DbContext(typeof(AppDataContext))]
[Migration("20260930230000_EnableCollectionRequestedPaymentMethods")]
public sealed class EnableCollectionRequestedPaymentMethods : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE collection.PaymentMethod SET IsActive = 1
        WHERE Code IN (N'GTS', N'BANK_TRANSFER') AND IsActive = 0;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException(
        "Ödeme yöntemi kullanım kararı otomatik geri alınamaz; kontrollü veri incelemesi gerekir.");
}
