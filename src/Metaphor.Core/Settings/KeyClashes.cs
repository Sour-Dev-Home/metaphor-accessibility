namespace Metaphor.Core.Settings;

/// <summary>Two actions bound to the same key or pad chord in overlapping places (#755): the earlier action keeps the
/// binding, and <see cref="UnboundPath"/> is left unbound until the settings file fixes it.</summary>
public sealed record KeyClash(
    string FirstAction, string SecondAction, string Binding, string UnboundPath, string Spoken, string LogLine)
{
    /// <summary>Identifies this clash across reloads so an unchanged clash is not repeated on a duplicate watcher event
    /// or an unrelated setting change.</summary>
    public string Id => $"{FirstAction}|{SecondAction}|{UnboundPath}|{Binding}";
}

/// <summary>Finds duplicate key or pad-chord bindings across active actions whose <see cref="KeyPlaces"/> overlap
/// (#755).</summary>
public static class KeyClashes
{
    private const KeyPlaces EverywhereOutsideSettings =
        KeyPlaces.Anywhere | KeyPlaces.Dialogue | KeyPlaces.Menu | KeyPlaces.Field | KeyPlaces.Battle;

    /// <summary>Every clash in <paramref name="settings"/> across <paramref name="entries"/>, in registration order.
    /// Hidden Debug-only keys, fixed keys and actions whose feature is off do not claim bindings.</summary>
    public static IReadOnlyList<KeyClash> Find(ModSettings settings, IEnumerable<KeyEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(entries);
        List<Claim<(int Code, bool WithCtrl)>> keys = [];
        List<Claim<int>> buttons = [];
        List<KeyClash> clashes = [];
        foreach (var entry in entries)
        {
            if (entry.Key.Hidden || entry.Key.Fixed is not null || !settings.IsOn(entry.Feature))
            {
                continue;
            }
            var places = Expand(entry.Places);
            if (entry.Key.Key is { } key)
            {
                CheckKey(settings, entry.Key, key, places, keys, clashes);
            }
            if (entry.Key.Button is { } button)
            {
                CheckButtons(settings, entry.Key, button, entry.Key.Hold, places, buttons, clashes);
            }
        }
        return clashes;
    }

    private static void CheckKey(
        ModSettings settings, ModKey modKey, BoundKey key, KeyPlaces places,
        List<Claim<(int Code, bool WithCtrl)>> claims, List<KeyClash> clashes)
    {
        var code = key.From(settings);
        if (code == 0)
        {
            return;
        }
        var bound = (code, modKey.WithCtrl);
        var path = $"{key.Group}.{key.Name}";
        if (claims.FirstOrDefault(claim => claim.Bound == bound && claim.SettingId == path) is { } shared)
        {
            shared.Places |= places;
            return;
        }
        if (claims.FirstOrDefault(claim => claim.Bound == bound && (claim.Places & places) != 0) is { } first)
        {
            var spoken = (modKey.WithCtrl ? "Control " : "") + BindingNames.SpokenKey(code);
            clashes.Add(Make(first.Does, first.UnboundPath, modKey.Does, path, spoken));
            return;
        }
        claims.Add(new(bound, path, path, modKey.Does, places));
    }

    private static void CheckButtons(
        ModSettings settings, ModKey modKey, BoundKey button, BoundKey? hold, KeyPlaces places,
        List<Claim<int>> claims, List<KeyClash> clashes)
    {
        var buttonMask = button.From(settings);
        var holdMask = hold is null ? 0 : hold.From(settings);
        if (buttonMask == 0 || (hold is not null && holdMask == 0))
        {
            return;
        }
        var mask = holdMask | buttonMask;
        var buttonPath = $"{button.Group}.{button.Name}";
        var settingId = hold is null ? buttonPath : $"{hold.Group}.{hold.Name}+{buttonPath}";
        if (claims.FirstOrDefault(claim => claim.Bound == mask && claim.SettingId == settingId) is { } shared)
        {
            shared.Places |= places;
            return;
        }
        if (claims.FirstOrDefault(claim => claim.Bound == mask && (claim.Places & places) != 0) is { } first)
        {
            var spoken = holdMask == 0
                ? BindingNames.SpokenButtons(buttonMask)!
                : $"{BindingNames.SpokenButtons(holdMask)} with {BindingNames.SpokenButtons(buttonMask)}";
            clashes.Add(Make(first.Does, first.UnboundPath, modKey.Does, buttonPath, spoken));
            return;
        }
        claims.Add(new(mask, settingId, buttonPath, modKey.Does, places));
    }

    private static KeyClash Make(
        string firstAction, string firstPath, string secondAction, string secondPath, string binding) =>
        new(
            firstAction,
            secondAction,
            binding,
            secondPath,
            $"{firstAction} and {secondAction} are both bound to {binding}. {secondAction} is unbound.",
            $"settings file: \"keys.{firstPath}\" and \"keys.{secondPath}\" are both bound to {binding} " +
                $"({firstAction}, {secondAction}); leaving \"keys.{secondPath}\" unbound");

    private static KeyPlaces Expand(KeyPlaces places) =>
        places.HasFlag(KeyPlaces.Anywhere) ? places | EverywhereOutsideSettings : places;

    private sealed class Claim<T>(T bound, string settingId, string unboundPath, string does, KeyPlaces places)
    {
        public T Bound { get; } = bound;

        public string SettingId { get; } = settingId;

        public string UnboundPath { get; } = unboundPath;

        public string Does { get; } = does;

        public KeyPlaces Places { get; set; } = places;
    }
}
