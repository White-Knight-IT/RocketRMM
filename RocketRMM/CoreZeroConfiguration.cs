using RocketRMM.Data.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RocketRMM
{
    internal class CoreZeroConfiguration
    {
        [JsonInclude]
        internal string? TenantId { get; set; }
        [JsonInclude]
        internal string? ClientId { get; set; }
        [JsonInclude]
        internal string? Domain { get; set; }
        [JsonInclude]
        internal string? Instance { get; set; }
        [JsonInclude]
        internal string? Scopes { get; set; }
        [JsonInclude]
        internal string? AuthorizationUrl { get; set; }
        [JsonInclude]
        internal string? TokenUrl { get; set; }
        [JsonInclude]
        internal string? ApiScope { get; set; }
        [JsonInclude]
        internal string? OpenIdClientId { get; set; }
        [JsonInclude]
        internal string? CallbackPath { get; set; }
        [JsonInclude]
        internal string? AppPassword { get; set; }
        [JsonInclude]
        internal string? RefreshToken { get; set; }
        [JsonInclude]
        internal bool? IsBootstrapped { get; set; }

        internal static async Task<bool> Setup(string ownerTenant = "")
        {
            string domain = ownerTenant;
            string scopes = CoreEnvironment.ApiAccessScope;
            string authorizationUrl = string.Format("https://login.microsoftonline.com/{0}/oauth2/v2.0/authorize", ownerTenant);
            string tokenUrl = string.Format("https://login.microsoftonline.com/{0}/oauth2/v2.0/token", ownerTenant);
            string instance = "https://login.microsoftonline.com/";
            string apiScopeGuid = Guid.NewGuid().ToString();

            // step one - create EntraSam SPA that the Swagger UI will use to authenticate

            JsonElement samSpa = (await EntraSam.CreateSAMAuthApp($"RocketRMM UI - {CoreEnvironment.DeviceTag}", EntraSam.SamAppType.Spa, domain, spaRedirectUri: new string[] { $"{CoreEnvironment.FrontEndUri.TrimEnd('/')}/swagger/oauth2-redirect.html", $"{CoreEnvironment.FrontEndUri.TrimEnd('/')}/index.html", CoreEnvironment.FrontEndUri.TrimEnd('/') })).EntraSam;
            string openIdClientId = samSpa.GetProperty("appId").GetString() ?? string.Empty;
            if (!openIdClientId.Equals(string.Empty))
            {
                // Wait 30 seconds to ensure the SPA gets registered
                await Task.Delay(30000);

                // step two - create EntraSam that will act as the authentication hub of the API
                EntraSam.SamAndPassword result = await EntraSam.CreateSAMAuthApp($"RocketRMM API - {CoreEnvironment.DeviceTag}", EntraSam.SamAppType.Api, domain, openIdClientId, scopeGuid: apiScopeGuid);
                JsonElement samApi = result.EntraSam;
                string? appPassword = result.AppPassword;
                string clientId = samApi.GetProperty("appId").GetString() ?? string.Empty;
                string idUri = samApi.GetProperty("identifierUris").EnumerateArray().ToArray()[0].GetString() ?? string.Empty;
                string apiScope = string.Format("{0}/{1}", idUri, CoreEnvironment.ApiAccessScope);

                if (!clientId.Equals(string.Empty))
                {
                    CoreZeroConfiguration zeroConf = new()
                    {
                        TenantId = ownerTenant,
                        ClientId = clientId,
                        Domain = domain,
                        Instance = instance,
                        Scopes = scopes,
                        AuthorizationUrl = authorizationUrl,
                        TokenUrl = tokenUrl,
                        ApiScope = apiScope,
                        OpenIdClientId = openIdClientId,
                        CallbackPath = "/signin-oidc",
                        AppPassword = appPassword

                    };

                    _ = zeroConf.Save();
                    string bootstrapPath = $"{CoreEnvironment.PersistentDir}{Path.DirectorySeparatorChar}bootstrap.json";
                    await File.WriteAllTextAsync(bootstrapPath, await Utilities.RandomByteString());
                    File.Delete(bootstrapPath);

                    // Setup our front end config file
                    await File.WriteAllTextAsync($"{CoreEnvironment.WebRootPath}{Path.DirectorySeparatorChar}config.js", $@"/* Don't put secret configuration settings in this file, this is rendered
by the client. */

const config = {{
  auth: {{
    clientId: '{zeroConf.OpenIdClientId}',
    authority: 'https://login.microsoftonline.com/organizations/',
    redirectUri: '{CoreEnvironment.FrontEndUri}',
    postLogoutRedirectUri: '{CoreEnvironment.FrontEndUri}/signedout'
  }},
  cache: {{
    cacheLocation: 'sessionStorage',
    storeAuthStateInCookie: false
  }},
  api: {{
    scopes: ['{zeroConf.ApiScope}'],
    swagger: {CoreEnvironment.RunSwagger.ToString().ToLower()},
    deviceTag: '{await CoreEnvironment.GetDeviceTag()}'
  }},
  ui: {{
    frontEndUrl: '{CoreEnvironment.FrontEndUri}',
    swaggerUi: {CoreEnvironment.ShowSwaggerUi.ToString().ToLower()}
  }}
}};");
                }
            }

            return false;
        }

        internal static bool ImportApiZeroConf(ref WebApplicationBuilder builder)
        {
            try
            {
                CoreZeroConfiguration? zero = Read().Result;

                if (zero != null)
                {
                    CoreEnvironment.Secrets.TenantId = zero.TenantId;
                    CoreEnvironment.Secrets.ApplicationId = zero.ClientId;
                    CoreEnvironment.Secrets.ApplicationSecret = zero.AppPassword;
                    CoreEnvironment.Secrets.RefreshToken = zero.RefreshToken;
                    CoreEnvironment.IsBoostrapped = zero.IsBootstrapped ?? false;
                    builder.Configuration["ZeroConf:AzureAd:TenantId"] = zero.TenantId;
                    builder.Configuration["ZeroConf:AzureAd:ClientId"] = zero.ClientId;
                    builder.Configuration["ZeroConf:AzureAd:Domain"] = zero.Domain;
                    builder.Configuration["ZeroConf:AzureAd:Scopes"] = zero.Scopes;
                    builder.Configuration["ZeroConf:AzureAd:AuthorizationUrl"] = zero.AuthorizationUrl;
                    builder.Configuration["ZeroConf:AzureAd:TokenUrl"] = zero.TokenUrl;
                    builder.Configuration["ZeroConf:AzureAd:ApiScope"] = zero.ApiScope;
                    builder.Configuration["ZeroConf:AzureAd:OpenIdClientId"] = zero.OpenIdClientId;
                    builder.Configuration["ZeroConf:AzureAd:Instance"] = zero.Instance;
                    builder.Configuration["ZeroConf:AzureAd:CallbackPath"] = zero.CallbackPath;
                    return true;
                }
                else if (!CoreEnvironment.CheckForBootstrap().Result)
                {
                    Utilities.ConsoleColourWriteLine($"Waiting for bootstrap.json to be placed at {CoreEnvironment.PersistentDir} to provision the API...");
                }
            }
            catch (Exception ex)
            {
                CoreEnvironment.RunErrorCount++;
                _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                {
                    Message = $"Exception reading CoreZeroConfiguration file: {ex.Message}",
                    Severity = "Error",
                    API = "ImportApiZeroConf"
                });
            }

            return false;
        }

        internal static async Task<CoreZeroConfiguration?> Read()
        {
            string apiZeroConfPath = $"{CoreEnvironment.PersistentDir}{Path.DirectorySeparatorChar}api.zeroconf.aes";

            if (File.Exists(apiZeroConfPath))
            {
                return await Utilities.ReadJsonFromFile<CoreZeroConfiguration>(apiZeroConfPath, true);
            }

            return null;
        }

        internal async Task<bool> Save()
        {
            try
            {
                _ = Utilities.WriteJsonToFile<CoreZeroConfiguration>(this, $"{CoreEnvironment.PersistentDir}{Path.DirectorySeparatorChar}api.zeroconf.aes", true, null, true);
                CoreEnvironment.Secrets.TenantId = TenantId;
                CoreEnvironment.Secrets.ApplicationId = ClientId;
                CoreEnvironment.Secrets.ApplicationSecret = AppPassword;
                CoreEnvironment.Secrets.RefreshToken = RefreshToken;
                CoreEnvironment.IsBoostrapped = IsBootstrapped ?? false;
                return true;
            }
            catch (Exception ex)
            {
                CoreEnvironment.RunErrorCount++;
                _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                {
                    Message = $"Exception saving CoreZeroConfiguration file: {ex.Message}",
                    Severity = "Error",
                    API = "Save"
                });
            }

            return false;
        }
    }
}
