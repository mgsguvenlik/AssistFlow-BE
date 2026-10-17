using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Business.Interfaces;
using Core.Settings.Concrete;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Business.Services.Sms;

public sealed class TelsamSmsService(HttpClient client, IOptionsSnapshot<SmsServiceOptions> options,
    IHostEnvironment environment) : ISmsSender
{
    public async Task<SmsSendResult> SendAsync(string phone, string message, string correlationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var number = SmsPhone.Normalize(phone);
        if (number is null) return Rejected("Geçerli cep telefonu bulunamadı.", "INVALID_PHONE");
        if (string.IsNullOrWhiteSpace(message) || message.Length > 2000)
            return Rejected("SMS metni boş olamaz ve 2000 karakteri geçemez.", "INVALID_MESSAGE");
        if (!Regex.IsMatch(correlationId ?? "", @"\A[A-Za-z0-9.\-]{1,100}\z"))
            return Rejected("SMS işlem kimliği geçersiz.", "INVALID_CORRELATION");
        var cfg = options.Value;
        // A false setting cannot turn development, test or staging into a live sender.
        if (!environment.IsProduction() || cfg.SmsSimulationEnabled)
            return new(SmsSendStatus.Simulation, "Simülasyon tamamlandı; gerçek SMS gönderilmedi.");
        if (!Uri.TryCreate(cfg.TelsamSmsEndpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(endpoint.UserInfo)
            || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment)
            || string.IsNullOrWhiteSpace(cfg.TelsamSmsAuthorizationToken)
            || !Regex.IsMatch(cfg.TelsamSmsAuthorizationToken, @"\A[A-Za-z0-9+/]+={0,2}\z")
            || string.IsNullOrWhiteSpace(cfg.TelsamSmsSender)
            || string.IsNullOrWhiteSpace(cfg.TelsamSmsTitle)
            || cfg.TelsamSmsTitle.Length is < 5 or > 50 || cfg.TelsamSmsValidity is < 60 or > 1440)
            return Rejected("SMS servis ayarları eksik veya geçersiz.", "INVALID_SETTINGS");
        try { _ = Convert.FromBase64String(cfg.TelsamSmsAuthorizationToken); }
        catch (FormatException) { return Rejected("SMS servis yetkilendirme ayarı geçersiz.", "INVALID_SETTINGS"); }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", cfg.TelsamSmsAuthorizationToken);
        request.Content = JsonContent.Create(new
        {
            type = 1, sendingType = 0, title = cfg.TelsamSmsTitle, content = message,
            number = long.Parse(number, System.Globalization.CultureInfo.InvariantCulture),
            encoding = 1, sender = cfg.TelsamSmsSender, validity = cfg.TelsamSmsValidity,
            customID = correlationId
        });
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.Content.Headers.ContentLength is > 16384)
                return new(SmsSendStatus.Unknown, "SMS servis cevabı doğrulanamadı.", ErrorCode: "INVALID_RESPONSE");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                if (buffer.Length + read > 16384)
                    return new(SmsSendStatus.Unknown, "SMS servis cevabı doğrulanamadı.", ErrorCode: "INVALID_RESPONSE");
                buffer.Write(chunk, 0, read);
            }
            using var json = JsonDocument.Parse(buffer.ToArray());
            return ParseResponse((int)response.StatusCode, json.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or System.IO.IOException)
        {
            // A timeout may have happened after acceptance; never retry this blindly.
            return new(SmsSendStatus.Unknown, "SMS sonucu doğrulanamadı. Yeniden göndermeden önce servis kaydı kontrol edilmelidir.",
                ErrorCode: "PROVIDER_RESULT_UNKNOWN");
        }
    }

    public static SmsSendResult ParseResponse(int httpStatus, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return new(SmsSendStatus.Unknown, "SMS servis cevabı doğrulanamadı.", ErrorCode: "INVALID_RESPONSE");
        if (root.TryGetProperty("err", out var error) && error.ValueKind == JsonValueKind.Object)
        {
            var code = error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            // Only a bounded identifier is exposed; raw provider bodies/credentials are never logged.
            code = code is not null && Regex.IsMatch(code, @"\A[A-Za-z0-9_\-.]{1,100}\z") ? code : "PROVIDER_REJECTED";
            return Rejected("SMS servisi gönderimi reddetti.", code);
        }
        if (httpStatus is >= 200 and < 300 && root.TryGetProperty("err", out var err) && err.ValueKind == JsonValueKind.Null
            && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("pkgID", out var pkg))
        {
            var id = pkg.ValueKind == JsonValueKind.String ? pkg.GetString() : pkg.ValueKind == JsonValueKind.Number ? pkg.GetRawText() : null;
            if (long.TryParse(id, out var packageId) && packageId > 0)
                return new(SmsSendStatus.Accepted, "SMS servis tarafından kabul edildi; teslim durumu henüz doğrulanmadı.", id);
        }
        return new(SmsSendStatus.Unknown, "SMS servis cevabı doğrulanamadı.", ErrorCode: "INVALID_RESPONSE");
    }

    private static SmsSendResult Rejected(string message, string? code) => new(SmsSendStatus.Rejected, message, ErrorCode: code);
}
