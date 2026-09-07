using Business.Interfaces;
using Business.Interfaces.Ykb;
using Business.UnitOfWork;
using ClosedXML.Excel;
using Core.Common;
using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using Model.Concrete.Ykb;
using Model.Dtos.OvertimeReport;
using Model.Dtos.WorkingHourPolicy;

namespace Business.Services.Ykb
{
    public class YkbOvertimeReportService : IYkbOvertimeReportService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<YkbOvertimeReportService> _logger;
        private readonly ICurrentUser _currentUser;
        private readonly IWorkingHourPolicyService _workingHourPolicyService;

        public YkbOvertimeReportService(
            IUnitOfWork uow,
            ILogger<YkbOvertimeReportService> logger,
            ICurrentUser currentUser,
            IWorkingHourPolicyService workingHourPolicyService)
        {
            _uow = uow;
            _logger = logger;
            _currentUser = currentUser;
            _workingHourPolicyService = workingHourPolicyService;
        }

        public async Task<ResponseModel<YkbTechnicianOvertimeReportDto>> GetTechnicianOvertimeReportAsync(
            long technicianId,
            DateTime startDate,
            DateTime endDate,
            bool includeCustomerDetails = false)
        {
            try
            {
                var startDateOffset = new DateTimeOffset(startDate, TimeSpan.Zero);
                var endDateOffset = new DateTimeOffset(endDate, TimeSpan.Zero);

                // Teknisyen kontrolü
                var technician = await _uow.Repository
                    .GetQueryable<User>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == technicianId);

                if (technician == null)
                    return ResponseModel<YkbTechnicianOvertimeReportDto>.Fail(
                        "Teknisyen bulunamadı.",
                        StatusCode.NotFound);

                // YKB WorkFlow'ları çek
                var workFlows = await _uow.Repository
                    .GetQueryable<YkbWorkFlow>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.ApproverTechnicianId == technicianId)
                    .ToListAsync();

                var requestNos = workFlows.Select(x => x.RequestNo).ToList();

                // YKB TechnicalService'leri çek
                var technicalServices = await _uow.Repository
                    .GetQueryable<YkbTechnicalService>()
                    .AsNoTracking()
                    .Where(x => requestNos.Contains(x.RequestNo))
                    .Where(x => x.ServicesStatus == TechnicalServiceStatus.Completed)
                    .Where(x => x.StartTime.HasValue && x.EndTime.HasValue)
                    .Where(x => x.StartTime >= startDateOffset && x.EndTime <= endDateOffset)
                    .Include(x => x.ServiceType)
                    .ToListAsync();

                // Müşteri bilgileri (YKB için)
                Dictionary<string, Customer>? customers = null;
                if (includeCustomerDetails)
                {
                    var servicesRequests = await _uow.Repository
                        .GetQueryable<YkbServicesRequest>()
                        .Include(x => x.Customer)
                        .AsNoTracking()
                        .Where(x => requestNos.Contains(x.RequestNo))
                        .ToListAsync();

                    customers = servicesRequests
                        .Where(x => x.Customer != null)
                        .GroupBy(x => x.RequestNo)
                        .ToDictionary(x => x.Key, x => x.First().Customer!);
                }

                var jobs = new List<YkbOvertimeJobDto>();
                double totalOvertimeHours = 0;

                foreach (var ts in technicalServices)
                {
                    if (!ts.StartTime.HasValue || !ts.EndTime.HasValue)
                        continue;

                    var startTime = ts.StartTime.Value;
                    var endTime = ts.EndTime.Value;

                    var overtimeResult = await CalculateOvertimeWithBreakdownAsync(startTime, endTime);

                    if (overtimeResult.TotalOvertimeHours > 0)
                    {
                        var workflow = workFlows.FirstOrDefault(x => x.RequestNo == ts.RequestNo);
                        Customer? customer = null;
                        customers?.TryGetValue(ts.RequestNo, out customer);

                        var job = new YkbOvertimeJobDto
                        {
                            RequestNo = ts.RequestNo,
                            RequestTitle = workflow?.RequestTitle ?? "Bilinmiyor",
                            Date = DateOnly.FromDateTime(startTime.DateTime),
                            StartTime = startTime.DateTime,
                            EndTime = endTime.DateTime,
                            TotalWorkingHours = Math.Round(overtimeResult.TotalHours, 2),
                            NormalHours = Math.Round(overtimeResult.NormalHours, 2),
                            OvertimeHours = Math.Round(overtimeResult.TotalOvertimeHours, 2),
                            OvertimeBreakdown = overtimeResult.Breakdown,
                            StartLocation = ts.StartLocation,
                            EndLocation = ts.EndLocation
                        };

                        if (includeCustomerDetails && customer != null)
                        {
                            job.YkbCustomerFormId = customer.Id;
                            job.CustomerName = customer.ContactName1 ?? customer.SubscriberCompany;
                            job.CustomerAddress = customer.SubscriberAddress;
                            job.CustomerCity = customer.City;
                            job.ServiceTypeName = ts.ServiceType?.Name;
                        }

                        jobs.Add(job);
                        totalOvertimeHours += overtimeResult.TotalOvertimeHours;
                    }
                }

