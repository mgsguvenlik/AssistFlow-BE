using ClosedXML.Excel;
using Model.Dtos.ArchiveExport;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbArchive;
using Model.Dtos.WorkFlowDtos.WorkFlowArchive;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbArchive;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Business.Utilities.Export
{

    public static class ArchiveExcelExportHelper
    {
        // ------------------------------------------------------------------
        // WorkFlow (Bireysel)
        // ------------------------------------------------------------------
        public static void AddSnapshotDetailRows(string requestNo, WorkFlowArchiveSnapshotDto snapshot, List<ArchiveExportDetailRow> sink)
        {
            void Add(string section, string field, object? value) =>
                sink.Add(new ArchiveExportDetailRow
                {
                    RequestNo = requestNo,
                    Section = section,
                    Field = field,
                    Value = value?.ToString()
                });

            var sr = snapshot.ServicesRequest;
            if (sr != null)
            {
                Add("Servis Talebi", "OracleNo", sr.OracleNo);
                Add("Servis Talebi", "ServisTarihi", sr.ServicesDate);
                Add("Servis Talebi", "PlanlananTamamlanmaTarihi", sr.PlannedCompletionDate);
                Add("Servis Talebi", "MaliyetDurumu", sr.ServicesCostStatus);
                Add("Servis Talebi", "Açıklama", sr.Description);
                Add("Servis Talebi", "ÜrünGerekliMi", sr.IsProductRequirement);
                Add("Servis Talebi", "Öncelik", sr.Priority);
                Add("Servis Talebi", "TalepDurumu", sr.ServicesRequestStatus);
                Add("Servis Talebi", "ServisTürü", sr.ServiceType?.Name);
                Add("Servis Talebi", "SözleşmeNo", sr.ServiceType?.ContractNumber);
            }

            var c = snapshot.Customer;
            if (c != null)
            {
                Add("Müşteri", "İletişimAdı1", c.ContactName1);
                Add("Müşteri", "AbonelikŞirketi", c.SubscriberCompany);
            }

            if (snapshot.ApproverTechnician != null)
                Add("Onaylayan Teknisyen", "Ad", snapshot.ApproverTechnician.TechnicianName);

            if (snapshot.WorkFlow != null)
                Add("İş Akışı", "Durum", snapshot.WorkFlow.WorkFlowStatus);

            if (snapshot.TechnicalService != null)
                Add("Teknik Servis", "Kayıt", JsonConvert.SerializeObject(snapshot.TechnicalService));

            if (snapshot.Warehouse != null)
                Add("Depo", "Kayıt", JsonConvert.SerializeObject(snapshot.Warehouse));

            if (snapshot.Pricing != null)
                Add("Fiyatlandırma", "Kayıt", JsonConvert.SerializeObject(snapshot.Pricing));

            if (snapshot.FinalApproval != null)
                Add("Nihai Onay", "Kayıt", JsonConvert.SerializeObject(snapshot.FinalApproval));

            if (snapshot.Products != null)
                for (int i = 0; i < snapshot.Products.Count; i++)
                    Add("Ürünler", $"Ürün[{i}]", JsonConvert.SerializeObject(snapshot.Products[i]));

            if (snapshot.WorkFlowReviewLogs != null)
                for (int i = 0; i < snapshot.WorkFlowReviewLogs.Count; i++)
                    Add("İnceleme Logları", $"Log[{i}]", JsonConvert.SerializeObject(snapshot.WorkFlowReviewLogs[i]));
        }

        // ------------------------------------------------------------------
        // Qnb
        // ------------------------------------------------------------------
        public static void AddSnapshotDetailRows(string requestNo, QnbWorkFlowArchiveSnapshotDto snapshot, List<ArchiveExportDetailRow> sink)
        {
            void Add(string section, string field, object? value) =>
                sink.Add(new ArchiveExportDetailRow
                {
                    RequestNo = requestNo,
                    Section = section,
                    Field = field,
                    Value = value?.ToString()
                });

            var sr = snapshot.ServicesRequest;
            if (sr != null)
            {
                Add("Servis Talebi", "OracleNo", "");
                Add("Servis Talebi", "ServisTarihi", sr.ServicesDate);
                Add("Servis Talebi", "PlanlananTamamlanmaTarihi", sr.PlannedCompletionDate);
                Add("Servis Talebi", "MaliyetDurumu", sr.ServicesCostStatus);
                Add("Servis Talebi", "Açıklama", sr.Description);
                Add("Servis Talebi", "ÜrünGerekliMi", sr.IsProductRequirement);
                Add("Servis Talebi", "Öncelik", sr.Priority);
                Add("Servis Talebi", "TalepDurumu", sr.ServicesRequestStatus);
                Add("Servis Talebi", "ServisTürü", sr.ServiceType?.Name);
                Add("Servis Talebi", "SözleşmeNo", sr.ServiceType?.ContractNumber);
            }

            var c = snapshot.Customer;
            if (c != null)
            {
                Add("Müşteri", "İletişimAdı1", c.ContactName1);
                Add("Müşteri", "AbonelikŞirketi", c.SubscriberCompany);
            }

            if (snapshot.ApproverTechnician != null)
                Add("Onaylayan Teknisyen", "Ad", snapshot.ApproverTechnician.TechnicianName);

            if (snapshot.WorkFlow != null)
                Add("İş Akışı", "Durum", snapshot.WorkFlow.WorkFlowStatus);

            if (snapshot.TechnicalService != null)
                Add("Teknik Servis", "Kayıt", JsonConvert.SerializeObject(snapshot.TechnicalService));

            if (snapshot.Warehouse != null)
                Add("Depo", "Kayıt", JsonConvert.SerializeObject(snapshot.Warehouse));

            if (snapshot.Pricing != null)
                Add("Fiyatlandırma", "Kayıt", JsonConvert.SerializeObject(snapshot.Pricing));

            if (snapshot.FinalApproval != null)
                Add("Nihai Onay", "Kayıt", JsonConvert.SerializeObject(snapshot.FinalApproval));

            if (snapshot.Products != null)
                for (int i = 0; i < snapshot.Products.Count; i++)
                    Add("Ürünler", $"Ürün[{i}]", JsonConvert.SerializeObject(snapshot.Products[i]));

            if (snapshot.WorkFlowReviewLogs != null)
                for (int i = 0; i < snapshot.WorkFlowReviewLogs.Count; i++)
                    Add("İnceleme Logları", $"Log[{i}]", JsonConvert.SerializeObject(snapshot.WorkFlowReviewLogs[i]));
        }

        // ------------------------------------------------------------------
        // Ykb
        // ------------------------------------------------------------------
        public static void AddSnapshotDetailRows(string requestNo, YkbWorkFlowArchiveSnapshotDto snapshot, List<ArchiveExportDetailRow> sink)
        {
            void Add(string section, string field, object? value) =>
                sink.Add(new ArchiveExportDetailRow
                {
                    RequestNo = requestNo,
                    Section = section,
                    Field = field,
                    Value = value?.ToString()
                });

            var sr = snapshot.ServicesRequest;
            if (sr != null)
            {
                Add("Servis Talebi", "OracleNo", "");
                Add("Servis Talebi", "ServisTarihi", sr.ServicesDate);
                Add("Servis Talebi", "PlanlananTamamlanmaTarihi", sr.PlannedCompletionDate);
                Add("Servis Talebi", "MaliyetDurumu", sr.ServicesCostStatus);
                Add("Servis Talebi", "Açıklama", sr.Description);
                Add("Servis Talebi", "ÜrünGerekliMi", sr.IsProductRequirement);
                Add("Servis Talebi", "Öncelik", sr.Priority);
                Add("Servis Talebi", "TalepDurumu", sr.ServicesRequestStatus);
                Add("Servis Talebi", "ServisTürü", sr.ServiceType?.Name);
                Add("Servis Talebi", "SözleşmeNo", sr.ServiceType?.ContractNumber);
            }

            var c = snapshot.Customer;
            if (c != null)
            {
                Add("Müşteri", "İletişimAdı1", c.ContactName1);
                Add("Müşteri", "AbonelikŞirketi", c.SubscriberCompany);
            }

            if (snapshot.ApproverTechnician != null)
                Add("Onaylayan Teknisyen", "Ad", snapshot.ApproverTechnician.TechnicianName);

            if (snapshot.WorkFlow != null)
                Add("İş Akışı", "Durum", snapshot.WorkFlow.WorkFlowStatus);

            if (snapshot.TechnicalService != null)
                Add("Teknik Servis", "Kayıt", JsonConvert.SerializeObject(snapshot.TechnicalService));

            if (snapshot.Warehouse != null)
                Add("Depo", "Kayıt", JsonConvert.SerializeObject(snapshot.Warehouse));  
            if (snapshot.Pricing != null)
                Add("Fiyatlandırma", "Kayıt", JsonConvert.SerializeObject(snapshot.Pricing));

            if (snapshot.FinalApproval != null)
                Add("Nihai Onay", "Kayıt", JsonConvert.SerializeObject(snapshot.FinalApproval));
            if (snapshot.Products != null)
                for (int i = 0; i < snapshot.Products.Count; i++)
                    Add("Ürünler", $"Ürün[{i}]", JsonConvert.SerializeObject(snapshot.Products[i]));

            if (snapshot.WorkFlowReviewLogs != null)
                for (int i = 0; i < snapshot.WorkFlowReviewLogs.Count; i++)
                    Add("İnceleme Logları", $"Log[{i}]", JsonConvert.SerializeObject(snapshot.WorkFlowReviewLogs[i]));
        }

        // ------------------------------------------------------------------
        // Ortak: Workbook oluşturma (üç modül de bunu kullanır)
        // ------------------------------------------------------------------
        public static byte[] BuildWorkbook(
            List<ArchiveExportSummaryRow> summary,
            List<ArchiveExportDetailRow> detail,
            List<ArchiveExportImageRow> images)
        {
            using var wb = new XLWorkbook();

            var wsSummary = wb.Worksheets.Add("Özet");
            string[] summaryHeaders = { "Id", "Talep No", "Müşteri", "Teknisyen", "İş Akış Durumu", "Servis Türü", "İş Emri Türleri", "Arşiv Nedeni", "Arşiv Tarihi" };
            for (int i = 0; i < summaryHeaders.Length; i++) wsSummary.Cell(1, i + 1).Value = summaryHeaders[i];
            int r = 2;
            foreach (var s in summary)
            {
                wsSummary.Cell(r, 1).Value = s.Id;
                wsSummary.Cell(r, 2).Value = s.RequestNo;
                wsSummary.Cell(r, 3).Value = s.CustomerName;
                wsSummary.Cell(r, 4).Value = s.TechnicianName;
                wsSummary.Cell(r, 5).Value = s.WorkFlowStatus;
                wsSummary.Cell(r, 6).Value = s.ServiceTypeName;
                wsSummary.Cell(r, 7).Value = s.WorkOrderTypes;
                wsSummary.Cell(r, 8).Value = s.ArchiveReason;
                wsSummary.Cell(r, 9).Value = s.ArchivedAt;
                wsSummary.Cell(r, 9).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
                r++;
            }
            FormatSheet(wsSummary);

            var wsDetail = wb.Worksheets.Add("Detay");
            string[] detailHeaders = { "Talep No", "Bölüm", "Alan", "Değer" };
            for (int i = 0; i < detailHeaders.Length; i++) wsDetail.Cell(1, i + 1).Value = detailHeaders[i];
            r = 2;
            foreach (var d in detail)
            {
                wsDetail.Cell(r, 1).Value = d.RequestNo;
                wsDetail.Cell(r, 2).Value = d.Section;
                wsDetail.Cell(r, 3).Value = d.Field;
                wsDetail.Cell(r, 4).Value = d.Value;
                r++;
            }
            FormatSheet(wsDetail);

            var wsImages = wb.Worksheets.Add("Resimler");
            string[] imgHeaders = { "Talep No", "Grup", "Resim Id", "Açıklama", "Url" };
            for (int i = 0; i < imgHeaders.Length; i++) wsImages.Cell(1, i + 1).Value = imgHeaders[i];
            r = 2;
            foreach (var img in images)
            {
                wsImages.Cell(r, 1).Value = img.RequestNo;
                wsImages.Cell(r, 2).Value = img.ImageGroup;
                wsImages.Cell(r, 3).Value = img.ImageId;
                wsImages.Cell(r, 4).Value = img.Caption;
                var cell = wsImages.Cell(r, 5);
                cell.Value = img.NormalizedUrl;
                if (!string.IsNullOrWhiteSpace(img.NormalizedUrl))
                {
                    cell.SetHyperlink(new XLHyperlink(img.NormalizedUrl));
                    cell.Style.Font.Underline = XLFontUnderlineValues.Single;
                    cell.Style.Font.FontColor = XLColor.Blue;
                }
                r++;
            }
            FormatSheet(wsImages);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static void FormatSheet(IXLWorksheet ws)
        {
            ws.SheetView.FreezeRows(1);
            ws.RangeUsed()?.SetAutoFilter();
            ws.Row(1).Style.Font.Bold = true;
            ws.Columns().AdjustToContents();
        }
    }
}

