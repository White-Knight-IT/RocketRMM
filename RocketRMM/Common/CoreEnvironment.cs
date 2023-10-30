using Asp.Versioning.Builder;
using Asp.Versioning;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RocketRMM.Data.Logging;
using RocketRMM.Data;

namespace RocketRMM.Common
{
    internal class CoreEnvironment
    {
        public enum ProductionSecretStores { EncryptedFile };
        // Roles for managing permissions
        public static readonly string RoleOwner = "owner";
        public static readonly string RoleAdmin = "admin";
        public static readonly string RoleEditor = "editor";
        public static readonly string RoleReader = "reader";
#if DEBUG
        public static readonly bool IsDebug = true;
#else
            public static readonly bool IsDebug = false;
#endif
        public static readonly string WorkingDir = Directory.GetCurrentDirectory();
        public static readonly string DataDir = $"{WorkingDir}/Data";
        public static string CacheDir = $"{DataDir}/Cache";
        public static string PersistentDir = ".";
        public static string WebRootPath = $"{WorkingDir}/wwwroot";
        public static readonly string ApiHeader = "Api";
        public static readonly string ApiAccessScope = "rocketrmm-api.access";
        public static readonly string FfppSimulatedAuthUsername = "RocketRMM Simulated Authentication";
        public static string RocketRmmFrontEndUri = "http://localhost";
        public static string Db = "rocketrmmdb";
        public static string DbUser = "sa";
        public static string DbPassword = "localdevroot!!!1";
        public static string DbServer = "localhost";
        public static string DbServerPort = "1433";
        public static List<double> ApiRouteVersions = new() { 1.0 };
        public static ApiVersionSet? ApiVersionSet { get; set; }
        public static readonly ApiVersion ApiDev = new(1.1);
        public static readonly ApiVersion ApiV10 = new(ApiRouteVersions[0]);
        public static readonly ApiVersion ApiV11 = ApiDev;
        public static readonly ApiVersion ApiCurrent = new(ApiRouteVersions[^1]);
        public static readonly DateTime Started = DateTime.UtcNow;
        public static readonly int DbBackoffMs = 20;
        public static bool SimulateAuthenticated = false;
        public static bool ShowDevEnvEndpoints = false;
        public static bool ShowSwaggerUi = false;
        public static bool RunSwagger = false;
        public static bool ServeStaticFiles = false;
        public static bool UseHttpsRedirect = true;
        public static string? DeviceTag = string.Empty;
        public static string KestrelHttp = "https://localhost:7073";
        public static string KestrelHttps = "https://localhost:7074";
        public static long RunErrorCount = 0;
        public static bool IsBoostrapped = false;
        public static List<AccessToken> AccessTokenCache = new();
        public static readonly string DefaultSystemUsername = "HAL";
        /// <summary>
        /// Build data directories including cache directories if they don't exist
        /// </summary>
        public static void DataAndCacheDirectoriesBuild()
        {
            Directory.CreateDirectory(CacheDir);
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(PersistentDir);
            Console.WriteLine($"Cache Directory: {CacheDir}");
            Console.WriteLine($"Data Directory: {DataDir}");
            Console.WriteLine($"Persistent Directory: {PersistentDir}");
        }

        /// <summary>
        /// Gets a unique 32 byte Device ID
        /// </summary>
        /// <returns>DeviceId as byte[32] array</returns>
        public static async Task<byte[]> GetDeviceId()
        {
            byte[] hmacSalt = UTF8Encoding.UTF8.GetBytes($"ffppDevId{await GetDeviceIdTokenSeed()}seedBytes");

            try
            {
                byte[] hashyBytes = HMACSHA512.HashData(hmacSalt, await GetEntropyBytes());

                // key strech the device id using 173028 HMACSHA512 iterations
                for (int i = 0; i < 173028; i++)
                {
                    hashyBytes = HMACSHA512.HashData(hmacSalt, hashyBytes);
                }

                // Final hash reduces bytes to byte[32] array perfect for use as AES256 key
                return HMACSHA256.HashData(hmacSalt, hashyBytes);
            }
            catch (Exception ex)
            {
                CoreEnvironment.RunErrorCount++;

                LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                {
                    Message = $"Exception GetDeviceId: {ex.Message}",
                    Severity = "Error",
                    API = "GetDeviceId"
                });

                throw ex;
            }
        }

