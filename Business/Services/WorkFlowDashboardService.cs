using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using Model.Concrete.Qnb;
using Model.Concrete.WorkFlows;
using Model.Concrete.Ykb;
using Model.Concrete.Ekb;
using Model.Dtos.Dashboard;

namespace Business.Services
{
    public class WorkFlowDashboardService : IWorkFlowDashboardService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<WorkFlowDashboardService> _logger;
        private readonly ICurrentUser _currentUser;

        public WorkFlowDashboardService(
            IUnitOfWork uow,
            ILogger<WorkFlowDashboardService> logger,
            ICurrentUser currentUser)
        {
            _uow = uow;
            _logger = logger;
            _currentUser = currentUser;
        }

        public async Task<ResponseModel<DashboardKpiDto>> GetKpiAsync()
        {
            try
            {
                var now = DateTime.Now;
                var todayStart = now.Date;
                var monthStart = new DateTime(now.Year, now.Month, 1);

                var workFlows = await _uow.Repository
                    .GetQueryable<WorkFlow>()
                    .Include(x => x.CurrentStep)
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .ToListAsync();

                var pendingWorkFlows = workFlows
                    .Where(x => x.WorkFlowStatus == WorkFlowStatus.Pending)
                    .ToList();

                var normalPriorities = new HashSet<WorkFlowPriority>
                    {
                        WorkFlowPriority.Normal,
                        WorkFlowPriority.Region1Normal,
                        WorkFlowPriority.Region2Normal,
                        WorkFlowPriority.Region3Normal
                    };

                var criticalPriorities = new HashSet<WorkFlowPriority>
                    {
                        WorkFlowPriority.Urgent,
                        WorkFlowPriority.Region1Urgent,
                        WorkFlowPriority.Region2Urgent,
                        WorkFlowPriority.Region3Urgent
                    };

                var dto = new DashboardKpiDto
                {
                    // Genel İstatistikler
                    TotalActiveWorkFlows = pendingWorkFlows.Count,
                    TotalCompletedWorkFlows = workFlows.Count(x =>
                        x.WorkFlowStatus == WorkFlowStatus.Complated),

                    TotalCancelledWorkFlows = workFlows.Count(x =>
                        x.WorkFlowStatus == WorkFlowStatus.Cancelled),

                    TotalPendingWorkFlows = pendingWorkFlows.Count,

                    // Adım Bazlı Dağılım
                    InServiceRequest = pendingWorkFlows.Count(x =>
                        x.CurrentStep != null &&
                        x.CurrentStep.Code == "SR"),

                    InWarehouse = pendingWorkFlows.Count(x =>
                        x.CurrentStep != null &&
                        x.CurrentStep.Code == "WH"),

                    InTechnicalService = pendingWorkFlows.Count(x =>
                        x.CurrentStep != null &&
                        x.CurrentStep.Code == "TS"),

                    InPricing = pendingWorkFlows.Count(x =>
                        x.CurrentStep != null &&
                        x.CurrentStep.Code == "PRC"),

                    InFinalApproval = pendingWorkFlows.Count(x =>
                        x.CurrentStep != null &&
                        x.CurrentStep.Code == "APR"),

                    // Bugün/Bu Ay
                    CreatedToday = workFlows.Count(x =>
                        x.CreatedDate.Date == todayStart),

                    CompletedToday = workFlows.Count(x =>
                        x.WorkFlowStatus == WorkFlowStatus.Complated &&
                        x.UpdatedDate.HasValue &&
                        x.UpdatedDate.Value.Date == todayStart),

                    CreatedThisMonth = workFlows.Count(x =>
                        x.CreatedDate >= monthStart),

                    CompletedThisMonth = workFlows.Count(x =>
                        x.WorkFlowStatus == WorkFlowStatus.Complated &&
                        x.UpdatedDate.HasValue &&
                        x.UpdatedDate.Value >= monthStart),

                    // Öncelik Dağılımı
                    LowPriorityCount = pendingWorkFlows.Count(x =>
                        x.Priority == WorkFlowPriority.Low),

                    // Normal + 1/2/3. Bölge Normal
                    NormalPriorityCount = pendingWorkFlows.Count(x =>
                        normalPriorities.Contains(x.Priority)),

                    HighPriorityCount = pendingWorkFlows.Count(x =>
                        x.Priority == WorkFlowPriority.High),

                    // Acil + 1/2/3. Bölge Acil
                    CriticalPriorityCount = pendingWorkFlows.Count(x =>
                        criticalPriorities.Contains(x.Priority))
                };
                // Zaman Metrikleri
                var completedWfs = workFlows
                    .Where(x => x.WorkFlowStatus == WorkFlowStatus.Complated && x.UpdatedDate.HasValue)
                    .ToList();

                if (completedWfs.Any())
                {
                    var avgCompletionHours = completedWfs
                        .Select(x => (x.UpdatedDate!.Value - x.CreatedDate).TotalHours)
                        .Average();

                    dto.AverageCompletionTimeHours = Math.Round(avgCompletionHours, 2);
                }

                // Teknik Servis Ortalama Süresi
                var techServices = await _uow.Repository
                    .GetQueryable<TechnicalService>()
                    .AsNoTracking()
                    .Where(x => x.StartTime.HasValue && x.EndTime.HasValue)
                    .ToListAsync();

                if (techServices.Any())
                {
                    var avgTechHours = techServices
                        .Select(x => (x.EndTime!.Value - x.StartTime!.Value).TotalHours)
                        .Average();

                    dto.AverageTechnicalServiceTimeHours = Math.Round(avgTechHours, 2);
                }

                return ResponseModel<DashboardKpiDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetKpiAsync");
                return ResponseModel<DashboardKpiDto>.Fail($"KPI verileri getirilirken hata: {ex.Message}", StatusCode.Error);
            }
        }

