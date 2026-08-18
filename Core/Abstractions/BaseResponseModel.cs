using Core.Enums;

namespace Core.Abstractions
{


    public abstract class BaseResponseModel
    {
        public bool IsSuccess { get; set; } = false;
        public string Message { get; set; } = string.Empty;
        public StatusCode StatusCode { get; set; } = StatusCode.Ok;

        // İstersen validasyon hatalarını burada toplayabilirsin
        public Dictionary<string, string[]>? ValidationErrors { get; set; }

        protected BaseResponseModel() { }

        protected BaseResponseModel(bool result, StatusCode statusCode)
        {
            IsSuccess = result;
            StatusCode = statusCode;
        }

        protected BaseResponseModel(bool isSuccess, string message, StatusCode statusCode)
        {
            IsSuccess = isSuccess;
            Message = message;
            StatusCode = statusCode;
        }
    }

}
