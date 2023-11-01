using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using System.Web;
using RocketRMM.Data.Logging;

namespace RocketRMM.Common
{
    internal static class GraphRequestHelper
    {
        private static readonly HttpClient _httpClient = new();

        /// <summary>
        /// Obtain an access_token from refresh_token to query the Graph API
        /// </summary>
        /// <param name="tenantId">tenantId that you wish to query graph for information about</param>
        /// <param name="asApp">The query will be running as an app or delegated user?</param>
        /// <param name="appId">ID of the app that will be executing the query</param>
        /// <param name="refreshToken">refresh_token used to request access_token</param>
        /// <param name="scope">Scope of our query</param>
        /// <param name="returnRefresh">Return the refresh_token as well as the access_token?</param>
        /// <returns>A dictionary containing either the access_token or all values returned by the query for use in query headers</returns>
        internal static async Task<Dictionary<string, string>> GetGraphToken(string tenantId, bool asApp, string appId = "", string refreshToken = "", string scope = "https://graph.microsoft.com//.default", bool returnRefresh = false)
        {
            string? authBody;

            if (asApp)
            {
                authBody = $"client_id={HttpUtility.UrlEncode(CoreEnvironment.Secrets.ApplicationId)}&client_secret={HttpUtility.UrlEncode(CoreEnvironment.Secrets.ApplicationSecret)}&scope={HttpUtility.UrlEncode(scope)}&grant_type=client_credentials";
            }
            else
            {
                authBody = $"client_id={CoreEnvironment.Secrets.ApplicationId}&client_secret={CoreEnvironment.Secrets.ApplicationSecret}&scope={scope}&refresh_token={CoreEnvironment.Secrets.RefreshToken}&grant_type=refresh_token";
            }

            if (!string.IsNullOrEmpty(appId) && !string.IsNullOrEmpty(refreshToken))
            {
                authBody = $"client_id={appId}&refresh_token={refreshToken}&scope={scope}&grant_type=refresh_token";
            }

            if (string.IsNullOrEmpty(tenantId))
            {
                tenantId = CoreEnvironment.Secrets.TenantId;
            }

            if (!returnRefresh)
            {
                CoreEnvironment.AccessToken accessToken = CoreEnvironment.AccessTokenCache.Find(x => x.AppId.Equals(appId) && x.AsApp.Equals(asApp) && x.Scope.Equals(scope) && x.TenantId.Equals(tenantId));
                if (!string.IsNullOrEmpty(accessToken.Token))
                {
                    if (accessToken.Expires > DateTimeOffset.Now.Subtract(new TimeSpan(3000000000)).ToUnixTimeSeconds())
                    {
                        return new Dictionary<string, string> { ["Authorization"] = accessToken.Token };
                    }

                    // Remove expired accessToken from cache
                    CoreEnvironment.AccessTokenCache.Remove(accessToken);
                }
            }

            using HttpRequestMessage requestMessage = new(HttpMethod.Post, $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token");
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);
                requestMessage.Content = new StringContent(authBody);
                requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                HttpResponseMessage responseMessage = await SendHttpRequest(requestMessage);

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

                    if (returnRefresh)
                    {
                        return headers;
                    }

                    string accessToken = headers.GetValueOrDefault("access_token", string.Empty);
                    CoreEnvironment.AccessTokenCache.Add(new() { AppId = appId, AsApp = asApp, Scope = scope, TenantId = tenantId, Token = accessToken, Expires = (await ReadJwtv1AccessDetails(accessToken)).Expires });

                    return new Dictionary<string, string> { ["Authorization"] = accessToken };
                }
                else if (responseMessage.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // Sleep 1 second if we get a 429 and retry
                    Console.WriteLine($"Got a 429 too many requests to GetGraphToken, waiting 1 second and retrying...");
                    Thread.CurrentThread.Join(1020);
                    return await GetGraphToken(tenantId, asApp, appId, refreshToken, scope, returnRefresh);
                }

                CoreEnvironment.RunErrorCount++;

                // Write to log an error that we didn't get HTTP 2XX
                LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                {
                    Message = $"Incorrect HTTP status code. Expected 2XX got {responseMessage.StatusCode.ToString()}",
                    Severity = "Error",                   
                    API = "GetGraphToken"
                });

