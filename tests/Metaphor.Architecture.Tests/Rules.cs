using System.Globalization;
using Mono.Cecil;

namespace Metaphor.Architecture.Tests;

/// <summary>
/// The architecture rules from ADR 0003, listed in docs/coding-standards.md. Each rule returns one line per violation,
/// empty when the assembly complies.
/// </summary>
internal static class Rules
{
    /// <summary>The one assembly allowed to touch the game, the loader, Windows and raw memory.</summary>
    public const string HookLayer = "Metaphor.Hooks";

    /// <summary>Each feature is its own assembly under this prefix.</summary>
    public const string FeaturePrefix = "Metaphor.Features.";

    /// <summary>Assemblies allowed to use networking, each with the ADR that approved it. None so far.</summary>
    private static readonly Dictionary<string, string> NetworkingExceptions = [];

    /// <summary>Assemblies allowed to load native libraries themselves, each with the ADR that approved it. None so far.</summary>
    private static readonly Dictionary<string, string> NativeLoadingExceptions = [];

    /// <summary>APIs that read or write memory by address, bypassing the runtime's checks.</summary>
    private static readonly HashSet<string> RawMemoryTypes =
    [
        "System.IO.UnmanagedMemoryAccessor",
        "System.IO.UnmanagedMemoryStream",
        "System.Runtime.CompilerServices.Unsafe",
        "System.Runtime.InteropServices.CollectionsMarshal",
        "System.Runtime.InteropServices.GCHandle",
        "System.Runtime.InteropServices.Marshal",
        "System.Runtime.InteropServices.MemoryMarshal",
        "System.Runtime.InteropServices.NativeMemory",
        "System.Runtime.InteropServices.SafeBuffer",
    ];

    /// <summary>Runtime APIs that load a native library or let code choose where one is loaded from.</summary>
    private static readonly HashSet<string> NativeLoadingMembers =
    [
        "System.Runtime.InteropServices.NativeLibrary::Load",
        "System.Runtime.InteropServices.NativeLibrary::TryLoad",
        "System.Runtime.InteropServices.NativeLibrary::SetDllImportResolver",
        "System.Runtime.Loader.AssemblyLoadContext::LoadUnmanagedDll",
        "System.Runtime.Loader.AssemblyLoadContext::LoadUnmanagedDllFromPath",
        "System.Runtime.Loader.AssemblyLoadContext::add_ResolvingUnmanagedDll",
    ];

    private const int System32SearchPath = 0x800; // DllImportSearchPath.System32

    /// <summary>No assembly uses networking without an ADR-approved exception.</summary>
    public static IReadOnlyList<string> NoNetworking(AssemblyDefinition assembly) =>
        NetworkingExceptions.ContainsKey(assembly.Name.Name)
            ? []
            : [.. ExternalTypes(assembly)
                .Where(type => IsInNamespace(type, "System.Net"))
                .Select(type => $"{assembly.Name.Name} uses networking type {type.FullName}")];

    /// <summary>Plain libraries reference neither the loader (Reloaded-II) nor anything Windows-only.</summary>
    public static IReadOnlyList<string> NoLoaderOrWindows(AssemblyDefinition assembly)
    {
        if (IsHookLayer(assembly))
        {
            return [];
        }
        var name = assembly.Name.Name;
        var loader = assembly.Modules
            .SelectMany(module => module.AssemblyReferences)
            .Where(reference => reference.Name.StartsWith("Reloaded", StringComparison.Ordinal))
            .Select(reference => $"{name} references the loader assembly {reference.Name}");
        var windowsTypes = ExternalTypes(assembly)
            .Where(type => IsInNamespace(type, "Microsoft.Win32"))
            .Select(type => $"{name} uses Windows-only type {type.FullName}");
        var platform = TargetsWindows(assembly) ? [$"{name} targets windows"] : Array.Empty<string>();
        var windowsMembers = Members(assembly)
            .Where(member => member.CustomAttributes.Any(IsWindowsPlatform))
            .Select(member => $"{name} marks {member.FullName} Windows-only");
        return [.. loader, .. windowsTypes, .. platform, .. windowsMembers];
    }

    /// <summary>True when the assembly declares Windows as its target or supported platform, as net9.0-windows does.</summary>
    public static bool TargetsWindows(AssemblyDefinition assembly) => assembly.CustomAttributes.Any(IsWindowsPlatform);

    /// <summary>Only the hook layer allows unsafe code or uses pointers, raw-memory APIs or native calls (P/Invoke).</summary>
    public static IReadOnlyList<string> NoRawMemoryOutsideHooks(AssemblyDefinition assembly)
    {
        if (IsHookLayer(assembly))
        {
            return [];
        }
        var name = assembly.Name.Name;
        // The compiler marks every module built with unsafe code allowed, which catches pointers that never reach a
        // parameter, local or field.
        var unsafeCode = assembly.Modules
            .Where(module => module.CustomAttributes.Any(a =>
                a.AttributeType.FullName == "System.Security.UnverifiableCodeAttribute"))
            .Select(module => $"{name} allows unsafe code in {module.Name}");
        var apis = ExternalTypes(assembly)
            .Where(type => RawMemoryTypes.Contains(type.FullName))
            .Select(type => $"{name} uses raw-memory API {type.FullName}");
        var pointers = Methods(assembly)
            .Where(method => SignatureAndLocals(method).Any(ContainsPointer))
            .Select(method => $"{name} uses a pointer in {method.FullName}");
        var fields = Types(assembly)
            .SelectMany(type => type.Fields)
            .Where(field => ContainsPointer(field.FieldType))
            .Select(field => $"{name} uses a pointer in field {field.FullName}");
        var nativeCalls = NativeCalls(assembly).Select(method => $"{name} declares native call {method.FullName}");
        return [.. unsafeCode, .. apis, .. pointers, .. fields, .. nativeCalls];
    }

