using RocketRMM.Common;
using ApiCurrent = RocketRMM.Api;
using ApiV10 = RocketRMM.Api.v10;
using ApiDev = RocketRMM.Api.v11;
using ApiBootstrap = RocketRMM.Api.Bootstrap;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using RocketRMM.Data.Logging;
using Microsoft.EntityFrameworkCore;
using RocketRMM.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Drawing.Text;

Console.WriteLine($@"                                                                    /\
 _____                _           _    _____   __  __  __  __      |--|
|  __ \              | |         | |  |  __ \ |  \/  ||  \/  |     |--|
| |__) |  ___    ___ | | __  ___ | |_ | |__) || \  / || \  / |    /|/\|\
|  _  /  / _ \  / __|| |/ / / _ \| __||  _  / | |\/| || |\/| |   /_||||_\
| | \ \ | (_) || (__ |   < |  __/| |_ | | \ \ | |  | || |  | |      **
|_|  \_\ \___/  \___||_|\_\ \___| \__||_|  \_\|_|  |_||_|  |_|      **

RocketRMM

Created by Ian Harris (@knightian) - White Knight IT - https://whiteknightit.com.au

2023-10-30

Licensed under the AGPL-3.0 License + Security License Addendum

v{CoreEnvironment.CoreVersion}
");

var builder = WebApplication.CreateBuilder(args);

// Load individual settings
CoreEnvironment.UseHttpsRedirect = builder.Configuration.GetValue<bool>("ApiSettings:HttpsRedirect");
CoreEnvironment.ShowDevEnvEndpoints = builder.Configuration.GetValue<bool>("ApiSettings:ShowDevEndpoints");
CoreEnvironment.ShowSwaggerUi = builder.Configuration.GetValue<bool>("ApiSettings:ShowSwaggerUi");
CoreEnvironment.RunSwagger = builder.Configuration.GetValue<bool>("ApiSettings:RunSwagger");
CoreEnvironment.ServeStaticFiles = builder.Configuration.GetValue<bool>("ApiSettings:ServeStaticFiles");
CoreEnvironment.Db = builder.Configuration.GetValue<string>("ApiSettings:DbSettings:Db").Trim() ?? "rocketrmmdb";
CoreEnvironment.DbUser = builder.Configuration.GetValue<string>("ApiSettings:DbSettings:DbUser").Trim() ?? "rocketrmmcoreservice";
CoreEnvironment.DbPassword = builder.Configuration.GetValue<string>("ApiSettings:DbSettings:DbPassword").Trim() ?? "wellknownpassword";
CoreEnvironment.DbServer = builder.Configuration.GetValue<string>("ApiSettings:DbSettings:DbServer").Trim() ?? "localhost";
CoreEnvironment.DbServerPort = builder.Configuration.GetValue<string>("ApiSettings:DbSettings:DbServerPort").Trim() ?? "7704";
CoreEnvironment.CacheDir = builder.Configuration.GetValue<string>("ApiSettings:CachePath").Trim() ?? $"{CoreEnvironment.DataDir}/Cache";
CoreEnvironment.PersistentDir = builder.Configuration.GetValue<string>("ApiSettings:PersistentPath").Trim() ?? CoreEnvironment.WorkingDir;
CoreEnvironment.WebRootPath = builder.Configuration.GetValue<string>("ApiSettings:WebRootPath").Trim() ?? $"{CoreEnvironment.WorkingDir}/wwwroot";
CoreEnvironment.RocketRmmFrontEndUri = builder.Configuration.GetValue<string>("ApiSettings:WebUiUrl").TrimEnd('/').Trim() ?? "http://localhost";
CoreEnvironment.DeviceTag = await CoreEnvironment.GetDeviceTag();
CoreEnvironment.KestrelHttps = builder.Configuration.GetValue<string>("Kestrel:Endpoints:Https:Url").Trim() ?? "https://localhost:7074";
CoreEnvironment.KestrelHttp = builder.Configuration.GetValue<string>("Kestrel:Endpoints:Http:Url").Trim() ?? "https://localhost:7073";

// Build Data/Cache directories if they don't exist
CoreEnvironment.DataAndCacheDirectoriesBuild();

// We skip a lot of the setup/config stuff if it is a DB migration
if (!Environment.GetCommandLineArgs().Contains("migrations", StringComparer.OrdinalIgnoreCase))
{
    // Update DB if new manifest or create if not exist
    await CoreEnvironment.UpdateDbContexts();

    // These bytes form the basis of persistent but importantly unique seed entropy throughout crypto functions in this API
    await CoreEnvironment.GetEntropyBytes();

    // We will import our ApiZeroConf settings else try find bootstrap app to build from
    while (!CoreZeroConfiguration.ImportApiZeroConf(ref builder))
    {
        Thread.CurrentThread.Join(10000);
    }
}

// Ties the API to an Azure AD app for auth
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "ZeroConf:AzureAd");

// CORS policy to allow the UI to access the API
string[] corsUris = new string[] { CoreEnvironment.RocketRmmFrontEndUri, CoreEnvironment.KestrelHttps, CoreEnvironment.KestrelHttp } ?? new string[] { CoreEnvironment.KestrelHttps, CoreEnvironment.KestrelHttp };

builder.Services.AddCors(p => p.AddPolicy("corsapp", builder =>
{
    // This allows for our Web UI which may be at a totally different domain and/or port to comminucate with the API
    builder.WithOrigins(corsUris).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
}));

