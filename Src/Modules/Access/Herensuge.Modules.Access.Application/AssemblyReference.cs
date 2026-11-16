using System.Reflection;

namespace Herensuge.Modules.Access.Application;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
