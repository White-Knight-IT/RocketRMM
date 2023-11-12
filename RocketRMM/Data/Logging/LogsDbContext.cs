using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace RocketRMM.Data.Logging
{
    public class LogsDbContext : DbContext
    {
        private DbSet<LogEntry>? _logEntries { get; set; }

        public LogsDbContext()
        {

        }

        public async Task<List<LogEntry>> ListLogs()
        {
            return await _logEntries.ToListAsync() ?? new();
        }

        public async Task<List<LogEntry>> Top10Logs()
        {
            return await _logEntries.OrderByDescending(x => x.Timestamp).Take(10).ToListAsync() ?? new();
        }

        public async Task<bool> AddLogEntry(LogEntry logEntry)
        {
            try
            {
                logEntry.Timestamp = DateTime.UtcNow;

                if (string.IsNullOrEmpty(logEntry.Username))
                {
                    logEntry.Username = CoreEnvironment.DefaultSystemUsername;
                }

                if (string.IsNullOrEmpty(logEntry.API))
                {
                    logEntry.API = "None";
                }

                if (null == logEntry.SentAsAlert)
                {
                    logEntry.SentAsAlert = false;
                }

                // Write to console for debug environment
                switch (logEntry.Severity.ToLower())
                {
                    case "critical":
                        if (!CoreEnvironment.LogLevel.Equals("critical") && !CoreEnvironment.LogLevel.Equals("error") && !CoreEnvironment.LogLevel.Equals("warning") && !CoreEnvironment.LogLevel.Equals("debug") && !CoreEnvironment.LogLevel.Equals("trace") && !CoreEnvironment.LogLevel.Equals("information"))
                        {
                            return false;
                        }
                        Utilities.ConsoleColourWriteLine($"[ {DateTime.UtcNow} ] - {logEntry.Severity} - {logEntry.Message} - {logEntry.API} - {logEntry.Username} - {logEntry.SentAsAlert}", ConsoleColor.Red);
                        break;

                    case "error":
                        if (!CoreEnvironment.LogLevel.Equals("error") && !CoreEnvironment.LogLevel.Equals("warning") && !CoreEnvironment.LogLevel.Equals("debug") && !CoreEnvironment.LogLevel.Equals("trace") && !CoreEnvironment.LogLevel.Equals("information"))
                        {
                            return false;
                        }
                        Utilities.ConsoleColourWriteLine($"[ {DateTime.UtcNow} ] - {logEntry.Severity} - {logEntry.Message} - {logEntry.API} - {logEntry.Username} - {logEntry.SentAsAlert}", ConsoleColor.Red);
                        break;

                    case "warning":
                        if (!CoreEnvironment.LogLevel.Equals("warning") && !CoreEnvironment.LogLevel.Equals("debug") && !CoreEnvironment.LogLevel.Equals("trace") && !CoreEnvironment.LogLevel.Equals("information"))
                        {
                            return false;
                        }
                        Utilities.ConsoleColourWriteLine($"[ {DateTime.UtcNow} ] - {logEntry.Severity} - {logEntry.Message} - {logEntry.API} - {logEntry.Username} - {logEntry.SentAsAlert}", ConsoleColor.Yellow);
                        break;

                    case "information":
                        if(!CoreEnvironment.LogLevel.Equals("debug") && !CoreEnvironment.LogLevel.Equals("trace") && !CoreEnvironment.LogLevel.Equals("information"))
                        {
                            return false;
                        }
                        Utilities.ConsoleColourWriteLine($"[ {DateTime.UtcNow} ] - {logEntry.Severity} - {logEntry.Message} - {logEntry.API} - {logEntry.Username} - {logEntry.SentAsAlert}");
                        break;

                    case "debug":
                        if(!CoreEnvironment.IsDebug && !CoreEnvironment.LogLevel.Equals("debug") && !CoreEnvironment.LogLevel.Equals("trace"))
                        {
                            return false;
                        }
                        Utilities.ConsoleColourWriteLine($"[ {DateTime.UtcNow} ] - {logEntry.Severity} - {logEntry.Message} - {logEntry.API} - {logEntry.Username} - {logEntry.SentAsAlert}", ConsoleColor.Cyan);
                        break;

                    case "trace":
                        if(!CoreEnvironment.IsDebug && !CoreEnvironment.LogLevel.Equals("trace"))
                        {
                            return false;
                        }
                        Utilities.ConsoleColourWriteLine($"[ {DateTime.UtcNow} ] - {logEntry.Severity} - {logEntry.Message} - {logEntry.API} - {logEntry.Username} - {logEntry.SentAsAlert}", ConsoleColor.Gray);
                        break;

                    default:
                        Utilities.ConsoleColourWriteLine($"[ {DateTime.UtcNow} ] - {logEntry.Severity} - {logEntry.Message} - {logEntry.API} - {logEntry.Username} - {logEntry.SentAsAlert}");
                        break;
                }

                Task<bool> task = new(() =>
                {
                    int repeatOnFail = 5;
                    int attempts = 1;

                    do
                    {
                        try
                        {
                            Add(logEntry);
                            SaveChanges();
                            attempts = repeatOnFail + 1;
                            return true;
                        }
                        catch
                        {
                            _ = Thread.CurrentThread.Join(attempts * CoreEnvironment.DbBackoffMs); // Sleep a multiple of a 5th of a second each attempt
                            attempts++;

                            if (attempts > repeatOnFail)
                            {
                                throw;
                            }
                        }
                    }
                    while (attempts <= repeatOnFail);

                    return false;
                });

                task.Start();

                return await task;
            }
            catch (Exception ex)
            {
                CoreEnvironment.RunErrorCount++;
                Utilities.ConsoleColourWriteLine($"Exception writing log entry in RocketRMMLogs: {ex.Message}\nWhat would have been logged is: [ {DateTime.UtcNow} ] - {logEntry.Severity} - {logEntry.Message} - {logEntry.API} - {logEntry.Username} - {logEntry.SentAsAlert}");
            }

            return false;
        }

        // Tells EF that we want to use MySQL
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            string connectionString = $"server={CoreEnvironment.DbServer};port={CoreEnvironment.DbServerPort};database={CoreEnvironment.Db};user={CoreEnvironment.DbUser};password={CoreEnvironment.DbPassword}";
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        }
    }

    // Represents a LogEntry object as it exists in the Logs DB
    public class LogEntry
    {
        [Key] // Public key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Auto Generate GUID for our PK
        public Guid RowKey { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Severity { get; set; }
        public string? Message { get; set; }
        public string? API { get; set; }
        public bool? SentAsAlert { get; set; }
        public string? Username { get; set; }
    }

    /// <summary>
    /// A class for accessing the LogsDbContext in a thread safe manner
    /// </summary>
    public static class LogsDbThreadSafeCoordinator
    {
        private static bool _locked = false;

        public static async Task<List<LogEntry>> ThreadSafeTop10Logs()
        {
            WaitForUnlock();

            _locked = true;

            Task<List<LogEntry>> getTop10 = new(() =>
            {
                using LogsDbContext logEntries = new();
                return logEntries.Top10Logs().Result;
            });

            return await ExecuteQuery(getTop10);
        }

        /// <summary>
        /// Add a log entry to the CoreEnvironment in a thread safe manner
        /// </summary>
        /// <param name="log">Log to add to DB</param>
        /// <returns>bool indicating success</returns>
        public static async Task<bool> ThreadSafeAdd(LogEntry log)
        {
            WaitForUnlock();

            // By setting lock we do not allow any other DbContexts to be created and other queries will queue
            // until this value returns false;
            _locked = true;

            Task<bool> addLog = new(() =>
            {
                using LogsDbContext logEntries = new();
                return logEntries.AddLogEntry(log).Result;
            });

            return await ExecuteQuery(addLog);
        }

        private static async Task<T> ExecuteQuery<T>(Task<T> taskToRun)
        {
            try
            {
                taskToRun.Start();
                return await taskToRun;
            }
            catch
            {
                // We make sure we unlock when an exception occurs as to not end up in a perpetually locked state
                _locked = false;
                throw;
            }
            finally
            {
                _locked = false;
            }
        }

        // Blocking wait for DB context to become unlocked
        private static void WaitForUnlock()
        {
            while (_locked)
            {
                _ = Thread.CurrentThread.Join(CoreEnvironment.DbBackoffMs);
            }
        }
    }
}
