namespace Metaphor.Features.Menus;

/// <summary>Where the yes/no dialog finds the name the player typed (#850). The menus' module makes it and hands it to
/// the dialog; the name entry's module, registered later, says where the name is (<see cref="NameEntrySpeech"/>).
/// </summary>
public sealed class TypedNameSource
{
    private volatile Func<string?>? read;

    /// <summary>The name last typed, or null when none is known.</summary>
    public string? Name() => read?.Invoke();

    /// <summary>Says where the name is read from.</summary>
    public void ReadFrom(Func<string?> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        read = source;
    }
}
