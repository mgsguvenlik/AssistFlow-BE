namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbCustomerForm
{
    public class EkbCustomerAgreementDto
    {
        public string RequestNo { get; set; } = string.Empty;

        /// <summary>
        /// true: Mutabık Kalındı
        /// false: Geri Gönder
        /// </summary>
        public bool IsAgreed { get; set; }

        /// <summary>
        /// EKB’nin yazdığı açıklama
        /// </summary>
        public string? CustomerNote { get; set; }
    }
}
