namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbReviewLog
{
    public class QnbCustomerReviewMessageDto
    {
        public string Message { get; set; } = string.Empty;
        public string RequestNo { get; set; } = string.Empty;
        public string FromStepCode { get; set; } = string.Empty;
        public string ToStepCode { get; set; } = string.Empty;
    }
}