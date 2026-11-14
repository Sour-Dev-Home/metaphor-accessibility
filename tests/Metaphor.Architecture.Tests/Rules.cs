using Mono.Cecil;

namespace Metaphor.Architecture.Tests;

/// <summary>
/// The architecture rules from ADR 0003, listed in docs/coding-standards.md. Each rule returns one line per violation,
/// empty when the assembly complies.
/// </summary>
internal static class Rules
{
    /// <summary>Assemblies allowed to use networking, each with the ADR that approved it. None so far.</summary>
    private static readonly Dictionary<string, string> NetworkingExceptions = [];

    public static IReadOnlyList<string> NoNetworking(AssemblyDefinition assembly) =>
        NetworkingExceptions.ContainsKey(assembly.Name.Name)
            ? []
            : [.. ExternalTypes(assembly)
                .Where(type => IsInNamespace(type, "System.Net"))
                .Select(type => $"{assembly.Name.Name} uses networking type {type.FullName}")];

    /// <summary>The one assembly allowed to touch the game, the loader, Windows and raw memory.</summary>
    public const string HookLayer = "Metaphor.Hooks";

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
        return [.. loader, .. windowsTypes, .. platform];
    }

    /// <summary>True when the assembly declares Windows as its target or supported platform, as net9.0-windows does.</summary>
    public static bool TargetsWindows(AssemblyDefinition assembly) =>
        assembly.CustomAttributes.Any(attribute =>
            attribute.AttributeType.FullName is "System.Runtime.Versioning.TargetPlatformAttribute"
                or "System.Runtime.Versioning.SupportedOSPlatformAttribute"
            && attribute.ConstructorArguments is [{ Value: string platform }]
            && platform.StartsWith("windows", StringComparison.OrdinalIgnoreCase));

    /// <summary>APIs that read or write memory by address, bypassing the runtime's checks.</summary>
    private static readonly HashSet<string> RawMemoryTypes =
    [
        "System.Runtime.InteropServices.Marshal",
        "System.Runtime.InteropServices.NativeMemory",
        "System.Runtime.CompilerServices.Unsafe",
    ];

    /// <summary>Only the hook layer uses pointers, raw-memory APIs or native calls (P/Invoke).</summary>
    public static IReadOnlyList<string> NoRawMemoryOutsideHooks(AssemblyDefinition assembly)
    {
        if (IsHookLayer(assembly))
        {
            return [];
        }
        var name = assembly.Name.Name;
        var apis = ExternalTypes(assembly)
            .Where(type => RawMemoryTypes.Contains(type.FullName))
            .Select(type => $"{name} uses raw-memory API {type.FullName}");
        var pointers = Methods(assembly)
            .Where(method => SignatureAndLocals(method).Any(ContainsPointer))
            .Select(method => $"{name} uses a pointer in {method.FullName}");
        var fields = assembly.Modules
            .SelectMany(module => module.GetTypes())
            .SelectMany(type => type.Fields)
            .Where(field => ContainsPointer(field.FieldType))
            .Select(field => $"{name} uses a pointer in {field.FullName}");
        var pinvokes = Methods(assembly)
            .Where(method => method.IsPInvokeImpl)
            .Select(method => $"{name} declares native call {method.FullName}");
        return [.. apis, .. pointers, .. fields, .. pinvokes];
    }

    /// <summary>Assemblies allowed to load native libraries themselves, each with the ADR that approved it. None so far.</summary>
    private static readonly Dictionary<string, string> NativeLoadingExceptions = [];

    private const int System32SearchPath = 0x800; // DllImportSearchPath.System32

    /// <summary>
    /// Native calls search System32 only, so a DLL planted next to the game can't stand in for a system one. Loading any
    /// other native library needs an ADR-approved exception that loads it by full path.
    /// </summary>
    public static IReadOnlyList<string> NativeLibrariesLoadSafely(AssemblyDefinition assembly)
    {
        var name = assembly.Name.Name;
        if (NativeLoadingExceptions.ContainsKey(name))
        {
            return [];
        }
        var loads = assembly.Modules
            .SelectMany(module => module.GetMemberReferences())
            .Where(member => member.DeclaringType.FullName == "System.Runtime.InteropServices.NativeLibrary"
                && member.Name is "Load" or "TryLoad")
            .Select(member => $"{name} calls {member.DeclaringType.FullName}::{member.Name}");
        var nativeCalls = Methods(assembly).Where(method => method.IsPInvokeImpl).ToList();
        var loadLibrary = nativeCalls
            .Where(method => method.PInvokeInfo.EntryPoint.StartsWith("LoadLibrary", StringComparison.Ordinal)
                || method.PInvokeInfo.EntryPoint == "LdrLoadDll")
            .Select(method => $"{name} loads a library through native call {method.FullName}");
        var assemblyWide = SearchPaths(assembly.CustomAttributes);
        var unrestricted = nativeCalls
            .Where(method => (SearchPaths(method.CustomAttributes) ?? assemblyWide) != System32SearchPath)
            .Select(method => $"{name} declares native call {method.FullName} without restricting its search to System32");
        return [.. loads, .. loadLibrary, .. unrestricted];
    }

    /// <summary>Each feature is its own assembly under this prefix.</summary>
    public const string FeaturePrefix = "Metaphor.Features.";

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

    private static int? SearchPaths(IEnumerable<CustomAttribute> attributes) =>
        attributes
            .Where(attribute => attribute.AttributeType.FullName
                == "System.Runtime.InteropServices.DefaultDllImportSearchPathsAttribute")
            .Select(attribute => (int?)Convert.ToInt32(attribute.ConstructorArguments[0].Value,
                System.Globalization.CultureInfo.InvariantCulture))
            .FirstOrDefault();

    private static IEnumerable<MethodDefinition> Methods(AssemblyDefinition assembly) =>
        assembly.Modules.SelectMany(module => module.GetTypes()).SelectMany(type => type.Methods);

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

    private static bool IsHookLayer(AssemblyDefinition assembly) => assembly.Name.Name == HookLayer;

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
