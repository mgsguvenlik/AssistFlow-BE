using Core.Abstractions;
using Core.Enums;
using Core.Utilities.Constants;

namespace Core.Common
{

    public class ResponseModel : BaseResponseModel
    {
        public ResponseModel() : base() { }

        public ResponseModel(bool result, StatusCode statusCode) : base(result, statusCode) { }

        public ResponseModel(bool isSuccess, string message, StatusCode statusCode)
            : base(isSuccess, message, statusCode) { }

        // Helper'lar (isteğe bağlı)
        public static ResponseModel Success(string? message = null, StatusCode status = StatusCode.Ok)
            => new ResponseModel(true, message ?? Messages.Success, status);

        public static ResponseModel Fail(string message, StatusCode status = StatusCode.BadRequest,
                                         Dictionary<string, string[]>? validation = null)
            => new ResponseModel(false, message, status) { ValidationErrors = validation };
    }

    public class ResponseModel<T> : BaseResponseModel
    {
        public T? Data { get; set; }

        public ResponseModel() : base() { }

        public ResponseModel(bool result, StatusCode statusCode, T? data) : base(result, statusCode)
        {
            Data = data;
        }

        public ResponseModel(bool isSuccess, T? data, string message, StatusCode statusCode)
            : base(isSuccess, message, statusCode)
        {
            Data = data;
        }

        // Helper'lar (kullanışlı)
        public static ResponseModel<T> Success(T data, string? message = null, StatusCode status = StatusCode.Ok)
            => new ResponseModel<T>(true, data, message ?? Messages.Success, status);

        public static ResponseModel<T> Fail(string message, StatusCode status = StatusCode.BadRequest,
                                            T? data = default,
                                            Dictionary<string, string[]>? validation = null)
            => new ResponseModel<T>(false, data, message, status) { ValidationErrors = validation };
    }
}
