using Herensuge.Modules.Access.Application.Abstractions;
using Herensuge.Modules.Access.Application.Decisions;
using Herensuge.Modules.Access.Domain.ResourceServers;
using Herensuge.Modules.Access.Infrastructure.Relationships;
using OpenFga.Sdk.Client.Model;

namespace Herensuge.Modules.Access.UnitTests;

/// <summary>Per-object permissions (plan D10): ids, the fixed model and how roles and relations combine.</summary>
public sealed class RelationshipTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);

    private static readonly ResourceServer Docs = ResourceServer.Register(
        new AccessManifest(
            "docs",
            "Documentos",
            "docs-app",
            null,
            [
                new ManifestPermission("document:read", null, null, Relation: Relations.Viewer),
                new ManifestPermission("document:edit", null, null, Relation: Relations.Editor, RelationMode: RelationModes.And),
            ],
            [new ManifestRole("reader", "Lectura", null, ["document:read"], [])]),
        Now).Value;

    private static readonly SubjectFacts Signed = new(true, ["pwd"], DateTimeOffset.UtcNow, null);

    private readonly AccessEvaluator _evaluator = new(null!, null!, null!, null!, null!, TimeProvider.System);

    private static AccessGrants Holding(params string[] permissions) =>
        new("member", [], new SortedSet<string>(permissions, StringComparer.Ordinal));

    [Fact]
    public void Ids_keep_each_application_and_organization_apart()
    {
        Guid tenant = Guid.Parse("0198a1b2-0000-7000-8000-000000000002");

        Assert.Equal(
            "object:docs/0198a1b2-0000-7000-8000-000000000002/document/d-1",
            RelationshipIds.Object(new RelationshipObject("docs", tenant, "document", "d-1")));
        Assert.Equal("group:g1#member", RelationshipIds.Subject(new RelationshipSubject(RelationshipSubject.Group, "g1"), "docs", tenant));
        Assert.Equal(
            "object:docs/0198a1b2-0000-7000-8000-000000000002/folder/f-1",
            RelationshipIds.Subject(new RelationshipSubject(RelationshipSubject.Object, "folder/f-1"), "docs", tenant));

        Assert.True(RelationshipIds.IsValidPart("inv-2026.11_a~b"));
        Assert.False(RelationshipIds.IsValidPart("a:b"));
        Assert.False(RelationshipIds.IsValidPart("a#b"));
        Assert.False(RelationshipIds.IsValidPart("a b"));
        Assert.False(RelationshipIds.IsValidPart("../x/y"));
    }

    [Fact]
    public void The_fixed_model_parses_with_four_types()
    {
        ClientWriteAuthorizationModelRequest model = ClientWriteAuthorizationModelRequest.FromJson(OpenFgaBootstrapper.Model);

        Assert.Equal("1.1", model.SchemaVersion);
        Assert.Equal(["user", "client", "group", "object"], model.TypeDefinitions.Select(type => type.Type));
    }

    [Fact]
    public void With_or_the_role_suffices_and_the_relation_covers_the_rest()
    {
        Assert.Null(AccessEvaluator.RelationNeeded(Docs, Holding("document:read"), "document:read"));
        Assert.True(_evaluator.Evaluate(Docs, Holding("document:read"), Signed, "document:read").Decision);

        Assert.Equal(Relations.Viewer, AccessEvaluator.RelationNeeded(Docs, Holding(), "document:read"));
        Assert.True(_evaluator.Evaluate(Docs, Holding(), Signed, "document:read", relation: RelationCheck.Held).Decision);
        Assert.Equal(AccessReasons.NoRelation, _evaluator.Evaluate(Docs, Holding(), Signed, "document:read", relation: RelationCheck.Missing).Reason);
        Assert.Equal("relationships_unavailable", _evaluator.Evaluate(Docs, Holding(), Signed, "document:read", relation: RelationCheck.Unavailable).Reason);
        Assert.Equal("relationships_disabled", _evaluator.Evaluate(Docs, Holding(), Signed, "document:read", relation: RelationCheck.Disabled).Reason);
    }

    [Fact]
    public void With_and_both_the_role_and_the_relation_are_needed()
    {
        Assert.Null(AccessEvaluator.RelationNeeded(Docs, Holding(), "document:edit"));
        Assert.Equal(AccessReasons.NoRole, _evaluator.Evaluate(Docs, Holding(), Signed, "document:edit", relation: RelationCheck.Held).Reason);

        AccessGrants editor = Holding("document:edit");
        Assert.Equal(Relations.Editor, AccessEvaluator.RelationNeeded(Docs, editor, "document:edit"));
        Assert.True(_evaluator.Evaluate(Docs, editor, Signed, "document:edit", relation: RelationCheck.Held).Decision);
        Assert.Equal(AccessReasons.NoRelation, _evaluator.Evaluate(Docs, editor, Signed, "document:edit").Reason);

        // Granted only together with a relation: the token lists it as conditional, so the SDK asks the PDP (plan D3).
        AccessClaimSet claims = AccessEvaluator.ToClaims(Docs, Holding("document:read", "document:edit"));
        Assert.Equal(["document:read"], claims.Permissions);
        Assert.Equal(["document:edit"], claims.ConditionalPermissions);
    }

    [Fact]
    public void A_relation_mode_without_a_relation_is_rejected()
    {
        var manifest = new AccessManifest("x", "X", "x-app", null, [new ManifestPermission("a:b", null, null, RelationMode: RelationModes.And)], []);

        Assert.True(ResourceServer.Register(manifest, Now).IsFailure);
    }
}
