using Amazon.Runtime;
using Amazon.S3;
using Business.DependencyResolvers.Autofac;
using Business.Interfaces.Storage;
using Business.Services.Storage;
using Business.UnitOfWork;
using Core.Extensions;
using Core.Settings.Concrete;
using Core.Utilities.IoC;
using Data.Abstract;
using Data.Concrete;
using Data.Concrete.EfCore.Context;
using Data.Seeding.Infrastructure;
using Data.Seeding.Seeds;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Model.Concrete;
using Scalar.AspNetCore;
using Serilog;
using System.Security.Claims;
using System.Text;
using WebAPI.Extensions;
using WebAPI.Middleware;



var builder = WebApplication.CreateBuilder(args);


builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var appSettingsSection = builder.Configuration.GetSection("AppSettings");

builder.Services.Configure<AppSettings>(appSettingsSection);
builder.Services.Configure<PeriodicReportOptions>(
    builder.Configuration.GetSection(PeriodicReportOptions.SectionName));

var appSettings = appSettingsSection.Get<AppSettings>();

builder.Services.AddHttpClient("CustomClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (httpRequestMessage, certificate, chain, sslPolicyErrors) =>
        {
            return true;
        }
    });

builder.Services.AddControllers();
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("AssistFlow");
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
builder.Services.AddSingleton<Business.Interfaces.Helpdesk.IHelpdeskSecretProtector, WebAPI.HelpdeskSecretProtector>();


#region Serilog
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
      .Enrich.FromLogContext()
      .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName);
});
#endregion

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
        policy => policy
            .WithOrigins(
            "http://localhost:3000",
            "http://localhost:3001",
            "http://localhost:5173",
            "http://localhost:8084",
            "http://192.168.1.46:300",
            "http://192.168.1.46:5173",
            "https://192.168.1.46:5174",
            "https://localhost:5174",
            "http://localhost:8081",
            "https://flowassisttest.mgs.com.tr",
            "http://flowassisttest.mgs.com.tr",
            "http://flowassist.mgs.com.tr",
            "https://flowassist.mgs.com.tr") // React frontend URL'si
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials() // Bunu kullanıyorsan WithOrigins zorunlu!
    );
});

builder.Services.AddAdvancedDependencyInjection();
builder.Services.AddDependencyResolvers(new ICoreModule[]
{
    new AutofacBusinessModule()
});



builder.Services
    .AddOptions<R2StorageOptions>()
    .Bind(builder.Configuration.GetSection(R2StorageOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        x => Uri.TryCreate(
            x.Endpoint,
            UriKind.Absolute,
            out _),
        "R2 Endpoint geçerli bir URL olmalıdır.")
    .Validate(
        x => Uri.TryCreate(
            x.PublicBaseUrl,
            UriKind.Absolute,
            out _),
        "R2 PublicBaseUrl geçerli bir URL olmalıdır.")
    .ValidateOnStart();

builder.Services.AddSingleton<IAmazonS3>(serviceProvider =>
{
    var options = serviceProvider
        .GetRequiredService<IOptions<R2StorageOptions>>()
        .Value;

    var credentials = new BasicAWSCredentials(
        options.AccessKeyId,
        options.SecretAccessKey);

    var config = new AmazonS3Config
    {
        ServiceURL = options.Endpoint,

        // Bucket adı URL host'una eklenmek yerine
        // endpoint/bucket/key biçimi kullanılır.
        ForcePathStyle = true
    };

    return new AmazonS3Client(
        credentials,
        config);
});


#region Mapper

var mapsterConfig = new TypeAdapterConfig();
mapsterConfig.Scan(AppDomain.CurrentDomain.GetAssemblies()); // AppMappings bulunur
mapsterConfig.Compile();

builder.Services.AddSingleton(mapsterConfig);
builder.Services.AddScoped<IMapper, Mapper>();///MZK Bunu düzenle. Mapster için

#endregion


#region Seed servislerini kaydet
builder.Services.AddDataSeeding(
    typeof(TurkeyCitiesSeed)   // buraya diğer seed tiplerini de ekleyebilirsin
);
builder.Services.AddDataSeeding(
    typeof(ConfigSeed)   // buraya diğer seed tiplerini de ekleyebilirsin
);
builder.Services.AddDataSeeding(
    typeof(WorkFlowStepSeed)   // buraya diğer seed tiplerini de ekleyebilirsin
);

builder.Services.AddDataSeeding(
    typeof(YkbWorkFlowStepSeed)   // buraya diğer seed tiplerini de ekleyebilirsin
);
builder.Services.AddDataSeeding(
    typeof(WorkFlowTransitionSeed)   // buraya diğer seed tiplerini de ekleyebilirsin
);

builder.Services.AddDataSeeding(
    typeof(MenuSeed)
);
builder.Services.AddDataSeeding(
    typeof(PeriodicReportMenuSeed)
);
builder.Services.AddDataSeeding(
    typeof(HelpdeskSeed)
);
#endregion



builder.Services.AddDbContext<AppDataContext>(options =>
    options.UseSqlServer(
        appSettings.MSSQLConnectionString,
        x => x.MigrationsAssembly("Data")
    )
);


builder.Services.Add(new ServiceDescriptor(
                typeof(IUnitOfWork),
                serviceProvider =>
                {
                    var repository = serviceProvider.GetService<IRepository>();
                    return new UnitOfWork(repository ?? throw new ArgumentException("Bir Hata oluştu. UnitOfWork null"));
                }, ServiceLifetime.Scoped));

builder.Services.Add(new ServiceDescriptor(
                typeof(IRepository),
                serviceProvider =>
                {
                    var dbContext = ActivatorUtilities.CreateInstance<AppDataContext>(serviceProvider);
                    return new Repository(dbContext);
                }, ServiceLifetime.Scoped));

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.ReportApiVersions = true;
});

#region OpenApi
builder.Services.AddOpenApi(options =>
{
    options.UseJwtBearerAuthentication();

});
#endregion



#region jwt login
// Session
//builder.Services.AddDistributedMemoryCache();
//builder.Services.AddSession(options =>
//{
//    options.Cookie.Name = "app_session";
//    options.IdleTimeout = TimeSpan.FromHours(8);
//    options.Cookie.HttpOnly = true;
//    options.Cookie.IsEssential = true;
//});

// Cookie Auth
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = appSettings.Issuer,
            ValidAudience = appSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.Key)),
            ClockSkew = TimeSpan.Zero, // İsteğe bağlı: expire anında düşsün
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
        };
    });
builder.Services.AddAuthorization();

// HttpContext
builder.Services.AddHttpContextAccessor();

#endregion

var app = builder.Build();

await app.UseDataSeedingAsync<AppDataContext>(); // Migration’dan önce/sonra çağırabilirsin

/// Otomatik Migration işlemi
MigrationApplier.ApplyMigrations(app);


app.UseMiddleware<ErrorHandlerMiddleware>();

// Configure the HTTP request pipeline.

#region OpenAPI
app.MapOpenApi();
app.MapScalarApiReference(o =>
{
    o.WithOpenApiRoutePattern("/openapi/{documentName}.json");
    o.WithTheme(ScalarTheme.BluePlanet);
});


#endregion

app.UseRequestLocalization(options =>
{
    options.CultureInfoUseUserOverride = false;
});

app.UseHttpsRedirection();
app.UseStaticFiles();
// Sıra önemli:
//app.UseSession();
app.UseRouting();

app.UseCors("CorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
