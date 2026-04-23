using Core.Common;
using Model.Dtos.OvertimeReport;

namespace Business.Interfaces.Ykb
{
    public interface IYkbOvertimeReportService
    {
        /// <summary>
        /// YKB - Belirli bir teknisyenin fazla mesai raporunu getirir
        /// </summary>
        Task<ResponseModel<YkbTechnicianOvertimeReportDto>> GetTechnicianOvertimeReportAsync(
            long technicianId,
            DateTime startDate,
            DateTime endDate,
            bool includeCustomerDetails = false);

        /// <summary>
        /// YKB - Tüm teknisyenlerin fazla mesai özetini getirir
        /// </summary>
        Task<ResponseModel<YkbAllTechniciansOvertimeSummaryDto>> GetAllTechniciansOvertimeSummaryAsync(
            DateTime startDate,
            DateTime endDate);

        /// <summary>
        /// YKB - Fazla mesai raporunu Excel formatýnda export eder
        /// </summary>
        Task<ResponseModel<byte[]>> ExportOvertimeReportToExcelAsync(
            long? technicianId,
            DateTime startDate,
            DateTime endDate);
    }
}