using CurrentApi = RocketRMM.Api.v10;
using RocketRMM.Data;

namespace RocketRMM.Api
{
    public static class Routes
    {
        public static void InitRoutes(ref WebApplication app)
        {
            /// <summary>
            /// /.auth/me
            /// </summary>
            app.MapGet("/.auth/me", async (HttpContext context, HttpRequest request) =>
            {
                try
                {
                    return await CurrentApi.Routes.AuthMe(context);
                }
                catch (UnauthorizedAccessException)
                {
                    context.Response.StatusCode = 401;
                    return Results.Unauthorized();
                }

            }).WithName("/.auth/me").ExcludeFromDescription();

            /// <summary>
            /// /api/.auth/me
            /// </summary>
            app.MapGet(string.Format("/{0}/.auth/me", CoreEnvironment.ApiHeader), async (HttpContext context, HttpRequest request) =>
            {
                try
                {
                    return await CurrentApi.Routes.AuthMe(context);
                }
                catch (UnauthorizedAccessException)
                {
                    context.Response.StatusCode = 401;
                    return Results.Unauthorized();
                }

            }).WithName(string.Format("/{0}/.auth/me", CoreEnvironment.ApiHeader)).ExcludeFromDescription();

            /// <summary>
            /// /api/CurrentRouteVersion
            /// </summary>
            app.MapGet(string.Format("/{0}/CurrentRouteVersion", CoreEnvironment.ApiHeader), async () =>
            {
                return await CurrentApi.Routes.CurrentRouteVersion();

            }).WithName(string.Format("/{0}/CurrentRouteVersion", CoreEnvironment.ApiHeader)).ExcludeFromDescription();

            /// <summary>
            /// /api/EditUserProfile
            /// </summary>
            app.MapPut(string.Format("/{0}/EditUserProfile", CoreEnvironment.ApiHeader), async (HttpContext context, UserProfile inputProfile, bool? updatePhoto) =>
            {
                try
                {
                    return await CurrentApi.Routes.UpdateUserProfile(context, inputProfile, updatePhoto ?? false);
                }
                catch (UnauthorizedAccessException)
                {
                    context.Response.StatusCode = 401;
                    return Results.Unauthorized();
                }

            }).WithName(string.Format("/{0}/EditUserProfile", CoreEnvironment.ApiHeader)).ExcludeFromDescription();

            /// <summary>
            /// /api/Heartbeat
            /// </summary>
            app.MapGet(string.Format("/{0}/Heartbeat", CoreEnvironment.ApiHeader), async () =>
            {
                Task<CurrentApi.Routes.Heartbeat> task = new(() =>
                {
                    return new CurrentApi.Routes.Heartbeat();
                });

                task.Start();

                return await task;
            })
            .WithName(string.Format("/{0}/Heartbeat", CoreEnvironment.ApiHeader)).ExcludeFromDescription();
        }
    }
}