// Add auth services
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// Add API versioning capabilities
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.DefaultApiVersion = CoreEnvironment.ApiCurrent;
}).AddApiExplorer(options =>
{
    options.SubstitutionFormat = "VV";
    options.GroupNameFormat = "'v'VV";
    options.SubstituteApiVersionInUrl = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
});

// Configure JSON options
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.AllowTrailingCommas = false;

    // Official CIPP-API has absolutely no standards for serializing JSON, we need this to match it, and it hurts my soul immensly.
    options.SerializerOptions.PropertyNamingPolicy = null;
});

builder.WebHost.UseUrls();

// Expose development environment API endpoints if set in settings to do so
if (CoreEnvironment.ShowDevEnvEndpoints)
{
    CoreEnvironment.ApiRouteVersions.Add(double.Parse(CoreEnvironment.ApiDev.ToString()));
}

if (CoreEnvironment.IsDebug)
{
    Console.WriteLine("######################## RocketRMM is running in DEBUG context");

    // In dev env we can get secrets from local environment (use `dotnet user-secrets` tool to safely store local secrets)
    if (builder.Environment.IsDevelopment())
    {
        Console.WriteLine("######################## RocketRMM is running from a development environment");
    }
}

// Prep Swagger and specify the auth settings for it to use a EntraSam on Azure AD
builder.Services.AddSwaggerGen(customSwagger => {

    customSwagger.EnableAnnotations();

    foreach (double version in CoreEnvironment.ApiRouteVersions)
    {
        if (version.ToString("f1").Contains(CoreEnvironment.ApiDev.ToString()))
        {
            customSwagger.SwaggerDoc(string.Format("v{0}", version.ToString("f1")), new() { Title = "RockekRMM API Dev", Version = string.Format("v{0}", version.ToString("f1")) });
            continue;
        }
        customSwagger.SwaggerDoc(string.Format("v{0}", version.ToString("f1")), new() { Title = "RocketRMM API", Version = string.Format("v{0}", version.ToString("f1")) });

    }

    customSwagger.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Description = "OAuth2.0 Auth Code with PKCE",
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri(builder.Configuration["ZeroConf:AzureAd:AuthorizationUrl"]),
                TokenUrl = new Uri(builder.Configuration["ZeroConf:AzureAd:TokenUrl"]),
                Scopes = new Dictionary<string, string>
                {
                    { builder.Configuration["ZeroConf:AzureAd:ApiScope"], builder.Configuration["ZeroConf:AzureAd:Scopes"]}
                }
            }
        }
    });
    customSwagger.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
            },
            new[] { builder.Configuration["ZeroConf:AzureAd:ApiScope"] }
        }
    });
});

var app = builder.Build();

app.UseCors("corsapp");

app.UseAuthentication();
app.UseAuthorization();

if (CoreEnvironment.UseHttpsRedirect)
{
    // Redirect HTTP to HTTPS, seems to use 307 temporary redirect
    app.UseHttpsRedirection();
}

if (CoreEnvironment.ServeStaticFiles)
{
    // Allows us to serve files from wwwroot to customise swagger etc.
    app.UseStaticFiles();
}

ApiVersionSetBuilder apiVersionSetBuilder = app.NewApiVersionSet();

foreach (double version in CoreEnvironment.ApiRouteVersions)
{
    apiVersionSetBuilder.HasApiVersion(new(version));
}

CoreEnvironment.ApiVersionSet = apiVersionSetBuilder.ReportApiVersions().Build();

// /bootstrap special API endpoints for bootstrapping this API, not used by anything other than FFPP realistically
ApiBootstrap.BootsrapRoutes.InitRoutes(ref app);

// /x.x (CoreEnvironment.ApiDev) path which uses the latest devenv API specification (will only be accessible if ShowDevEnvEndpoints = true)
ApiDev.Routes.InitRoutes(ref app);

// /api path which always uses the latest API specification
ApiCurrent.Routes.InitRoutes(ref app);

// /v1.0 path using API specification v1.0
ApiV10.Routes.InitRoutes(ref app);

if (CoreEnvironment.RunSwagger)
{
    app.UseSwagger();

    // But we don't always show the UI for swagger
    if (CoreEnvironment.ShowSwaggerUi)
    {
        app.UseSwaggerUI(customSwagger =>
        {

            foreach (var desc in app.DescribeApiVersions())
            {
                var url = $"/swagger/{desc.GroupName}/swagger.json";
                var name = desc.GroupName.ToUpper();
                if (desc.ApiVersion.ToString().Contains(CoreEnvironment.ApiDev.ToString()))
                {
                    customSwagger.SwaggerEndpoint(url, $"RocketRMM API Dev {name}");
                    continue;
                }
                customSwagger.SwaggerEndpoint(url, $"RocketRMM API {name}");
            }

            customSwagger.InjectStylesheet("/swagger/css/swagger-customisation.css");
            customSwagger.InjectJavascript("/swagger/js/swagger-customisation.js", "text/javascript");
            customSwagger.OAuthClientId(app.Configuration["ZeroConf:AzureAd:OpenIdClientId"]);
            customSwagger.OAuthUsePkce();
            customSwagger.OAuthScopeSeparator(" ");
        });
    }
}

//await new FfppLogs().LogDb.LogRequest("Test Message", "", "Information", "M365B654613.onmicrosoft.com", "ThisIsATest");

//app.Run();