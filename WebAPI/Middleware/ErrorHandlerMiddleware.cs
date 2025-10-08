using Core.Common;
using Core.Enums;
using Core.Utilities.Constants;
using System.Net;
using System.Text;
using System.Text.Json;

namespace WebAPI.Middleware
{
    public class ErrorHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlerMiddleware> _logger;

        public ErrorHandlerMiddleware(RequestDelegate next, ILogger<ErrorHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var originalBodyStream = context.Response.Body;

            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                var requestInfo = await GetRequestDetails(context.Request);
                _logger.LogInformation(Messages.ReceivedRequest, requestInfo);

                await _next(context); // Middleware zinciri çağrılır

                // Başarılı durumda response loglanır ve geri yazılır
                context.Response.Body.Seek(0, SeekOrigin.Begin);
                var responseInfo = await GetResponseDetails(context);
                _logger.LogInformation(Messages.ResponseInfo, responseInfo);

                context.Response.Body.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
            catch (Exception ex)
            {
                // Hata durumunda sadece logla ve hatayı yönet
                _logger.LogError(ex, Messages.ErrorOccurred, ex.Message);

                // Body restore edilmezse response’a yazılamaz
                context.Response.Body = originalBodyStream;

                await HandleExceptionAsync(context, ex);
            }
            finally
            {
                context.Response.Body = originalBodyStream; // HER DURUMDA geri al
            }
        }

        private static async Task<string> GetRequestDetails(HttpRequest request)
        {
            request.EnableBuffering(); // Allow re-reading the body stream

            var requestBody = string.Empty;
            if (request.ContentLength > 0 && request.Body.CanSeek)
            {
                request.Body.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                request.Body.Seek(0, SeekOrigin.Begin);
            }

            var headers = string.Join("; ", request.Headers.Select(h => $"{h.Key}: {h.Value}"));

            return $"Method: {request.Method}, Path: {request.Path}, QueryString: {request.QueryString}, Headers: [{headers}], Body: {requestBody}";
        }

        private static async Task<string> GetResponseDetails(HttpContext context)
        {
            var response = context.Response;
            response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
            var responseBody = await reader.ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);

            var headers = string.Join("; ", response.Headers.Select(h => $"{h.Key}: {h.Value}"));

            return $"StatusCode: {response.StatusCode}, Headers: [{headers}], Body: {responseBody}";
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = CommonConstants.ApplicationJson;
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new ResponseModel
            {
                Message = $"{Messages.InternalServerErrorDetailed} {exception.Message}",
                StatusCode = StatusCode.Error,
                IsSuccess = false
            };

            return context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
