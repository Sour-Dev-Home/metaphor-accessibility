namespace Metaphor.Core.Features;

/// <summary>What the pop-up on screen last said, for the dialogue's repeat binding to say again (#118): the menus
/// provide it through the <see cref="FeatureContext"/>, behind the tutorials' setting, and the dialogue reads it on each
/// input read (#784).</summary>
public sealed class PopupOnScreen(Func<string?> said)
{
    /// <summary>The pop-up's last words, or null while none is up or its setting is off.</summary>
    public string? Said => said();
}
