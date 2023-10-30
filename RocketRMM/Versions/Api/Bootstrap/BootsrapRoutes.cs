using System.Text.Json;
using System.Net.Http.Headers;
using RocketRMM.Common;

namespace RocketRMM.Api.Bootstrap
{
    public static class BootsrapRoutes
    {
        public static void InitRoutes(ref WebApplication app)
        {
            /// <summary>
            /// /bootstrap/TokenStatus
            /// </summary>
            app.MapGet("/bootstrap/TokenStatus", async () =>
            {
                Task<CoreEnvironment.ApiTokenStatus> getTokenStatus = new(() =>
                {
                    return new CoreEnvironment.ApiTokenStatus();
                });

                getTokenStatus.Start();
                return await getTokenStatus;

            }).WithName("/bootstrap/TokenStatus").ExcludeFromDescription();

            /// <summary>
            /// /bootstrap/GetGraphTokenUrl
            /// </summary>
            app.MapGet("/bootstrap/GetGraphTokenUrl", async (HttpContext context) =>
            {
                if (!CoreEnvironment.IsBoostrapped)
                {
                    Task<GraphTokenUrl> getUrl = new(() =>
                    {
                        return new() { url = $"https://login.microsoftonline.com/{CoreEnvironment.Secrets.TenantId}/oauth2/v2.0/authorize?scope=https://graph.microsoft.com/.default+offline_access+openid+profile&response_type=code&client_id={CoreEnvironment.Secrets.ApplicationId}&redirect_uri={CoreEnvironment.RocketRmmFrontEndUri}/bootstrap/receivegraphtoken" };
                    });

                    getUrl.Start();
                    return await getUrl;
                }
                else
                {
                    context.Response.StatusCode = 410;
                    return new();
                }

            }).WithName("/bootstrap/GetGraphTokenUrl").ExcludeFromDescription();

            /// <summary>
            /// /bootstrap/ReceiveGraphToken
            /// </summary>
            app.MapGet("/bootstrap/ReceiveGraphToken", async (HttpContext context, HttpRequest request, string code) =>
            {
                if (!CoreEnvironment.IsBoostrapped)
                {
                    HttpRequestMessage requestMessage = new(HttpMethod.Post, $"https://login.microsoftonline.com/{CoreEnvironment.Secrets.TenantId}/oauth2/v2.0/token");
                    requestMessage.Content = new StringContent($"client_id={CoreEnvironment.Secrets.ApplicationId}&scope=https://graph.microsoft.com/.default+offline_access+openid+profile&code={code}&redirect_uri={CoreEnvironment.RocketRmmFrontEndUri}/bootstrap/receivegraphtoken&grant_type=authorization_code&client_secret={CoreEnvironment.Secrets.ApplicationSecret}");

                    requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                    HttpResponseMessage responseMessage = await GraphRequestHelper.SendHttpRequest(requestMessage);

                    if (responseMessage.IsSuccessStatusCode)
                    {
                        string responseRawString = await responseMessage.Content.ReadAsStringAsync();
                        string[] headersRaw = responseRawString[1..^1].Split(",");

                        Dictionary<string, string> headers = new();

                        foreach (string headerPairRaw in headersRaw)
                        {
                            string[] h = headerPairRaw.Split(":");

                            if (h[0].StartsWith('"'))
                            {
                                // Trim the redundant " char from start and end of string
                                h[0] = h[0][1..^1];
                            }

                            if (h[1].StartsWith('"'))
                            {
                                h[1] = h[1][1..^1];
                            }

                            headers.Add(h[0], h[1]);
                        }

                        CoreEnvironment.Secrets.RefreshToken = headers["refresh_token"];
                        CoreZeroConfiguration zeroConf = await CoreZeroConfiguration.Read();
                        zeroConf.RefreshToken = CoreEnvironment.Secrets.RefreshToken;
                        zeroConf.Save();
                        //redirect to success page
                        context.Response.Redirect($"{CoreEnvironment.RocketRmmFrontEndUri}/setup/graphtoken/success");
                    }
                    else
                    {
                        //redirect to error page
                        context.Response.Redirect($"{CoreEnvironment.RocketRmmFrontEndUri}/setup/graphtoken/error");
                    }
                }
                else
                {
                    context.Response.StatusCode = 410;
                }

            }).WithName("/bootstrap/ReceiveGraphToken").ExcludeFromDescription();
        }

        public struct GraphTokenUrl
        {
            public string url { get; set; }
        }
    }
}
