namespace Herensuge.Modules.Identity.Presentation.Me;

/// <summary>/api/v1/me: self-service, called by the portal through the BFF with a Bearer token.</summary>
internal static class MeRoutes
{
    public const string Prefix = "/api/v1/me";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder app) =>
        app.MapGroup(Prefix)
            .WithTags("Me")
            .RequireAuthorization(HerensugePolicies.Api);
}
