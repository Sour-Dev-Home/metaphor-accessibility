using Metaphor.NavSounds;

// Writes the navigation sounds to data/sounds/ (#839), or with --check says whether the files are current.
// Run from the repository root: dotnet run --project tools/Metaphor.NavSounds -c Release -v q [-- --check]
var check = args is ["--check"];
if (args.Length > 0 && !check)
{
    Console.Error.WriteLine("usage: Metaphor.NavSounds [--check]");
    return 2;
}
var folder = Path.Combine(Environment.CurrentDirectory, NavSoundSet.RelativeFolder);
var sounds = NavSoundSet.Render();
if (check)
{
    var stale = sounds.Where(sound =>
        !File.Exists(Path.Combine(folder, sound.Name)) ||
        !File.ReadAllBytes(Path.Combine(folder, sound.Name)).AsSpan().SequenceEqual(sound.Wav)).ToList();
    if (stale.Count == 0)
    {
        return 0;
    }
    Console.Error.WriteLine($"{string.Join(", ", stale.Select(sound => sound.Name))} stale: {NavSoundSet.Regenerate}");
    return 1;
}
Directory.CreateDirectory(folder);
foreach (var sound in sounds)
{
    File.WriteAllBytes(Path.Combine(folder, sound.Name), sound.Wav);
}
Console.WriteLine($"wrote {sounds.Count} files to {NavSoundSet.RelativeFolder}");
return 0;
