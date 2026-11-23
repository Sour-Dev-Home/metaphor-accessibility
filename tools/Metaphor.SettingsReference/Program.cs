using Metaphor.SettingsReference;

// Writes the settings reference from the settings in code (#784), or with --check says whether the page is current.
// Run from the repository root: dotnet run --project tools/Metaphor.SettingsReference -c Release -v q [-- --check]
var check = args is ["--check"];
if (args.Length > 0 && !check)
{
    Console.Error.WriteLine("usage: Metaphor.SettingsReference [--check]");
    return 2;
}
var path = Path.Combine(Environment.CurrentDirectory, SettingsPage.RelativePath);
var rendered = SettingsPage.Render(AllFeatures.Catalogue());
if (check)
{
    var current = File.Exists(path) ? File.ReadAllText(path) : null;
    if (current == rendered)
    {
        return 0;
    }
    Console.Error.WriteLine($"{SettingsPage.RelativePath} is stale: {SettingsPage.Regenerate}");
    return 1;
}
File.WriteAllText(path, rendered);
Console.WriteLine($"wrote {SettingsPage.RelativePath}");
return 0;
