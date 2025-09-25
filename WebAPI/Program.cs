using Business.DependencyResolvers.Autofac;
using Business.Interfaces;
using Business.Mapper;
using Business.UnitOfWork;
using Core.Extensions;
using Core.Settings.Concrete;
using Core.Utilities.IoC;
using Data.Abstract;
using Data.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
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
            .WithOrigins("http://localhost:3000", "http://localhost:3001", "http://localhost:5173", "http://localhost:8081") // React frontend URL'si
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
//builder.Services.AddAutoMapper(typeof(MapperProfile));
MapsterConfig.Configure();
//builder.Services.AddDbContext<AppDataContext>(options =>
//        options.UseSqlServer(appSettings.MSSQLConnectionString));

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
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = appSettings.Issuer,
            ValidAudience = appSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(appSettings.Key!)
            )
        };
    });

builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("Basic", null);

builder.Services.AddAuthorization();

#endregion

var app = builder.Build();


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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();