using System.Reflection;

namespace Herensuge.Modules.Audit.Application;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