        public async Task<ResponseModel<List<TechnicianPerformanceDto>>> GetTechnicianPerformanceAsync(
            DateTime? from = null,
            DateTime? to = null)
        {
            try
            {
                var dateFrom = from ?? DateTime.Now.AddMonths(-1);
                var dateTo = to ?? DateTime.Now;
                var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                var workFlows = await _uow.Repository
                    .GetQueryable<WorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.ApproverTechnicianId.HasValue)
                    .Where(x => x.CreatedDate >= dateFrom && x.CreatedDate <= dateTo)
                    .ToListAsync();

                var activationLogs = await _uow.Repository
                    .GetQueryable<WorkFlowActivityRecord>()
                    .AsNoTracking()
                    .Where(x => x.ActionType == WorkFlowActionType.LocationCheckFailed ||
                               x.ActionType == WorkFlowActionType.WorkFlowStepChanged)
                    .Where(x => x.OccurredAtUtc >= dateFrom && x.OccurredAtUtc <= dateTo)
                    .ToListAsync();

                var reviewBacks = await _uow.Repository
                    .GetQueryable<WorkFlowReviewLog>()
                    .AsNoTracking()
                    .Where(x => x.CreatedDate >= dateFrom && x.CreatedDate <= dateTo)
                    .ToListAsync();

                var technicianGroups = workFlows
                    .Where(x => x.ApproverTechnicianId.HasValue)
                    .GroupBy(x => x.ApproverTechnicianId!.Value);

                var result = new List<TechnicianPerformanceDto>();

                foreach (var group in technicianGroups)
                {
                    var techId = group.Key;
                    var techWfs = group.ToList();
                    var technician = techWfs.First().ApproverTechnician;

                    if (technician == null) continue;

                    var activeCount = techWfs.Count(x => x.WorkFlowStatus == WorkFlowStatus.Pending);
                    var completedCount = techWfs.Count(x => x.WorkFlowStatus == WorkFlowStatus.Complated);
                    var totalCount = techWfs.Count;

                    var completedWfs = techWfs
                        .Where(x => x.WorkFlowStatus == WorkFlowStatus.Complated && x.UpdatedDate.HasValue)
                        .ToList();

                    var avgCompletionHours = completedWfs.Any()
                        ? completedWfs.Select(x => (x.UpdatedDate!.Value - x.CreatedDate).TotalHours).Average()
                        : 0;

                    var completionRate = totalCount > 0
                        ? (double)completedCount / totalCount * 100
                        : 0;

                    var locationFailures = activationLogs.Count(x =>
                        x.ActionType == WorkFlowActionType.LocationCheckFailed &&
                        x.FromStepCode == "TS");

                    var reviewBacksForTech = reviewBacks.Count(x =>
                        techWfs.Any(wf => wf.RequestNo == x.RequestNo));

                    var completedThisMonth = techWfs.Count(x =>
                        x.WorkFlowStatus == WorkFlowStatus.Complated &&
                        x.UpdatedDate.HasValue &&
                        x.UpdatedDate.Value >= monthStart);

                    result.Add(new TechnicianPerformanceDto
                    {
                        TechnicianId = techId,
                        Name = technician.Name ?? "Bilinmiyor",
                        Email = technician.Email,
                        City = technician.City,
                        ActiveTasksCount = activeCount,
                        CompletedTasksCount = completedCount,
                        TotalTasksCount = totalCount,
                        AverageCompletionTimeHours = Math.Round(avgCompletionHours, 2),
                        CompletionRate = Math.Round(completionRate, 2),
                        LocationCheckFailures = locationFailures,
                        LocationOverrideRequests = 0,
                        ReviewBackCount = reviewBacksForTech,
                        CompletedThisMonth = completedThisMonth
                    });
                }

                result = result.OrderByDescending(x => x.CompletedTasksCount).ToList();

                return ResponseModel<List<TechnicianPerformanceDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTechnicianPerformanceAsync");
                return ResponseModel<List<TechnicianPerformanceDto>>.Fail(
                    $"Teknisyen performans verileri getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }


        #region Bireysel Müşteri
        public async Task<ResponseModel<List<CustomerStatisticsDto>>> GetTopCustomersAsync(int count = 10)
        {
            try
            {
                var servicesRequests = await _uow.Repository
                    .GetQueryable<ServicesRequest>()
                    .Include(x => x.Customer)
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .ToListAsync();

                var workFlows = await _uow.Repository
                    .GetQueryable<WorkFlow>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .ToListAsync();

                var products = await _uow.Repository
                    .GetQueryable<ServicesRequestProduct>()
                    .Include(x => x.Product)
                    .AsNoTracking()
                    .ToListAsync();

                var customerGroups = servicesRequests
                    .Where(x => x.Customer != null)
                    .GroupBy(x => x.CustomerId);

                var result = new List<CustomerStatisticsDto>();

                foreach (var group in customerGroups)
                {
                    var customerId = group.Key;
                    var customerRequests = group.ToList();
                    var customer = customerRequests.First().Customer!;

                    var requestNos = customerRequests.Select(x => x.RequestNo).ToHashSet();
                    var customerWfs = workFlows.Where(x => requestNos.Contains(x.RequestNo)).ToList();
                    var customerProducts = products.Where(x => x.CustomerId == customerId).ToList();

                    var totalCostTL = GetTotalByCurrency(customerProducts, "TRY");
                    var totalCostUSD = GetTotalByCurrency(customerProducts, "USD");
                    var totalCostEUR = GetTotalByCurrency(customerProducts, "EUR");

                    var now = DateTime.Now;
                    var installDate = customer.InstallationDate;
                    var warrantyYears = customer.WarrantyYears ?? 0;

                    var inWarrantyCount = 0;
                    var outOfWarrantyCount = 0;

                    if (installDate.HasValue && warrantyYears > 0)
                    {
                        var warrantyEndDate = installDate.Value.AddYears(warrantyYears);

                        foreach (var req in customerRequests)
                        {
                            if (req.ServicesDate <= warrantyEndDate)
                                inWarrantyCount++;
                            else
                                outOfWarrantyCount++;
                        }
                    }

                    result.Add(new CustomerStatisticsDto
                    {
                        CustomerId = customerId,
                        CustomerName = customer.ContactName1 ?? customer.SubscriberCompany ?? "Bilinmiyor",
                        CustomerCode = customer.SubscriberCode,
                        City = customer.City,
                        District = customer.District,
                        TotalRequests = customerRequests.Count,
                        CompletedRequests = customerWfs.Count(x => x.WorkFlowStatus == WorkFlowStatus.Complated),
                        ActiveRequests = customerWfs.Count(x => x.WorkFlowStatus == WorkFlowStatus.Pending),
                        CancelledRequests = customerWfs.Count(x => x.WorkFlowStatus == WorkFlowStatus.Cancelled),
                        TotalServiceCostTL = totalCostTL,
                        TotalServiceCostUSD = totalCostUSD,
                        TotalServiceCostEUR = totalCostEUR,
                        InWarrantyCount = inWarrantyCount,
                        OutOfWarrantyCount = outOfWarrantyCount,
                        LastServiceDate = customerRequests.Count > 0 ? customerRequests.Max(x => x.ServicesDate).DateTime : (DateTime?)null
                    });
                }

                result = result
                    .OrderByDescending(x => x.TotalRequests)
                    .Take(count)
                    .ToList();

                return ResponseModel<List<CustomerStatisticsDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTopCustomersAsync");
                return ResponseModel<List<CustomerStatisticsDto>>.Fail(
                    $"Müşteri istatistikleri getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<ProductStatisticsDto>> GetProductStatisticsAsync()
        {
            try
            {
                var products = await _uow.Repository
                    .GetQueryable<ServicesRequestProduct>()
                    .Include(x => x.Product)
                    .AsNoTracking()
                    .ToListAsync();

                var warehouses = await _uow.Repository
                    .GetQueryable<Warehouse>()
                    .AsNoTracking()
                    .ToListAsync();

                var productGroups = products
                    .Where(x => x.Product != null)
                    .GroupBy(x => x.ProductId);

                var topProducts = new List<ProductUsageDto>();

                foreach (var group in productGroups)
                {
                    var productId = group.Key;
                    var productItems = group.ToList();
                    var product = productItems.First().Product;

                    if (product == null) continue;

                    var totalQuantity = productItems.Sum(x => x.Quantity);
                    var usageCount = productItems.Count;

                    var totalCostTL = GetTotalByCurrency(productItems, "TRY");
                    var totalCostUSD = GetTotalByCurrency(productItems, "USD");
                    var totalCostEUR = GetTotalByCurrency(productItems, "EUR");

                    topProducts.Add(new ProductUsageDto
                    {
                        ProductId = productId,
                        ProductCode = product.ProductCode ?? "Bilinmiyor",
                        ProductName = product.Description ?? "Bilinmiyor",
                        UsageCount = usageCount,
                        TotalQuantity = totalQuantity,
                        TotalCostTL = totalCostTL,
                        TotalCostUSD = totalCostUSD,
                        TotalCostEUR = totalCostEUR
                    });
                }

                topProducts = topProducts
                    .OrderByDescending(x => x.UsageCount)
                    .Take(20)
                    .ToList();

                var dto = new ProductStatisticsDto
                {
                    TopProducts = topProducts,
                    PendingWarehouseDeliveries = warehouses.Count(x => x.WarehouseStatus == WarehouseStatus.Pending),
                    CompletedWarehouseDeliveries = warehouses.Count(x => x.WarehouseStatus == WarehouseStatus.Shipped),
                    AwaitingReviewWarehouses = warehouses.Count(x => x.WarehouseStatus == WarehouseStatus.AwaitingReview),
                    TotalProductsUsed = productGroups.Count(),
                    TotalQuantity = products.Sum(x => x.Quantity)
                };

                return ResponseModel<ProductStatisticsDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetProductStatisticsAsync");
                return ResponseModel<ProductStatisticsDto>.Fail(
                    $"Ürün istatistikleri getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<TimeBasedTrendDto>> GetTrendAnalysisAsync(int days = 30)
        {
            try
            {
                var endDate = DateTime.Now.Date.AddDays(1);
                var startDate = endDate.AddDays(-days);

                var workFlows = await _uow.Repository
                    .GetQueryable<WorkFlow>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.CreatedDate >= startDate && x.CreatedDate < endDate)
                    .ToListAsync();

                var requestNos = workFlows.Select(x => x.RequestNo).ToHashSet();

                var products = await _uow.Repository
                    .GetQueryable<ServicesRequestProduct>()
                    .Include(x => x.Product)
                    .AsNoTracking()
                    .Where(x => requestNos.Contains(x.RequestNo))
                    .ToListAsync();

                // Günlük Trend
                var dailyTrend = new List<TrendDataPoint>();

                for (var date = startDate; date < endDate; date = date.AddDays(1))
                {
                    var dayEnd = date.AddDays(1);
                    var dayWfs = workFlows.Where(x => x.CreatedDate >= date && x.CreatedDate < dayEnd).ToList();
                    var dayRequestNos = dayWfs.Select(x => x.RequestNo).ToHashSet();
                    var dayProducts = products.Where(x => dayRequestNos.Contains(x.RequestNo)).ToList();

                    var revenueTL = GetTotalByCurrency(dayProducts, "TRY");
                    var revenueUSD = GetTotalByCurrency(dayProducts, "USD");
                    var revenueEUR = GetTotalByCurrency(dayProducts, "EUR");

                    dailyTrend.Add(new TrendDataPoint
                    {
                        Date = date,
                        Period = date.ToString("yyyy-MM-dd"),
                        CreatedCount = dayWfs.Count,
                        CompletedCount = dayWfs.Count(x =>
                            x.WorkFlowStatus == WorkFlowStatus.Complated &&
                            x.UpdatedDate.HasValue &&
                            x.UpdatedDate.Value >= date &&
                            x.UpdatedDate.Value < dayEnd),
                        CancelledCount = dayWfs.Count(x =>
                            x.WorkFlowStatus == WorkFlowStatus.Cancelled &&
                            x.UpdatedDate.HasValue &&
                            x.UpdatedDate.Value >= date &&
                            x.UpdatedDate.Value < dayEnd),
                        TotalRevenueTL = revenueTL,
                        TotalRevenueUSD = revenueUSD,
                        TotalRevenueEUR = revenueEUR
                    });
                }

                // Haftalık Trend (son 12 hafta)
                var weeklyTrend = new List<TrendDataPoint>();
                var weekStart = endDate.AddDays(-84);

                for (int i = 0; i < 12; i++)
                {
                    var weekEnd = weekStart.AddDays(7);
                    var weekWfs = workFlows.Where(x => x.CreatedDate >= weekStart && x.CreatedDate < weekEnd).ToList();
                    var weekRequestNos = weekWfs.Select(x => x.RequestNo).ToHashSet();
                    var weekProducts = products.Where(x => weekRequestNos.Contains(x.RequestNo)).ToList();

                    var revenueTL = GetTotalByCurrency(weekProducts, "TRY");
                    var revenueUSD = GetTotalByCurrency(weekProducts, "USD");
                    var revenueEUR = GetTotalByCurrency(weekProducts, "EUR");

                    weeklyTrend.Add(new TrendDataPoint
                    {
                        Date = weekStart,
                        Period = $"Hafta {i + 1}",
                        CreatedCount = weekWfs.Count,
                        CompletedCount = weekWfs.Count(x => x.WorkFlowStatus == WorkFlowStatus.Complated),
                        CancelledCount = weekWfs.Count(x => x.WorkFlowStatus == WorkFlowStatus.Cancelled),
                        TotalRevenueTL = revenueTL,
                        TotalRevenueUSD = revenueUSD,
                        TotalRevenueEUR = revenueEUR
                    });

                    weekStart = weekEnd;
                }

                // Aylık Trend (son 12 ay)
                var monthlyTrend = new List<TrendDataPoint>();
                var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-11);
                var allWorkFlowsForMonths = await _uow.Repository
                    .GetQueryable<WorkFlow>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.CreatedDate >= monthStart)
                    .ToListAsync();

                var allRequestNosForMonths = allWorkFlowsForMonths.Select(x => x.RequestNo).ToHashSet();
                var allProductsForMonths = await _uow.Repository
                    .GetQueryable<ServicesRequestProduct>()
                    .Include(x => x.Product)
                    .AsNoTracking()
                    .Where(x => allRequestNosForMonths.Contains(x.RequestNo))
                    .ToListAsync();

                for (int i = 0; i < 12; i++)
                {
                    var monthEnd = monthStart.AddMonths(1);
                    var monthWfs = allWorkFlowsForMonths.Where(x => x.CreatedDate >= monthStart && x.CreatedDate < monthEnd).ToList();
                    var monthRequestNos = monthWfs.Select(x => x.RequestNo).ToHashSet();
                    var monthProducts = allProductsForMonths.Where(x => monthRequestNos.Contains(x.RequestNo)).ToList();

                    var revenueTL = GetTotalByCurrency(monthProducts, "TRY");
                    var revenueUSD = GetTotalByCurrency(monthProducts, "USD");
                    var revenueEUR = GetTotalByCurrency(monthProducts, "EUR");

                    monthlyTrend.Add(new TrendDataPoint
                    {
                        Date = monthStart,
                        Period = monthStart.ToString("MMM yyyy"),
                        CreatedCount = monthWfs.Count,
                        CompletedCount = monthWfs.Count(x => x.WorkFlowStatus == WorkFlowStatus.Complated),
                        CancelledCount = monthWfs.Count(x => x.WorkFlowStatus == WorkFlowStatus.Cancelled),
                        TotalRevenueTL = revenueTL,
                        TotalRevenueUSD = revenueUSD,
                        TotalRevenueEUR = revenueEUR
                    });

                    monthStart = monthEnd;
                }

                var dto = new TimeBasedTrendDto
                {
                    DailyTrend = dailyTrend,
                    WeeklyTrend = weeklyTrend,
                    MonthlyTrend = monthlyTrend
                };

                return ResponseModel<TimeBasedTrendDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTrendAnalysisAsync");
                return ResponseModel<TimeBasedTrendDto>.Fail(
                    $"Trend analizi getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<List<StepDurationAnalysisDto>>> GetStepDurationAnalysisAsync()
        {
            try
            {
                var reviewLogs = await _uow.Repository
                    .GetQueryable<WorkFlowReviewLog>()
                    .AsNoTracking()
                    .OrderBy(x => x.CreatedDate)
                    .ToListAsync();

                var workFlows = await _uow.Repository
                    .GetQueryable<WorkFlow>()
                    .Include(x => x.CurrentStep)
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .ToListAsync();

                var steps = await _uow.Repository
                    .GetQueryable<WorkFlowStep>()
                    .AsNoTracking()
                    .ToListAsync();

                var result = new List<StepDurationAnalysisDto>();

                foreach (var step in steps)
                {
                    var logsToStep = reviewLogs.Where(x => x.ToStepCode == step.Code).ToList();
                    var logsFromStep = reviewLogs.Where(x => x.FromStepCode == step.Code).ToList();

                    var durations = new List<double>();

                    foreach (var logTo in logsToStep)
                    {
                        var entryTime = logTo.CreatedDate;

                        var exitLog = logsFromStep
                            .Where(x => x.RequestNo == logTo.RequestNo && x.CreatedDate > entryTime)
                            .OrderBy(x => x.CreatedDate)
                            .FirstOrDefault();

                        if (exitLog != null)
                        {
                            var duration = (exitLog.CreatedDate - entryTime).TotalHours;
                            if (duration > 0)
                                durations.Add(duration);
                        }
                    }

                    var currentlyInStep = workFlows.Count(x =>
                        x.WorkFlowStatus == WorkFlowStatus.Pending &&
                        x.CurrentStep != null &&
                        x.CurrentStep.Code == step.Code);

                    if (durations.Any())
                    {
                        durations.Sort();
                        var median = durations.Count % 2 == 0
                            ? (durations[durations.Count / 2 - 1] + durations[durations.Count / 2]) / 2
                            : durations[durations.Count / 2];

                        result.Add(new StepDurationAnalysisDto
                        {
                            StepCode = step.Code ?? "Bilinmiyor",
                            StepName = step.Name ?? "Bilinmiyor",
                            AverageDuration = Math.Round(durations.Average(), 2),
                            MinDuration = Math.Round(durations.Min(), 2),
                            MaxDuration = Math.Round(durations.Max(), 2),
                            MedianDuration = Math.Round(median, 2),
                            TotalProcessed = durations.Count,
                            CurrentlyInStep = currentlyInStep
                        });
                    }
                    else
                    {
                        result.Add(new StepDurationAnalysisDto
                        {
                            StepCode = step.Code ?? "Bilinmiyor",
                            StepName = step.Name ?? "Bilinmiyor",
                            AverageDuration = 0,
                            MinDuration = 0,
                            MaxDuration = 0,
                            MedianDuration = 0,
                            TotalProcessed = 0,
                            CurrentlyInStep = currentlyInStep
                        });
                    }
                }

                return ResponseModel<List<StepDurationAnalysisDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetStepDurationAnalysisAsync");
                return ResponseModel<List<StepDurationAnalysisDto>>.Fail(
                    $"Adım süre analizi getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<FinancialDashboardDto>> GetFinancialDashboardAsync()
        {
            try
            {
                var now = DateTime.Now;
                var todayStart = now.Date;
                var weekStart = now.Date.AddDays(-(int)now.DayOfWeek);
                var monthStart = new DateTime(now.Year, now.Month, 1);

                var products = await _uow.Repository
                    .GetQueryable<ServicesRequestProduct>()
                    .Include(x => x.Product)
                    .AsNoTracking()
                    .ToListAsync();

                var servicesRequests = await _uow.Repository
                    .GetQueryable<ServicesRequest>()
                    .AsNoTracking()
                    .ToListAsync();

                var pricings = await _uow.Repository
                    .GetQueryable<Pricing>()
                    .AsNoTracking()
                    .ToListAsync();

                var finalApprovals = await _uow.Repository
                    .GetQueryable<FinalApproval>()
                    .AsNoTracking()
                    .ToListAsync();

                // Toplam Gelir
                var totalRevenueTL = GetTotalByCurrency(products, "TRY");
                var totalRevenueUSD = GetTotalByCurrency(products, "USD");
                var totalRevenueEUR = GetTotalByCurrency(products, "EUR");

                // Aylık Gelir
                var monthlyRequestNos = servicesRequests
                    .Where(x => x.CreatedDate >= monthStart)
                    .Select(x => x.RequestNo)
                    .ToHashSet();

                var monthlyProducts = products.Where(x => monthlyRequestNos.Contains(x.RequestNo)).ToList();

                var monthlyRevenueTL = GetTotalByCurrency(monthlyProducts, "TRY");

                var monthlyRevenueUSD = GetTotalByCurrency(monthlyProducts, "USD");

                var monthlyRevenueEUR = GetTotalByCurrency(monthlyProducts, "EUR");

                // Haftalık Gelir
                var weeklyRequestNos = servicesRequests
                    .Where(x => x.CreatedDate >= weekStart)
                    .Select(x => x.RequestNo)
                    .ToHashSet();

                var weeklyProducts = products.Where(x => weeklyRequestNos.Contains(x.RequestNo)).ToList();

                var weeklyRevenueTL = GetTotalByCurrency(weeklyProducts, "TRY");

                var weeklyRevenueUSD = GetTotalByCurrency(weeklyProducts, "USD");

                var weeklyRevenueEUR = GetTotalByCurrency(weeklyProducts, "EUR");

                // Günlük Gelir
                var dailyRequestNos = servicesRequests
                    .Where(x => x.CreatedDate.Date == todayStart)
                    .Select(x => x.RequestNo)
                    .ToHashSet();

                var dailyProducts = products.Where(x => dailyRequestNos.Contains(x.RequestNo)).ToList();

                var dailyRevenueTL = GetTotalByCurrency(dailyProducts, "TRY");
                var dailyRevenueUSD = GetTotalByCurrency(dailyProducts, "USD");
                var dailyRevenueEUR = GetTotalByCurrency(dailyProducts, "EUR");

                // Maliyet Tipleri
                var costTypeDict = servicesRequests.GroupBy(x => x.ServicesCostStatus);
                var warrantyCount = costTypeDict.FirstOrDefault(x => x.Key == ServicesCostStatus.NotRequired)?.Count() ?? 0;
                var paidCount = costTypeDict.FirstOrDefault(x => x.Key == ServicesCostStatus.Chargeable)?.Count() ?? 0;
                var freeCount = costTypeDict.FirstOrDefault(x => x.Key == ServicesCostStatus.Maintenance)?.Count() ?? 0;
                var unknownCount = costTypeDict.FirstOrDefault(x => x.Key == ServicesCostStatus.Unknown)?.Count() ?? 0;

                // Ortalama İş Değeri
                var totalJobs = servicesRequests.Count;
                var avgJobValueTL = totalJobs > 0 ? totalRevenueTL / totalJobs : 0;
                var avgJobValueUSD = totalJobs > 0 ? totalRevenueUSD / totalJobs : 0;
                var avgJobValueEUR = totalJobs > 0 ? totalRevenueEUR / totalJobs : 0;

                // İndirim İstatistikleri
                var discounts = finalApprovals.Where(x => x.DiscountPercent > 0).ToList();
                var avgDiscountPercent = discounts.Any() ? discounts.Average(x => x.DiscountPercent!) : 0;

                var dto = new FinancialDashboardDto
                {
                    TotalRevenueTL = totalRevenueTL,
                    TotalRevenueUSD = totalRevenueUSD,
                    TotalRevenueEUR = totalRevenueEUR,

                    MonthlyRevenueTL = monthlyRevenueTL,
                    MonthlyRevenueUSD = monthlyRevenueUSD,
                    MonthlyRevenueEUR = monthlyRevenueEUR,

                    WeeklyRevenueTL = weeklyRevenueTL,
                    WeeklyRevenueUSD = weeklyRevenueUSD,
                    WeeklyRevenueEUR = weeklyRevenueEUR,

                    DailyRevenueTL = dailyRevenueTL,
                    DailyRevenueUSD = dailyRevenueUSD,
                    DailyRevenueEUR = dailyRevenueEUR,

                    PendingPricing = pricings.Count(x =>
                        x.Status == PricingStatus.Pending),

                    ApprovedPricing = pricings.Count(x =>
                        x.Status == PricingStatus.Approved),

                    RejectedPricing = pricings.Count(x =>
                        x.Status == PricingStatus.Rejected),

                    WarrantyServices = warrantyCount,
                    PaidServices = paidCount,
                    FreeServices = freeCount,
                    UnknownCostServices = unknownCount,

                    AverageJobValueTL = avgJobValueTL,
                    AverageJobValueUSD = avgJobValueUSD,
                    AverageJobValueEUR = avgJobValueEUR,

                    TotalDiscountAmount = 0,
                    AverageDiscountPercent = avgDiscountPercent
                };

                return ResponseModel<FinancialDashboardDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetFinancialDashboardAsync");
                return ResponseModel<FinancialDashboardDto>.Fail(
                    $"Finansal dashboard verileri getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<CriticalAlertsDto>> GetCriticalAlertsAsync()
        {
            try
            {
                var now = DateTime.Now;

                var workFlows = await _uow.Repository
                    .GetQueryable<WorkFlow>()
                    .Include(x => x.CurrentStep)
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.WorkFlowStatus == WorkFlowStatus.Pending)
                    .ToListAsync();

                var servicesRequests = await _uow.Repository
                    .GetQueryable<ServicesRequest>()
                    .Include(x => x.Customer)
                    .AsNoTracking()
                    .ToListAsync();

                var reviewLogs = await _uow.Repository
                    .GetQueryable<WorkFlowReviewLog>()
                    .AsNoTracking()
                    .Where(x => x.CreatedDate >= now.AddDays(-7))
                    .OrderByDescending(x => x.CreatedDate)
                    .Take(50)
                    .ToListAsync();

                var activationLogs = await _uow.Repository
                    .GetQueryable<WorkFlowActivityRecord>()
                    .AsNoTracking()
                    .Where(x => x.ActionType == WorkFlowActionType.LocationCheckFailed)
                    .Where(x => x.OccurredAtUtc >= now.AddDays(-7))
                    .Take(50)
                    .ToListAsync();

                var finalApprovals = await _uow.Repository
                    .GetQueryable<FinalApproval>()
                    .AsNoTracking()
                    .Where(x => x.Status == FinalApprovalStatus.Pending)
                    .ToListAsync();

                var pricings = await _uow.Repository
                    .GetQueryable<Pricing>()
                    .AsNoTracking()
                    .Where(x => x.Status == PricingStatus.Pending)
                    .ToListAsync();

                var warehouses = await _uow.Repository
                    .GetQueryable<Warehouse>()
                    .AsNoTracking()
                    .Where(x => x.WarehouseStatus == WarehouseStatus.Pending)
                    .ToListAsync();

                // Geciken İşler
                var delayedWorkFlows = new List<DelayedWorkFlowDto>();

                foreach (var wf in workFlows)
                {
                    var sr = servicesRequests.FirstOrDefault(x => x.RequestNo == wf.RequestNo);
                    if (sr?.PlannedCompletionDate.HasValue == true && sr.PlannedCompletionDate < now)
                    {
                        var delayHours = (int)(now - sr.PlannedCompletionDate.Value).TotalHours;

                        delayedWorkFlows.Add(new DelayedWorkFlowDto
                        {
                            Id = wf.Id,
                            RequestNo = wf.RequestNo,
                            CustomerName = sr.Customer?.ContactName1 ?? sr.Customer?.SubscriberCompany ?? "Bilinmiyor",
                            CurrentStep = wf.CurrentStep?.Name ?? "Bilinmiyor",
                            DelayHours = delayHours,
                            DelayDays = delayHours / 24,
                            Priority = wf.Priority,
                            CreatedDate = wf.CreatedDate.DateTime, // Convert DateTimeOffset to DateTime
                            PlannedCompletionDate = sr.PlannedCompletionDate?.DateTime // Convert DateTimeOffset? to DateTime?
                        });
                    }
                }

                delayedWorkFlows = delayedWorkFlows
                    .OrderByDescending(x => x.DelayHours)
                    .Take(20)
                    .ToList();

                // Lokasyon Sorunları
                var locationIssues = activationLogs
                    .Select(x => new LocationIssueDto
                    {
                        RequestNo = x.RequestNo ?? "Bilinmiyor",
                        Name = "Bilinmiyor",
                        CustomerName = "Bilinmiyor",
                        IssueDate = x.OccurredAtUtc,
                        IssueType = "Failed Check",
                        DistanceKm = null
                    })
                    .Take(20)
                    .ToList();

                // Geri Gönderilenler
                var reviewBacks = reviewLogs
                    .Select(x => new ReviewBackDto
                    {
                        RequestNo = x.RequestNo,
                        FromStep = x.FromStepCode ?? "Bilinmiyor",
                        ToStep = x.ToStepCode ?? "Bilinmiyor",
                        ReviewNotes = x.ReviewNotes ?? string.Empty,
                        ReviewDate = x.CreatedDate,
                        ReviewedBy = x.CreatedUser
                    })
                    .ToList();

                var urgentPriorities = new HashSet<WorkFlowPriority>
                {
                    WorkFlowPriority.Urgent,
                    WorkFlowPriority.Region1Urgent,
                    WorkFlowPriority.Region2Urgent,
                    WorkFlowPriority.Region3Urgent
                };
                var dto = new CriticalAlertsDto
                {
                    DelayedWorkFlows = delayedWorkFlows,
                    LocationIssues = locationIssues,
                    RecentReviewBacks = reviewBacks,
                    PendingFinalApprovals = finalApprovals.Count,
                    PendingPricingApprovals = pricings.Count,
                    PendingWarehouseDeliveries = warehouses.Count,
                    CriticalPriorityPending = workFlows.Count(x => urgentPriorities.Contains(x.Priority)),
                    HighPriorityPending = workFlows.Count(x => x.Priority == WorkFlowPriority.High),
                };

                return ResponseModel<CriticalAlertsDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCriticalAlertsAsync");
                return ResponseModel<CriticalAlertsDto>.Fail(
                    $"Kritik uyarılar getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<GeographicDistributionDto>> GetGeographicDistributionAsync()
        {
            try
            {
                var servicesRequests = await _uow.Repository
                    .GetQueryable<ServicesRequest>()
                    .Include(x => x.Customer)
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .ToListAsync();

                var workFlows = await _uow.Repository
                    .GetQueryable<WorkFlow>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .ToListAsync();

                var products = await _uow.Repository
                    .GetQueryable<ServicesRequestProduct>()
                    .Include(x => x.Product)
                    .AsNoTracking()
                    .ToListAsync();

                var cityGroups = servicesRequests
                    .Where(x => x.Customer != null && !string.IsNullOrWhiteSpace(x.Customer.City))
                    .GroupBy(x => x.Customer!.City!);

                var cityStats = new List<CityStatDto>();

                foreach (var cityGroup in cityGroups)
                {
                    var city = cityGroup.Key;
                    var cityRequests = cityGroup.ToList();
                    var requestNos = cityRequests.Select(x => x.RequestNo).ToHashSet();
                    var cityWfs = workFlows.Where(x => requestNos.Contains(x.RequestNo)).ToList();
                    var cityProducts = products.Where(x => requestNos.Contains(x.RequestNo)).ToList();

                    var revenueTL = GetTotalByCurrency(cityProducts, "TRY");

                    var revenueUSD =
                        GetTotalByCurrency(cityProducts, "USD");

                    var revenueEUR =
                        GetTotalByCurrency(cityProducts, "EUR");

                    var firstCustomer = cityRequests.First().Customer;

                    // İlçe dağılımı
                    var districtStats = cityRequests
                        .Where(x => x.Customer != null && !string.IsNullOrWhiteSpace(x.Customer.District))
                        .GroupBy(x => x.Customer!.District!)
                        .Select(g => new DistrictStatDto
                        {
                            District = g.Key,
                            RequestCount = g.Count()
                        })
                        .OrderByDescending(x => x.RequestCount)
                        .ToList();

                    cityStats.Add(new CityStatDto
                    {
                        City = city,
                        TotalRequests = cityRequests.Count,
                        ActiveRequests = cityWfs.Count(x =>
                            x.WorkFlowStatus == WorkFlowStatus.Pending),

                        CompletedRequests = cityWfs.Count(x =>
                            x.WorkFlowStatus == WorkFlowStatus.Complated),

                        TotalRevenueTL = revenueTL,
                        TotalRevenueUSD = revenueUSD,
                        TotalRevenueEUR = revenueEUR,

                        Latitude = firstCustomer?.Latitude,
                        Longitude = firstCustomer?.Longitude,
                        Districts = districtStats
                    });
                }

                cityStats = cityStats.OrderByDescending(x => x.TotalRequests).ToList();

                var dto = new GeographicDistributionDto
                {
                    CityStatistics = cityStats,
                    TotalCities = cityStats.Count,
                    TotalDistricts = cityStats.SelectMany(x => x.Districts).Count()
                };

                return ResponseModel<GeographicDistributionDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetGeographicDistributionAsync");
                return ResponseModel<GeographicDistributionDto>.Fail(
                    $"Coğrafi dağılım verileri getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<List<TechnicalServiceStatusCountDto>>> GetMyTechnicalServiceStatusCountsAsync()
        {
            try
            {
                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;

                if (meId <= 0)
                {
                    return ResponseModel<List<TechnicalServiceStatusCountDto>>.Fail(
                        "Kullanıcı bilgisi alınamadı.",
                        StatusCode.Unauthorized);
                }

                var workFlowQuery = _uow.Repository
                    .GetQueryable<WorkFlow>()
                    .AsNoTracking()
                    .Where(wf =>
                        !wf.IsDeleted &&
                        wf.ApproverTechnicianId == meId);

                var statusCounts = await _uow.Repository
                    .GetQueryable<TechnicalService>()
                    .AsNoTracking()
                    .Where(ts =>
                        !ts.IsDeleted &&
                        workFlowQuery.Any(wf =>
                            wf.RequestNo == ts.RequestNo))
                    .GroupBy(ts => ts.ServicesStatus)
                    .Select(group => new
                    {
                        ServicesStatus = group.Key,
                        RecordCount = group.Count()
                    })
                    .ToListAsync();

                var countDictionary = statusCounts.ToDictionary(
                    x => x.ServicesStatus,
                    x => x.RecordCount);

                // Kaydı olmayan durumları da 0 olarak döndürür.
                var result = Enum
                    .GetValues<TechnicalServiceStatus>()
                    .Select(status => new TechnicalServiceStatusCountDto
                    {
                        ServicesStatus = status,
                        ServicesStatusName =
                            GetTechnicalServiceStatusName(status),

                        RecordCount = countDictionary.TryGetValue(
                            status,
                            out var count)
                                ? count
                                : 0
                    })
                    .OrderBy(x => (int)x.ServicesStatus)
                    .ToList();

                return ResponseModel<List<TechnicalServiceStatusCountDto>>
                    .Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "GetMyTechnicalServiceStatusCountsAsync sırasında hata oluştu.");

                return ResponseModel<List<TechnicalServiceStatusCountDto>>.Fail(
                    $"Teknik servis durum adetleri alınırken hata oluştu: {ex.Message}",
                    StatusCode.Error);
            }
        }

        #endregion

        #region Ykb
        public async Task<ResponseModel<YkbDashboardKpiDto>> GetYkbKpiAsync(DateTimeOffset? from = null, DateTimeOffset? to = null)
        {
            try
            {
                var query = _uow.Repository
                    .GetQueryable<YkbWorkFlow>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted);

                if (from.HasValue)
                {
                    query = query.Where(x => x.CreatedDate >= from.Value);
                }

                if (to.HasValue)
                {
                    // Eğer frontend sadece tarih gönderirse örn: 2026-06-13 00:00,
                    // o günü komple dahil etmek için bitişi ertesi gün exclusive yapıyoruz.
                    if (to.Value.TimeOfDay == TimeSpan.Zero)
                    {
                        var endExclusive = to.Value.AddDays(1);
                        query = query.Where(x => x.CreatedDate < endExclusive);
                    }
                    else
                    {
                        query = query.Where(x => x.CreatedDate <= to.Value);
                    }
                }

                var dto = await query
                    .GroupBy(x => 1)
                    .Select(g => new YkbDashboardKpiDto
                    {
                        TotalWorkFlows = g.Count(),

                        CompletedWorkFlows = g.Count(x =>
                            x.WorkFlowStatus == WorkFlowStatus.Complated),

                        NotCompletedWorkFlows = g.Count(x =>
                            x.WorkFlowStatus != WorkFlowStatus.Complated),

                        InServiceRequest = g.Count(x =>
                            x.CurrentStep != null &&
                            x.CurrentStep.Code == "SR"),

                        InWarehouse = g.Count(x =>
                            x.CurrentStep != null &&
                            x.CurrentStep.Code == "WH"),

                        InTechnicalService = g.Count(x =>
                            x.CurrentStep != null &&
                            x.CurrentStep.Code == "TS"),

                        InPricing = g.Count(x =>
                            x.CurrentStep != null &&
                            x.CurrentStep.Code == "PRC"),

                        InFinalApproval = g.Count(x =>
                            x.CurrentStep != null &&
                            x.CurrentStep.Code == "APR"),

                        InCustomerApproval = g.Count(x =>
                            x.CurrentStep != null &&
                            x.CurrentStep.Code == "CAPR")
                    })
                    .FirstOrDefaultAsync();

                dto ??= new YkbDashboardKpiDto();
                return ResponseModel<YkbDashboardKpiDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetYkbKpiAsync");

                return ResponseModel<YkbDashboardKpiDto>.Fail(
                    $"YKB dashboard KPI verileri getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<List<YkbTechnicalServiceStatusCountDto>>> YkbGetMyTechnicalServiceStatusCountsAsync()
        {
            try
            {
                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;

                if (meId <= 0)
                {
                    return ResponseModel<List<YkbTechnicalServiceStatusCountDto>>.Fail(
                        "Kullanıcı bilgisi alınamadı.",
                        StatusCode.Unauthorized);
                }

                var workFlowQuery = _uow.Repository
                    .GetQueryable<YkbWorkFlow>()
                    .AsNoTracking()
                    .Where(wf =>
                        !wf.IsDeleted &&
                        wf.ApproverTechnicianId == meId);

                var statusCounts = await _uow.Repository
                    .GetQueryable<YkbTechnicalService>()
                    .AsNoTracking()
                    .Where(ts =>
                        !ts.IsDeleted &&
                        workFlowQuery.Any(wf =>
                            wf.RequestNo == ts.RequestNo))
                    .GroupBy(ts => ts.ServicesStatus)
                    .Select(group => new
                    {
                        ServicesStatus = group.Key,
                        RecordCount = group.Count()
                    })
                    .ToListAsync();

                var countDictionary = statusCounts.ToDictionary(
                    x => x.ServicesStatus,
                    x => x.RecordCount);

                // Kaydı olmayan durumları da 0 adet olarak döndürür.
                var result = Enum
                    .GetValues<TechnicalServiceStatus>()
                    .Select(status => new YkbTechnicalServiceStatusCountDto
                    {
                        ServicesStatus = status,
                        ServicesStatusName = GetTechnicalServiceStatusName(status),
                        RecordCount = countDictionary.TryGetValue(
                            status,
                            out var count)
                                ? count
                                : 0
                    })
                    .OrderBy(x => (int)x.ServicesStatus)
                    .ToList();

                return ResponseModel<List<YkbTechnicalServiceStatusCountDto>>
                    .Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "YkbGetMyTechnicalServiceStatusCountsAsync sırasında hata oluştu.");

                return ResponseModel<List<YkbTechnicalServiceStatusCountDto>>.Fail(
                    $"Teknik servis durum adetleri alınırken hata oluştu: {ex.Message}",
                    StatusCode.Error);
            }
        }

        #endregion
        #region Ekb
        public async Task<ResponseModel<EkbDashboardKpiDto>> GetEkbKpiAsync(DateTimeOffset? from = null, DateTimeOffset? to = null)
        {
            try
            {
                var query = _uow.Repository
                    .GetQueryable<EkbWorkFlow>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted);

                if (from.HasValue)
                {
                    query = query.Where(x => x.CreatedDate >= from.Value);
                }

                if (to.HasValue)
                {
                    // Eğer frontend sadece tarih gönderirse örn: 2026-06-13 00:00,
                    // o günü komple dahil etmek için bitişi ertesi gün exclusive yapıyoruz.
                    if (to.Value.TimeOfDay == TimeSpan.Zero)
                    {
                        var endExclusive = to.Value.AddDays(1);
                        query = query.Where(x => x.CreatedDate < endExclusive);
                    }
                    else
                    {
                        query = query.Where(x => x.CreatedDate <= to.Value);
                    }
                }

                var dto = await query
                    .GroupBy(x => 1)
                    .Select(g => new EkbDashboardKpiDto
                    {
                        TotalWorkFlows = g.Count(),

                        CompletedWorkFlows = g.Count(x =>
                            x.WorkFlowStatus == WorkFlowStatus.Complated),

                        NotCompletedWorkFlows = g.Count(x =>
                            x.WorkFlowStatus != WorkFlowStatus.Complated),

                        InServiceRequest = g.Count(x =>
                            x.CurrentStep != null &&
                            x.CurrentStep.Code == "SR"),

                        InWarehouse = g.Count(x =>
                            x.CurrentStep != null &&
                            x.CurrentStep.Code == "WH"),

                        InTechnicalService = g.Count(x =>
                            x.CurrentStep != null &&
                            x.CurrentStep.Code == "TS"),

                        InPricing = g.Count(x =>
                            x.CurrentStep != null &&
                            x.CurrentStep.Code == "PRC"),

                        InFinalApproval = g.Count(x =>
                            x.CurrentStep != null &&
                            x.CurrentStep.Code == "APR"),

                        InCustomerApproval = g.Count(x =>
                            x.CurrentStep != null &&
                            x.CurrentStep.Code == "CAPR")
                    })
                    .FirstOrDefaultAsync();

                dto ??= new EkbDashboardKpiDto();
                return ResponseModel<EkbDashboardKpiDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetEkbKpiAsync");

                return ResponseModel<EkbDashboardKpiDto>.Fail(
                    $"EKB dashboard KPI verileri getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<List<EkbTechnicalServiceStatusCountDto>>> EkbGetMyTechnicalServiceStatusCountsAsync()
        {
            try
            {
                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;

                if (meId <= 0)
                {
                    return ResponseModel<List<EkbTechnicalServiceStatusCountDto>>.Fail(
                        "Kullanıcı bilgisi alınamadı.",
                        StatusCode.Unauthorized);
                }

                var workFlowQuery = _uow.Repository
                    .GetQueryable<EkbWorkFlow>()
                    .AsNoTracking()
                    .Where(wf =>
                        !wf.IsDeleted &&
                        wf.ApproverTechnicianId == meId);

                var statusCounts = await _uow.Repository
                    .GetQueryable<EkbTechnicalService>()
                    .AsNoTracking()
                    .Where(ts =>
                        !ts.IsDeleted &&
                        workFlowQuery.Any(wf =>
                            wf.RequestNo == ts.RequestNo))
                    .GroupBy(ts => ts.ServicesStatus)
                    .Select(group => new
                    {
                        ServicesStatus = group.Key,
                        RecordCount = group.Count()
                    })
                    .ToListAsync();

                var countDictionary = statusCounts.ToDictionary(
                    x => x.ServicesStatus,
                    x => x.RecordCount);

                // Kaydı olmayan durumları da 0 adet olarak döndürür.
                var result = Enum
                    .GetValues<TechnicalServiceStatus>()
                    .Select(status => new EkbTechnicalServiceStatusCountDto
                    {
                        ServicesStatus = status,
                        ServicesStatusName = GetTechnicalServiceStatusName(status),
                        RecordCount = countDictionary.TryGetValue(
                            status,
                            out var count)
                                ? count
                                : 0
                    })
                    .OrderBy(x => (int)x.ServicesStatus)
                    .ToList();

                return ResponseModel<List<EkbTechnicalServiceStatusCountDto>>
                    .Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "EkbGetMyTechnicalServiceStatusCountsAsync sırasında hata oluştu.");

                return ResponseModel<List<EkbTechnicalServiceStatusCountDto>>.Fail(
                    $"Teknik servis durum adetleri alınırken hata oluştu: {ex.Message}",
                    StatusCode.Error);
            }
        }

        #endregion

        #region Qnb
        public async Task<ResponseModel<List<QnbTechnicalServiceStatusCountDto>>> QnbGetMyTechnicalServiceStatusCountsAsync()
        {
            try
            {
                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;

                if (meId <= 0)
                {
                    return ResponseModel<List<QnbTechnicalServiceStatusCountDto>>.Fail(
                        "Kullanıcı bilgisi alınamadı.",
                        StatusCode.Unauthorized);
                }

                var workFlowQuery = _uow.Repository
                    .GetQueryable<QnbWorkFlow>()
                    .AsNoTracking()
                    .Where(wf =>
                        !wf.IsDeleted &&
                        wf.ApproverTechnicianId == meId);

                var statusCounts = await _uow.Repository
                    .GetQueryable<QnbTechnicalService>()
                    .AsNoTracking()
                    .Where(ts =>
                        !ts.IsDeleted &&
                        workFlowQuery.Any(wf =>
                            wf.RequestNo == ts.RequestNo))
                    .GroupBy(ts => ts.ServicesStatus)
                    .Select(group => new
                    {
                        ServicesStatus = group.Key,
                        RecordCount = group.Count()
                    })
                    .ToListAsync();

                var countDictionary = statusCounts.ToDictionary(
                    x => x.ServicesStatus,
                    x => x.RecordCount);

                // Hiç kaydı olmayan durumlar da 0 olarak döner.
                var result = Enum
                    .GetValues<TechnicalServiceStatus>()
                    .Select(status => new QnbTechnicalServiceStatusCountDto
                    {
                        ServicesStatus = status,

                        ServicesStatusName =
                            GetTechnicalServiceStatusName(status),

                        RecordCount = countDictionary.TryGetValue(
                            status,
                            out var count)
                                ? count
                                : 0
                    })
                    .OrderBy(x => (int)x.ServicesStatus)
                    .ToList();

                return ResponseModel<List<QnbTechnicalServiceStatusCountDto>>
                    .Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "QnbGetMyTechnicalServiceStatusCountsAsync sırasında hata oluştu.");

                return ResponseModel<List<QnbTechnicalServiceStatusCountDto>>.Fail(
                    $"QNB teknik servis durum adetleri alınırken hata oluştu: {ex.Message}",
                    StatusCode.Error);
            }
        }
        #endregion

        private static string GetTechnicalServiceStatusName(TechnicalServiceStatus status)
        {
            return status switch
            {
                TechnicalServiceStatus.Pending =>
                    "Beklemede",

                TechnicalServiceStatus.InProgress =>
                    "İşlemde",

                TechnicalServiceStatus.Completed =>
                    "Tamamlandı",

                TechnicalServiceStatus.AwaitingReview =>
                    "Revizyon Bekliyor",

                TechnicalServiceStatus.Cancelled =>
                    "İptal Edildi",

                _ => "Bilinmeyen Durum"
            };
        }



        private static string GetProductCurrency(ServicesRequestProduct item)
        {
            var currency = item.IsPriceCaptured &&
                           !string.IsNullOrWhiteSpace(item.CapturedCurrency)
                ? item.CapturedCurrency
                : item.Product?.PriceCurrency;

            var normalized = (currency ?? "TRY")
                .Trim()
                .ToUpperInvariant();

            return normalized switch
            {
                "TL" => "TRY",
                "₺" => "TRY",
                "€" => "EUR",
                "$" => "USD",
                _ => normalized
            };
        }

        private static decimal GetProductTotal(ServicesRequestProduct item)
        {
            if (item.IsPriceCaptured)
            {
                if (item.CapturedTotal.HasValue)
                    return item.CapturedTotal.Value;

                var capturedUnitPrice =
                    item.CapturedUnitPrice ??
                    item.Product?.Price ??
                    0m;

                return capturedUnitPrice * item.Quantity;
            }

            return (item.Product?.Price ?? 0m) * item.Quantity;
        }

        private static decimal GetTotalByCurrency(IEnumerable<ServicesRequestProduct> products, string currency)
        {
            return products
                .Where(x => GetProductCurrency(x) == currency)
                .Sum(x => GetProductTotal(x));
        }
    }
}