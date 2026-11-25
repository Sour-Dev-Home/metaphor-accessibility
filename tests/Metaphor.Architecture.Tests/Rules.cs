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

    /// <summary>Assemblies allowed to use HTTP (System.Net.Http and its Headers namespace, nothing else in System.Net),
    /// each with the ADR that approved it.</summary>
    private static readonly Dictionary<string, string> NetworkingExceptions = new()
    {
        // One request per launch to this repository's GitHub releases, which the settings can turn off (#576).
        ["Metaphor.Features.Updates"] = "ADR 0018",
    };

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
        ["Metaphor.Hooks.IChangesBehaviour"] = "#58: what a hook that changes behaviour implements instead of a shape",
        ["Metaphor.Hooks.PassThrough"] = "#88: the loader's adapters hook each function for its hook",
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

    /// <summary>The namespaces an exception allows: HTTP only, so no sockets, web sockets or other System.Net types.</summary>
    private static readonly string[] HttpNamespaces = ["System.Net.Http", "System.Net.Http.Headers"];

    /// <summary>No assembly uses networking without an ADR-approved exception, and an exception covers HTTP only.
    /// </summary>
    public static IReadOnlyList<string> NoNetworking(AssemblyDefinition assembly)
    {
        var excepted = NetworkingExceptions.ContainsKey(assembly.Name.Name);
        return [.. ExternalTypes(assembly)
            .Where(type => IsInNamespace(type, "System.Net") && !(excepted && HttpNamespaces.Contains(OuterNamespace(type))))
            .Select(type => $"{assembly.Name.Name} uses networking type {type.FullName}")];
    }

    private static string OuterNamespace(TypeReference type)
    {
        while (type.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }
        return type.Namespace;
    }

    /// <summary>No Release assembly uses named pipes, so the live inspector (#291), the mod's one way in from outside the
    /// game, never reaches players: it is compiled into Debug builds only.</summary>
    public static IReadOnlyList<string> NoPipesInRelease(AssemblyDefinition assembly) =>
        IsDebugBuild(assembly)
            ? []
            : [.. ExternalTypes(assembly)
                .Where(type => IsInNamespace(type, "System.IO.Pipes"))
                .Select(type => $"{assembly.Name.Name} uses pipe type {type.FullName} in a Release build")];

    /// <summary>The debug-only types: diagnostics that read more of the game than a feature needs, logged to settle a
    /// finding in a session, never shipped to players.</summary>
    public static readonly IReadOnlyList<string> DebugOnlyTypes =
        ["Metaphor.Core.FieldEnemyState", "Metaphor.Core.CaptureLines"]; // #649; the capture build's probes

    /// <summary>No Release assembly calls a <see cref="DebugOnlyTypes"/> member from outside that type, so that logging
    /// is compiled into Debug builds only.</summary>
    public static IReadOnlyList<string> NoDebugOnlyCallsInRelease(AssemblyDefinition assembly) =>
        IsDebugBuild(assembly)
            ? []
            : [.. assembly.MainModule.Types.Where(type => !DebugOnlyTypes.Contains(type.FullName)).SelectMany(WithNested)
                .SelectMany(type => type.Methods)
                .Where(method => method.HasBody)
                .SelectMany(method => method.Body.Instructions
                    .Select(instruction => instruction.Operand)
                    .OfType<MethodReference>()
                    .Where(called => DebugOnlyTypes.Contains(called.DeclaringType.FullName))
                    .Select(called => $"{method.FullName} calls debug-only {called.FullName} in a Release build"))];

    /// <summary>Whether <paramref name="assembly"/> was compiled without optimizations, as a Debug build is: its
    /// <c>DebuggableAttribute</c> has <c>DisableOptimizations</c>.</summary>
    public static bool IsDebugBuild(AssemblyDefinition assembly)
    {
        const int disableOptimizations = 0x100; // DebuggableAttribute.DebuggingModes
        var debuggable = assembly.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Diagnostics.DebuggableAttribute");
        return debuggable is not null &&
            (Convert.ToInt32(debuggable.ConstructorArguments[0].Value, CultureInfo.InvariantCulture) & disableOptimizations) != 0;
    }

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

    private const string AnnouncerInterface = "Metaphor.Core.Announcements.IAnnouncer";
    private const string CuePlayerInterface = "Metaphor.Core.Cues.ICuePlayer";
    private const string FeatureSettingAttribute = "Metaphor.Core.Settings.FeatureSettingAttribute`1";
    private static readonly string[] KeyInterfaces =
        ["Metaphor.Core.Input.IInputControls", "Metaphor.Core.Input.IKeyWatcher", "Metaphor.Core.Input.IStickControls"];

    /// <summary>
    /// Rule 18 (#661): a feature class that speaks (calls <c>Announce</c> on an announcer), plays a cue (calls the cue
    /// player) or takes keys (implements the input controls or the key watcher) is marked
    /// <c>[FeatureSetting&lt;TFeature&gt;(...)]</c> with its project's feature enum (#784), so every feature can be
    /// turned off. Lambdas count for the class that holds them. An announcer itself, such as the reader switch, passes on
    /// what it's handed and isn't counted as speaking.
    /// </summary>
    public static IReadOnlyList<string> FeaturesHaveSettings(AssemblyDefinition assembly)
    {
        if (!assembly.Name.Name.StartsWith(FeaturePrefix, StringComparison.Ordinal))
        {
            return [];
        }
        List<string> violations = [];
        foreach (var type in assembly.Modules.SelectMany(module => module.Types))
        {
            if (type.CustomAttributes.Any(attribute =>
                    attribute.AttributeType.GetElementType().FullName == FeatureSettingAttribute))
            {
                continue;
            }
            var calls = WithNested(type).SelectMany(nested => nested.Methods).Where(method => method.HasBody)
                .SelectMany(method => method.Body.Instructions)
                .Select(instruction => instruction.Operand as MethodReference).OfType<MethodReference>().ToList();
            List<string> does = [];
            if (!Implements(type, AnnouncerInterface) &&
                calls.Any(call => call.Name == "Announce" && Implements(call.DeclaringType, AnnouncerInterface)))
            {
                does.Add("speaks");
            }
            if (calls.Any(call => Implements(call.DeclaringType, CuePlayerInterface)))
            {
                does.Add("plays a cue");
            }
            if (KeyInterfaces.Any(key => Implements(type, key)))
            {
                does.Add("takes keys");
            }
            if (does.Count > 0)
            {
                violations.Add(
                    $"{type.FullName} {string.Join(" and ", does)} but has no setting. Add a member to its project's " +
                    "feature enum with its [SettingKey(\"group\", \"name\", \"Spoken name\", Order = ..., Description = " +
                    $"\"...\")], mark {type.Name} [FeatureSetting<TheFeatureEnum>(TheFeatureEnum.YourFeature)], gate it " +
                    "in the project's feature module through context.Output(...) or settings.Input(...), and run " +
                    "tools/Metaphor.SettingsReference to list it in docs/players/reference/settings.md");
            }
        }
        return violations;
    }

    private static readonly (string Type, string Method)[] UngatedOutputs =
    [
        ("Metaphor.Core.Settings.SettingsReloader", "Speech"),
        ("Metaphor.Core.Features.FeatureContext", "get_Announcer"),
        ("Metaphor.Core.Features.FeatureContext", "get_Cues"),
    ];

    /// <summary>
    /// Rule 18 by construction (#723): a feature project takes every announcer and cue player it hands a feature class
    /// from the speech factory (<c>FeatureContext.Output</c>, <c>FeatureOutputs.For</c>), already behind the feature's
    /// setting. It never builds its own speech gate (<c>SettingsReloader.Speech</c>) or reads the context's ungated
    /// announcer or cue player.
    /// </summary>
    public static IReadOnlyList<string> FeaturesTakeOutputsFromTheFactory(AssemblyDefinition assembly)
    {
        if (!assembly.Name.Name.StartsWith(FeaturePrefix, StringComparison.Ordinal))
        {
            return [];
        }
        return [.. assembly.Modules.SelectMany(module => module.Types).SelectMany(WithNested)
            .SelectMany(type => type.Methods).Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions
                .Select(instruction => instruction.Operand as MethodReference).OfType<MethodReference>()
                .Where(call => UngatedOutputs.Contains((call.DeclaringType.FullName, call.Name)))
                .Select(call =>
                    $"{method.DeclaringType.FullName}.{method.Name} calls {call.DeclaringType.Name}.{call.Name}: take " +
                    "the feature's outputs from context.Output(TheFeature) instead (#723)"))
            .Distinct()];
    }

    private const string FeatureModuleInterface = "Metaphor.Core.Features.IFeatureModule";
    private const string ModuleList = "Metaphor.Hooks.FeatureModules";

    /// <summary>The feature modules the hook layer's explicit list creates (#784, ADR 0022), in its order: the types
    /// <c>FeatureModules.Create</c> constructs.</summary>
    public static IReadOnlyList<string> ListedModules(AssemblyDefinition hooks)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        var create = hooks.MainModule.GetType(ModuleList)?.Methods.SingleOrDefault(method => method.Name == "Create");
        return create is { HasBody: true }
            ? [.. create.Body.Instructions.Where(instruction => instruction.OpCode == Mono.Cecil.Cil.OpCodes.Newobj)
                .Select(instruction => ((MethodReference)instruction.Operand).DeclaringType)
                .Where(type => Implements(type, FeatureModuleInterface))
                .Select(type => type.FullName)]
            : [];
    }

    /// <summary>
    /// The hook layer's list names every feature module (#784): each class in <paramref name="features"/> that implements
    /// <c>IFeatureModule</c> is created once in <c>FeatureModules.Create</c>, so no project's settings or wiring is left
    /// out of the mod.
    /// </summary>
    public static IReadOnlyList<string> ModulesAreListed(
        AssemblyDefinition hooks, IEnumerable<AssemblyDefinition> features)
    {
        ArgumentNullException.ThrowIfNull(features);
        var listed = ListedModules(hooks);
        var declared = features
            .Where(assembly => assembly.Name.Name.StartsWith(FeaturePrefix, StringComparison.Ordinal))
            .SelectMany(assembly => assembly.Modules.SelectMany(module => module.Types))
            .Where(type => type is { IsClass: true, IsAbstract: false } && Implements(type, FeatureModuleInterface))
            .Select(type => type.FullName);
        return
        [
            .. declared.Where(module => !listed.Contains(module))
                .Select(module => $"{module} is not in {ModuleList}.Create: add it where its controls and observers belong"),
            .. listed.GroupBy(module => module, StringComparer.Ordinal).Where(group => group.Count() > 1)
                .Select(group => $"{group.Key} is in {ModuleList}.Create more than once"),
        ];
    }

    private static IEnumerable<TypeDefinition> WithNested(TypeDefinition type) =>
        [type, .. type.NestedTypes.SelectMany(WithNested)];

    /// <summary>Whether <paramref name="type"/> is the interface <paramref name="fullName"/> or implements it, through
    /// its base classes and other interfaces. A type that can't be resolved doesn't.</summary>
    private static bool Implements(TypeReference? type, string fullName)
    {
        if (type is null)
        {
            return false;
        }
        if (type.FullName == fullName)
        {
            return true;
        }
        TypeDefinition? definition;
        try
        {
            definition = type.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            return false;
        }
        return definition is not null &&
            (definition.Interfaces.Any(implemented => Implements(implemented.InterfaceType, fullName)) ||
                Implements(definition.BaseType, fullName));
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

    /// <summary>
    /// #69, rule 9: a delegate declared in a hook passes only pointer-sized values (<c>nint</c>, <c>nuint</c>,
    /// <c>long</c>, <c>ulong</c>, pointers) and <c>double</c>. Windows x64 leaves the upper half of a register undefined
    /// for a 32-bit argument, so a narrower declaration can hand the game garbage when the hook passes the call on, and
    /// the game's own width is often unverified. <c>double</c> carries the register's low 64 bits, which holds a
    /// 32-bit float intact too (a callback reading it as a double gets garbage, which only matters for logging).
    /// Checked: delegates declared in a hook, every delegate passed to <c>CreateHook</c>, and a hook's function-pointer
    /// fields.
    /// </summary>
    public static IReadOnlyList<string> HookDelegatesArePointerSized(AssemblyDefinition assembly)
    {
        var name = assembly.Name.Name;
        var delegates = Types(assembly)
            .Where(type => type.BaseType?.FullName == "System.MulticastDelegate" && IsInsideHook(type))
            .Cast<TypeReference>()
            .Concat(Methods(assembly).Where(method => method.HasBody)
                .SelectMany(method => method.Body.Instructions)
                .Select(instruction => instruction.Operand)
                .OfType<GenericInstanceMethod>()
                .Where(called => called.Name == "CreateHook" &&
                    called.DeclaringType.Namespace.StartsWith("Reloaded.Hooks", StringComparison.Ordinal))
                .Select(called => called.GenericArguments[0]))
            .DistinctBy(type => type.FullName);
        var functionPointers = Types(assembly).Where(IsInsideHook)
            .SelectMany(type => type.Fields
                .Where(field => field.FieldType is FunctionPointerType)
                .Select(field => (Owner: $"{type.FullName} field {field.Name}", Pointer: (FunctionPointerType)field.FieldType)));
        return [.. delegates.SelectMany(DelegateProblems)
            .Concat(functionPointers.SelectMany(pointer => NarrowValues(
                pointer.Owner, pointer.Pointer.ReturnType, pointer.Pointer.Parameters.Select(
                    (parameter, index) => ($"{index + 1}", parameter.ParameterType)))))
            .Select(problem => $"{name} {problem}; declare it nint (rule 9)")];
    }

    private static readonly HashSet<string> PointerSizedTypes =
        ["System.IntPtr", "System.UIntPtr", "System.Int64", "System.UInt64", "System.Double"];

    private static IEnumerable<string> DelegateProblems(TypeReference @delegate)
    {
        // A library delegate (Func, Action): its type arguments are what the game sees.
        if (@delegate is GenericInstanceType generic)
        {
            return generic.GenericArguments.Where(argument => !IsPointerSized(argument))
                .Select(argument => $"delegate {generic.FullName} passes {argument.FullName}");
        }
        var invoke = @delegate.Resolve()?.Methods.FirstOrDefault(method => method.Name == "Invoke");
        return invoke is null
            ? [$"delegate {@delegate.FullName} can't be read"]
            : NarrowValues($"delegate {@delegate.FullName}", invoke.ReturnType,
                invoke.Parameters.Select(parameter => (parameter.Name, parameter.ParameterType)));
    }

    private static IEnumerable<string> NarrowValues(
        string owner, TypeReference returnType, IEnumerable<(string Name, TypeReference Type)> parameters)
    {
        if (returnType.FullName != "System.Void" && !IsPointerSized(returnType))
        {
            yield return $"{owner} returns {returnType.FullName}";
        }
        foreach (var (parameter, type) in parameters.Where(parameter => !IsPointerSized(parameter.Type)))
        {
            yield return $"{owner} parameter {parameter} is {type.FullName}";
        }
    }

    private static bool IsPointerSized(TypeReference type) =>
        type is PointerType or FunctionPointerType || PointerSizedTypes.Contains(type.FullName);

    private static bool IsInsideHook(TypeDefinition type)
    {
        for (var owner = type; owner is not null; owner = owner.DeclaringType)
        {
            if (owner.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == GameHookAttribute))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Hook-layer types that may read memory by address themselves, each with why.</summary>
    private static readonly Dictionary<string, string> MemoryReadingExceptions = new()
    {
        ["Metaphor.Hooks.ModuleMemoryReader"] = "#55: the safe readers, through ReadProcessMemory",
    };

    /// <summary>Members that read memory by address, and can fault where the game hands over a bad pointer.</summary>
    private static readonly string[] MemoryReadingMembers =
    [
        "System.Runtime.InteropServices.Marshal::PtrToString",
        "System.Runtime.InteropServices.Marshal::PtrToStructure",
        "System.Runtime.InteropServices.Marshal::Read",
        "System.Runtime.InteropServices.Marshal::Copy",
        "System.Runtime.CompilerServices.Unsafe::Read",
        "System.Runtime.CompilerServices.Unsafe::AsRef",
        "System.Runtime.CompilerServices.Unsafe::Copy",
        "System.Runtime.InteropServices.MemoryMarshal::",
        "System.Threading.Volatile::Read",
        "System.Buffer::MemoryCopy",
        "System.Runtime.InteropServices.Marshal::GetDelegateForFunctionPointer",
        "System.Runtime.InteropServices.NativeLibrary::GetExport",
    ];

    /// <summary>IL that loads or copies through a pointer or a managed reference.</summary>
    private static readonly HashSet<Mono.Cecil.Cil.Code> PointerReadingCodes =
    [
        Mono.Cecil.Cil.Code.Ldind_I, Mono.Cecil.Cil.Code.Ldind_I1, Mono.Cecil.Cil.Code.Ldind_I2,
        Mono.Cecil.Cil.Code.Ldind_I4, Mono.Cecil.Cil.Code.Ldind_I8, Mono.Cecil.Cil.Code.Ldind_U1,
        Mono.Cecil.Cil.Code.Ldind_U2, Mono.Cecil.Cil.Code.Ldind_U4, Mono.Cecil.Cil.Code.Ldind_R4,
        Mono.Cecil.Cil.Code.Ldind_R8, Mono.Cecil.Cil.Code.Ldind_Ref, Mono.Cecil.Cil.Code.Ldobj,
        Mono.Cecil.Cil.Code.Cpblk,
    ];

    /// <summary>
    /// #69, rule 10: in the hook layer, only <c>ModuleMemoryReader</c> reads memory by address. A hook's callback gets
    /// pointers from the game, and .NET can't catch the access violation a bad one causes; the reader copies through
    /// <c>ReadProcessMemory</c>, which fails instead. Loads through managed references are flagged too, so the hook
    /// layer avoids <c>ref</c> and <c>in</c> parameters outside the reader.
    /// </summary>
    public static IReadOnlyList<string> GameMemoryIsReadSafely(AssemblyDefinition assembly)
    {
        if (!IsHookLayer(assembly))
        {
            return [];
        }
        var name = assembly.Name.Name;
        var reads = Types(assembly)
            .Where(type => !IsListed(type, MemoryReadingExceptions))
            .SelectMany(type => MemoryReads(type).Distinct()
                .Where(use => !(FunctionPointerUses.Contains(use) && IsListed(type, FunctionPointerCallers)))
                .Select(use =>
                    $"{name} type {type.FullName} reads memory by address ({use}); use ModuleMemoryReader (rule 10)"));
        // #428: a native call has no IL to inspect, and one such as ReadProcessMemory or RtlMoveMemory reads by address,
        // so outside the reader each one is listed by the function it binds to, whatever the C# method is called.
        var nativeCalls = NativeCalls(assembly)
            .Where(method => !IsListed(method.DeclaringType, MemoryReadingExceptions) &&
                !NativeCallsThatCantFault.ContainsKey(NativeTarget(method)))
            .Select(method => $"{name} declares native call {method.FullName} ({NativeTarget(method)}) outside " +
                "ModuleMemoryReader; it may read memory by address, so move it there or list why it can't fault (rule 10)");
        return [.. reads, .. nativeCalls];
    }

    /// <summary>The function a P/Invoke binds to, as <c>dll!EntryPoint</c>, with the DLL lower-cased and its
    /// <c>.dll</c> added when left off; the entry point is the method's name when none is given.</summary>
    private static string NativeTarget(MethodDefinition method)
    {
        var dll = method.PInvokeInfo.Module.Name.ToLowerInvariant();
        if (!dll.EndsWith(".dll", StringComparison.Ordinal))
        {
            dll += ".dll";
        }
        var entryPoint = string.IsNullOrEmpty(method.PInvokeInfo.EntryPoint) ? method.Name : method.PInvokeInfo.EntryPoint;
        return $"{dll}!{entryPoint}";
    }

    /// <summary>Native functions the hook layer may declare outside <c>ModuleMemoryReader</c>, each with why it can't
    /// fault on a bad address (#428).</summary>
    private static readonly Dictionary<string, string> NativeCallsThatCantFault = new()
    {
        ["kernel32.dll!RtlCaptureStackBackTrace"] =
            "CallerAddress: walks this thread's own stack within its limits and stops early instead of faulting",
        ["kernel32.dll!GetCurrentThreadStackLimits"] = "CallerAddress: reads no address it is given",
        ["kernel32.dll!WriteProcessMemory"] =
            "PadReadHook, KeyboardReadHook, BattleTargetCursorHook: writes through the kernel, which fails instead " +
            "of faulting (ADR 0007)",
        ["kernel32.dll!VirtualProtect"] =
            "BattleTargetCursorHook: makes the one checked vtable slot's read-only page writable for its write and " +
            "puts the old protection back (ReadOnlyPageWrite); fails instead of faulting (#253, decided 2026-11-24)",
        // ADR 0019: WaveOutHandle passes only its own handle and the headers and buffers it keeps pinned until close.
        ["winmm.dll!waveOutOpen"] = "WaveOutHandle: reads the format on its own stack, writes the handle (ADR 0019)",
        ["winmm.dll!waveOutPrepareHeader"] = "WaveOutHandle: the mod's own pinned header and buffer (ADR 0019)",
        ["winmm.dll!waveOutWrite"] = "WaveOutHandle: the mod's own pinned header and buffer (ADR 0019)",
        ["winmm.dll!waveOutUnprepareHeader"] = "WaveOutHandle: the mod's own pinned header (ADR 0019)",
        ["winmm.dll!waveOutReset"] = "WaveOutHandle: takes only the mod's own handle (ADR 0019)",
        ["winmm.dll!waveOutClose"] = "WaveOutHandle: takes only the mod's own handle (ADR 0019)",
    };

    /// <summary>What <see cref="MemoryReads"/> reports for a call through a function pointer, or for making one: the
    /// callee runs native code that can read anything (#428).</summary>
    private static readonly HashSet<string> FunctionPointerUses =
    [
        "calli",
        "System.Runtime.InteropServices.Marshal::GetDelegateForFunctionPointer",
        "System.Runtime.InteropServices.NativeLibrary::GetExport",
    ];

    /// <summary>Hook-layer types that may call through function pointers, each with why. They are checked for every
    /// other read.</summary>
    private static readonly Dictionary<string, string> FunctionPointerCallers = new()
    {
        ["Metaphor.Hooks.PrismSpeech"] =
            "ADR 0005: calls the pinned prism.dll's exports, passing only the mod's own handles and buffers",
        ["Metaphor.Hooks.SquirrelRegistrationListener"] =
            "dev-only: calls the game's sq_gettop, sq_getstring and sq_getuserdata with the VM the game passed it, " +
            "into its own locals; what they return is read through ModuleMemoryReader. Debug builds only, pinned to one " +
            "game build, each signature matched exactly once; a wrong unique match would fault uncaught, accepted for a spike",
    };

    private static bool IsListed(TypeDefinition type, Dictionary<string, string> list)
    {
        for (var owner = type; owner is not null; owner = owner.DeclaringType)
        {
            if (list.ContainsKey(owner.FullName))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<string> MemoryReads(TypeDefinition type)
    {
        foreach (var method in type.Methods.Where(method => method.HasBody))
        {
            foreach (var instruction in method.Body.Instructions)
            {
                var code = instruction.OpCode.Code;
                if (PointerReadingCodes.Contains(code) || code == Mono.Cecil.Cil.Code.Calli)
                {
                    yield return instruction.OpCode.Name;
                }
                // p->Field: a field load whose object is a pointer.
                if (code is Mono.Cecil.Cil.Code.Ldfld or Mono.Cecil.Cil.Code.Ldflda &&
                    LoadsPointer(instruction.Previous, method))
                {
                    yield return $"{instruction.OpCode.Name} through a pointer";
                }
                if (instruction.Operand is not MethodReference called)
                {
                    continue;
                }
                var member = $"{called.DeclaringType.FullName}::{called.Name}";
                if (MemoryReadingMembers.Any(reader => member.StartsWith(reader, StringComparison.Ordinal)))
                {
                    yield return member;
                }
                // Any library call that takes a pointer reads through it: new string(sbyte*),
                // Encoding.GetString(byte*, int), new Span<T>(void*, int). A "text"u8 literal isn't game memory.
                else if (called.DeclaringType.Scope is not ModuleDefinition &&
                    called.Parameters.Any(parameter => parameter.ParameterType is PointerType) &&
                    !IsUtf8Literal(instruction))
                {
                    yield return member;
                }
            }
        }
    }

    private static bool LoadsPointer(Mono.Cecil.Cil.Instruction? previous, MethodDefinition method)
    {
        var variables = method.Body.Variables;
        return previous?.OpCode.Code switch
        {
            Mono.Cecil.Cil.Code.Conv_I or Mono.Cecil.Cil.Code.Conv_U => true,
            >= Mono.Cecil.Cil.Code.Ldarg_0 and <= Mono.Cecil.Cil.Code.Ldarg_3 =>
                ArgumentType(method, previous.OpCode.Code - Mono.Cecil.Cil.Code.Ldarg_0) is PointerType,
            Mono.Cecil.Cil.Code.Ldarg or Mono.Cecil.Cil.Code.Ldarg_S =>
                ((ParameterDefinition)previous.Operand).ParameterType is PointerType,
            >= Mono.Cecil.Cil.Code.Ldloc_0 and <= Mono.Cecil.Cil.Code.Ldloc_3 =>
                variables[previous.OpCode.Code - Mono.Cecil.Cil.Code.Ldloc_0].VariableType is PointerType,
            Mono.Cecil.Cil.Code.Ldloc or Mono.Cecil.Cil.Code.Ldloc_S =>
                ((Mono.Cecil.Cil.VariableDefinition)previous.Operand).VariableType is PointerType,
            Mono.Cecil.Cil.Code.Call or Mono.Cecil.Cil.Code.Callvirt =>
                ((MethodReference)previous.Operand).ReturnType is PointerType,
            _ => false,
        };
    }

    private static TypeReference ArgumentType(MethodDefinition method, int index)
    {
        var parameter = method.HasThis ? index - 1 : index;
        return parameter < 0 ? method.DeclaringType : method.Parameters[parameter].ParameterType;
    }

    /// <summary>The compiler's form of <c>"text"u8</c>: a span over its own static data.</summary>
    private static bool IsUtf8Literal(Mono.Cecil.Cil.Instruction instruction) =>
        instruction.Previous?.Previous is { OpCode.Code: Mono.Cecil.Cil.Code.Ldsflda, Operand: FieldReference data } &&
        data.DeclaringType.FullName.StartsWith("<PrivateImplementationDetails>", StringComparison.Ordinal);

    /// <summary>
    /// #69, rule 11: a hook is activated only through the field that holds it, so it was stored first. Since #88 only
    /// the loader's adapters activate hooks (rule 14), so this checks them; it runs on every type all the same. The game can
    /// call the hook the moment it is active, and the callback reaches the original function through that field; and
    /// a hook nobody references can be collected while the game still calls it (#55's first run crashed this way).
    /// Checked as: the instruction before each <c>Activate</c> call loads a field the method's own type declares, and
    /// neither is compiler-made (a captured local becomes a field of a closure nothing keeps alive). So
    /// <c>hook?.Activate()</c> and <c>(hook = ...).Activate()</c> are refused too; write <c>hook.Activate()</c>.
    /// </summary>
    public static IReadOnlyList<string> HooksAreStoredBeforeActivating(AssemblyDefinition assembly)
    {
        var name = assembly.Name.Name;
        return [.. Types(assembly)
            .SelectMany(type => type.Methods.Where(method => method.HasBody)
                .Where(method => method.Body.Instructions.Any(instruction => ActivatesUnstoredHook(instruction, type)))
                .Select(method => $"{name} type {type.FullName} activates a hook before storing it in one of its own " +
                    $"fields (in {method.Name}) (rule 11)"))];
    }

    private static bool ActivatesUnstoredHook(Mono.Cecil.Cil.Instruction instruction, TypeDefinition type) =>
        instruction.Operand is MethodReference { Name: "Activate" } called &&
        called.DeclaringType.Namespace.StartsWith("Reloaded.Hooks", StringComparison.Ordinal) &&
        !(instruction.Previous is
        { OpCode.Code: Mono.Cecil.Cil.Code.Ldfld or Mono.Cecil.Cil.Code.Ldsfld, Operand: FieldReference field } &&
            field.DeclaringType.FullName == type.FullName &&
            !type.FullName.Contains('<', StringComparison.Ordinal) &&
            !field.Name.Contains('<', StringComparison.Ordinal));

    /// <summary>The record every hook's constructor takes.</summary>
    public const string HookServices = "Metaphor.Hooks.HookServices";

    /// <summary>
    /// #85, rule 12: hooks get their dependencies through the constructor. In the hook layer, no static field is
    /// writable after startup (each is <c>readonly</c> or <c>const</c>) and no static property has a setter, so there
    /// is no "set this before hooks install" to get wrong; and every <c>[GameHook]</c> type has a public constructor
    /// taking only <c>HookServices</c>, which the loader calls. Compiler-made types (lambda caches) are skipped.
    /// </summary>
    public static IReadOnlyList<string> HooksGetDependenciesThroughConstructor(AssemblyDefinition assembly)
    {
        if (!IsHookLayer(assembly))
        {
            return [];
        }
        var name = assembly.Name.Name;
        var types = Types(assembly).Where(type => !type.FullName.Contains('<', StringComparison.Ordinal)).ToList();
        var fields = types.SelectMany(type => type.Fields
            .Where(field => field.IsStatic && !field.IsInitOnly && !field.IsLiteral)
            .Select(field => $"{name} type {type.FullName} has mutable static field {field.Name} (rule 12)"));
        var setters = types.SelectMany(type => type.Properties
            .Where(property => property.SetMethod is { IsStatic: true })
            .Select(property => $"{name} type {type.FullName} has static setter {property.Name} (rule 12)"));
        var constructors = types
            .Where(type => type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == GameHookAttribute))
            .Where(type => !type.Methods.Any(method => method.IsConstructor && !method.IsStatic && method.IsPublic &&
                method.Parameters is [{ ParameterType.FullName: HookServices }]))
            .Select(type => $"{name} hook {type.FullName} has no public constructor taking only {HookServices} (rule 12)");
        return [.. fields, .. setters, .. constructors];
    }

    /// <summary>
    /// #86, rule 13: one hook per game function. No two <c>[GameHook]</c> types return the same
    /// <c>SignatureName</c>, read from the constant string its getter returns; a getter that returns anything but a
    /// constant can't be checked, so it is a violation too.
    /// </summary>
    public static IReadOnlyList<string> OneHookPerFunction(AssemblyDefinition assembly)
    {
        var name = assembly.Name.Name;
        var hooks = Types(assembly)
            .Where(type => type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == GameHookAttribute))
            .Select(type => (Type: type.FullName, Signature: ConstantSignatureName(type)))
            .ToList();
        var notConstant = hooks
            .Where(hook => hook.Signature is null)
            .Select(hook => $"{name} hook {hook.Type} has no constant SignatureName (rule 13)");
        var shared = hooks
            .Where(hook => hook.Signature is not null)
            .GroupBy(hook => hook.Signature, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{name} hooks {string.Join(", ", group.Select(hook => hook.Type).Order(StringComparer.Ordinal))} all hook {group.Key} (rule 13)");
        return [.. notConstant, .. shared];
    }

    /// <summary>
    /// #167, rule 15 (ADR 0010): a <c>[GameHook]</c> type whose <c>SignatureName</c> is in <c>.text1</c>, by
    /// <paramref name="sectionBySignature"/> from <c>hook-signatures.json</c>, sets <c>OptInFile</c> or
    /// <c>DevOnly = true</c>. A hook whose name isn't constant is rule 13's violation, and one whose signature isn't
    /// listed never installs, so neither is checked here.
    /// </summary>
    public static IReadOnlyList<string> Text1HooksAreOptIn(
        AssemblyDefinition assembly, IReadOnlyDictionary<string, string> sectionBySignature)
    {
        ArgumentNullException.ThrowIfNull(sectionBySignature);

        var name = assembly.Name.Name;
        return [.. Types(assembly)
            .Select(type => (Type: type, Hook: type.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == GameHookAttribute)))
            .Where(hook => hook.Hook is not null)
            .Select(hook => (hook.Type, hook.Hook, Signature: ConstantSignatureName(hook.Type)))
            .Where(hook => hook.Signature is not null &&
                sectionBySignature.TryGetValue(hook.Signature, out var section) && section == Text1Section)
            .Where(hook => !hook.Hook!.Properties.Any(p =>
                (p.Name == "DevOnly" && p.Argument.Value is true) || (p.Name == "OptInFile" && p.Argument.Value is string)))
            .Select(hook => $"{name} hook {hook.Type.FullName} hooks {hook.Signature} in {Text1Section} but sets neither " +
                "OptInFile nor DevOnly (rule 15)")];
    }

    private const string Text1Section = ".text1";

    /// <summary>
    /// #321, rule 16 (#300's globals): each <c>new GlobalSpec(…)</c> in the assembly (its primary constructor; a record's copy
    /// constructor builds no new spec) points at a RIP-relative load in its
    /// signature's pattern, by <paramref name="patternBySignature"/> from <c>hook-signatures.json</c>: a known
    /// <c>[rip+disp32]</c> load opcode at <c>LoadOffset</c> (<see cref="RipRelativeLoads"/>), exactly four <c>??</c> bytes
    /// at <c>DisplacementOffset</c> from there, and <c>InstructionLength</c> ending right after them. A typo there would
    /// point a reader at the wrong address. A spec whose signature, offsets and length aren't constants passed in order
    /// can't be checked, so it is a violation too, and so is a GlobalSpec constructor this rule doesn't know the shape
    /// of. It checks each spec's shape against its pattern, not against a game build: the specs name their own builds,
    /// and the loader's build check and exactly-once match decide those.
    /// </summary>
    public static IReadOnlyList<string> GlobalSpecsPointAtALoad(
        AssemblyDefinition assembly, IReadOnlyDictionary<string, string> patternBySignature)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(patternBySignature);

        var name = assembly.Name.Name;
        var problems = new List<string>();
        foreach (var method in Methods(assembly).Where(method => method.HasBody))
        {
            var code = method.Body.Instructions.Where(i => i.OpCode.Code != Mono.Cecil.Cil.Code.Nop).ToList();
            for (var at = 0; at < code.Count; at++)
            {
                if (code[at] is { OpCode.Code: Mono.Cecil.Cil.Code.Newobj, Operand: MethodReference constructor } &&
                    constructor.DeclaringType.FullName == GlobalSpecType && !IsCopyConstructor(constructor))
                {
                    var where = $"{name} {method.DeclaringType.FullName}";
                    if (!HasGlobalSpecShape(constructor))
                    {
                        problems.Add($"{where} builds a GlobalSpec through a constructor this rule can't read: update " +
                            "GlobalSpecProblem to the record's new shape (rule 16)");
                    }
                    else if (GlobalSpecProblem(code, at, patternBySignature) is { } problem)
                    {
                        problems.Add($"{where} declares a GlobalSpec {problem} (rule 16)");
                    }
                }
            }
        }
        return problems;
    }

    private const string GlobalSpecType = "Metaphor.Core.GlobalSpec";

    /// <summary>The primary constructor's parameters, in order, which <see cref="GlobalSpecProblem"/> reads the
    /// arguments by.</summary>
    private static readonly string[] GlobalSpecParameters =
        ["Name", "GameBuilds", "SignatureName", "LoadOffset", "DisplacementOffset", "InstructionLength", "TurnsOff"];

    // A reference into another assembly carries no parameter names, so this reads the definition; one that can't be
    // resolved can't be read either.
    private static bool HasGlobalSpecShape(MethodReference constructor) =>
        constructor.Resolve() is { } definition &&
        definition.Parameters.Select(parameter => parameter.Name).SequenceEqual(GlobalSpecParameters);

    // A record's copy constructor (its synthesized Clone calls it) takes the record itself and builds no new spec.
    private static bool IsCopyConstructor(MethodReference constructor) =>
        constructor.Parameters is [{ ParameterType.FullName: GlobalSpecType }];

    /// <summary>The <c>[rip+disp32]</c> loads a global can be read from: REX.W (<c>48</c>, or <c>4C</c> with REX.R for
    /// r8-r15), <c>8B</c> (mov) or <c>8D</c> (lea), then a ModR/M byte with mod 00 and r/m 101, any register. Source:
    /// docs/raw-sources/x86-rip-relative-loads-2026-11-19.md (sandpile.org's REX and ModR/M tables, and the Intel
    /// manual's MOV and LEA opcode rows). Also <c>8B</c> with no REX, a 32-bit mov of a 32-bit global such as the voice
    /// language (#538): docs/raw-sources/voice-language-getter-2026-11-20.md. A lea without REX.W isn't one: it gives a
    /// 32-bit address.</summary>
    private static readonly HashSet<string> RipRelativeLoads =
        [.. (from rex in new[] { "48", "4C" }
            from opcode in new[] { "8B", "8D" }
            from register in Enumerable.Range(0, 8)
            select $"{rex} {opcode} {(register << 3) | 5:X2}")
            .Concat(from register in Enumerable.Range(0, 8) select $"8B {(register << 3) | 5:X2}")];

    /// <summary>What is wrong with the GlobalSpec built by the <c>newobj</c> at <paramref name="at"/>, or null. Its
    /// arguments end <c>ldstr SignatureName; ldc LoadOffset; ldc DisplacementOffset; ldc InstructionLength; ldstr
    /// TurnsOff</c>.</summary>
    private static string? GlobalSpecProblem(
        List<Mono.Cecil.Cil.Instruction> code, int at, IReadOnlyDictionary<string, string> patternBySignature)
    {
        if (at < 5 || code[at - 5] is not { OpCode.Code: Mono.Cecil.Cil.Code.Ldstr, Operand: string signature } ||
            IntConstant(code[at - 4]) is not { } load || IntConstant(code[at - 3]) is not { } displacement ||
            IntConstant(code[at - 2]) is not { } length)
        {
            return "whose signature, offsets and length aren't constants";
        }
        if (!patternBySignature.TryGetValue(signature, out var pattern))
        {
            return $"on {signature}, which isn't in hook-signatures.json";
        }
        var bytes = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var spec = $"on {signature} (load at {load}, disp32 at {displacement}, length {length})";
        if (load < 0 || displacement < 0 || length != displacement + 4 || load + length > bytes.Length)
        {
            return $"{spec} whose disp32 isn't the last 4 bytes of a load inside the pattern";
        }
        if (!RipRelativeLoads.Contains(string.Join(' ', bytes[load..(load + displacement)])))
        {
            return $"{spec} that isn't at a [rip+disp32] mov or lea";
        }
        return bytes[(load + displacement)..(load + length)].All(b => b == "??")
            ? null
            : $"{spec} whose disp32 isn't 4 wildcard bytes";
    }

    private static int? IntConstant(Mono.Cecil.Cil.Instruction instruction) => instruction.OpCode.Code switch
    {
        Mono.Cecil.Cil.Code.Ldc_I4_M1 => -1,
        >= Mono.Cecil.Cil.Code.Ldc_I4_0 and <= Mono.Cecil.Cil.Code.Ldc_I4_8 =>
            instruction.OpCode.Code - Mono.Cecil.Cil.Code.Ldc_I4_0,
        Mono.Cecil.Cil.Code.Ldc_I4_S => (sbyte)instruction.Operand,
        Mono.Cecil.Cil.Code.Ldc_I4 => (int)instruction.Operand,
        _ => null,
    };

    /// <summary>The namespace and name prefix of the interfaces, one per parameter shape, a hook observes through.</summary>
    public const string ShapePrefix = "Metaphor.Hooks.IObserves";

    /// <summary>
    /// #88, rule 14: hooks pass calls through the loader's adapters. A <c>[GameHook]</c> type implements exactly one
    /// shape interface (<c>IObserves…</c>), and neither it nor code nested in it calls the hooking library at all
    /// (<c>CreateHook</c>, <c>Activate</c>, <c>OriginalFunction</c>, …) or catches every exception (<c>catch
    /// (Exception)</c> or a bare <c>catch</c>, filtered or not): the adapter for its shape does those, once for every
    /// hook. Rule 11 then has only the adapters to check. A hook that lists <c>Calls</c> declares <c>UseCalls</c>,
    /// explicitly implemented or not. The exception: a hook that changes the game's behaviour implements
    /// <c>IChangesBehaviour</c> and no shape, and hooks and guards its function itself (#58).
    /// </summary>
    public static IReadOnlyList<string> HooksPassThroughAdapters(AssemblyDefinition assembly)
    {
        var name = assembly.Name.Name;
        var hooks = Types(assembly)
            .Where(type => type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == GameHookAttribute))
            .ToList();
        var shapes = hooks.Select(type => (type, Count: type.Interfaces.Count(implemented =>
                implemented.InterfaceType.FullName.StartsWith(ShapePrefix, StringComparison.Ordinal)),
                Changes: ChangesBehaviour(type)))
            .Where(hook => hook.Changes ? hook.Count != 0 : hook.Count != 1)
            .Select(hook => $"{name} hook {hook.type.FullName} " + (hook.Changes ? "changes behaviour and observes a shape"
                : hook.Count == 0 ? "observes no shape" : "observes more than one shape") + " (rule 14)");
        // A hook that lists Calls and keeps the empty default UseCalls would call through null function pointers.
        // An explicit implementation is named after its interface (Metaphor.Hooks.IGameHook.UseCalls).
        static bool Named(string member, string name) =>
            member == name || member.EndsWith("." + name, StringComparison.Ordinal);
        var calls = hooks
            .Where(type => type.Properties.Any(property => Named(property.Name, "Calls")) &&
                !type.Methods.Any(method => Named(method.Name, "UseCalls")))
            .Select(type => $"{name} hook {type.FullName} lists Calls but has no UseCalls to take them (rule 14)");
        var installs = Types(assembly).Where(type => IsInsideHook(type) && !IsInsideBehaviourChangingHook(type))
            .SelectMany(type => type.Methods.Where(method => method.HasBody).SelectMany(method => InstallsOrGuards(method)
                .Select(problem => $"{name} hook code {type.FullName} {problem} (in {method.Name}); the adapter " +
                    "for its shape does that (rule 14)")));
        return [.. shapes, .. calls, .. installs.Distinct()];
    }

    /// <summary>The interface a hook that changes the game's behaviour implements instead of a shape (#58): rule 14's
    /// exception. It creates, activates and guards its own hook, still under rules 8-11.</summary>
    public const string ChangesBehaviourInterface = "Metaphor.Hooks.IChangesBehaviour";

    private static bool ChangesBehaviour(TypeDefinition type) =>
        type.Interfaces.Any(implemented => implemented.InterfaceType.FullName == ChangesBehaviourInterface);

    private static bool IsInsideBehaviourChangingHook(TypeDefinition type)
    {
        for (var owner = type; owner is not null; owner = owner.DeclaringType)
        {
            if (ChangesBehaviour(owner) &&
                owner.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == GameHookAttribute))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<string> InstallsOrGuards(MethodDefinition method)
    {
        foreach (var called in method.Body.Instructions.Select(instruction => instruction.Operand).OfType<MethodReference>()
            .Where(called => called.DeclaringType.Namespace.StartsWith("Reloaded.Hooks", StringComparison.Ordinal))
            .Select(called => called.Name).Distinct())
        {
            yield return $"calls {called}";
        }
        if (method.Body.ExceptionHandlers.Any(IsCatchAll))
        {
            yield return "catches every exception";
        }
    }

    /// <summary>Whether <paramref name="handler"/> catches <c>System.Exception</c> or <c>object</c>, filtered or not.
    /// A filtered catch (<c>catch (T) when …</c>) has no catch type; its filter block tests the type with
    /// <c>isinst T</c> first, and has no such test when it catches everything.</summary>
    private static bool IsCatchAll(Mono.Cecil.Cil.ExceptionHandler handler)
    {
        static bool Everything(TypeReference? type) => type?.FullName is null or "System.Exception" or "System.Object";
        static bool FilterCatchesEverything(Mono.Cecil.Cil.Instruction? first) =>
            first is not { OpCode.Code: Mono.Cecil.Cil.Code.Isinst, Operand: TypeReference tested } || Everything(tested);
        return handler.HandlerType switch
        {
            Mono.Cecil.Cil.ExceptionHandlerType.Catch => Everything(handler.CatchType),
            Mono.Cecil.Cil.ExceptionHandlerType.Filter => FilterCatchesEverything(FirstInstruction(handler.FilterStart)),
            _ => false,
        };

        static Mono.Cecil.Cil.Instruction? FirstInstruction(Mono.Cecil.Cil.Instruction? start)
        {
            while (start is { OpCode.Code: Mono.Cecil.Cil.Code.Nop })
            {
                start = start.Next;
            }
            return start;
        }
    }

    /// <summary>The string a hook's <c>SignatureName</c> getter returns, when its body is just <c>ldstr; ret</c>.</summary>
    private static string? ConstantSignatureName(TypeDefinition type)
    {
        var getter = type.Properties.FirstOrDefault(property => property.Name == "SignatureName")?.GetMethod;
        var code = getter is { HasBody: true }
            ? getter.Body.Instructions.Where(i => i.OpCode.Code != Mono.Cecil.Cil.Code.Nop).ToList()
            : [];
        return code is [{ OpCode.Code: Mono.Cecil.Cil.Code.Ldstr, Operand: string value }, { OpCode.Code: Mono.Cecil.Cil.Code.Ret }]
            ? value
            : null;
    }

    /// <summary>The field feature, whose cues and speech rule 19 holds to the free-roam gate.</summary>
    public const string FieldFeature = "Metaphor.Features.Field";

    private const string FreeRoamGateOpen = "Metaphor.Features.Field.FreeRoamGate::Open";

    /// <summary>The members that make a field sound: a cue played or held, or speech. Silencing needs no gate.</summary>
    private static readonly HashSet<string> SoundingMembers =
    [
        "Metaphor.Core.Cues.ICuePlayer::Play",
        "Metaphor.Core.Cues.ICuePlayer::Hold",
        "Metaphor.Core.Announcements.IAnnouncer::Announce",
    ];

    /// <summary>
    /// #702, rule 19: field sounds play only in free roam. In the field feature, every type that plays or holds a cue or
    /// speaks, with the code nested in it (lambdas, iterators), also calls <c>FreeRoamGate.Open</c>, so a new sonar or
    /// call-out can't sound through a cutscene, dialogue, menu or scripted move.
    /// </summary>
    public static IReadOnlyList<string> FieldSoundsGoThroughTheGate(AssemblyDefinition assembly)
    {
        if (assembly.Name.Name != FieldFeature)
        {
            return [];
        }
        var calls = Types(assembly)
            .SelectMany(type => type.Methods.Where(method => method.HasBody).SelectMany(method => method.Body.Instructions
                .Select(instruction => instruction.Operand).OfType<MethodReference>()
                .Select(called => (Owner: Outermost(type), Called: $"{called.DeclaringType.FullName}::{called.Name}"))))
            .ToList();
        var gated = calls.Where(call => call.Called == FreeRoamGateOpen).Select(call => call.Owner).ToHashSet();
        return [.. calls
            .Where(call => SoundingMembers.Contains(call.Called) && !gated.Contains(call.Owner))
            .Select(call => call.Owner).Distinct()
            .Select(owner => $"{assembly.Name.Name} type {owner.FullName} makes a field sound without FreeRoamGate.Open " +
                "(rule 19)")];
    }

    private static TypeDefinition Outermost(TypeDefinition type)
    {
        while (type.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }
        return type;
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
