using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using RocketRMM.Common;

namespace RocketRMM.Data.Logging
{
    internal class LogsDbContext : DbContext
    {
        private DbSet<LogEntry>? _logEntries { get; set; }

        public LogsDbContext()
        {

        }

        internal async Task<List<LogEntry>> ListLogs()
        {
            return await _logEntries.ToListAsync() ?? new();
        }

        internal async Task<List<LogEntry>> Top10Logs()
        {
            return await _logEntries.OrderByDescending(x => x.Timestamp).Take(10).ToListAsync() ?? new();
        }

        /// <summary>
        /// Writes to the console only if we are running in debug
        /// </summary>
        /// <param name="content">Content to write to console</param>
        /// <returns>bool which indicates successful write to console</returns>
        internal static bool DebugConsoleWrite(string content)
        {
            if (CoreEnvironment.IsDebug)
            {
                Console.WriteLine(content);
                return true;
            }

            return false;
        }

        internal async Task<bool> AddLogEntry(LogEntry logEntry)
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

                if (logEntry.Severity.ToLower().Equals("debug") && !CoreEnvironment.IsDebug)
                {
                    Console.WriteLine("Not writing to log file - Debug mode is not enabled.");
                }

                // Write to console for debug environment
                DebugConsoleWrite($"[ {DateTime.UtcNow} ] - {logEntry.Severity} - {logEntry.Message} - {logEntry.API} - {logEntry.Username} - {logEntry.SentAsAlert}");

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
                            Thread.CurrentThread.Join(attempts * CoreEnvironment.DbBackoffMs); // Sleep a multiple of a 5th of a second each attempt
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
                Console.WriteLine($"Exception writing log entry in RocketRMMLogs: {ex.Message}");
            }

            return false;
        }

        // Tells EF that we want to use SQLServerExpress
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            string connectionString = $"Data Source={CoreEnvironment.DbServer},{CoreEnvironment.DbServerPort}; Initial Catalog={CoreEnvironment.Db}; User Id={CoreEnvironment.DbUser}; Password={CoreEnvironment.DbPassword}; TrustServerCertificate=true";
            options.UseSqlServer(connectionString);
        }
    }

    // Represents a LogEntry object as it exists in the Logs DB
    internal class LogEntry
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
    internal static class LogsDbThreadSafeCoordinator
    {
        private static bool _locked = false;

        public static async Task<List<LogEntry>> ThreadSafeTop10Logs()
        {
            WaitForUnlock();

            _locked = true;

            Task<List<LogEntry>> getTop10 = new(() =>
            {
                using (LogsDbContext logEntries = new())
                {
                    return logEntries.Top10Logs().Result;
                }
            });

            return await ExecuteQuery(getTop10);
        }

        /// <summary>
        /// Add a log entry to the CoreEnvironment in a thread safe manner
        /// </summary>
        /// <param name="log">Log to add to DB</param>
        /// <returns>bool indicating success</returns>
        internal static async Task<bool> ThreadSafeAdd(LogEntry log)
        {
            WaitForUnlock();

            // By setting lock we do not allow any other DbContexts to be created and other queries will queue
            // until this value returns false;
            _locked = true;

            Task<bool> addLog = new(() =>
            {
                using (LogsDbContext logEntries = new())
                {
                    return logEntries.AddLogEntry(log).Result;
                }
            });

            return await ExecuteQuery(addLog);
        }

        private static async Task<type> ExecuteQuery<type>(Task<type> taskToRun)
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
                Thread.CurrentThread.Join(CoreEnvironment.DbBackoffMs);
            }
        }
    }
}
