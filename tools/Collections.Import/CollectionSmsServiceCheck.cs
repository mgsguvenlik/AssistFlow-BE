using System.Text.Json;
using Business.Interfaces;
using Business.Services.Sms;
using Business.Services.Crm.Collections;
using Core.Settings.Concrete;
using Core.Utilities.Constants;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Data.Concrete.EfCore.Context;
using Business.DependencyResolvers.Autofac;

internal static class CollectionSmsServiceCheck
{
    public static async Task RunAsync()
    {
        if (SmsPhone.Select("02123555000", "05551112233") != "905551112233"
            || SmsPhone.Normalize("+90 (555) 111-22-33") != "905551112233"
            || SmsPhone.Normalize("5551112233 dahili 1") is not null)
            throw new InvalidOperationException("SMS telefon seçimi kontrolü başarısız.");
        var values = CollectionSmsTemplate.Parameters.ToDictionary(x => x, _ => "örnek");
        var text = CollectionSmsTemplate.Render(CollectionSmsConstants.DefaultRateChangeTemplate, values);
        if (text.Contains('{')) throw new InvalidOperationException("SMS şablon kontrolü başarısız.");
        using var accepted = JsonDocument.Parse("""{"err":null,"data":{"pkgID":88657576}}""");
        using var rejected = JsonDocument.Parse("""{"err":{"code":"ERR_INVALID_PARAM","status":417},"data":null}""");
        using var empty = JsonDocument.Parse("{}");
        if (TelsamSmsService.ParseResponse(200, accepted.RootElement).Status != SmsSendStatus.Accepted
            || TelsamSmsService.ParseResponse(417, rejected.RootElement).Status != SmsSendStatus.Rejected
            || TelsamSmsService.ParseResponse(200, empty.RootElement).Status != SmsSendStatus.Unknown)
            throw new InvalidOperationException("SMS servis cevap kontrolü başarısız.");
        using var handler = new ForbiddenHandler();
        using var client = new HttpClient(handler);
        // Deliberately false: the environment guard must still prevent all network calls.
        var cfg = new Snapshot(new SmsServiceOptions { SmsSimulationEnabled = false });
        foreach (var environment in new[] { "Development", "Test", "Staging", "", "production-test" })
        {
            var sender = new TelsamSmsService(client, cfg, new EnvironmentInfo(environment));
            var result = await sender.SendAsync("05551112233", "Simülasyon kontrolü", "simulation-check");
            if (result.Status != SmsSendStatus.Simulation)
                throw new InvalidOperationException("Canlı olmayan ortam simülasyon kontrolü başarısız.");
        }
        var production = new TelsamSmsService(client,
            new Snapshot(new SmsServiceOptions()), new EnvironmentInfo("Production"));
        if ((await production.SendAsync("05551112233", "Simülasyon kontrolü", "simulation-check")).Status != SmsSendStatus.Simulation)
            throw new InvalidOperationException("Production varsayılan simülasyon kontrolü başarısız.");
        if (handler.Calls != 0) throw new InvalidOperationException("Simülasyon ağ çağrısı yaptı.");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new EnvironmentInfo("Test"));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppSettings:CollectionSmsEnabled"] = "true",
            ["AppSettings:SmsSimulationEnabled"] = "false"
        }).Build());
        services.AddScoped(_ => new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SmsCheckDesignOnly;Trusted_Connection=True;TrustServerCertificate=True").Options));
        new AutofacBusinessModule().Load(services);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<ICollectionRateChangeService>();
        if (!scope.ServiceProvider.GetRequiredService<CollectionSmsService>().Enabled
            || (await scope.ServiceProvider.GetRequiredService<ISmsSender>().SendAsync("05551112233", "DI simülasyon kontrolü", "di-check")).Status != SmsSendStatus.Simulation)
            throw new InvalidOperationException("Mevcut Autofac modülündeki SMS/HTTP client/options kayıtları doğrulanamadı.");
        Console.WriteLine("SMS kontrolü başarılı: telefon önceliği, şablon, gerçek cevap formatları, dev/test/staging ve production varsayılanında sıfır ağ çağrısı. Gerçek SMS gönderilmedi.");
    }

    private sealed class ForbiddenHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            throw new InvalidOperationException("Simülasyon ağ çağrısı yapmamalıdır.");
        }
    }
    private sealed class Snapshot(SmsServiceOptions options) : IOptionsSnapshot<SmsServiceOptions>
    {
        public SmsServiceOptions Value => options;
        public SmsServiceOptions Get(string? name) => options;
    }
    private sealed class EnvironmentInfo(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "SMS kontrolü";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
