using System.Reflection;
using Business.Interfaces;
using Business.Services;
using Business.Services.Ekb;
using Business.Services.Qnb;
using Business.Services.Ykb;
using Business.UnitOfWork;
using Data.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Model.Abstractions;
using Model.Concrete;
using Model.Concrete.Ekb;
using Model.Concrete.Qnb;
using Model.Concrete.WorkFlows;
using Model.Concrete.Ykb;
using Core.Enums;

internal static class ServiceNotificationMailTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    public static async Task Main()
    {
        var services = new[] { typeof(WorkFlowService), typeof(YkbWorkFlowService),
            typeof(EkbWorkFlowService), typeof(QnbWorkFlowService) };
        foreach (var service in services)
        {
            foreach (var methodName in new[] { "BuildToTechnician", "BuildToWarehouse" })
            {
                var method = service.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!;
                (string subject, string html) Build(string? subscriberName) =>
                    ((string, string))method.Invoke(null, ["TEST-123", "SR", "TS", "Yetkili & Kişi", subscriberName])!;

                var result = Build("  İstanbul Merkez\r\nŞubesi  ");
                Check(result.subject.EndsWith(" - İstanbul Merkez Şubesi"), $"{service.Name}/{methodName}: subscriber name normalized");
                Check(result.subject.Contains("[TEST-123]") && result.subject.Contains("SR → TS"), "request and transition retained");
                Check(!result.subject.Contains("Yetkili"), "subscriber name rather than contact name in subject");
                Check(result.html.Contains("Yetkili &amp; Kişi"), "existing contact in mail body retained");
                Check(Build(null).subject == Build(" \r\n\t ").subject, "missing name keeps original subject without suffix");
                Check(Build("A & B <Şube>").subject.EndsWith(" - A & B <Şube>"), "subject is plain text, not HTML encoded");
            }
        }

        // Only an in-memory database and MailPushService are used. No SMTP dispatcher or app configuration is loaded.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new MailTestContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var customers = new[] { "Bireysel Abone", "YKB Abone", "EKB Abone", "QNB Abone" }
            .Select(name => new Customer { SubscriberCompany = name, ContactName1 = "Yetkili Kişi" }).ToArray();
        // Deliberately reuse the number in all four tables to catch accidental cross-tenant lookup.
        db.AddRange(new ServicesRequest { RequestNo = "TEST-SLA", Customer = customers[0] },
            new YkbServicesRequest { RequestNo = "TEST-SLA", Customer = customers[1] },
            new EkbServicesRequest { RequestNo = "TEST-SLA", Customer = customers[2] },
            new QnbServicesRequest { RequestNo = "TEST-SLA", Customer = customers[3] });
        await db.SaveChangesAsync();
        var uow = new UnitOfWork(new Repository(db));
        var mailPush = new MailPushService(uow, NullLogger<MailPushService>.Instance);
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var dispatcher = new SlaNotificationDispatcher(provider, NullLogger<SlaNotificationDispatcher>.Instance);
        var create = typeof(SlaNotificationDispatcher).GetMethod("CreateSlaNotificationMailAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var types = new[] { WorkFlowCustomerType.Individual, WorkFlowCustomerType.YKB, WorkFlowCustomerType.EKB, WorkFlowCustomerType.QNB };
        const string original = "SLA Uyarısı: TEST-SLA - Normal Öncelik";
        for (var i = 0; i < types.Length; i++)
        {
            var setting = new WorkFlowSlaSetting { CustomerType = types[i], Priority = WorkFlowPriority.Normal,
                NotificationEmails = "test@example.invalid", SlaDurationHours = 240 };
            async Task Queue() => await (Task)create.Invoke(dispatcher,
                [uow, mailPush, "TEST-SLA", setting, DateTimeOffset.UtcNow.AddHours(-200), DateTimeOffset.UtcNow, CancellationToken.None])!;
            async Task Clear()
            {
                await db.Set<MailOutbox>().ExecuteDeleteAsync();
                foreach (var entry in db.ChangeTracker.Entries<MailOutbox>().ToArray()) entry.State = EntityState.Detached;
            }
            await Queue();
            var queued = await db.Set<MailOutbox>().AsNoTracking().SingleAsync();
            Check(queued.Subject == original + " - " + customers[i].SubscriberCompany, $"{types[i]}: correct subscriber selected");
            Check(queued.RequestNo == "TEST-SLA" && queued.ToRecipients == setting.NotificationEmails && queued.Status == MailOutboxStatus.Pending,
                "outbox request, recipients and status unchanged");
            await Queue();
            Check(await db.Set<MailOutbox>().CountAsync() == 1, "same warning not queued twice");
            customers[i].SubscriberCompany = "Yeni Abone İsmi";
            await db.SaveChangesAsync();
            await Queue();
            Check(await db.Set<MailOutbox>().CountAsync() == 1, "renaming customer does not send another SLA warning");
            await Clear();
            db.Add(new MailOutbox { RequestNo = "TEST-SLA", Subject = original });
            await db.SaveChangesAsync();
            await Queue();
            Check(await db.Set<MailOutbox>().CountAsync() == 1, "old warning without customer name prevents duplicate after upgrade");
            await Clear();
            customers[i].SubscriberCompany = " \r\n ";
            await db.SaveChangesAsync();
            await Queue();
            Check((await db.Set<MailOutbox>().AsNoTracking().SingleAsync()).Subject == original, "blank subscriber preserves original SLA subject");
            await Clear();
        }
        Console.WriteLine($"PASS: {checks} notification checks across all four tenants (in-memory SQLite; no SMTP or live database).");
    }
}

internal sealed class MailTestContext(DbContextOptions<AppDataContext> options) : AppDataContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        var keep = new[] { typeof(Customer), typeof(ServicesRequest), typeof(YkbServicesRequest),
            typeof(EkbServicesRequest), typeof(QnbServicesRequest), typeof(MailOutbox) };
        foreach (var property in typeof(AppDataContext).GetProperties())
            if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            {
                var type = property.PropertyType.GetGenericArguments()[0];
                if (!keep.Contains(type)) model.Ignore(type);
            }
        foreach (var type in keep)
        {
            var entity = model.Entity(type);
            foreach (var property in type.GetProperties())
            {
                var target = property.PropertyType;
                if (target.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(target))
                    target = target.GetGenericArguments()[0];
                if (typeof(BaseEntity).IsAssignableFrom(target) && !keep.Contains(target)) entity.Ignore(property.Name);
            }
        }
    }
}
