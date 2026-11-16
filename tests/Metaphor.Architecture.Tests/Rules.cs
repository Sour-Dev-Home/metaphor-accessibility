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

    /// <summary>The attribute that marks a hook and names its game builds.</summary>
    public const string GameHookAttribute = "Metaphor.Hooks.GameHookAttribute";

    /// <summary>Assemblies allowed to use networking, each with the ADR that approved it. None so far.</summary>
    private static readonly Dictionary<string, string> NetworkingExceptions = [];

    /// <summary>Assemblies allowed to load native libraries themselves, each with the ADR that approved it.</summary>
    private static readonly Dictionary<string, string> NativeLoadingExceptions = new()
    {
        // prism.dll, by full path from the mod's folder after its SHA-256 is checked.
        [HookLayer] = "ADR 0005",
    };

    /// <summary>Hook-layer types besides hooks that may use the hooking API or write through pointers, each with why.
    /// Only in the hook layer itself: the same name elsewhere is no exemption.</summary>
    private static readonly Dictionary<string, string> PatchingExceptions = new()
    {
        ["Metaphor.Hooks.HookLoader"] = "#39: the loader installs hooks",
        ["Metaphor.Hooks.IGameHook"] = "#39: what every hook implements",
        ["Metaphor.Hooks.PrismSpeech"] = "ADR 0005: calls prism.dll's exports through function pointers",
    };

    /// <summary>Members that write memory by address.</summary>
    private static readonly string[] MemoryWritingMembers =
    [
        "System.Runtime.InteropServices.Marshal::Write",
        "System.Runtime.InteropServices.Marshal::Copy",
        "System.Runtime.InteropServices.Marshal::StructureToPtr",
        "System.Runtime.CompilerServices.Unsafe::Write",
        "System.Runtime.CompilerServices.Unsafe::Copy",
        "System.Runtime.CompilerServices.Unsafe::InitBlock",
        "System.Runtime.InteropServices.NativeMemory::Copy",
        "System.Runtime.InteropServices.NativeMemory::Fill",
        "System.Runtime.InteropServices.NativeMemory::Clear",
        "System.Buffer::MemoryCopy",
    ];

    /// <summary>Native functions that change memory protection, allocate executable memory, or write memory.</summary>
    private static readonly string[] MemoryWritingEntryPoints =
        ["VirtualProtect", "VirtualAlloc", "WriteProcessMemory", "NtProtectVirtualMemory", "NtWriteVirtualMemory"];

    /// <summary>IL that writes through a pointer, copies or fills memory, or calls through a function pointer. Checked
    /// in the hook layer only: pointers are banned everywhere else (rule 2). It also flags writes through managed
    /// <c>ref</c>/<c>out</c> parameters, which the hook layer avoids outside hooks.</summary>
    private static readonly HashSet<Mono.Cecil.Cil.Code> PointerWritingCodes =
    [
        Mono.Cecil.Cil.Code.Stind_I, Mono.Cecil.Cil.Code.Stind_I1, Mono.Cecil.Cil.Code.Stind_I2,
        Mono.Cecil.Cil.Code.Stind_I4, Mono.Cecil.Cil.Code.Stind_I8, Mono.Cecil.Cil.Code.Stind_R4,
        Mono.Cecil.Cil.Code.Stind_R8, Mono.Cecil.Cil.Code.Stind_Ref, Mono.Cecil.Cil.Code.Stobj,
        Mono.Cecil.Cil.Code.Cpblk, Mono.Cecil.Cil.Code.Initblk, Mono.Cecil.Cil.Code.Calli,
    ];

    /// <summary>Prism exports that let Prism choose or probe backends itself (ADR 0005).</summary>
    private static readonly string[] PrismOwnChoiceExports =
        ["prism_registry_create_best", "prism_registry_acquire_best", "prism_availability"];

    /// <summary>Prism backends that load a DLL by bare name, by their IDs in prism.h v0.18.3 (ADR 0005).</summary>
    private static readonly Dictionary<long, string> BareNamePrismBackends = new()
    {
        [unchecked((long)0x3D93C56C9E7F2A2EUL)] = "ZDSR",
        [unchecked((long)0x344B951962E3B835UL)] = "PC-Talker",
        [unchecked((long)0x285ABA1C16F3300FUL)] = "Boy PC Reader",
    };

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

    /// <summary>
    /// Each hook (a type marked <c>[GameHook]</c>) names at least one game build, and only builds from the known-builds
    /// list, so it can't patch a build nobody has checked (AGENTS.md ground rule 2).
    /// </summary>
    public static IReadOnlyList<string> HooksNameKnownBuilds(AssemblyDefinition assembly, IReadOnlySet<string> knownBuilds)
    {
        ArgumentNullException.ThrowIfNull(knownBuilds);

        var name = assembly.Name.Name;
        return [.. Types(assembly).SelectMany(type => type.CustomAttributes
            .Where(attribute => attribute.AttributeType.FullName == GameHookAttribute)
            .SelectMany(attribute =>
            {
                var builds = attribute.ConstructorArguments is [{ Value: CustomAttributeArgument[] values }]
                    ? values.Select(value => value.Value as string).ToList()
                    : [];
                return builds.Count == 0
                    ? [$"{name} hook {type.FullName} names no game build"]
                    : builds
                        .Where(build => build is null || !knownBuilds.Contains(build))
                        .Select(build => $"{name} hook {type.FullName} names unknown game build {build}");
            }))];
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

    /// <summary>
    /// No assembly names Prism's own backend choice or availability poll, or a backend that loads a DLL by bare name, so
    /// the mod only ever creates backends from its fixed list (ADR 0005).
    /// </summary>
    public static IReadOnlyList<string> PrismBackendsOnlyFromTheList(AssemblyDefinition assembly)
    {
        var name = assembly.Name.Name;
        return [.. Methods(assembly)
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions.Select(instruction => (method, instruction.Operand)))
            .Select(use => use.Operand switch
            {
                string text when PrismOwnChoiceExports.Any(export => text.StartsWith(export, StringComparison.Ordinal)) =>
                    $"{name} names Prism's {text} in {use.method.FullName}",
                long id when BareNamePrismBackends.TryGetValue(id, out var reader) =>
                    $"{name} names Prism's {reader} backend in {use.method.FullName}",
                _ => null,
            })
            .OfType<string>()];
    }

    /// <summary>
    /// #39: a type that uses the hooking API (Reloaded.Hooks) or writes memory by address is a hook, marked
    /// <c>[GameHook]</c> itself, or one of the hook loader's own types; so no patch skips the loader's build check. The
    /// attribute isn't inherited, so a subclass needs its own. Code nested in a hook (lambdas, closures) belongs to it.
    /// Reflection can't be seen this way; the security review covers it.
    /// </summary>
    public static IReadOnlyList<string> PatchingNeedsGameHook(AssemblyDefinition assembly)
    {
        var name = assembly.Name.Name;
        var hookLayer = IsHookLayer(assembly);
        return [.. Types(assembly)
            .Where(type => !IsHookOrException(type, hookLayer))
            .SelectMany(type => PatchingUses(type, hookLayer).Distinct().Select(use =>
                $"{name} type {type.FullName} patches the game ({use}) without [GameHook]"))];
    }

    private static bool IsHookOrException(TypeDefinition type, bool hookLayer)
    {
        for (var owner = type; owner is not null; owner = owner.DeclaringType)
        {
            if ((hookLayer && PatchingExceptions.ContainsKey(owner.FullName)) ||
                owner.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == GameHookAttribute))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<string> PatchingUses(TypeDefinition type, bool hookLayer)
    {
        static bool IsHooking(TypeReference reference) =>
            reference.Namespace.StartsWith("Reloaded.Hooks", StringComparison.Ordinal);

        foreach (var field in type.Fields.Where(field => IsHooking(field.FieldType)))
        {
            yield return field.FieldType.FullName;
        }
        foreach (var method in type.Methods)
        {
            foreach (var used in SignatureAndLocals(method).Where(IsHooking))
            {
                yield return used.FullName;
            }
            if (method.PInvokeInfo is { } native &&
                MemoryWritingEntryPoints.Any(entry => native.EntryPoint.StartsWith(entry, StringComparison.Ordinal)))
            {
                yield return native.EntryPoint;
            }
            if (!method.HasBody)
            {
                continue;
            }
            foreach (var instruction in method.Body.Instructions)
            {
                if (hookLayer && PointerWritingCodes.Contains(instruction.OpCode.Code))
                {
                    yield return instruction.OpCode.Name;
                }
                if (instruction.Operand is not MethodReference called)
                {
                    continue;
                }
                var member = $"{called.DeclaringType.FullName}::{called.Name}";
                if (IsHooking(called.DeclaringType) ||
                    MemoryWritingMembers.Any(writer => member.StartsWith(writer, StringComparison.Ordinal)))
                {
                    yield return member;
                }
                // A writable span over raw memory: new Span<T>(void*, int).
                if (called.Name == ".ctor" && called.DeclaringType.Name == "Span`1" &&
                    called.Parameters.FirstOrDefault()?.ParameterType is PointerType)
                {
                    yield return "Span<T>(void*, int)";
                }
            }
        }
    }

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