                return new Dictionary<string, string> { [string.Empty] = string.Empty };
            }
        }

        /// <summary>
        /// Sends a HTTP GET request to supplied uri using graph access_token for auth
        /// </summary>
        /// <param name="uri">The url we wish to GET from</param>
        /// <param name="tenantId">The tenant the request relates to</param>
        /// <param name="scope"></param>
        /// <param name="asApp">As application or as delegated user</param>
        /// <param name="noPagination"></param>
        /// <returns>A List containing one or more JSON Elements</returns>
        internal static async Task<List<JsonElement>> NewGraphGetRequest(string uri, string tenantId, string scope = "https://graph.microsoft.com//.default", bool asApp = false, bool noPagination = false)
        {
            List<JsonElement> data = new();
            Dictionary<string, string> headers;

            headers = await GetGraphToken(tenantId, asApp, string.Empty, string.Empty, scope);

            LogsDbContext.DebugConsoleWrite($"Using {uri} as url");

            string nextUrl = uri;

            do
            {
                try
                {
                    using HttpRequestMessage requestMessage = new(HttpMethod.Get, uri);
                    {
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", headers.GetValueOrDefault("Authorization", "FAILED-TO-GET-AUTH-TOKEN"));
                        requestMessage.Headers.TryAddWithoutValidation("ConsistencyLevel", "eventual");

                        foreach (KeyValuePair<string, string> _h in headers)
                        {
                            if (!_h.Key.ToLower().Equals("authorization"))
                            {
                                requestMessage.Headers.TryAddWithoutValidation(_h.Key, _h.Value);
                            }
                        }
                        HttpResponseMessage responseMessage = await SendHttpRequest(requestMessage);

                        if (responseMessage.IsSuccessStatusCode)
                        {

                            JsonDocument jsonDoc = await JsonDocument.ParseAsync(new MemoryStream(await responseMessage.Content.ReadAsByteArrayAsync()));

                            if (jsonDoc.RootElement.TryGetProperty("value", out JsonElement outValue))
                            {
                                if (outValue.ValueKind == JsonValueKind.Array)
                                {
                                    data.AddRange(jsonDoc.RootElement.GetProperty("value").EnumerateArray().ToList());
                                }
                                else
                                {
                                    data.Add(jsonDoc.RootElement.GetProperty("value"));
                                }
                            }
                            else
                            {
                                if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                                {
                                    data.AddRange(jsonDoc.RootElement.EnumerateArray().ToList());
                                }
                                else
                                {
                                    data.Add(jsonDoc.RootElement);
                                }
                            }

                            nextUrl = string.Empty;

                            if (!noPagination)
                            {
                                if (jsonDoc.RootElement.TryGetProperty("@odata.nextLink", out JsonElement outNextLink))
                                {
                                    nextUrl = outNextLink.GetString() ?? string.Empty;
                                }
                            }
                        }
                        else if (responseMessage.StatusCode == HttpStatusCode.TooManyRequests)
                        {
                            // Sleep 1 second if we get a 429 and retry
                            Console.WriteLine($"Got a 429 too many requests to {uri}, waiting 1 second and retrying...");
                            Thread.CurrentThread.Join(1020);
                            return await NewGraphGetRequest(uri, tenantId, scope, asApp, noPagination);
                        }
                        else
                        {
                            CoreEnvironment.RunErrorCount++;
                            nextUrl = string.Empty;
                            LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                            {
                                Message = $"Incorrect HTTP status code. Expected 2XX got {responseMessage.StatusCode.ToString()}. Uri: {uri}",
                                Severity = "Error",
                                API = "NewGraphGetRequest"
                            });
                            throw new BadHttpRequestException("We did not get a http ok response from the upstream (graph)");
                        }

                    }
                }
                catch (Exception ex)
                {
                    CoreEnvironment.RunErrorCount++;
                    Console.WriteLine($"Exception in NewGraphGetRequest: {ex.Message}");
                    nextUrl = string.Empty;
                    throw;
                }
            }
            while (!string.IsNullOrEmpty(nextUrl));

            return data;
        }

        /// <summary>
        /// Sends a HTTP GET request to supplied uri using graph access_token for auth
        /// </summary>
        /// <param name="uri">The url we wish to GET from</param>
        /// <param name="tenantId">The tenant the request relates to</param>
        /// <param name="scope"></param>
        /// <param name="asApp">As application or as delegated user</param>
        /// <param name="contentHeader">Set the content header for the type of data we want returned</param>
        /// <returns>A byte[] representing content returned in the response</returns>
        internal static async Task<byte[]>? NewGraphGetRequestBytes(string uri, string tenantId, string scope = "https://graph.microsoft.com//.default", bool asApp = false, string contentHeader = "")
        {
            List<byte> data = new();
            Dictionary<string, string> headers;

            headers = await GetGraphToken(tenantId, asApp, string.Empty, string.Empty, scope);

            LogsDbContext.DebugConsoleWrite($"Using {uri} as url");

            try
            {
                using HttpRequestMessage requestMessage = new(HttpMethod.Get, uri);
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", headers.GetValueOrDefault("Authorization", "FAILED-TO-GET-AUTH-TOKEN"));
                    requestMessage.Headers.TryAddWithoutValidation("ConsistencyLevel", "eventual");

                    if (string.IsNullOrEmpty(contentHeader))
                    {
                        requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(contentHeader);

                    }

                    foreach (KeyValuePair<string, string> _h in headers)
                    {
                        if (!_h.Key.ToLower().Equals("authorization"))
                        {
                            requestMessage.Headers.TryAddWithoutValidation(_h.Key, _h.Value);
                        }
                    }
                    HttpResponseMessage responseMessage = await SendHttpRequest(requestMessage);

                    if (responseMessage.IsSuccessStatusCode)
                    {

                        data.AddRange(await responseMessage.Content.ReadAsByteArrayAsync());

                    }
                    else if (responseMessage.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        // Sleep 1 second if we get a 429 and retry
                        Console.WriteLine($"Got a 429 too many requests to {uri}, waiting 1 second and retrying...");
                        Thread.CurrentThread.Join(1020);
                        return await NewGraphGetRequestBytes(uri, tenantId, scope, asApp, contentHeader);
                    }
                    else
                    {
                        CoreEnvironment.RunErrorCount++;

                        LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                        {
                            Message = $"Incorrect HTTP status code. Expected 2XX got {responseMessage.StatusCode.ToString()}. Uri: {uri}",
                            Severity = "Error",
                            API = "NewGraphGetRequestBytes"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                CoreEnvironment.RunErrorCount++;

                Console.WriteLine($"Exception in NewGraphGetRequest: {ex.Message}");
                throw;
            }

            return data.ToArray();
        }

        /// <summary>
        /// Sends a HTTP POST or other method supplied request to supplied uri using graph access_token for auth
        /// </summary>
        /// <param name="uri">The url we wish to POST to</param>
        /// <param name="tenantId">The tenant relevant to the operation</param>
        /// <param name="body">The object we wish to send as JSON payload in body</param>
        /// <param name="type">HTTP Method POST/PUT etc.</param>
        /// <param name="scope"></param>
        /// <param name="asApp">As application or delegated user</param>
        /// <returns>The content in any response as JSON</returns>
        internal static async Task<JsonElement> NewGraphPostRequest(string uri, string tenantId, object body, HttpMethod type, string scope, bool asApp)
        {
            Dictionary<string, string> headers = await GetGraphToken(tenantId, asApp, string.Empty, string.Empty, scope);

            LogsDbContext.DebugConsoleWrite($"Using {uri} as url");

            try
            {
                using HttpRequestMessage requestMessage = new(type, uri);
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", headers.GetValueOrDefault("Authorization", "FAILED-TO-GET-AUTH-TOKEN"));
                    requestMessage.Headers.TryAddWithoutValidation("ConsistencyLevel", "eventual");

                    foreach (KeyValuePair<string, string> _h in headers)
                    {
                        if (!_h.Key.ToLower().Equals("authorization"))
                        {
                            requestMessage.Headers.TryAddWithoutValidation(_h.Key, _h.Value);
                        }
                    }

                    requestMessage.Content = new StringContent(JsonSerializer.Serialize(body)); 
                    requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                     HttpResponseMessage responseMessage = await SendHttpRequest(requestMessage);

                    if (responseMessage.IsSuccessStatusCode)
                    {
                        if (responseMessage.StatusCode != HttpStatusCode.NoContent)
                        {
                            JsonDocument jsonDoc = await JsonDocument.ParseAsync(new MemoryStream(await responseMessage.Content.ReadAsByteArrayAsync()));
                            return jsonDoc.RootElement;
                        }
                        else
                        {
                            // HTTP 204 No Content so returning empty JsonElement
                            return new JsonElement();
                        }
                    }
                    else if (responseMessage.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        // Sleep 1 second if we get a 429 and retry
                        Console.WriteLine($"Got a 429 too many requests to {uri}, waiting 1 second and retrying...");
                        Thread.CurrentThread.Join(1020);
                        return await NewGraphPostRequest(uri, tenantId, body, type, scope, asApp);
                    }

                    CoreEnvironment.RunErrorCount++;

                    LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                    {
                        Message = $"Incorrect HTTP status code.Expected 2XX got {responseMessage.StatusCode.ToString()}. Uri: {uri}",
                        Severity = "Error",
                        API = "NewGraphPostRequest"
                    });
                }
            }
            catch (Exception ex)
            {
                CoreEnvironment.RunErrorCount++;
                Console.WriteLine($"Exception in NewGraphPostRequest: {ex.Message}");
                throw;
            }

            return new JsonElement();
        }

        /// <summary>
        /// Used to describe a JWT v1 Token
        /// </summary>
        internal struct TokenDetails
        {
            internal TokenDetails(string appId = "", string appName = "", string audience = "", string authMethods = "", string iPAddress = "", string name = "", string scope = "", string tenantId = "", string userPrincipleName = "", string roles = "", long exp = 0, string token = "")
            {
                AppId = appId;
                AppName = appName;
                Audience = audience;
                AuthMethods = authMethods;
                IpAddress = iPAddress;
                Name = name;
                Roles = roles;
                ScopeString = scope;
                Scope = scope.Split(' ');
                TenantId = tenantId;
                UserPrincipalName = userPrincipleName;
                Expires = exp;
                AccessToken = token;

            }

            internal string AppId { get; }
            internal string AppName { get; }
            internal string Audience { get; }
            internal string AuthMethods { get; }
            internal string IpAddress { get; }
            internal string Name { get; }
            internal string Roles { get; }
            internal string ScopeString { get; }
            internal string[] Scope { get; }
            internal string TenantId { get; }
            internal string UserPrincipalName { get; }
            internal long Expires { get; }
            internal string AccessToken { get; }
        }

        /// <summary>
        /// Converts a JWT v1 token into a JSON object
        /// </summary>
        /// <param name="token">Token to decode</param>
        /// <returns>JSON object representing the token</returns>
        internal static async Task<TokenDetails> ReadJwtv1AccessDetails(string token)
        {


            if (!token.Contains('.') || !token.StartsWith("eyJ"))
            {
                return new TokenDetails();
            }

            byte[] tokenPayload = Utilities.Base64UrlDecode(token.Split('.')[1]);
            string appName = string.Empty;
            string upn = string.Empty;
            string amr = string.Empty;
            string ipaddr = string.Empty;
            string name = string.Empty;
            string scp = string.Empty;
            string roles = string.Empty;

            JsonElement jsonToken = (await JsonDocument.ParseAsync(new MemoryStream(tokenPayload))).RootElement;

            if (jsonToken.TryGetProperty("app_displayname", out JsonElement appNameJson))
            {
                appName = appNameJson.GetString() ?? string.Empty;
            }

            if (jsonToken.TryGetProperty("upn", out JsonElement upnJson))
            {
                upn = upnJson.GetString() ?? string.Empty;
            }
            else if (jsonToken.TryGetProperty("unique_name", out upnJson))
            {
                upn = upnJson.GetString() ?? string.Empty;
            }

            if (jsonToken.TryGetProperty("amr", out JsonElement amrJson))
            {
                amr = jsonToken.GetProperty("amr").ToString();
            }

            if (jsonToken.TryGetProperty("ipaddr", out JsonElement ipaddrJson))
            {
                ipaddr = ipaddrJson.GetString() ?? string.Empty;
            }

            if (jsonToken.TryGetProperty("name", out JsonElement nameJson))
            {
                name = nameJson.GetString() ?? string.Empty;
            }

            if (jsonToken.TryGetProperty("scp", out JsonElement scpJson))
            {
                scp = scpJson.GetString() ?? string.Empty;
            }

            if (jsonToken.TryGetProperty("roles", out JsonElement rolesJson))
            {
                roles = jsonToken.GetProperty("roles").ToString() ?? string.Empty;
            }

            return new(jsonToken.GetProperty("appid").ToString(), appName,
                jsonToken.GetProperty("aud").ToString(), amr, ipaddr,
                name, scp, jsonToken.GetProperty("tid").ToString(), upn, roles, long.Parse(jsonToken.GetProperty("exp").ToString()));
        }

        /// <summary>
        /// Uses the HttpClient attached to this class to send HTTP request
        /// </summary>
        /// <param name="requestMessage">HttpRequestMessage to send</param>
        /// <returns>HttpResponseMessage is returned</returns>
        internal static async Task<HttpResponseMessage> SendHttpRequest(HttpRequestMessage requestMessage)
        {
            return await _httpClient.SendAsync(requestMessage);
        }
    }
}
