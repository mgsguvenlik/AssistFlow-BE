using Core.Abstractions;
using Core.Enums;

namespace Core.Common
{
    public class ResponseModel : BaseResponseModel
    {
        public ResponseModel() : base()
        {
        }

        public ResponseModel(bool result, StatusCode statusCode) : base(result, statusCode)
        {
        }
        public ResponseModel(bool isSuccess, string message, StatusCode statusCode) : base(isSuccess, message, statusCode)
        {
        }
    }

    public class ResponseModel<T> : BaseResponseModel
    {
        public T? Data { get; set; }

        public ResponseModel() : base()
        {
        }

        public ResponseModel(bool result, StatusCode statusCode, T? data) : base(result, statusCode)
        {
            Data = data;
        }

        public ResponseModel(bool isSuccess, T? data, string message, StatusCode statusCode) : base(isSuccess, message, statusCode)
        {
            Data = data;
        }
    }
}
