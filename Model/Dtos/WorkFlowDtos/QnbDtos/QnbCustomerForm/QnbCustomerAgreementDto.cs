namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbCustomerForm
{
    public class QnbCustomerAgreementDto
    {
        public string RequestNo { get; set; } = string.Empty;

        /// <summary>
        /// true: Mutabýk Kalýndý
        /// false: Geri Gönder
        /// </summary>
        public bool IsAgreed { get; set; }

        /// <summary>
        /// QNB'nin yazdýðý açýklama
        /// </summary>
        public string? CustomerNote { get; set; }
    }
}