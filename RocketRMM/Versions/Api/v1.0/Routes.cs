using Microsoft.Identity.Web.Resource;
using System.Security.Claims;
using RocketRMM.Data;
using RocketRMM.Api.v10.Users;

namespace RocketRMM.Api.v10
{
    /// <summary>
    /// /v1.0 ### THIS IS THE V1.0 ENDPOINTS ###
    /// </summary>
    public static class Routes
    {
        private static readonly string[] _tags = ["RocketRMM API"];
        private static readonly string _versionHeader = "v1.0";

        public static void InitRoutes(ref WebApplication app)
        {
            /// <summary>
            /// /v1.0/.auth/me
            /// </summary>
            app.MapGet("/v{version:apiVersion}/.auth/me", async (HttpContext context, HttpRequest request) =>
            {
                try
                {
                    return await AuthMe(context);
                }
                catch (UnauthorizedAccessException)
                {
                    context.Response.StatusCode = 401;
                    return Results.Unauthorized();
                }
                catch
                {
                    context.Response.StatusCode = 500;
                    return Results.Problem();
                }

            }).Produces<Auth>(200).WithTags(_tags).WithName(string.Format("/{0}/.auth/me", _versionHeader)).WithApiVersionSet(CoreEnvironment.ApiVersionSet).MapToApiVersion(CoreEnvironment.ApiV10);

            /// <summary>
            /// /v1.0/CurrentRouteVersion
            /// </summary>
            app.MapGet("/v{version:apiVersion}/CurrentRouteVersion", async (HttpContext context) =>
            {
                try
                {
                    return await CurrentRouteVersion();
                }
                catch
                {
                    context.Response.StatusCode = 500;
                    return Results.Problem();
                }

            }).WithTags(_tags).WithName(string.Format("/{0}/CurrentRouteVersion", _versionHeader)).WithApiVersionSet(CoreEnvironment.ApiVersionSet).MapToApiVersion(CoreEnvironment.ApiV10);

            /// <summary>
            /// /v1.0/EditUserProfile
            /// </summary>
            app.MapPut("/v{version:apiVersion}/EditUserProfile", async (HttpContext context, UserProfile inputProfile, bool? updatePhoto) =>
            {
                try
                {
                    return await UpdateUserProfile(context, inputProfile, updatePhoto ?? false);
                }
                catch (UnauthorizedAccessException)
                {
                    context.Response.StatusCode = 401;
                    return Results.Unauthorized();
                }
                catch
                {
                    context.Response.StatusCode = 500;
                    return Results.Problem();
                }

            }).WithTags(_tags).WithName(string.Format("/{0}/EditUserProfile", _versionHeader)).WithApiVersionSet(CoreEnvironment.ApiVersionSet).MapToApiVersion(CoreEnvironment.ApiV10);

            /// <summary>
            /// /v1.0/Heartbeat
            /// </summary>
            app.MapGet("/v{version:apiVersion}/Heartbeat", async (HttpContext context) =>
            {
                try
                {
                    Task<object> task = new(() =>
                    {
                        return new Heartbeat();
                    });

                    task.Start();

                    return await task;
                }
                catch
                {
                    context.Response.StatusCode = 500;
                    return Results.Problem();
                }
            }).WithTags(_tags).WithName(string.Format("/{0}/Heartbeat", _versionHeader)).WithApiVersionSet(CoreEnvironment.ApiVersionSet).MapToApiVersion(CoreEnvironment.ApiV10);
        }

        public static async Task<object> CurrentRouteVersion()
        {
            Task<CurrentApiRoute> task = new(() =>
            {
                return new CurrentApiRoute();
            });

            task.Start();

            return await task;
        }

        public static async Task<object> AuthMe(HttpContext context)
        {
            if (!CoreEnvironment.SimulateAuthenticated)
            {
                CheckUserIsReader(context);
            }

            Task<Auth> task = new(() =>
            {
                List<string> roles = [];

                // I think we can only have one role but I'll iterate just in case it happens
                foreach (Claim c in context.User.Claims.Where(x => x.Type.ToLower().Equals("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")).ToList())
                {
                    roles.Add(c.Value);
                }
                try
                {
                    return new Auth()
                    {
                        ClientPrincipal = new()
                        {
                            UserId = Guid.Parse(context.User.Claims.First(x => x.Type.ToLower().Equals("http://schemas.microsoft.com/identity/claims/objectidentifier")).Value),
                            IdentityProvider = "aad",
                            Name = context.User.Claims.First(x => x.Type.ToLower().Equals("name")).Value,
                            UserDetails = context.User.Claims.First(x => x.Type.ToLower().Equals("preferred_username")).Value,
                            UserRoles = roles
                        }
                    };
                }
                catch
                {
                    context.Response.StatusCode = 400;
                    return new Auth();
                }
            });

            task.Start();

            Auth authUserProfile = await task;

            authUserProfile.ClientPrincipal.PhotoData = await User.GetUserPhoto(authUserProfile.ClientPrincipal.UserId.ToString(), UserPhotoSize.Small, context.User.Claims.First(x => x.Type.ToLower().Contains("tenantid")).Value);

            // Check if profile exists, update and use if it does, create and use if it doesn't

            UserProfile? userDbProfile = await UserProfilesDbThreadSafeCoordinator.ThreadSafeGetUserProfile(authUserProfile.ClientPrincipal.UserId);

            // User has no profile so we will create it
            if (userDbProfile == null)
            {
                authUserProfile.ClientPrincipal.Theme = "dark";
                UserProfilesDbThreadSafeCoordinator.ThreadSafeAdd(authUserProfile.ClientPrincipal);
                return authUserProfile;
            }

            // User exists in the DB yay let us use it
            authUserProfile.ClientPrincipal.Theme = userDbProfile.Theme;
            UserProfilesDbThreadSafeCoordinator.ThreadSafeAdd(authUserProfile.ClientPrincipal);
            return authUserProfile;
        }

