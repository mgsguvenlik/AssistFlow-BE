using Core.Utilities.Security;
using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;

namespace Data.Seeding.Seeds;

public sealed class PasswordPolicySeed : IDataSeed
{
    public string Key => "SeedPasswordPolicyV1";
    public int Order => 11;
    public Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct) => Task.FromResult(true);

    public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
    {
        if (!await db.Set<Configuration>().AnyAsync(x => x.Name == PasswordPolicyRules.ValidityDaysParameter, ct))
            db.Set<Configuration>().Add(new Configuration
            {
                Name = PasswordPolicyRules.ValidityDaysParameter, Value = "180",
                Description = "Şifre geçerlilik süresi (gün). Pozitif tam sayı; varsayılan 180 gün."
            });
        // Initialize only legacy rows. Repeated startup must never extend existing passwords or clear a request.
        var baseline = DateTimeOffset.UtcNow;
        await db.Set<User>().Where(x => x.PasswordChangedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(u => u.PasswordChangedAt, baseline), ct);
        await db.SaveChangesAsync(ct);
    }
}
