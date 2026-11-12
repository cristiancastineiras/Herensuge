using System.Reflection;

namespace Herensuge.Api.Hosting;

internal static class ModuleAssemblies
{
    /// <summary>Commands, queries, validators and event handlers of every module.</summary>
    public static readonly Assembly[] Application =
    [
        Modules.Identity.Application.AssemblyReference.Assembly,
        Modules.Tenancy.Application.AssemblyReference.Assembly,
        Modules.Oidc.Application.AssemblyReference.Assembly,
        Modules.Audit.Application.AssemblyReference.Assembly,
        Modules.Notifications.Application.AssemblyReference.Assembly,
    ];

    /// <summary>Minimal API endpoints of every module.</summary>
    public static readonly Assembly[] Presentation =
    [
        Modules.Identity.Presentation.AssemblyReference.Assembly,
        Modules.Tenancy.Presentation.AssemblyReference.Assembly,
        Modules.Oidc.Presentation.AssemblyReference.Assembly,
        Modules.Audit.Presentation.AssemblyReference.Assembly,
    ];
}