                var report = new YkbTechnicianOvertimeReportDto
                {
                    TechnicianId = technician.Id,
                    Name = technician.Name,
                    Code = technician.Code,
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalOvertimeHours = Math.Round(totalOvertimeHours, 2),
                    TotalJobs = jobs.Count,
                    Jobs = jobs.OrderBy(x => x.Date).ToList()
                };

                return ResponseModel<YkbTechnicianOvertimeReportDto>.Success(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "YKB - GetTechnicianOvertimeReportAsync - TechnicianId: {TechnicianId}", technicianId);
                return ResponseModel<YkbTechnicianOvertimeReportDto>.Fail(
                    $"Fazla mesai raporu getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<YkbAllTechniciansOvertimeSummaryDto>> GetAllTechniciansOvertimeSummaryAsync(
            DateTime startDate,
            DateTime endDate)
        {
            try
            {
                var startDateOffset = new DateTimeOffset(startDate, TimeSpan.Zero);
                var endDateOffset = new DateTimeOffset(endDate, TimeSpan.Zero);

                var workFlows = await _uow.Repository
                    .GetQueryable<YkbWorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.ApproverTechnicianId.HasValue)
                    .ToListAsync();

                var requestNos = workFlows.Select(x => x.RequestNo).ToList();

                var technicalServices = await _uow.Repository
                    .GetQueryable<YkbTechnicalService>()
                    .AsNoTracking()
                    .Where(x => requestNos.Contains(x.RequestNo))
                    .Where(x => x.StartTime.HasValue && x.EndTime.HasValue)
                    .Where(x => x.StartTime >= startDateOffset && x.EndTime <= endDateOffset)
                    .ToListAsync();

                var technicianGroups = workFlows
                    .Where(x => x.ApproverTechnicianId.HasValue && x.ApproverTechnician != null)
                    .GroupBy(x => x.ApproverTechnicianId!.Value);

                var technicians = new List<TechnicianOvertimeSummaryDto>();
                double totalOvertimeHours = 0;
                int totalJobs = 0;

                foreach (var group in technicianGroups)
                {
                    var techId = group.Key;
                    var techWorkFlows = group.ToList();
                    var technician = techWorkFlows.First().ApproverTechnician!;
                    var techRequestNos = techWorkFlows.Select(x => x.RequestNo).ToHashSet();

                    var techServices = technicalServices
                        .Where(x => techRequestNos.Contains(x.RequestNo))
                        .ToList();

                    double techOvertimeHours = 0;
                    var overtimeRequestNos = new List<string>();

                    foreach (var ts in techServices)
                    {
                        if (!ts.StartTime.HasValue || !ts.EndTime.HasValue)
                            continue;

                        var overtimeResult = await CalculateOvertimeWithBreakdownAsync(ts.StartTime.Value, ts.EndTime.Value);

                        if (overtimeResult.TotalOvertimeHours > 0)
                        {
                            techOvertimeHours += overtimeResult.TotalOvertimeHours;
                            overtimeRequestNos.Add(ts.RequestNo);
                        }
                    }

                    if (techOvertimeHours > 0)
                    {
                        technicians.Add(new TechnicianOvertimeSummaryDto
                        {
                            TechnicianId = techId,
                            Name = technician.Name,
                            Code = technician.Code,
                            TotalOvertimeHours = Math.Round(techOvertimeHours, 2),
                            TotalJobs = overtimeRequestNos.Count,
                            RequestNos = overtimeRequestNos.Distinct().ToList()
                        });

                        totalOvertimeHours += techOvertimeHours;
                        totalJobs += overtimeRequestNos.Count;
                    }
                }

                var summary = new YkbAllTechniciansOvertimeSummaryDto
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalOvertimeHours = Math.Round(totalOvertimeHours, 2),
                    TotalJobs = totalJobs,
                    TotalTechnicians = technicians.Count,
                    Technicians = technicians.OrderByDescending(x => x.TotalOvertimeHours).ToList()
                };

