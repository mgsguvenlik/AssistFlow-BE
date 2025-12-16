namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbCustomerForm
{
    public class YkbCustomerAgreementDto
    {
        public string RequestNo { get; set; } = string.Empty;

        /// <summary>
        /// true: Mutabık Kalındı
        /// false: Geri Gönder
        /// </summary>
        public bool IsAgreed { get; set; }

        /// <summary>
        /// YKB’nin yazdığı açıklama
        /// </summary>
        public string? CustomerNote { get; set; }
    }
}
