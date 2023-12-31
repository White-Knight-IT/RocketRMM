﻿namespace RocketRMM.Api.v11
{
    /// <summary>
    /// /v1.1 ### THIS IS THE V1.1 ENDPOINTS (CURRENT DEV) ###
    /// </summary>
    public static class Routes
    {
        private static readonly string[] _tags = ["RocketRMM API Dev"];
        private static string _versionHeader = "v1.1";

        public static void InitRoutes(ref WebApplication app)
        {
            /// <summary>
            /// /v1.1/CurrentRouteVersion
            /// </summary>
            app.MapGet("/v{version:apiVersion}/CurrentRouteVersion", () =>
            {
                return CurrentRouteVersion();

            }).WithTags(_tags).WithName(string.Format("/{0}/CurrentRouteVersion", _versionHeader)).WithApiVersionSet(CoreEnvironment.ApiVersionSet).MapToApiVersion(CoreEnvironment.ApiV11);
        }

        public static CurrentApiRoute CurrentRouteVersion()
        {
            return new CurrentApiRoute();
        }

        /// <summary>
        /// Defines the latest version API scheme when queried (returns dev when dev endpoints enabled)
        /// </summary>
        public struct CurrentApiRoute
        {
            public readonly string Api { get => "v" + CoreEnvironment.ApiRouteVersions[^1].ToString("f1"); }
        }
    }
}