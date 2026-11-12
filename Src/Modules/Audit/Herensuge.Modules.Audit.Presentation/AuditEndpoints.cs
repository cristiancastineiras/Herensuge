using System.Reflection;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Application.Security;
using Herensuge.Common.Presentation.Endpoints;
using Herensuge.Common.Presentation.Filters;
using Herensuge.Common.Presentation.Results;
using Herensuge.Modules.Audit.Application.Entries.GetAuditEntries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Herensuge.Modules.Audit.Presentation;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}

/// <summary>Audit log of one organization (owners/admins) and of the whole platform (platform admins).</summary>
internal sealed class AuditEndpoints : IEndpoint
{
    private const int DefaultPageSize = 50;

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/tenants/{tenantId:guid}/audit", async (
                Guid tenantId,
                string? action,
                DateTime? before,
                int? pageSize,
                IQueryHandler<GetAuditEntriesQuery, AuditPage> handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new GetAuditEntriesQuery(tenantId, null, action, before, pageSize ?? DefaultPageSize), cancellationToken))
                    .Match(value => Results.Ok(value)))
            .WithTags("Organization")
            .RequireAuthorization(HerensugePolicies.Api)
            .RequireActiveTenant()
            .RequireTenantAdministrator()
            .Produces<AuditPage>();

        app.MapGet("/api/v1/platform/audit", async (
                Guid? tenantId,
                Guid? userId,
                string? action,
                DateTime? before,
                int? pageSize,
                IQueryHandler<GetAuditEntriesQuery, AuditPage> handler,
                CancellationToken cancellationToken) =>
                (await handler.Handle(new GetAuditEntriesQuery(tenantId, userId, action, before, pageSize ?? DefaultPageSize), cancellationToken))
                    .Match(value => Results.Ok(value)))
            .WithTags("Platform")
            .RequireAuthorization(HerensugePolicies.PlatformAdmin)
            .Produces<AuditPage>();
    }
}