        public static async Task<object> UpdateUserProfile(HttpContext context, UserProfile inputProfile, bool updatePhoto)
        {
            if (!CoreEnvironment.SimulateAuthenticated)
            {
                CheckUserIsReader(context);
            }

            // Make sure that the users auth token matches the user who's profile they are trying to update
            if (Guid.Parse(context.User.Claims.First(x => x.Type.ToLower().Equals("http://schemas.microsoft.com/identity/claims/objectidentifier")).Value) != inputProfile.UserId)
            {
                throw new UnauthorizedAccessException();
            }

            try
            {
                List<string> roles = [];

                // Make sure we get all roles
                foreach (Claim c in context.User.Claims.Where(x => x.Type.ToLower().Equals("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")).ToList())
                {
                    roles.Add(c.Value);
                }

                inputProfile.IdentityProvider = "aad";
                inputProfile.Name = context.User.Claims.First(x => x.Type.ToLower().Equals("name")).Value;
                inputProfile.UserDetails = context.User.Claims.First(x => x.Type.ToLower().Equals("preferred_username")).Value;
                inputProfile.UserRoles = roles;
                // We apply sensible defaults below if we are given null for values
                inputProfile.Theme ??= "dark";

                await UserProfilesDbThreadSafeCoordinator.ThreadSafeUpdateUserProfile(inputProfile, updatePhoto);

                return true;
            }
            catch
            {
                // to-do error handling
            }

            return false;
        }

        private static void CheckUserIsReader(HttpContext context)
        {
            string[] scopes = [CoreEnvironment.ApiAccessScope];
            string[] roles = [CoreEnvironment.RoleOwner, CoreEnvironment.RoleAdmin, CoreEnvironment.RoleTech, CoreEnvironment.RoleReader];
            context.ValidateAppRole(roles);
            context.VerifyUserHasAnyAcceptedScope(scopes);
        }

        private static void CheckUserIsTech(HttpContext context)
        {
            string[] scopes = [CoreEnvironment.ApiAccessScope];
            string[] roles = [CoreEnvironment.RoleOwner, CoreEnvironment.RoleAdmin, CoreEnvironment.RoleTech];
            context.ValidateAppRole(roles);
            context.VerifyUserHasAnyAcceptedScope(scopes);
        }

        private static void CheckUserIsAdmin(HttpContext context)
        {
            string[] scopes = [CoreEnvironment.ApiAccessScope];
            string[] roles = [CoreEnvironment.RoleOwner, CoreEnvironment.RoleAdmin];
            context.ValidateAppRole(roles);
            context.VerifyUserHasAnyAcceptedScope(scopes);
        }

        private static void CheckUserIsOwner(HttpContext context)
        {
            string[] scopes = [CoreEnvironment.ApiAccessScope];
            string[] roles = [CoreEnvironment.RoleOwner];
            context.ValidateAppRole(roles);
            context.VerifyUserHasAnyAcceptedScope(scopes);
        }

        /// <summary>
        /// Defines a ClientPrincipal returned when /.auth/me is called
        /// </summary>
        public struct Auth
        {
            public UserProfile ClientPrincipal { get; set; }
        }

        /// <summary>
        /// Defines a Heartbeat object we return when the /api/Heartbeat API is polled
        /// </summary>
        public struct Heartbeat
        {
            public readonly DateTime Started { get => CoreEnvironment.Started; }
            public readonly long ErrorsSinceStarted { get => CoreEnvironment.RunErrorCount; }
            public readonly bool? IsBootstrapped { get => CoreEnvironment.IsBoostrapped; }
        }

        /// <summary>
        /// Defines the latest version API scheme when /api/CurrentApiRoute queried (returns dev when dev endpoints enabled)
        /// </summary>
        public struct CurrentApiRoute
        {
            public readonly string Api { get => "v" + CoreEnvironment.ApiRouteVersions[^1].ToString("f1"); }
        }

        /// <summary>
        /// Defines the error we send back in JSON payload
        /// </summary>
        public struct ErrorResponseBody
        {
            public int ErrorCode { get; set; }
            public string Message { get; set; }
        }
    }
}
