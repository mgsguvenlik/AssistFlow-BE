using Business.Interfaces.Manitou;
using Core.Settings.Concrete;
using Core.Utilities.IoC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model.Dtos.Manitou;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Business.Services.Manitou
{
    public sealed class ManitouApiService : IManitouApiService
    {
        public const string HttpClientName = "ManitouApiClient";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ManitouApiService> _log;
        private readonly AppSettings _appSettings;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ManitouApiService(
            IHttpClientFactory httpClientFactory,
            ILogger<ManitouApiService> log)
        {
            _httpClientFactory = httpClientFactory;
            _log = log;

            var appSettings = ServiceTool.ServiceProvider
                .GetService<IOptionsSnapshot<AppSettings>>();

            if (appSettings == null)
                throw new InvalidOperationException(
                    "AppSettings servisinden okunamadı.");

            _appSettings = appSettings.Value;
        }

        /// <summary>
        /// Manitou API üzerindeki endpointler için ortak HTTP çağrı metodu.
        /// </summary>
        public async Task<TResponse?> SendAsync<TResponse>(
            HttpMethod method,
            string relativeUrl,
            object? requestBody = null,
            string? accessToken = null,
            CancellationToken cancellationToken = default)
        {
            using var client = CreateClient();

            using var request = new HttpRequestMessage(method, relativeUrl);

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
            }

            if (requestBody is not null)
            {
                request.Content = JsonContent.Create(
                    requestBody,
                    options: JsonOptions);
            }

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var responseContent = await response.Content
                .ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogError(
                    "Manitou API çağrısı başarısız. Method={Method}, Url={Url}, StatusCode={StatusCode}, Response={Response}",
                    method.Method,
                    relativeUrl,
                    (int)response.StatusCode,
                    responseContent);

                throw new ManitouApiException(
                    $"Manitou API çağrısı başarısız. " +
                    $"Method={method.Method}, " +
                    $"Url={relativeUrl}, " +
                    $"StatusCode={(int)response.StatusCode}, " +
                    $"Response={responseContent}",
                    response.StatusCode,
                    responseContent);
            }

            if (response.StatusCode == HttpStatusCode.NoContent ||
                string.IsNullOrWhiteSpace(responseContent))
            {
                return default;
            }

            // JSON olmayan, düz metin response'lar için.
            if (typeof(TResponse) == typeof(string))
            {
                return (TResponse)(object)responseContent;
            }

            try
            {
                return JsonSerializer.Deserialize<TResponse>(
                    responseContent,
                    JsonOptions);
            }
            catch (JsonException ex)
            {
                _log.LogError(
                    ex,
                    "Manitou API response deserialize edilemedi. Type={Type}, Url={Url}, Response={Response}",
                    typeof(TResponse).Name,
                    relativeUrl,
                    responseContent);

                throw new InvalidOperationException(
                    $"Manitou API response '{typeof(TResponse).Name}' tipine dönüştürülemedi.",
                    ex);
            }
        }

        public async Task<string?> LoginAsync(
            CancellationToken cancellationToken = default)
        {
            var request = new ManitouLoginRequest
            {
                Name = _appSettings.ManitouUserName,
                Password = _appSettings.ManitouPassword,
                AuthenticationType = _appSettings.ManitouAuthenticationType,
                AcceptedEula = false,
                NewPassword = string.Empty
            };

            try
            {
                var response = await SendAsync<ManitouLoginResponse>(
                    HttpMethod.Post,
                    "manitou/account/login",
                    request,
                    cancellationToken: cancellationToken);

                if (string.IsNullOrWhiteSpace(response?.AccessToken))
                {
                    _log.LogError(
                        "Manitou login response içinde access_token bulunamadı.");

                    return null;
                }

                return response.AccessToken;
            }
            catch (ManitouApiException ex)
            {
                _log.LogError(
                    ex,
                    "Manitou login endpointi başarısız döndü. StatusCode={StatusCode}",
                    (int)ex.StatusCode);

                return null;
            }
        }

        public async Task<List<ManitouContactResult>> GetCustomerGroupsAsync(
            string accessToken,
            CancellationToken cancellationToken = default)
        {
            var requestBody = new List<ManitouSearchRequestItem>
            {
                new()
                {
                    ContactType = 2,
                    Display = 110,
                    FieldNo = 21,
                    Table = 25,
                    Value = "*"
                }
            };

            var url =
                $"api/contactsearch/2/search" +
                $"?maxRows={_appSettings.ManitouCustomerGroupMaxRows}" +
                $"&includeCancelled=false";

            var response = await SendAsync<ManitouSearchResponse>(
                HttpMethod.Post,
                url,
                requestBody,
                accessToken,
                cancellationToken);

            return response?.Results ?? new List<ManitouContactResult>();
        }

        public async Task<List<ManitouContactResult>> GetCustomersByGroupCodeAsync(
            string accessToken,
            string groupCode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(groupCode))
                return new List<ManitouContactResult>();

            var requestBody = new List<ManitouSearchRequestItem>
            {
                new()
                {
                    ContactType = 1,
                    Display = 180,
                    FieldNo = 4,
                    Table = 26,
                    Value = groupCode
                }
            };

            var url =
                $"api/contactsearch/1/search" +
                $"?maxRows={_appSettings.ManitouCustomerMaxRows}" +
                $"&includeCancelled=false";

            var response = await SendAsync<ManitouSearchResponse>(
                HttpMethod.Post,
                url,
                requestBody,
                accessToken,
                cancellationToken);

            return response?.Results ?? new List<ManitouContactResult>();
        }

        private HttpClient CreateClient()
        {
            if (string.IsNullOrWhiteSpace(_appSettings.ManitouBaseUrl))
            {
                throw new InvalidOperationException(
                    "ManitouBaseUrl ayarı boş veya tanımsız.");
            }

            var baseUrl = _appSettings.ManitouBaseUrl.Trim().TrimEnd('/') + "/";

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            {
                throw new InvalidOperationException(
                    $"ManitouBaseUrl geçerli bir URL değil: {baseUrl}");
            }

            var client = _httpClientFactory.CreateClient(HttpClientName);

            client.BaseAddress = baseUri;
            client.Timeout = TimeSpan.FromMinutes(5);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }

        public async Task BeginSystemTestAsync(
            string accessToken,
            int serialNo,
            CancellationToken cancellationToken = default)
        {
            var url = $"api/customers/{serialNo}/systemTest/begin";

            await SendAsync<string>(
                HttpMethod.Post,
                url,
                requestBody: null,
                accessToken: accessToken,
                cancellationToken: cancellationToken);
        }

        public async Task SetCustomerOnTestAsync(
            string accessToken,
            ManitouOnTestRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.SerialNo <= 0)
                throw new ArgumentException("SerialNo zorunludur.", nameof(request));

            var url = $"api/customers/{request.SerialNo}/ontest/on";

            await SendAsync<string>(
                HttpMethod.Put,
                url,
                request,
                accessToken,
                cancellationToken);
        }

        public async Task<List<ManitouSystemTestZoneResult>> QuerySystemTestAsync(
            string accessToken,
            int serialNo,
            CancellationToken cancellationToken = default)
        {
            var url = $"api/customers/{serialNo}/systemTest/query";

            var response = await SendAsync<List<ManitouSystemTestZoneResult>>(
                HttpMethod.Post,
                url,
                requestBody: null,
                accessToken: accessToken,
                cancellationToken: cancellationToken);

            return response ?? new List<ManitouSystemTestZoneResult>();
        }

        public async Task<ManitouCustomerActivityResponse?> GetCustomerActivityAsync(
            string accessToken,
            int serialNo,
            int days = 1,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0)
                days = 1;

            var url = $"api/customers/{serialNo}/activity?days={days}";

            return await SendAsync<ManitouCustomerActivityResponse>(
                HttpMethod.Get,
                url,
                requestBody: null,
                accessToken: accessToken,
                cancellationToken: cancellationToken);
        }

        public async Task<List<ManitouOutOfServiceResult>> GetOutOfServiceAsync(
            string accessToken,
            int serialNo,
            CancellationToken cancellationToken = default)
        {
            if (serialNo <= 0)
                throw new ArgumentException(
                    "Geçerli bir müşteri SerialNo değeri gönderilmelidir.",
                    nameof(serialNo));

            var url = $"api/outofservice/{serialNo}";

            var response = await SendAsync<List<ManitouOutOfServiceResult>>(
                HttpMethod.Get,
                url,
                requestBody: null,
                accessToken: accessToken,
                cancellationToken: cancellationToken);

            return response ?? new List<ManitouOutOfServiceResult>();
        }

        public async Task SetCustomerOffTestAsync(
            string accessToken,
            ManitouOffTestRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.SerialNo <= 0)
                throw new ArgumentException("SerialNo zorunludur.", nameof(request));

            if (request.LogSequence <= 0)
                throw new ArgumentException("LogSequence zorunludur.", nameof(request));

            var url = $"api/customers/{request.SerialNo}/ontest/off";

            await SendAsync<string>(
                HttpMethod.Put,
                url,
                request,
                accessToken,
                cancellationToken);
        }
    }

    public sealed class ManitouApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string? ResponseBody { get; }

        public ManitouApiException(
            string message,
            HttpStatusCode statusCode,
            string? responseBody = null)
            : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }


}