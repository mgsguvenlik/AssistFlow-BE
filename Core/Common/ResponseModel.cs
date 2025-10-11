using Core.Abstractions;
using Core.Enums;
using Core.Utilities.Constants;

namespace Core.Common
{
    public class ResponseModel : BaseResponseModel
    {
        public ResponseModel() : base() { }

        // Mesajsız kısa ctor (string parametre YOK!)
        public ResponseModel(bool isSuccess, StatusCode statusCode)
            : base(isSuccess, statusCode) { }

        // Mesajlı ctor
        public ResponseModel(bool isSuccess, string message, StatusCode statusCode)
            : base(isSuccess, message, statusCode) { }

        // Helper'lar
        public static ResponseModel Success(string? message = null, StatusCode status = StatusCode.Ok)
            => message is null
                ? new ResponseModel(true, status)
                : new ResponseModel(true, message, status);

        public static ResponseModel Fail(string message, StatusCode status = StatusCode.BadRequest,
                                         Dictionary<string, string[]>? validation = null)
            => new ResponseModel(false, message, status) { ValidationErrors = validation };
    }

    public class ResponseModel<T> : BaseResponseModel
    {
        public T? Data { get; set; }

        public ResponseModel() : base() { }

        public ResponseModel(bool isSuccess, StatusCode statusCode, T? data)
            : base(isSuccess, statusCode)
        {
            Data = data;
        }

        public ResponseModel(bool isSuccess, T? data, string message, StatusCode statusCode)
            : base(isSuccess, message, statusCode)
        {
            Data = data;
        }

        // Helper'lar
        public static ResponseModel<T> Success(T data, string? message = null, StatusCode status = StatusCode.Ok)
            => message is null
                ? new ResponseModel<T>(true, status, data)
                : new ResponseModel<T>(true, data, message, status);

        public static ResponseModel<T> Fail(string message, StatusCode status = StatusCode.BadRequest,
                                            T? data = default,
                                            Dictionary<string, string[]>? validation = null)
            => new ResponseModel<T>(false, data, message, status) { ValidationErrors = validation };
    }
}
