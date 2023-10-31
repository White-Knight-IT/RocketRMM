using System.Text.Json;

namespace RocketRMM.Common
{
    internal class CoreZeroConfiguration
    {

        internal string? TenantId { get; set; }
        internal string? ClientId { get; set; }
        internal string? Domain { get; set; }
        internal string? Instance { get; set; }
        internal string? Scopes { get; set; }
        internal string? AuthorizationUrl { get; set; }
        internal string? TokenUrl { get; set; }
        internal string? ApiScope { get; set; }
        internal string? OpenIdClientId { get; set; }
        internal string? CallbackPath { get; set; }
        internal string? AppPassword { get; set; }
        internal string? RefreshToken { get; set; }
        internal string? ExchangeRefreshToken { get; set; }
        internal bool? IsBootstrapped { get; set; }

        internal static async Task<bool> Setup(string ownerTenant = "")
        {
            // TenantId is GUID (CustomerId) and not domain
            if (!ownerTenant.Contains('.'))
            {
                string uri = "https://graph.microsoft.com/v1.0/organization?$select=id";
                List<JsonElement> organisation = await GraphRequestHelper.NewGraphGetRequest(uri, CoreEnvironment.Secrets.TenantId);
                ownerTenant = organisation[0].GetProperty("id").GetString();
            }

            string domain = ownerTenant;
            string scopes = CoreEnvironment.ApiAccessScope;
            string authorizationUrl = string.Format("https://login.microsoftonline.com/{0}/oauth2/v2.0/authorize", ownerTenant);
            string tokenUrl = string.Format("https://login.microsoftonline.com/{0}/oauth2/v2.0/token", ownerTenant);
            string instance = "https://login.microsoftonline.com/";
            string apiScopeGuid = Guid.NewGuid().ToString();

            // step one - create EntraSam SPA that the Swagger UI will use to authenticate

            JsonElement samSpa = (await EntraSam.CreateSAMAuthApp($"RocketRMM UI - {CoreEnvironment.DeviceTag}", EntraSam.SamAppType.Spa, domain, spaRedirectUri: new string[] { $"{CoreEnvironment.RocketRmmFrontEndUri.TrimEnd('/')}/swagger/oauth2-redirect.html", $"{CoreEnvironment.KestrelHttps}/swagger/oauth2-redirect.html", $"{CoreEnvironment.RocketRmmFrontEndUri.TrimEnd('/')}/index.html", $"{CoreEnvironment.KestrelHttps}/index.html", CoreEnvironment.RocketRmmFrontEndUri.TrimEnd('/'), CoreEnvironment.KestrelHttps })).EntraSam;
            string openIdClientId = samSpa.GetProperty("appId").GetString() ?? string.Empty;
            if (!openIdClientId.Equals(string.Empty))
            {
                // Wait 30 seconds to ensure the SPA gets registered
                await Task.Delay(30000);

                // step two - create EntraSam that will act as the authentication hub of the API
                EntraSam.SamAndPassword result = await EntraSam.CreateSAMAuthApp($"RocketRMM API - {CoreEnvironment.DeviceTag}", EntraSam.SamAppType.Api, domain, openIdClientId, scopeGuid: apiScopeGuid);
                JsonElement samApi = result.EntraSam;
                string? appPassword = result.appPassword;
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

                    zeroConf.Save();
                    string bootstrapPath = $"{CoreEnvironment.PersistentDir}/bootstrap.json";
                    await File.WriteAllTextAsync(bootstrapPath, await Utilities.RandomByteString());
                    File.Delete(bootstrapPath);

                    // Setup our front end config file
                    await File.WriteAllTextAsync($"{CoreEnvironment.WebRootPath}/config.js", $@"/* Don't put secret configuration settings in this file, this is rendered
by the client. */

const config = {{
  auth: {{
    clientId: '{zeroConf.OpenIdClientId}',
    authority: 'https://login.microsoftonline.com/organizations/',
    redirectUri: '{CoreEnvironment.RocketRmmFrontEndUri}',
    postLogoutRedirectUri: '{CoreEnvironment.RocketRmmFrontEndUri}/signedout'
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
    frontEndUrl: '{CoreEnvironment.RocketRmmFrontEndUri}',
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
                    Console.WriteLine($"Waiting for bootstrap.json to be placed at {CoreEnvironment.PersistentDir} to provision the API...");
                }
            }
            catch (Exception ex)
            {
                CoreEnvironment.RunErrorCount++;
                Console.WriteLine($"Exception reading CoreZeroConfiguration file: {ex.Message}");
            }

            return false;
        }

        internal static async Task<CoreZeroConfiguration?> Read()
        {
            string apiZeroConfPath = $"{CoreEnvironment.PersistentDir}/api.zeroconf.aes";

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
                Utilities.WriteJsonToFile<CoreZeroConfiguration>(this, $"{CoreEnvironment.PersistentDir}/api.zeroconf.aes", true);
                CoreEnvironment.Secrets.TenantId = this.TenantId;
                CoreEnvironment.Secrets.ApplicationId = this.ClientId;
                CoreEnvironment.Secrets.ApplicationSecret = this.AppPassword;
                CoreEnvironment.Secrets.RefreshToken = this.RefreshToken;
                CoreEnvironment.IsBoostrapped = this.IsBootstrapped ?? false;
                return true;
            }
            catch (Exception ex)
            {
                CoreEnvironment.RunErrorCount++;
                Console.WriteLine($"Exception saving CoreZeroConfiguration file: {ex.Message}");
            }

            return false;
        }
    }
}
