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
        internal enum ProductionSecretStores { EncryptedFile };
        // Roles for managing permissions
        internal static readonly string RoleOwner = "owner";
        internal static readonly string RoleAdmin = "admin";
        internal static readonly string RoleEditor = "editor";
        internal static readonly string RoleReader = "reader";
#if DEBUG
        internal static readonly bool IsDebug = true;
#else
            internal static readonly bool IsDebug = false;
#endif
        internal static readonly string CoreVersion = "0.0.1:alpha";
        internal static readonly string WorkingDir = Directory.GetCurrentDirectory();
        internal static readonly string DataDir = $"{WorkingDir}/Data";
        internal static string CacheDir = $"{DataDir}/Cache";
        internal static string PersistentDir = ".";
        internal static string WebRootPath = $"{WorkingDir}/wwwroot";
        internal static readonly string ApiHeader = "Api";
        internal static readonly string ApiAccessScope = "rocketrmm-api.access";
        internal static readonly string FfppSimulatedAuthUsername = "RocketRMM Simulated Authentication";
        internal static string RocketRmmFrontEndUri = "http://localhost";
        internal static string Db = "rocketrmmdb";
        internal static string DbUser = "sa";
        internal static string DbPassword = "localdevroot!!!1";
        internal static string DbServer = "localhost";
        internal static string DbServerPort = "1433";
        internal static List<double> ApiRouteVersions = new() { 1.0 };
        internal static ApiVersionSet? ApiVersionSet { get; set; }
        internal static readonly ApiVersion ApiDev = new(1.1);
        internal static readonly ApiVersion ApiV10 = new(ApiRouteVersions[0]);
        internal static readonly ApiVersion ApiV11 = ApiDev;
        internal static readonly ApiVersion ApiCurrent = new(ApiRouteVersions[^1]);
        internal static readonly DateTime Started = DateTime.UtcNow;
        internal static readonly int DbBackoffMs = 20;
        internal static bool SimulateAuthenticated = false;
        internal static bool ShowDevEnvEndpoints = false;
        internal static bool ShowSwaggerUi = false;
        internal static bool RunSwagger = false;
        internal static bool ServeStaticFiles = false;
        internal static bool UseHttpsRedirect = true;
        internal static string? DeviceTag = string.Empty;
        internal static string KestrelHttp = "https://localhost:7073";
        internal static string KestrelHttps = "https://localhost:7074";
        internal static long RunErrorCount = 0;
        internal static bool IsBoostrapped = false;
        internal static List<AccessToken> AccessTokenCache = new();
        internal static readonly string DefaultSystemUsername = "HAL";
        /// <summary>
        /// Build data directories including cache directories if they don't exist
        /// </summary>
        internal static void DataAndCacheDirectoriesBuild()
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
        internal static async Task<byte[]> GetDeviceId()
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
        internal static void ShutDownApi(int error = 0)
        {
            System.Environment.Exit(error);
        }

        /// <summary>
        /// Update DB with latest migrations
        /// </summary>
        internal static async Task<bool> UpdateDbContexts()
        {
            try
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
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        internal static async Task<byte[]> GetEntropyBytes()
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
        internal static async Task<bool> CheckForBootstrap()
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
        internal static async Task<string> GetDeviceTag()
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

        internal struct ApiTokenStatus
        {
            public ApiTokenStatus()
            {
                refreshToken = false;

                if (!string.IsNullOrEmpty(Secrets.RefreshToken))
                {
                    refreshToken = true;
                }
            }

            internal bool refreshToken { get; set; }
        }

        internal struct AccessToken
        {
            internal string AppId { get; set; }
            internal bool AsApp { get; set; }
            internal string TenantId { get; set; }
            internal string Scope { get; set; }
            internal string Token { get; set; }
            internal long Expires { get; set; }
        }

        /// <summary>
        /// This is used to store the secrets in an encrypted file
        /// </summary>
        internal static class Secrets
        {
            internal static string? ApplicationId { get; set; }
            internal static string? ApplicationSecret { get; set; }
            internal static string? TenantId { get; set; }
            internal static string? RefreshToken { get; set; }
        }
    }
}
