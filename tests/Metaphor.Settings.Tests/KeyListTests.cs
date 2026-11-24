using System.Reflection;
using System.Text.Json;
using Metaphor.Core.Settings;
using Metaphor.Features.Battle;
using Metaphor.Features.Dialogue;
using Metaphor.Features.Field;
using Metaphor.Features.Menus;

namespace Metaphor.Settings.Tests;

/// <summary>The keys each project lists for the help key and the settings menu (#868, #754) are every key its controls
/// read: a binding a project reads but doesn't list makes this fail, so the help can't drift from the bindings.
/// </summary>
[Trait("Category", "Unit")]
public sealed class KeyListTests
{
    [Fact]
    public void TheBattleListsEveryKeyItReads() => AssertListed(BattleBindings.From, BattleKeys.All);

    [Fact]
    public void TheDialogueListsEveryKeyItReads()
    {
        AssertListed(DialogueBindings.From, DialogueKeys.All);
        // The context keys (#879).
        AssertListed(DialogueKeyBindings.From, DialogueKeys.All);
    }

    [Fact]
    public void TheFieldListsEveryKeyItReads()
    {
        AssertListed(FieldBindings.From, FieldKeys.All);
        // The stick features' keys (#701, #184).
        AssertListed(StickBindings.From, FieldKeys.All);
    }

    // The reader binding comes in from the dialogue's, so the menus' own read is the details and read-back keys.
    [Fact]
    public void TheMenusListEveryKeyTheyRead()
    {
        AssertListed(now => MenuBindings.From(now, 0, 0), MenuKeys.All, nameof(MenuBindings.ReaderButtons),
            nameof(MenuBindings.ReaderKey));
        AssertListed(SettingsMenuBindings.From, MenuKeys.All);
    }

    // Planted: a list missing one key fails.
    [Fact]
    public void AListMissingAKeyFails() =>
        Assert.ThrowsAny<Exception>(() => AssertListed(DialogueBindings.From, DialogueKeys.All.Skip(1)));

    // Rebinds every listed key to something else, then checks every binding the project reads moved.
    private static void AssertListed<T>(Func<ModSettings, T> from, IEnumerable<KeyEntry> entries, params string[] notRead)
    {
        var listed = entries.SelectMany(entry => entry.Key.Bindings).DistinctBy(key => (key.Group, key.Name)).ToList();
        var json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["keys"] = listed.GroupBy(key => key.Group).ToDictionary(
                group => group.Key, group => group.ToDictionary(key => key.Name, Elsewhere)),
        });
        var rebound = from(ModSettings.Parse(json, SettingCatalogue.Empty, _ => { }));
        var defaults = from(ModSettings.Defaults);
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(int) && !notRead.Contains(property.Name)))
        {
            Assert.True(
                !Equals(property.GetValue(defaults), property.GetValue(rebound)),
                $"{typeof(T).Name}.{property.Name} is read, but no listed key binds it");
        }
    }

    // A binding that differs from the key's default.
    private static string Elsewhere(BoundKey key) =>
        key.Buttons ? (key.Default == 0x1000 ? "X" : "Y") : (key.Default == 0x7B ? "F11" : "F12");
}
