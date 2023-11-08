using ApiCurrent = RocketRMM.Api;
using ApiV10 = RocketRMM.Api.v10;
using ApiDev = RocketRMM.Api.v11;
using ApiBootstrap = RocketRMM.Api.Bootstrap;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using RocketRMM;
using Microsoft.OpenApi.Extensions;
using RocketRMM.Data.Logging;

Utilities.ConsoleColourWriteLine($@"                                                                    /\
 _____                _           _    _____   __  __  __  __      |--|
|  __ \              | |         | |  |  __ \ |  \/  ||  \/  |     |--|
| |__) |  ___    ___ | | __  ___ | |_ | |__) || \  / || \  / |    /|/\|\
|  _  /  / _ \  / __|| |/ / / _ \| __||  _  / | |\/| || |\/| |   /_||||_\
| | \ \ | (_) || (__ |   < |  __/| |_ | | \ \ | |  | || |  | |      **
|_|  \_\ \___/  \___||_|\_\ \___| \__||_|  \_\|_|  |_||_|  |_|      **
",ConsoleColor.Yellow);
Utilities.ConsoleColourWriteLine($@"RocketRMM

Created by Ian Harris (@knightian) - White Knight IT - https://whiteknightit.com.au

2023-10-30

Licensed under the AGPL-3.0 License + Security License Addendum

v{CoreEnvironment.CoreVersion}", ConsoleColor.DarkGray);

var builder = WebApplication.CreateBuilder(args);

// Load individual settings
CoreEnvironment.ShowDevEnvEndpoints = CoreEnvironment.TryGetSetting(builder, "ApiSettings:ShowDevEndpoints", false);
CoreEnvironment.ShowSwaggerUi = CoreEnvironment.TryGetSetting(builder, "ApiSettings:ShowSwaggerUi", false);
CoreEnvironment.RunSwagger = CoreEnvironment.TryGetSetting(builder, "ApiSettings:RunSwagger", false);
CoreEnvironment.ServeStaticFiles = CoreEnvironment.TryGetSetting(builder, "ApiSettings:ServeStaticFiles", false);
CoreEnvironment.Db = CoreEnvironment.TryGetSetting(builder, "ApiSettings:DbSettings:Db", "rocketrmmdb").Trim();
CoreEnvironment.DbUser = CoreEnvironment.TryGetSetting(builder, "ApiSettings:DbSettings:DbUser", "rocketrmmcoreservice").Trim();
CoreEnvironment.DbPassword = CoreEnvironment.TryGetSetting(builder, "ApiSettings:DbSettings:DbPassword", "wellknownpassword").Trim();
CoreEnvironment.DbServer = CoreEnvironment.TryGetSetting(builder, "ApiSettings:DbSettings:DbServer", "localhost").Trim();
CoreEnvironment.DbServerPort = CoreEnvironment.TryGetSetting(builder, "ApiSettings:DbSettings:DbServerPort", "3306").Trim();
CoreEnvironment.DataDir = CoreEnvironment.TryGetSetting(builder, "ApiSettings:DataPath", $"{CoreEnvironment.WorkingDir}{Path.DirectorySeparatorChar}data").Trim();
CoreEnvironment.CacheDir = CoreEnvironment.TryGetSetting(builder, "ApiSettings:CachePath", $"{CoreEnvironment.DataDir}{Path.DirectorySeparatorChar}cache").Trim();
CoreEnvironment.PersistentDir = CoreEnvironment.TryGetSetting(builder, "ApiSettings:PersistentPath", CoreEnvironment.WorkingDir).Trim();
CoreEnvironment.PkiDir =  $"{CoreEnvironment.PersistentDir}{Path.DirectorySeparatorChar}pki";
CoreEnvironment.CaDir = $"{CoreEnvironment.PkiDir}{Path.DirectorySeparatorChar}ca{Path.DirectorySeparatorChar}root";
CoreEnvironment.CaIntermediateDir = $"{CoreEnvironment.PkiDir}{Path.DirectorySeparatorChar}ca{Path.DirectorySeparatorChar}intermediate";
CoreEnvironment.CertificatesDir = $"{CoreEnvironment.PkiDir}{Path.DirectorySeparatorChar}certificates";
CoreEnvironment.CrlDir = $"{CoreEnvironment.PkiDir}{Path.DirectorySeparatorChar}crl";
CoreEnvironment.WebRootPath = CoreEnvironment.TryGetSetting(builder, "ApiSettings:WebRootPath", $"{CoreEnvironment.WorkingDir}{Path.DirectorySeparatorChar}wwwroot").Trim();
CoreEnvironment.FrontEndUri = CoreEnvironment.TryGetSetting(builder, "ApiSettings:WebUiUrl", "http://localhost").Trim();
CoreEnvironment.KestrelHttp = CoreEnvironment.TryGetSetting(builder, "Kestrel:Endpoints:Http:Url", "http://localhost:8088").Trim();

// We skip a lot of the setup/config stuff if it is a DB migration
if (!Environment.GetCommandLineArgs().Contains("migrations", StringComparer.OrdinalIgnoreCase))
{
    Utilities.ConsoleColourWriteLine($"Detected platform {CoreEnvironment.GetOperatingSystem().GetDisplayName()}");
    // Build Data/Cache directories if they don't exist
    CoreEnvironment.DataAndCacheDirectoriesBuild();

    // Get the identifying tag of this instance
    CoreEnvironment.DeviceTag = await CoreEnvironment.GetDeviceTag();

    // Update DB if new manifest or create if not exist
    CoreEnvironment.UpdateDbContexts();

    // These bytes form the basis of persistent but importantly unique seed entropy throughout crypto functions in this API
    await CoreEnvironment.GetEntropyBytes();

    await Utilities.Crypto.GetCertificate([$"{CoreEnvironment.CertificatesDir}{Path.DirectorySeparatorChar}auth.cer"], [$"{CoreEnvironment.CertificatesDir}{Path.DirectorySeparatorChar}auth.pfx"], CoreEnvironment.CertificateType.Authentication, $"CN = \"RocketRMM - {await CoreEnvironment.GetDeviceTag()} - Auth\",O = \"RocketRMM\"");

    // We will import our ApiZeroConf settings else try find bootstrap app to build from
    while (!CoreZeroConfiguration.ImportApiZeroConf(ref builder))
    {
        Thread.CurrentThread.Join(10000);
    }
}

// Ties the API to an Azure AD app for auth
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "ZeroConf:AzureAd");

// CORS policy to allow the UI to access the API
string[] corsUris = [CoreEnvironment.FrontEndUri, CoreEnvironment.KestrelHttp];

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

    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.WebHost.UseUrls();

// Expose development environment API endpoints if set in settings to do so
if (CoreEnvironment.ShowDevEnvEndpoints)
{
    CoreEnvironment.ApiRouteVersions.Add(double.Parse(CoreEnvironment.ApiDev.ToString()));
}

// Check if the app is running as debug
if (CoreEnvironment.IsDebug)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Utilities.ConsoleColourWriteLine("######################## RocketRMM is running in DEBUG context", ConsoleColor.Yellow);

    if (builder.Environment.IsDevelopment())
    {
        Utilities.ConsoleColourWriteLine("######################## RocketRMM is running from a development environment", ConsoleColor.Yellow);
    }

    Utilities.ConsoleColourWriteLine("");
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
                AuthorizationUrl = CoreEnvironment.TryGetSetting(builder,"ZeroConf:AzureAd:AuthorizationUrl",new Uri("http://localhost")),
                TokenUrl = CoreEnvironment.TryGetSetting(builder, "ZeroConf:AzureAd:TokenUrl", new Uri("http://localhost")),
                Scopes = new Dictionary<string, string>
                {
                    { CoreEnvironment.TryGetSetting(builder, "ZeroConf:AzureAd:ApiScope", "There's no scope here"), CoreEnvironment.TryGetSetting(builder, "ZeroConf:AzureAd:Scopes", "Here, there's no scope")}
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
            new[] { CoreEnvironment.TryGetSetting(builder, "ZeroConf:AzureAd:ApiScope", "There's no scope here") }
        }
    });
});

var app = builder.Build();

app.UseCors("corsapp");
app.UseAuthentication();
app.UseAuthorization();

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

// /bootstrap special API endpoints for bootstrapping this API, not used by anything other than RocketRMM realistically
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

            customSwagger.InjectStylesheet($"/swagger/css/swagger-customisation.css");
            customSwagger.InjectJavascript($"/swagger/js/swagger-customisation.js", "text/javascript");
            customSwagger.OAuthClientId(app.Configuration["ZeroConf:AzureAd:OpenIdClientId"]);
            customSwagger.OAuthUsePkce();
            customSwagger.OAuthScopeSeparator(" ");
        });
    }
}

// We can do whatever we want to do here, and then the app just runs and listens for HTTP requests once app.Run() is called.

_ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
{
    Message = "RocketRMM Core is started and listening for HTTP requests.",
    Severity = "Information",
    API = "Program"
});

app.Run();