                return ResponseModel<YkbAllTechniciansOvertimeSummaryDto>.Success(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "YKB - GetAllTechniciansOvertimeSummaryAsync");
                return ResponseModel<YkbAllTechniciansOvertimeSummaryDto>.Fail(
                    $"Fazla mesai özeti getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<byte[]>> ExportOvertimeReportToExcelAsync(
            long? technicianId,
            DateTime startDate,
            DateTime endDate)
        {
            try
            {
                ResponseModel<YkbTechnicianOvertimeReportDto>? singleReport = null;
                ResponseModel<YkbAllTechniciansOvertimeSummaryDto>? summaryReport = null;

                if (technicianId.HasValue)
                {
                    singleReport = await GetTechnicianOvertimeReportAsync(
                        technicianId.Value,
                        startDate,
                        endDate,
                        includeCustomerDetails: true);

                    if (!singleReport.IsSuccess)
                        return ResponseModel<byte[]>.Fail(singleReport.Message, singleReport.StatusCode);
                }
                else
                {
                    summaryReport = await GetAllTechniciansOvertimeSummaryAsync(startDate, endDate);

                    if (!summaryReport.IsSuccess)
                        return ResponseModel<byte[]>.Fail(summaryReport.Message, summaryReport.StatusCode);
                }

                using var workbook = new XLWorkbook();

                if (singleReport != null && singleReport.Data != null)
                {
                    var worksheet = workbook.Worksheets.Add("YKB Fazla Mesai Raporu");

                    worksheet.Cell(1, 1).Value = "Teknisyen Adı";
                    worksheet.Cell(1, 2).Value = singleReport.Data.Name;
                    worksheet.Cell(2, 1).Value = "Teknisyen Kodu";
                    worksheet.Cell(2, 2).Value = singleReport.Data.Code;
                    worksheet.Cell(3, 1).Value = "Başlangıç Tarihi";
                    worksheet.Cell(3, 2).Value = singleReport.Data.StartDate.ToString("dd.MM.yyyy");
                    worksheet.Cell(4, 1).Value = "Bitiş Tarihi";
                    worksheet.Cell(4, 2).Value = singleReport.Data.EndDate.ToString("dd.MM.yyyy");
                    worksheet.Cell(5, 1).Value = "Toplam Fazla Mesai (Saat)";
                    worksheet.Cell(5, 2).Value = singleReport.Data.TotalOvertimeHours;
                    worksheet.Cell(6, 1).Value = "Toplam İş Sayısı";
                    worksheet.Cell(6, 2).Value = singleReport.Data.TotalJobs;

                    int row = 8;
                    worksheet.Cell(row, 1).Value = "Talep No";
                    worksheet.Cell(row, 2).Value = "Talep Başlığı";
                    worksheet.Cell(row, 3).Value = "Tarih";
                    worksheet.Cell(row, 4).Value = "Başlangıç";
                    worksheet.Cell(row, 5).Value = "Bitiş";
                    worksheet.Cell(row, 6).Value = "Toplam Saat";
                    worksheet.Cell(row, 7).Value = "Normal Saat";
                    worksheet.Cell(row, 8).Value = "Fazla Mesai";
                    worksheet.Cell(row, 9).Value = "Breakdown Detay";
                    worksheet.Cell(row, 10).Value = "Müşteri";
                    worksheet.Cell(row, 11).Value = "Şehir";

                    var headerRange = worksheet.Range(row, 1, row, 11);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                    row++;
                    foreach (var job in singleReport.Data.Jobs)
                    {
                        worksheet.Cell(row, 1).Value = job.RequestNo;
                        worksheet.Cell(row, 2).Value = job.RequestTitle;
                        worksheet.Cell(row, 3).Value = job.Date.ToString("dd.MM.yyyy");
                        worksheet.Cell(row, 4).Value = job.StartTime.ToString("dd.MM.yyyy HH:mm");
                        worksheet.Cell(row, 5).Value = job.EndTime.ToString("dd.MM.yyyy HH:mm");
                        worksheet.Cell(row, 6).Value = job.TotalWorkingHours;
                        worksheet.Cell(row, 7).Value = job.NormalHours;
                        worksheet.Cell(row, 8).Value = job.OvertimeHours;

                        var breakdownText = string.Join(", ", job.OvertimeBreakdown.Select(b => 
                            $"{b.PolicyName} ({b.Hours:F2} sa)"));
                        worksheet.Cell(row, 9).Value = breakdownText;

                        worksheet.Cell(row, 10).Value = job.CustomerName ?? "";
                        worksheet.Cell(row, 11).Value = job.CustomerCity ?? "";
                        row++;
                    }

                    worksheet.Columns().AdjustToContents();
                }
                else if (summaryReport != null && summaryReport.Data != null)
                {
                    var worksheet = workbook.Worksheets.Add("YKB Fazla Mesai Özeti");

                    worksheet.Cell(1, 1).Value = "Başlangıç Tarihi";
                    worksheet.Cell(1, 2).Value = summaryReport.Data.StartDate.ToString("dd.MM.yyyy");
                    worksheet.Cell(2, 1).Value = "Bitiş Tarihi";
                    worksheet.Cell(2, 2).Value = summaryReport.Data.EndDate.ToString("dd.MM.yyyy");
                    worksheet.Cell(3, 1).Value = "Toplam Fazla Mesai (Saat)";
                    worksheet.Cell(3, 2).Value = summaryReport.Data.TotalOvertimeHours;
                    worksheet.Cell(4, 1).Value = "Toplam İş Sayısı";
                    worksheet.Cell(4, 2).Value = summaryReport.Data.TotalJobs;
                    worksheet.Cell(5, 1).Value = "Teknisyen Sayısı";
                    worksheet.Cell(5, 2).Value = summaryReport.Data.TotalTechnicians;

                    int row = 7;
                    worksheet.Cell(row, 1).Value = "Teknisyen Kodu";
                    worksheet.Cell(row, 2).Value = "Teknisyen Adı";
                    worksheet.Cell(row, 3).Value = "Fazla Mesai (Saat)";
                    worksheet.Cell(row, 4).Value = "İş Sayısı";

                    var headerRange = worksheet.Range(row, 1, row, 4);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                    row++;
                    foreach (var tech in summaryReport.Data.Technicians)
                    {
                        worksheet.Cell(row, 1).Value = tech.Code;
                        worksheet.Cell(row, 2).Value = tech.Name;
                        worksheet.Cell(row, 3).Value = tech.TotalOvertimeHours;
                        worksheet.Cell(row, 4).Value = tech.TotalJobs;
                        row++;
                    }

                    worksheet.Columns().AdjustToContents();
                }

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var excelBytes = stream.ToArray();

                return ResponseModel<byte[]>.Success(excelBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "YKB - ExportOvertimeReportToExcelAsync");
                return ResponseModel<byte[]>.Fail(
                    $"Excel export sırasında hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        // Hesaplama metodları (Normal OvertimeReportService'teki ile aynı)
        private async Task<(double TotalHours, double NormalHours, double TotalOvertimeHours, List<OvertimeBreakdownDto> Breakdown)> 
            CalculateOvertimeWithBreakdownAsync(DateTimeOffset startTime, DateTimeOffset endTime)
        {
            var totalHours = (endTime - startTime).TotalHours;
            var normalHours = 0.0;
            var totalOvertimeHours = 0.0;
            var breakdown = new List<OvertimeBreakdownDto>();

            if (startTime.Date == endTime.Date)
            {
                var date = DateOnly.FromDateTime(startTime.Date);
                var policiesResult = await _workingHourPolicyService.GetPoliciesForDateAsync(date);

                WorkingHourPolicyGetDto? policy = null;
                if (policiesResult.IsSuccess && policiesResult.Data != null && policiesResult.Data.Any())
                {
                    policy = policiesResult.Data.First();
                }

                var (dayNormal, dayOvertime, dayBreakdown) = CalculateSingleDayOvertime(
                    startTime, 
                    endTime, 
                    policy?.WorkStartTime, 
                    policy?.WorkEndTime,
                    policy?.Name ?? "Hafta İçi Mesai Saatleri",
                    policy?.PolicyTypeText ?? "Hafta İçi Default");

                normalHours += dayNormal;
                totalOvertimeHours += dayOvertime;
                breakdown.AddRange(dayBreakdown);
            }
            else
            {
                var currentDate = startTime.Date;

                while (currentDate <= endTime.Date)
                {
                    var date = DateOnly.FromDateTime(currentDate);
                    var policiesResult = await _workingHourPolicyService.GetPoliciesForDateAsync(date);

                    WorkingHourPolicyGetDto? policy = null;
                    if (policiesResult.IsSuccess && policiesResult.Data?.Any() == true)
                    {
                        policy = policiesResult.Data.First();
                    }

                    DateTimeOffset dayStart, dayEnd;

                    if (currentDate == startTime.Date)
                    {
                        dayStart = startTime;
                        dayEnd = new DateTimeOffset(currentDate.AddDays(1), startTime.Offset);
                    }
                    else if (currentDate == endTime.Date)
                    {
                        dayStart = new DateTimeOffset(currentDate, startTime.Offset);
                        dayEnd = endTime;
                    }
                    else
                    {
                        dayStart = new DateTimeOffset(currentDate, startTime.Offset);
                        dayEnd = new DateTimeOffset(currentDate.AddDays(1), startTime.Offset);
                    }

                    var (dayNormal, dayOvertime, dayBreakdown) = CalculateSingleDayOvertime(
                        dayStart, 
                        dayEnd, 
                        policy?.WorkStartTime, 
                        policy?.WorkEndTime,
                        policy?.Name ?? "Hafta İçi Mesai Saatleri",
                        policy?.PolicyTypeText ?? "Hafta İçi Default");

                    normalHours += dayNormal;
                    totalOvertimeHours += dayOvertime;
                    breakdown.AddRange(dayBreakdown);

                    currentDate = currentDate.AddDays(1);
                }
            }

            return (totalHours, normalHours, totalOvertimeHours, breakdown);
        }

        private (double NormalHours, double OvertimeHours, List<OvertimeBreakdownDto> Breakdown) 
            CalculateSingleDayOvertime(
                DateTimeOffset startTime, 
                DateTimeOffset endTime,
                TimeOnly? workStart,
                TimeOnly? workEnd,
                string policyName,
                string policyTypeText)
        {
            var normalHours = 0.0;
            var overtimeHours = 0.0;
            var breakdown = new List<OvertimeBreakdownDto>();

            var duration = (endTime - startTime).TotalHours;

            if (!workStart.HasValue || !workEnd.HasValue)
            {
                overtimeHours = duration;

                breakdown.Add(new OvertimeBreakdownDto
                {
                    PolicyName = policyName,
                    PolicyTypeText = policyTypeText,
                    Hours = Math.Round(duration, 2),
                    StartTime = startTime.DateTime,
                    EndTime = endTime.DateTime,
                    Description = "Tüm gün fazla mesai"
                });

                return (normalHours, overtimeHours, breakdown);
            }

            var workStartDateTime = startTime.Date.Add(workStart.Value.ToTimeSpan());
            var workEndDateTime = startTime.Date.Add(workEnd.Value.ToTimeSpan());

            var actualStart = startTime;
            var actualEnd = endTime;

            // Mesai öncesi
            if (actualStart < workStartDateTime && actualEnd > actualStart)
            {
                var beforeEnd = actualEnd < workStartDateTime ? actualEnd : new DateTimeOffset(workStartDateTime, startTime.Offset);
                var beforeHours = (beforeEnd - actualStart).TotalHours;

                if (beforeHours > 0)
                {
                    overtimeHours += beforeHours;
                    breakdown.Add(new OvertimeBreakdownDto
                    {
                        PolicyName = "Mesai öncesi",
                        PolicyTypeText = "Mesai Dışı",
                        Hours = Math.Round(beforeHours, 2),
                        StartTime = actualStart.DateTime,
                        EndTime = beforeEnd.DateTime,
                        Description = $"Normal mesai başlangıcından ({workStart.Value:HH:mm}) önce"
                    });
                }

                actualStart = beforeEnd;
            }

            // Normal mesai
            if (actualStart < workEndDateTime && actualEnd > actualStart)
            {
                var normalStart = actualStart < workStartDateTime ? new DateTimeOffset(workStartDateTime, startTime.Offset) : actualStart;
                var normalEnd = actualEnd > workEndDateTime ? new DateTimeOffset(workEndDateTime, startTime.Offset) : actualEnd;

                if (normalEnd > normalStart)
                {
                    normalHours = (normalEnd - normalStart).TotalHours;
                    actualStart = normalEnd;
                }
            }

            // Mesai sonrası
            if (actualEnd > workEndDateTime && actualStart < actualEnd)
            {
                var afterStart = actualStart > workEndDateTime ? actualStart : new DateTimeOffset(workEndDateTime, startTime.Offset);
                var afterHours = (actualEnd - afterStart).TotalHours;

                if (afterHours > 0)
                {
                    overtimeHours += afterHours;
                    breakdown.Add(new OvertimeBreakdownDto
                    {
                        PolicyName = "Mesai Sonrası",
                        PolicyTypeText = "Mesai Dışı",
                        Hours = Math.Round(afterHours, 2),
                        StartTime = afterStart.DateTime,
                        EndTime = actualEnd.DateTime,
                        Description = $"Normal mesai bitiçinden ({workEnd.Value:HH:mm}) sonra"
                    });
                }
            }

            return (normalHours, overtimeHours, breakdown);
        }
    }
}