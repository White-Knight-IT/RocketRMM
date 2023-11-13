using Asp.Versioning.Builder;
using Asp.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RocketRMM.Data.Logging;
using RocketRMM.Data;
using MySqlConnector;
using System.Runtime.InteropServices;

namespace RocketRMM
{
    internal class CoreEnvironment
    {
        internal enum ProductionSecretStores { EncryptedFile };
        internal enum CertificateType { Authentication = 10000, SamAuthentication=10001, CodeSigning = 10002, Intermediary = 10003, Ca = 10004 };
        internal enum OsType { Windows, MacOs, Linux, FreeBsd, Indeterminate};
        // Roles for managing permissions
        internal static readonly string RoleOwner = "owner";
        internal static readonly string RoleAdmin = "admin";
        internal static readonly string RoleTech = "tech";
        internal static readonly string RoleReader = "reader";
#if DEBUG
        internal static readonly bool IsDebug = true;
#else
            internal static readonly bool IsDebug = false;
#endif
        internal static string LogLevel = "information";
        internal static readonly string CoreVersion = "0.0.1:alpha";
        internal static readonly string WorkingDir = Directory.GetCurrentDirectory();
        internal static string? PersistentDir;
        internal static string? DataDir;
        internal static string? CacheDir;
        internal static string? PkiDir;
        internal static string? CaDir;
        internal static string? CaIntermediateDir;
        internal static string? CertificatesDir;
        internal static string? CurrentSamAuthCertificateDir;
        internal static string? CrlDir;
        internal static string? CaRootCertPem;
        internal static string? CurrentCaIntermediateCertPem;
        internal static readonly string CaRootCertName = "ca";
        internal static string CurrentCaIntermediateCertName = "intermediateca1";
        internal static readonly string[] CaIntermediateCertNames = ["intermediateca1", "intermediateca2","intermediateca3"];
        internal static string? WebRootPath;
        internal static readonly string ApiHeader = "Api";
        internal static readonly string ApiAccessScope = "rocketrmm-api.access";
        internal static readonly string SimulatedAuthUsername = "RocketRMM Simulated Authentication";
        internal static string? FrontEndUri;
        internal static string? Db;
        internal static string? DbUser;
        internal static string? DbPassword;
        internal static string? DbServer;
        internal static string? DbServerPort;
        internal static List<double> ApiRouteVersions = [1.0];
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
        internal static string? DeviceTag = string.Empty;
        internal static string? KestrelHttp;
        internal static long RunErrorCount = 0;
        internal static bool IsBoostrapped = false;
        internal static List<AccessToken> AccessTokenCache = [];
        internal static readonly string DefaultSystemUsername = "HAL";

        /// <summary>
        /// Gets supplied setting from the appsettings.json file, handles exceptions for missing/malformed settings gracefully
        /// </summary>
        /// <typeparam name="T">Type of object we are getting from settings</typeparam>
        /// <param name="builder">WebApplicationBuilder used to access the settings file</param>
        /// <param name="settingPath">Setting that we want to retrieve i.e. "ApiSettings:HttpsRedirect" </param>
        /// <param name="defaultValue">Default value that is returned if the setting is null or has an error</param>
        /// <returns>The setting that we requested</returns>
        internal static T? TryGetSetting<T>(WebApplicationBuilder builder, string settingPath, T defaultValue)
        {
            try
            {
                return builder.Configuration.GetValue<T>(settingPath) ?? defaultValue;
            }
            catch(Exception ex)
            {
                _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                {
                    Message = $"Error getting setting: {settingPath} - instead using default value: {defaultValue} - {ex.Message}",
                    Severity = "Error",
                    API = "TryGetSetting"
                });
            }

            return defaultValue;
        }

