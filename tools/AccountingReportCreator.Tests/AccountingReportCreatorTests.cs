using System.Reflection;
using Business.Services.Ykb;
using Business.Services.Ekb;
using Business.UnitOfWork;
using Core.Enums;
using Data.Concrete;
using Data.Concrete.EfCore.Context;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Model.Concrete;
using Model.Concrete.Ykb;
using Model.Concrete.Ekb;
using Model.Concrete.WorkFlows;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbAccounting;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbAccounting;
using WebAPI.Authorization;
using WebAPI.Controllers;

internal static class AccountingReportCreatorTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        checks++;
    }

    public static async Task Main()
    {
        // Only this disposable LocalDB database is ever created/deleted; no app settings are read.
        var database = "MgsAccountingCreatorTests_" + Guid.NewGuid().ToString("N");
        var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Integrated Security=true;TrustServerCertificate=true";
        await using var db = new AccountingTestContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection).Options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            db.AddRange(new User { Id = 1, Code = "START-A", Name = "Başlatan A", IsActive = true },
                new User { Id = 2, Code = "SERVICE-B", Name = "Servis ve Muhasebe Kullanıcısı", IsActive = true },
                new User { Id = 3, Code = "OLD", Name = "Eski Kullanıcı", IsActive = false, IsDeleted = true },
                new User { Id = 4, Code = "EKB-ONLY", Name = "EKB Kullanıcısı", IsActive = true });
            await db.SaveChangesAsync();
            var type = new ServiceType { Name = "Bakım" };
            db.Add(type);
            await db.SaveChangesAsync();
            AddYkb(db, "YKB-A1", 1, type.Id, processed: true);
            AddYkb(db, "YKB-A2", 1, type.Id);
            AddYkb(db, "YKB-HISTORIC", 3, type.Id);
            AddYkb(db, "YKB-UNKNOWN", 99, type.Id);
            AddYkb(db, "YKB-PENDING", 2, type.Id, status: WorkFlowStatus.Pending);
            AddYkb(db, "YKB-DELETED", 2, type.Id, deleted: true);
            AddYkb(db, "YKB-NO-APPROVAL", 2, type.Id, approved: false);
            AddEkb(db, "EKB-A1", 4, type.Id, processed: true);
            AddEkb(db, "EKB-A2", 4, type.Id);
            AddEkb(db, "EKB-HISTORIC", 3, type.Id);
            AddEkb(db, "EKB-PENDING", 2, type.Id, status: WorkFlowStatus.Pending);
            AddEkb(db, "EKB-DELETED", 2, type.Id, deleted: true);
            AddEkb(db, "EKB-NO-APPROVAL", 2, type.Id, approved: false);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var uow = new UnitOfWork(new Repository(db));
            var ykb = new YkbWorkFlowService(uow, new TypeAdapterConfig(), null!, null!,
                NullLogger<YkbWorkFlowService>.Instance, null!, null!, db, null!, null!, null!, null!);
            var ekb = new EkbWorkFlowService(uow, new TypeAdapterConfig(), null!, null!,
                NullLogger<EkbWorkFlowService>.Instance, null!, null!, db, null!, null!, null!, null!);

            var ykbAll = await ykb.GetAccountingServiceReportAsync(new());
            Check(ykbAll.IsSuccess && ykbAll.Data!.TotalCount == 4, "YKB default report eligibility preserved");
            var ekbAll = await ekb.GetAccountingServiceReportAsync(new());
            Check(ekbAll.IsSuccess && ekbAll.Data!.TotalCount == 3, "EKB default report eligibility preserved");
            var ykbPage = await ykb.GetAccountingServiceReportAsync(new() { CreatedUserId = 1, PageSize = 1 });
            Check(ykbPage.IsSuccess && ykbPage.Data!.TotalCount == 2 && ykbPage.Data.Items.Count == 1,
                "YKB filters workflow CreatedUser before total and paging");
            var ekbPage = await ekb.GetAccountingServiceReportAsync(new() { CreatedUserId = 4, PageSize = 1 });
            Check(ekbPage.IsSuccess && ekbPage.Data!.TotalCount == 2 && ekbPage.Data.Items.Count == 1,
                "EKB filters workflow CreatedUser before total and paging");
            var ykbWrong = await ykb.GetAccountingServiceReportAsync(new() { CreatedUserId = 2 });
            Check(ykbWrong.IsSuccess && ykbWrong.Data!.TotalCount == 0,
                "YKB ignores service-request creator, accounting processor and ineligible workflows");
            var ekbWrong = await ekb.GetAccountingServiceReportAsync(new() { CreatedUserId = 2 });
            Check(ekbWrong.IsSuccess && ekbWrong.Data!.TotalCount == 0,
                "EKB ignores service-request creator, accounting processor and ineligible workflows");
            var ykbCombined = await ykb.GetAccountingServiceReportAsync(new() { CreatedUserId = 1, IsProcessed = true, Search = "A1", ServiceTypeId = type.Id });
            Check(ykbCombined.IsSuccess && ykbCombined.Data!.Items.Single().RequestNo == "YKB-A1", "YKB combines creator with existing filters");
            var ekbCombined = await ekb.GetAccountingServiceReportAsync(new() { CreatedUserId = 4, IsProcessed = false, Search = "A2", ServiceTypeId = type.Id });
            Check(ekbCombined.IsSuccess && ekbCombined.Data!.Items.Single().RequestNo == "EKB-A2", "EKB combines creator with existing filters");
            Check((await ykb.GetAccountingServiceReportAsync(new() { CreatedUserId = 0 })).Data!.TotalCount == 4, "Cleared/zero YKB creator does not filter");
            Check((await ekb.GetAccountingServiceReportAsync(new() { CreatedUserId = -1 })).Data!.TotalCount == 3, "Negative EKB creator is normalized");
            var ykbCreators = await ykb.GetAccountingReportCreatorsAsync();
            Check(ykbCreators.IsSuccess && ykbCreators.Data!.Select(x => x.Id).Order().SequenceEqual(new long[] { 1, 3, 99 }),
                "YKB lookup returns distinct eligible workflow creators only");
            Check(ykbCreators.Data!.Single(x => x.Id == 3).Name == "Eski Kullanıcı", "Historic inactive/deleted creators remain selectable");
            Check(ykbCreators.Data!.Single(x => x.Id == 99).Name == "Kullanıcı #99", "Missing user has a safe identity fallback");
            var ekbCreators = await ekb.GetAccountingReportCreatorsAsync();
            Check(ekbCreators.IsSuccess && ekbCreators.Data!.Select(x => x.Id).Order().SequenceEqual(new long[] { 3, 4 }),
                "EKB lookup does not leak YKB-only creators");
            foreach (var (controller, menu) in new[] { (typeof(YkbWorkFlowsController), "YkbAccountingServiceReportList"),
                (typeof(EkbWorkFlowsController), "EkbAccountingServiceReportList") })
            {
                var method = controller.GetMethod("GetAccountingReportCreators")!;
                var permission = method.GetCustomAttribute<MenuAuthorizeAttribute>()!;
                Check(method.GetCustomAttribute<HttpGetAttribute>()!.Template == "accounting-service-report/creators" &&
                    ((string[])permission.Arguments![0]).SequenceEqual(new[] { menu }) &&
                    (MenuPermission)permission.Arguments[1] == MenuPermission.View, "Lookup requires the tenant's accounting report permission");
            }
            Console.WriteLine($"PASS {checks} accounting creator checks (isolated SQL Server, no live data)");
        }
        finally
        {
            if (!database.StartsWith("MgsAccountingCreatorTests_", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe cleanup target");
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static void AddYkb(AccountingTestContext db, string no, long creator, long type, bool processed = false,
        WorkFlowStatus status = WorkFlowStatus.Complated, bool deleted = false, bool approved = true)
    {
        db.Add(new YkbWorkFlow { RequestNo = no, CreatedUser = creator, WorkFlowStatus = status, IsDeleted = deleted });
        db.Add(new YkbServicesRequest { RequestNo = no, CreatedUser = 2, ServiceTypeId = type });
        db.Add(new YkbFinalApproval { RequestNo = no, Status = approved ? FinalApprovalStatus.Approved : FinalApprovalStatus.Pending,
            CustomerApprovedAt = approved ? new DateTime(2026, 10, 13) : null, CustomerApprovedBy = 2 });
        if (processed) db.Add(new YkbAccountingProcess { RequestNo = no, IsProcessed = true, ProcessedBy = 2 });
    }

    private static void AddEkb(AccountingTestContext db, string no, long creator, long type, bool processed = false,
        WorkFlowStatus status = WorkFlowStatus.Complated, bool deleted = false, bool approved = true)
    {
        db.Add(new EkbWorkFlow { RequestNo = no, CreatedUser = creator, WorkFlowStatus = status, IsDeleted = deleted });
        db.Add(new EkbServicesRequest { RequestNo = no, CreatedUser = 2, ServiceTypeId = type });
        db.Add(new EkbFinalApproval { RequestNo = no, Status = approved ? FinalApprovalStatus.Approved : FinalApprovalStatus.Pending,
            CustomerApprovedAt = approved ? new DateTime(2026, 10, 13) : null, CustomerApprovedBy = 2 });
        if (processed) db.Add(new EkbAccountingProcess { RequestNo = no, IsProcessed = true, ProcessedBy = 2 });
    }
}

internal sealed class AccountingTestContext(DbContextOptions<AppDataContext> options) : AppDataContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        var keep = new[] { typeof(User), typeof(Customer), typeof(CustomerGroup), typeof(Tenant), typeof(Product),
            typeof(CustomerProductPrice), typeof(CustomerGroupProductPrice), typeof(TenantProductPrice),
            typeof(ServiceType), typeof(WorkOrderType), typeof(YkbWorkFlow), typeof(YkbServicesRequest), typeof(YkbPricing),
            typeof(YkbFinalApproval), typeof(YkbAccountingProcess), typeof(YkbServicesRequestProduct), typeof(YkbServicesRequestWorkOrderType),
            typeof(EkbWorkFlow), typeof(EkbServicesRequest), typeof(EkbPricing), typeof(EkbFinalApproval),
            typeof(EkbAccountingProcess), typeof(EkbServicesRequestProduct), typeof(EkbServicesRequestWorkOrderType) };
        foreach (var property in typeof(AppDataContext).GetProperties())
            if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            {
                var entity = property.PropertyType.GetGenericArguments()[0];
                if (!keep.Contains(entity)) model.Ignore(entity);
            }
        foreach (var entity in keep)
        {
            var builder = model.Entity(entity);
            foreach (var property in entity.GetProperties())
            {
                var target = property.PropertyType;
                if (target.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(target))
                    target = target.GetGenericArguments()[0];
                if (target.Namespace?.StartsWith("Model.Concrete", StringComparison.Ordinal) == true && !keep.Contains(target))
                    builder.Ignore(property.Name);
            }
        }
        model.Entity<YkbServicesRequestWorkOrderType>().HasKey(x => new { x.YkbServicesRequestId, x.WorkOrderTypeId });
        model.Entity<EkbServicesRequestWorkOrderType>().HasKey(x => new { x.EkbServicesRequestId, x.WorkOrderTypeId });
        model.Entity<User>().Property(x => x.Id).ValueGeneratedNever();
    }
}
