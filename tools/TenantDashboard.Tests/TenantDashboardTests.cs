using System.Reflection;
using Business.Interfaces;
using Business.Services;
using Business.UnitOfWork;
using Core.Enums;
using Data.Concrete;
using Data.Seeding.Infrastructure;
using Data.Seeding.Seeds;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Model.Concrete;
using Model.Concrete.Ekb;
using Model.Concrete.Qnb;
using Model.Concrete.WorkFlows;
using Model.Concrete.Ykb;
using Model.Dtos.Auth;
using Model.Dtos.Dashboard;
using WebAPI.Authorization;
using WebAPI.Controllers;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("FAIL: " + message);
        checks++;
    }

    public static async Task Main()
    {
        var database = "MgsTenantDashboardTests_" + Guid.NewGuid().ToString("N");
        await using var db = new TestContext(new DbContextOptionsBuilder<TestContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={database};Integrated Security=true;TrustServerCertificate=true").Options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            var admin = new Role { Name = "SuperAdmin", Code = "aDmIn" };
            var operatorRole = new Role { Name = "YKB Kullanıcısı", Code = "YKB_USER" };
            var technician = new Role { Name = "Teknisyen", Code = "TECH" };
            var menus = new[] { DashboardMenuKeys.Service, DashboardMenuKeys.Ykb, DashboardMenuKeys.Ekb, DashboardMenuKeys.Qnb, "YkbTechnicianDashboard", "Dashboard" }
                .Select(name => new Menu { Name = name }).ToArray();
            db.AddRange(admin, operatorRole, technician);
            db.AddRange(menus);
            db.AddRange(new User { Id = 1, Code = "ADMIN", Name = "Admin", IsActive = true },
                new User { Id = 2, Code = "USER", Name = "User", IsActive = true },
                new User { Id = 3, Code = "TECH", Name = "Tech", IsActive = true });
            await db.SaveChangesAsync();
            db.AddRange(new UserRole { UserId = 1, RoleId = admin.Id }, new UserRole { UserId = 2, RoleId = operatorRole.Id }, new UserRole { UserId = 3, RoleId = technician.Id });
            void Grant(Role role, string key, bool view = true) => db.Add(new MenuRole { RoleId = role.Id, MenuId = menus.Single(m => m.Name == key).Id, HasView = view });
            Grant(admin, DashboardMenuKeys.Service);
            Grant(admin, DashboardMenuKeys.Ykb);
            Grant(operatorRole, DashboardMenuKeys.Ykb);
            Grant(technician, "YkbTechnicianDashboard");
            foreach (var (workflow, step) in new[] {
                (typeof(WorkFlow), typeof(WorkFlowStep)), (typeof(YkbWorkFlow), typeof(YkbWorkFlowStep)),
                (typeof(EkbWorkFlow), typeof(EkbWorkFlowStep)), (typeof(QnbWorkFlow), typeof(QnbWorkFlowStep)) })
            {
                object MakeStep(string code)
                {
                    var entity = Activator.CreateInstance(step)!;
                    step.GetProperty("Code")!.SetValue(entity, code);
                    step.GetProperty("Name")!.SetValue(entity, code);
                    db.Add(entity);
                    return entity;
                }
                var ts = MakeStep("TS");
                var apr = MakeStep("APR");
                for (var i = 0; i < 8; i++)
                {
                    var entity = Activator.CreateInstance(workflow)!;
                    workflow.GetProperty("RequestNo")!.SetValue(entity, workflow.Name + i);
                    workflow.GetProperty("CurrentStep")!.SetValue(entity, i == 7 ? null : i == 5 ? apr : ts);
                    workflow.GetProperty("WorkFlowStatus")!.SetValue(entity, i == 1 ? WorkFlowStatus.Complated : i == 2 ? WorkFlowStatus.Cancelled : WorkFlowStatus.Pending);
                    workflow.GetProperty("IsDeleted")!.SetValue(entity, i == 3);
                    var date = i == 4 ? "2026-09-30T20:59:59Z" : i == 5 ? "2026-10-01T20:59:59Z" : i == 6 ? "2026-10-01T21:00:00Z" : "2026-09-30T21:00:00Z";
                    workflow.GetProperty("CreatedDate")!.SetValue(entity, DateTimeOffset.Parse(date));
                    db.Add(entity);
                }
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var me = new CurrentUserStub { UserId = 1 };
            var service = new WorkFlowDashboardService(new UnitOfWork(new Repository(db)), NullLogger<WorkFlowDashboardService>.Instance, me);
            var day = new DateOnly(2026, 10, 1);
            var total = await service.GetOperationsAsync(from: day, to: day);
            Check(total.IsSuccess && total.Data!.Tenants.Select(t => t.TenantCode).SequenceEqual(new[] { "individual", "ykb" }), "Aggregate contains only granted tenants");
            Check(total.Data!.Total.TotalWorkFlows == 10 && total.Data.Total.PendingWorkFlows == 6 && total.Data.Total.CompletedWorkFlows == 2 && total.Data.Total.CancelledWorkFlows == 2, "Aggregate uses consistent status definitions and excludes deleted/out-of-range rows");
            Check(total.Data.Total.InTechnicalService == 2 && total.Data.Total.InFinalApproval == 2, "Stages count only pending requests, including the end of the Istanbul day");
            foreach (var code in new[] { "individual", "ykb" })
            {
                var result = await service.GetOperationsAsync(code, day, day);
                Check(result.IsSuccess && result.Data!.Tenants.Count == 1 && result.Data.Total.TotalWorkFlows == 5, code + " matches the aggregate/date boundaries");
                Check((await service.GetOperationsAsync(code)).Data!.Total.TotalWorkFlows == 7, code + " all-time does not retain the date filter");
            }
            Check((await service.GetOperationsAsync("qnb")).StatusCode == Core.Enums.StatusCode.Unauthorized, "Admin role alone does not grant QNB access");
            Check((await service.GetOperationsAsync("unknown")).StatusCode == Core.Enums.StatusCode.BadRequest, "Unknown tenant rejected");
            Check((await service.GetOperationsAsync(from: day.AddDays(1), to: day)).StatusCode == Core.Enums.StatusCode.BadRequest, "Invalid date range rejected");
            var empty = await service.GetOperationsAsync(from: new DateOnly(2027, 1, 1), to: new DateOnly(2027, 1, 2));
            Check(empty.IsSuccess && empty.Data!.Tenants.Count == 2 && empty.Data.Total.TotalWorkFlows == 0, "An empty period retains authorized tabs with zero counts");
            me.UserId = 2;
            Check((await service.GetOperationsAsync()).StatusCode == Core.Enums.StatusCode.Unauthorized, "Non-admin cannot call aggregate even with operation permission");
            Check((await service.GetOperationsAsync("ykb", day, day)).IsSuccess, "Non-admin can access its granted tenant");
            Check((await service.GetOperationsAsync("ekb")).StatusCode == Core.Enums.StatusCode.Unauthorized, "One tenant permission cannot access another tenant");
            me.UserId = 3;
            Check((await service.GetOperationsAsync("ykb")).StatusCode == Core.Enums.StatusCode.Unauthorized, "Technician permission never grants general operation data");
            me.UserId = 1;
            var ykbMenuId = menus.Single(n => n.Name == DashboardMenuKeys.Ykb).Id;
            var qnbMenuId = menus.Single(n => n.Name == DashboardMenuKeys.Qnb).Id;
            var grant = await db.Set<MenuRole>().SingleAsync(m => m.RoleId == admin.Id && m.MenuId == ykbMenuId);
            grant.HasView = false;
            await db.SaveChangesAsync();
            Check((await service.GetOperationsAsync()).Data!.Tenants.Single().TenantCode == "individual", "Revoked grant is removed from aggregate immediately");
            var seed = new TenantDashboardSeed();
            Grant(admin, "Dashboard");
            Grant(operatorRole, "Dashboard");
            await db.SaveChangesAsync();
            await seed.RunAsync(db, null!, default);
            Check(!await db.Set<MenuRole>().AnyAsync(m => m.RoleId == admin.Id && m.MenuId == grant.MenuId && m.HasView), "Seed preserves an explicit denied grant");
            Check(await db.Set<MenuRole>().AnyAsync(m => m.RoleId == admin.Id && m.MenuId == qnbMenuId && m.HasView), "Seed gives legacy ADMIN QNB permission");
            Check(!await db.Set<MenuRole>().AnyAsync(m => m.RoleId == operatorRole.Id && m.MenuId == qnbMenuId && m.HasView), "Seed does not grant new QNB data to legacy non-admin roles");
            foreach (var code in new[] { "ekb", "qnb" })
                Check((await service.GetOperationsAsync(code, day, day)).Data!.Total.TotalWorkFlows == 5, code + " real SQL query matches shared semantics");
            var ekbId = menus.Single(n => n.Name == DashboardMenuKeys.Ekb).Id;
            var revoke = await db.Set<MenuRole>().SingleAsync(m => m.RoleId == admin.Id && m.MenuId == ekbId);
            db.Remove(revoke);
            await db.SaveChangesAsync();
            var grantsBefore = await db.Set<MenuRole>().CountAsync();
            await seed.RunAsync(db, null!, default);
            Check(await db.Set<MenuRole>().CountAsync() == grantsBefore && !await db.Set<MenuRole>().AnyAsync(m => m.RoleId == admin.Id && m.MenuId == ekbId), "Repeated seed never restores removed grants");
            Check(await db.Set<SeedHistory>().CountAsync(h => h.Key == seed.Key) == 1, "Seed history is idempotent");
            var method = typeof(DashboardController).GetMethod(nameof(DashboardController.GetOperations))!;
            Check(method.GetCustomAttribute<AdminRoleAuthorizeAttribute>() != null, "Aggregate HTTP endpoint requires database ADMIN role");
            foreach (var (name, key) in new[] { ("GetYkbKpi", DashboardMenuKeys.Ykb), ("GetEkbKpi", DashboardMenuKeys.Ekb) })
                Check(((string[])typeof(DashboardController).GetMethod(name)!.GetCustomAttribute<MenuAuthorizeAttribute>()!.Arguments![0]).SequenceEqual(new[] { key }), "Legacy KPI endpoint uses explicit tenant permission");
            foreach (var name in new[] { "GetKpi", "GetFullDashboard" })
                Check(((string[])typeof(DashboardController).GetMethod(name)!.GetCustomAttribute<MenuAuthorizeAttribute>()!.Arguments![0]).SequenceEqual(new[] { DashboardMenuKeys.Service }), "Home navigation permission cannot bypass individual data permission");
            Console.WriteLine($"PASS {checks} tenant dashboard checks (isolated SQL Server; no live data)");
        }
        finally
        {
            if (!database.StartsWith("MgsTenantDashboardTests_", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe cleanup target");
            await db.Database.EnsureDeletedAsync();
        }
    }
}

internal sealed class CurrentUserStub : ICurrentUser
{
    public long UserId { get; set; }
    public ValueTask<CurrentUserDto?> GetAsync(CancellationToken ct = default) => ValueTask.FromResult<CurrentUserDto?>(new() { Id = UserId });
    public long Id => UserId;
    public string? Email => null;
    public string? Name => null;
}

internal sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        var keep = new HashSet<Type> { typeof(User), typeof(Role), typeof(UserRole), typeof(Menu), typeof(MenuRole), typeof(SeedHistory),
            typeof(WorkFlow), typeof(WorkFlowStep), typeof(YkbWorkFlow), typeof(YkbWorkFlowStep), typeof(EkbWorkFlow), typeof(EkbWorkFlowStep), typeof(QnbWorkFlow), typeof(QnbWorkFlowStep) };
        foreach (var entity in typeof(User).Assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.Namespace?.StartsWith("Model.Concrete", StringComparison.Ordinal) == true))
            if (!keep.Contains(entity)) model.Ignore(entity);
        foreach (var entity in keep)
        {
            var builder = model.Entity(entity);
            foreach (var property in entity.GetProperties())
            {
                var target = property.PropertyType;
                if (target.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(target)) target = target.GetGenericArguments()[0];
                if (target.Namespace?.StartsWith("Model.Concrete", StringComparison.Ordinal) == true && !keep.Contains(target)) builder.Ignore(property.Name);
            }
        }
        model.Entity<User>().Property(u => u.Id).ValueGeneratedNever();
        model.Entity<WorkFlowStep>().Ignore(s => s.OutgoingTransitions).Ignore(s => s.IncomingTransitions);
    }
}
