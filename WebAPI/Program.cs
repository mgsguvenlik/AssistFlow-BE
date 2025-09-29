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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
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

//builder.Services.AddAutoMapper(typeof(MapperProfile));
//MapsterConfig.Configure();
//builder.Services.AddDbContext<AppDataContext>(options =>
//        options.UseSqlServer(appSettings.MSSQLConnectionString));
#endregion

// Seed servislerini kaydet
builder.Services.AddDataSeeding(
    typeof(TurkeyCitiesSeed)   // buraya diðer seed tiplerini de ekleyebilirsin
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
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "app_session";
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Cookie Auth
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "app_auth";
        options.LoginPath = "/api/auth/login";
        options.LogoutPath = "/api/auth/logout";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);

        // Cross-site SPA kullanýyorsan:
        // options.Cookie.SameSite = SameSiteMode.None;
        // options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

builder.Services.AddAuthorization();

// HttpContext
builder.Services.AddHttpContextAccessor();

#endregion

var app = builder.Build();

await app.UseDataSeedingAsync<AppDataContext>(); // Migration’dan önce/sonra çaðýrabilirsin

/// Otomatik Migration iþlemi
MigrationApplier.ApplyMigrations(app);


app.UseCors("CorsPolicy");

app.UseMiddleware<ErrorHandlerMiddleware>();

// Configure the HTTP request pipeline.

#region OpenAPI
app.MapScalarApiReference(o =>
    o.WithTheme(ScalarTheme.BluePlanet)
);
app.MapOpenApi();


#endregion

app.UseRequestLocalization(options =>
{
    options.CultureInfoUseUserOverride = false;
});

app.UseHttpsRedirection();
app.UseStaticFiles();
// Sýra önemli:
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();