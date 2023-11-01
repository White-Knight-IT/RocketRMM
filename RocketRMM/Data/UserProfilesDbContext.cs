using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using RocketRMM.Common;
using RocketRMM.Data.Logging;

namespace RocketRMM.Data
{
    public class UserProfilesDbContext : DbContext
    {
        private DbSet<UserProfile>? _userProfiles { get; set; }

        public UserProfilesDbContext()
        {

        }

        internal async Task<bool> AddUserProfile(UserProfile user)
        {
            Task<bool> addTask = new(() =>
            {
                try
                {
                    if (!ExistsById(user.UserId).Result)
                    {
                        Add(user);
                        SaveChanges();
                        return true;
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    CoreEnvironment.RunErrorCount++;
                    Console.WriteLine($"Exception writing  in UserProfiles: {ex.Message}");
                    throw;
                }
            });

            addTask.Start();
            return await addTask;
        }



        internal async Task<bool> ExistsById(Guid userId)
        {
            if (await _userProfiles.FindAsync(userId) == null)
            {
                return false;
            }

            return true;
        }

        internal async Task<UserProfile>? GetById(Guid userId)
        {
            try
            {
                return await _userProfiles.FindAsync(userId);
            }
            catch
            {

            }

            return null;
        }

        internal async Task<bool> UpdateUserProfile(UserProfile userProfile, bool updatePhoto = true)
        {
            try
            {
                UserProfile? foundUser = await _userProfiles.FindAsync(userProfile.UserId);

                if (foundUser != null)
                {
                    if (updatePhoto)
                    {
                        foundUser.PhotoData = userProfile.PhotoData;
                    }

                    foundUser.Name = userProfile.Name;
                    foundUser.IdentityProvider = userProfile.IdentityProvider;
                    foundUser.Theme = userProfile.Theme;
                    foundUser.UserDetails = userProfile.UserDetails;
                    SaveChanges();

                    return true;
                }
            }
            catch (Exception ex)
            {
                CoreEnvironment.RunErrorCount++;

                _ = LogsDbThreadSafeCoordinator.ThreadSafeAdd(new LogEntry()
                {
                    Message = $"Error updating user profile for {userProfile.UserId.ToString()} - {userProfile.Name}: {ex.Message}",
                    Severity = "Error",
                    API = "UpdateUserProfile"
                });
            }

            return false;
        }

        internal async Task<bool> Exists(Guid userId)
        {
            if (await _userProfiles.FindAsync(userId) == null)
            {
                return false;
            }

            return true;
        }

        // Tells EF that we want to use MySQL
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            string connectionString = $"server={CoreEnvironment.DbServer};port={CoreEnvironment.DbServerPort};database={CoreEnvironment.Db};user={CoreEnvironment.DbUser};password={CoreEnvironment.DbPassword}";
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        }
    }

    /// <summary>
    /// Represents a UserProfile object as it exists in the UserProfiles DB
    /// </summary>
    public class UserProfile
    {
        [Key] // Public key
        public Guid UserId { get; set; }
        public string? IdentityProvider { get; set; }
        public string? Name { get; set; }
        public string? UserDetails { get; set; }
        [NotMapped] // We never save roles they may change and relying on old roles is security risk
        public List<string>? UserRoles { get; set; }
        public string? Theme { get; set; }
        public string? PhotoData { get; set; }
    }

    /// <summary>
    /// A class for accessing the UserProfilesDbContext in a thread safe manner
    /// </summary>
    public static class UserProfilesDbThreadSafeCoordinator
    {
        private static bool _locked = false;

        /// <summary>
        /// Thread safe means of updating a user profile
        /// </summary>
        /// <param name="userProfile">The user profile to update in DB</param>
        /// <param name="updatePhoto">Bool to allow/reject updating of photo</param>
        /// <returns>bool indicating success</returns>
        internal static async Task<bool> ThreadSafeUpdateUserProfile(UserProfile userProfile, bool updatePhoto)
        {
            WaitForUnlock();

            _locked = true;

            Task<bool>? updateUserProfile = new(() =>
            {
                using (UserProfilesDbContext userProfiles = new())
                {
                    return userProfiles.UpdateUserProfile(userProfile, updatePhoto).Result;
                }
            });

            return await ExecuteQuery<bool>(updateUserProfile);
        }

        /// <summary>
        /// Thread safe means to get a User Profile from DB
        /// </summary>
        /// <param name="userId">User ID of the user profile to return</param>
        /// <returns>User profile if it exists else null</returns>
        internal static async Task<UserProfile>? ThreadSafeGetUserProfile(Guid userId)
        {
            WaitForUnlock();

            _locked = true;

            Task<UserProfile>? getUserProfile = new(() =>
            {
                using (UserProfilesDbContext userProfiles = new())
                {
                    return userProfiles.GetById(userId).Result;
                }
            });

            return await ExecuteQuery<UserProfile>(getUserProfile);
        }

        /// <summary>
        /// Add a tenant to the ExcludedTenantsDbContext in a thread safe manner
        /// </summary>
        /// <param name="excludedTenant">tenant to add to DB</param>
        /// <returns>bool indicating success</returns>
        internal static async Task<bool> ThreadSafeAdd(UserProfile userProfile)
        {
            WaitForUnlock();

            _locked = true;

            Task<bool> addUserProfile = new(() =>
            {
                using UserProfilesDbContext userProfiles = new();
                return userProfiles.AddUserProfile(userProfile).Result;
            });

            return await ExecuteQuery<bool>(addUserProfile);
        }

        private static async Task<type> ExecuteQuery<type>(Task<type> taskToRun)
        {
            try
            {
                taskToRun.Start();
                return (type)await taskToRun;
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