    /// <summary>
    /// Native calls search System32 only, so a DLL planted next to the game can't stand in for a system one. Loading any
    /// other native library needs an ADR-approved exception that loads it by full path.
    /// </summary>
    public static IReadOnlyList<string> NativeLibrariesLoadSafely(AssemblyDefinition assembly)
    {
        var name = assembly.Name.Name;
        var nativeCalls = NativeCalls(assembly).ToList();
        var assemblyWide = SearchPaths(assembly.CustomAttributes);
        var unrestricted = nativeCalls
            .Where(method => (SearchPaths(method.CustomAttributes) ?? assemblyWide) != System32SearchPath)
            .Select(method => $"{name} declares native call {method.FullName} without restricting its search to System32");
        if (NativeLoadingExceptions.ContainsKey(name))
        {
            return [.. unrestricted];
        }
        var loads = assembly.Modules
            .SelectMany(module => module.GetMemberReferences())
            .Select(member => $"{member.DeclaringType.FullName}::{member.Name}")
            .Where(NativeLoadingMembers.Contains)
            .Select(member => $"{name} calls {member}");
        var loadLibrary = nativeCalls
            .Where(method => method.PInvokeInfo.EntryPoint.StartsWith("LoadLibrary", StringComparison.Ordinal)
                || method.PInvokeInfo.EntryPoint == "LdrLoadDll")
            .Select(method => $"{name} loads a library through native call {method.FullName}");
        return [.. unrestricted, .. loads, .. loadLibrary];
    }

    /// <summary>A feature depends on interfaces in Core, never on another feature.</summary>
    public static IReadOnlyList<string> FeaturesAreIndependent(AssemblyDefinition assembly)
    {
        var name = assembly.Name.Name;
        return name.StartsWith(FeaturePrefix, StringComparison.Ordinal)
            ? [.. assembly.Modules
                .SelectMany(module => module.AssemblyReferences)
                .Where(reference => reference.Name.StartsWith(FeaturePrefix, StringComparison.Ordinal))
                .Select(reference => $"{name} depends on another feature, {reference.Name}")]
            : [];
    }

    private static bool IsHookLayer(AssemblyDefinition assembly) => assembly.Name.Name == HookLayer;

    private static bool IsWindowsPlatform(CustomAttribute attribute) =>
        attribute.AttributeType.FullName is "System.Runtime.Versioning.TargetPlatformAttribute"
            or "System.Runtime.Versioning.SupportedOSPlatformAttribute"
        && attribute.ConstructorArguments is [{ Value: string platform }]
        && platform.StartsWith("windows", StringComparison.OrdinalIgnoreCase);

    private static int? SearchPaths(IEnumerable<CustomAttribute> attributes) =>
        attributes
            .Where(attribute => attribute.AttributeType.FullName
                == "System.Runtime.InteropServices.DefaultDllImportSearchPathsAttribute")
            .Select(attribute => (int?)Convert.ToInt32(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture))
            .FirstOrDefault();

    private static IEnumerable<TypeDefinition> Types(AssemblyDefinition assembly) =>
        assembly.Modules.SelectMany(module => module.GetTypes());

    private static IEnumerable<MethodDefinition> Methods(AssemblyDefinition assembly) =>
        Types(assembly).SelectMany(type => type.Methods);

    private static IEnumerable<MethodDefinition> NativeCalls(AssemblyDefinition assembly) =>
        Methods(assembly).Where(method => method.IsPInvokeImpl);

    /// <summary>Every type and member the assembly defines, the things an attribute can be placed on.</summary>
    private static IEnumerable<IMemberDefinition> Members(AssemblyDefinition assembly) =>
        Types(assembly).SelectMany(type => (IEnumerable<IMemberDefinition>)
            [type, .. type.Methods, .. type.Properties, .. type.Fields, .. type.Events]);

    private static IEnumerable<TypeReference> SignatureAndLocals(MethodDefinition method) =>
    [
        method.ReturnType,
        .. method.Parameters.Select(parameter => parameter.ParameterType),
        .. method.HasBody ? method.Body.Variables.Select(variable => variable.VariableType) : [],
    ];

    private static bool ContainsPointer(TypeReference type) => type switch
    {
        PointerType or FunctionPointerType => true,
        GenericInstanceType generic => generic.GenericArguments.Any(ContainsPointer)
            || ContainsPointer(generic.ElementType),
        TypeSpecification specification => ContainsPointer(specification.ElementType),
        _ => false,
    };

    /// <summary>Every type the assembly uses from another assembly: each one needs a row in the type reference table.</summary>
    private static IEnumerable<TypeReference> ExternalTypes(AssemblyDefinition assembly) =>
        assembly.Modules.SelectMany(module => module.GetTypeReferences());

    private static bool IsInNamespace(TypeReference type, string root)
    {
        var outer = type;
        while (outer.DeclaringType is not null)
        {
            outer = outer.DeclaringType;
        }
        return outer.Namespace == root || outer.Namespace.StartsWith(root + ".", StringComparison.Ordinal);
    }
}