        /// <summary>
        /// Shuts down the API (terminates)
        /// </summary>
        /// <param name="error"></param>
        public static void ShutDownApi(int error = 0)
        {
            System.Environment.Exit(error);
        }

        /// <summary>
        /// Update DB with latest migrations
        /// </summary>
        public static async Task<bool> UpdateDbContexts()
        {
            /*try
            {
                using (LogsDbContext logsDb = new())
                {
                    await logsDb.Database.MigrateAsync();
                }

                using (UserProfilesDbContext userProfilesDb = new())
                {
                    await userProfilesDb.Database.MigrateAsync();
                }

                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Didn't create DB tables, this is expected if they already exist - server: {CoreEnvironment.DbServer} - port: {CoreEnvironment.DbServerPort}");
            }*/

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static async Task<byte[]> GetEntropyBytes()
        {
            string entropyBytesPath = $"{PersistentDir}/unique.entropy.bytes";
            if (!File.Exists(entropyBytesPath))
            {
                await File.WriteAllTextAsync(entropyBytesPath, await Utilities.RandomByteString());
            }

            return await Utilities.Base64Decode(await File.ReadAllTextAsync(entropyBytesPath));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> CheckForBootstrap()
        {
            string bootstrapPath = $"{PersistentDir}/bootstrap.json";
            try
            {

                // Bootstrap file exists and we don't already have an app password
                if (File.Exists(bootstrapPath))
                {
                    Console.WriteLine($"Found bootstrap.json at {bootstrapPath}");
                    JsonElement result = await Utilities.ReadJsonFromFile<JsonElement>(bootstrapPath);
                    CoreEnvironment.Secrets.TenantId = result.GetProperty("TenantId").GetString();
                    CoreEnvironment.Secrets.ApplicationId = result.GetProperty("ApplicationId").GetString();
                    CoreEnvironment.Secrets.ApplicationSecret = result.GetProperty("ApplicationSecret").GetString();
                    await CoreZeroConfiguration.Setup(CoreEnvironment.Secrets.TenantId);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to setup Azure AD EntraSam applications using bootstrap.json, exception: {ex.Message}");
                Console.WriteLine("This is a fatal exception because the API cannot function without the needed EntraSam apps. Shutting API down...");
                CoreEnvironment.ShutDownApi(1);
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static async Task<string> GetDeviceTag()
        {
            return (await CoreEnvironment.GetDeviceIdTokenSeed())[^6..];
        }

        // Gets the DeviceIdTokenSeed used as static entropy in DeviceId generation
        private static async Task<string> GetDeviceIdTokenSeed()
        {
            try
            {
                string deviceTokenPath = $"{PersistentDir}/device.id.token";

                if (!File.Exists(deviceTokenPath))
                {
                    await File.WriteAllTextAsync(deviceTokenPath, Guid.NewGuid().ToString());
                }

                return (await File.ReadAllTextAsync(deviceTokenPath)).TrimEnd('\n').Trim();
            }
            catch (Exception ex)
            {
                CoreEnvironment.RunErrorCount++;

                LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                {
                    Message = $"Exception GetDeviceIdToken: {ex.Message}",
                    Severity = "Error",
                    API = "GetDeviceIdTokenSeed"
                });

                throw ex;
            }
        }

        public struct ApiTokenStatus
        {
            public ApiTokenStatus()
            {
                refreshToken = false;
                exchangeRefreshToken = false;

                if (!string.IsNullOrEmpty(Secrets.RefreshToken))
                {
                    refreshToken = true;
                }
            }

            public bool refreshToken { get; set; }
            public bool exchangeRefreshToken { get; set; }
        }

        public struct AccessToken
        {
            public string AppId { get; set; }
            public bool AsApp { get; set; }
            public string TenantId { get; set; }
            public string Scope { get; set; }
            public string Token { get; set; }
            public long Expires { get; set; }
        }

        /// <summary>
        /// This is used to store the secrets in an encrypted file
        /// </summary>
        public static class Secrets
        {
            public static string? ApplicationId { get; set; }
            public static string? ApplicationSecret { get; set; }
            public static string? TenantId { get; set; }
            public static string? RefreshToken { get; set; }
        }
    }
}
