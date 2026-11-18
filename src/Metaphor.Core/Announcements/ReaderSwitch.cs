namespace Metaphor.Core.Announcements;

/// <summary>The reader on/off switch (#58) as every feature sees it: an announcer that drops what it's handed while off,
/// and the switch itself. Toggling announces the new state either way. The dialogue feature owns the one instance; menus
/// speak through it and toggle it while a menu is open (#214).</summary>
public interface IReaderSwitch : IAnnouncer
{
    /// <summary>Whether what goes through the reader is spoken.</summary>
    bool IsOn { get; }

    /// <summary>Turns the reader off if it's on, or on if it's off, and says which.</summary>
    void Toggle();
}