        /// <summary>
        /// Returns the OS this code is currently executing on
        /// </summary>
        public static OsType GetOperatingSystem()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return OsType.Windows;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return OsType.MacOs;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return OsType.Linux;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                return OsType.FreeBsd;
            }

            return OsType.Indeterminate;
        }

        /// <summary>
        /// Build data directories including cache directories if they don't exist
        /// </summary>
        internal static void DataAndCacheDirectoriesBuild()
        {
            Directory.CreateDirectory(PersistentDir);
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(CacheDir);
            Directory.CreateDirectory(CaDir);
            Directory.CreateDirectory($"{CaIntermediateDir}{Path.DirectorySeparatorChar}revoked");
            Directory.CreateDirectory($"{CertificatesDir}{Path.DirectorySeparatorChar}revoked");
            Directory.CreateDirectory($"{CertificatesDir}{Path.DirectorySeparatorChar}sam");
            Directory.CreateDirectory(CrlDir);

            _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
            {
                Message = $"Cache Directory: {CacheDir}",
                Severity = "Debug",
                API = "DataAndCacheDirectoriesBuild"
            });

            _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
            {
                Message = $"Data Directory: {DataDir}",
                Severity = "Debug",
                API = "DataAndCacheDirectoriesBuild"
            });

            _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
            {
                Message = $"Persistent Directory: {PersistentDir}",
                Severity = "Debug",
                API = "DataAndCacheDirectoriesBuild"
            });
        }

        /// <summary>
        /// Get keys that are pinned to the device id + unique entropy
        /// </summary>
        /// <param name="level">number of times to hash wrap the key before producing it. This allows us to create many different encryption keys all from the same source of entropy</param>
        /// <returns></returns>
        internal static async Task<byte[]> GetDeviceIdGeneratedKey(int level = 0, int iterations = 183029)
        {
            byte[] hmacSalt = SHA512.HashData(Encoding.UTF8.GetBytes($"saltisnot{await GetDeviceId()}secretsquirrel"));

            try
            {
                byte[] hashyBytes = HMACSHA512.HashData(hmacSalt, await GetEntropyBytes());

                // key strech the key using HMACSHA512 iterations + level to create as many different keys as we like from the single entropy source
                for (int i = 0; i < iterations + level; i++)
                {
                    hashyBytes = HMACSHA512.HashData(hmacSalt, hashyBytes);
                }

                // Final hash reduces bytes to byte[32] array perfect for use as AES256 key
                return HMACSHA256.HashData(hmacSalt, hashyBytes);
            }
            catch (Exception ex)
            {
                RunErrorCount++;

                _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                {
                    Message = $"Exception GetDeviceIdGeneratedKey: {ex.Message}",
                    Severity = "Error",
                    API = "GetDeviceIdGeneratedKey"
                });

                throw;
            }
        }

        /// <summary>
        /// Shuts down the Core (terminates)
        /// </summary>
        /// <param name="error"></param>
        internal static void ShutDownCore(int error = 0)
        {
            _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
            {
                Message = $"Shutting down RocketRMM Core",
                Severity = "Information",
                API = "ShutDownCore"
            });

            Environment.Exit(error);
        }

        /// <summary>
        /// Update DB with latest migrations
        /// </summary>
        internal static void UpdateDbContexts()
        {
            int dbConnectAttempt = 0;
            int maxAttempts = 12;

            while (dbConnectAttempt < maxAttempts)
            {
                try
                {
                    using LogsDbContext logsDb = new();
                    logsDb.Database.Migrate();

                    using UserProfilesDbContext userProfilesDb = new();
                    userProfilesDb.Database.Migrate();

                    break;
                }
                catch (MySqlException ex)
                {
                    dbConnectAttempt++;

                    if (ex.ErrorCode == MySqlErrorCode.TableExists)
                    {
                        break;
                    }
                    else if(ex.ErrorCode == MySqlErrorCode.UnableToConnectToHost)
                    {
                        Utilities.ConsoleColourWriteLine($"Error with DB server on {DbServer}:{DbServerPort}, waiting 5 seconds and trying again [{dbConnectAttempt}/{maxAttempts}].\nException: {ex.Message}", ConsoleColor.Yellow);
                        Thread.CurrentThread.Join(5000);

                        if (dbConnectAttempt >= maxAttempts)
                        {
                            Utilities.ConsoleColourWriteLine($"Error with DB server on {DbServer}:{DbServerPort}, This is a fatal error, shutting down RocketRMM Core.\nException: {ex.Message}", ConsoleColor.Yellow);
                            ShutDownCore(420);
                        }
                    }
                    else
                    { 
                        Utilities.ConsoleColourWriteLine($"Error with DB server on {DbServer}:{DbServerPort}, This is a fatal error, shutting down RocketRMM Core.\nException: {ex.Message}", ConsoleColor.Yellow);
                        ShutDownCore(911);
                    }
                }
            }
        }

        /// <summary>
        /// Gets an array containing 4098 crypto random bytes, these bytes are also stored in a file on disk so they are persistant and constant once created.
        /// </summary>
        /// <returns></returns>
        internal static async Task<byte[]> GetEntropyBytes()
        {
            string entropyBytesPath = $"{PersistentDir}{Path.DirectorySeparatorChar}unique.entropy.bytes";
            if (!File.Exists(entropyBytesPath))
            {
                await File.WriteAllTextAsync(entropyBytesPath, await Utilities.Crypto.RandomByteString(4098,true));
            }

            return await Utilities.Base64Decode(await File.ReadAllTextAsync(entropyBytesPath));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        internal static async Task<bool> CheckForBootstrap()
        {
            string bootstrapPath = $"{PersistentDir}{Path.DirectorySeparatorChar}bootstrap.json";
            try
            {

                // Bootstrap file exists and we don't already have an app password
                if (File.Exists(bootstrapPath))
                {
                    Utilities.ConsoleColourWriteLine($"Found bootstrap.json at {bootstrapPath}");
                    JsonElement result = await Utilities.ReadJsonFromFile<JsonElement>(bootstrapPath);
                    Secrets.TenantId = result.GetProperty("TenantId").GetString();
                    Secrets.ApplicationId = result.GetProperty("ApplicationId").GetString();
                    Secrets.ApplicationSecret = result.GetProperty("ApplicationSecret").GetString();
                    _ = await CoreZeroConfiguration.Setup(Secrets.TenantId);
                    return true;
                }
            }
            catch (Exception ex)
            {
                RunErrorCount++;

                _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                {
                    Message = $"Failed to setup Entra SAM applications using bootstrap.json, exception: {ex.Message}\nThis is a fatal exception because the Core cannot function without the needed SAM apps. Shutting Core down...",
                    Severity = "Critical",
                    API = "CheckForBootstrap"
                });

                ShutDownCore(1);
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        internal static async Task<string> GetDeviceTag()
        {
            return (await GetDeviceId())[^6..];
        }

        // Gets the DeviceIdTokenSeed used as static entropy in DeviceId generation
        internal static async Task<string> GetDeviceId()
        {
            try
            {
                string deviceTokenPath = $"{PersistentDir}{Path.DirectorySeparatorChar}device.id.token";

                if (!File.Exists(deviceTokenPath))
                {
                    if(!Directory.Exists(PersistentDir))
                    {
                        DataAndCacheDirectoriesBuild();
                    }

                    await File.WriteAllTextAsync(deviceTokenPath, Guid.NewGuid().ToString());
                }

                return (await File.ReadAllTextAsync(deviceTokenPath)).TrimEnd('\n').Trim();
            }
            catch (Exception ex)
            {
                RunErrorCount++;

                _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                {
                    Message = $"Exception GetDeviceId: {ex.Message}",
                    Severity = "Error",
                    API = "GetDeviceId"
                });

                throw;
            }
        }

        internal struct ApiTokenStatus
        {
            public ApiTokenStatus()
            {
                RefreshToken = false;

                if (!string.IsNullOrEmpty(Secrets.RefreshToken))
                {
                    RefreshToken = true;
                }
            }

            [JsonInclude]
            internal bool RefreshToken { get; set; }
        }

        internal struct AccessToken
        {
            [JsonInclude]
            internal string AppId { get; set; }
            [JsonInclude]
            internal bool AsApp { get; set; }
            [JsonInclude]
            internal string TenantId { get; set; }
            [JsonInclude]
            internal string Scope { get; set; }
            [JsonInclude]
            internal string Token { get; set; }
            [JsonInclude]
            internal long Expires { get; set; }
        }

        /// <summary>
        /// This is used to store the secrets in an encrypted file
        /// </summary>
        internal static class Secrets
        {
            [JsonInclude]
            internal static string? ApplicationId { get; set; }
            [JsonInclude]
            internal static string? ApplicationSecret { get; set; }
            [JsonInclude]
            internal static string? TenantId { get; set; }
            [JsonInclude]
            internal static string? RefreshToken { get; set; }
        }
    }
}
