using Business.DependencyResolvers.Autofac;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using System.Security.Claims;
using System.Text;
using WebAPI.Extensions;
using WebAPI.Middleware;


var builder = WebApplication.CreateBuilder(args);

var appSettingsSection = builder.Configuration.GetSection("AppSettings");

builder.Services.Configure<AppSettings>(appSettingsSection);

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

var logger = new LoggerConfiguration()
  .ReadFrom.Configuration(builder.Configuration)
  .Enrich.FromLogContext()
  .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);

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
            "http://localhost:8081") // React frontend URL'si
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials() // Bunu kullanýyorsan WithOrigins zorunlu!
    );
});

builder.Services.AddAdvancedDependencyInjection();
builder.Services.AddDependencyResolvers(new ICoreModule[]
{
    new AutofacBusinessModule()
});

#region Mapper

var mapsterConfig = new TypeAdapterConfig();
mapsterConfig.Scan(AppDomain.CurrentDomain.GetAssemblies()); // AppMappings bulunur
mapsterConfig.Compile();

builder.Services.AddSingleton(mapsterConfig);
builder.Services.AddScoped<IMapper, Mapper>();///MZK Bunu düzenle. Mapster için

#endregion

// Seed servislerini kaydet
builder.Services.AddDataSeeding(
    typeof(TurkeyCitiesSeed)   // buraya diðer seed tiplerini de ekleyebilirsin
);
builder.Services.AddDataSeeding(
    typeof(ConfigSeed)   // buraya diðer seed tiplerini de ekleyebilirsin
);
builder.Services.AddDataSeeding(
    typeof(WorkFlowStepSeed)   // buraya diðer seed tiplerini de ekleyebilirsin
);
builder.Services.AddDataSeeding(
    typeof(WorkFlowTransitionSeed)   // buraya diðer seed tiplerini de ekleyebilirsin
);


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
                    return new UnitOfWork(repository ?? throw new ArgumentException("Bir Hata oluþtu. UnitOfWork null"));
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
            ClockSkew = TimeSpan.Zero, // Ýsteðe baðlý: expire anýnda düþsün
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();

// HttpContext
builder.Services.AddHttpContextAccessor();

#endregion

var app = builder.Build();

await app.UseDataSeedingAsync<AppDataContext>(); // Migration’dan önce/sonra çaðýrabilirsin

/// Otomatik Migration iþlemi
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
// Sýra önemli:
//app.UseSession();
app.UseRouting();

app.UseCors("CorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();