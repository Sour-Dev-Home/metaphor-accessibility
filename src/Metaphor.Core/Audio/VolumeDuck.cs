using System.Globalization;

namespace Metaphor.Core.Audio;

/// <summary>The game process's own volume, as Windows' per-app mixer holds it, from 0 to 1 (#927).</summary>
public interface IAppVolume
{
    /// <summary>The level now; null when it can't be read.</summary>
    float? Read();

    /// <summary>Sets the level; false when it couldn't.</summary>
    bool Write(float level);
}

/// <summary>Lowers the game's volume while something the mod says needs hearing, and puts it back (#927).</summary>
public interface IVolumeDuck
{
    /// <summary>Lowers the volume, once until <see cref="Restore"/>.</summary>
    void Duck();

    /// <summary>Puts back the level <see cref="Duck"/> lowered; nothing when it isn't ducked.</summary>
    void Restore();
}

/// <summary>Never changes the volume: no mixer, or a test.</summary>
public sealed class NoVolumeDuck : IVolumeDuck
{
    public static readonly NoVolumeDuck Instance = new();

    public void Duck()
    {
    }

    public void Restore()
    {
    }
}

/// <summary>Ducks <see cref="IAppVolume"/> to a share of its level, writing the level first to a marker file, so a
/// crash or a kill while ducked can't leave the game quiet: Windows keeps an app's level (Microsoft's ISimpleAudioVolume
/// page, docs/raw-sources/core-audio-session-volume-2026-11-24.md), and <see cref="RestoreLeftover"/> puts it back at the
/// next start (#927).</summary>
public sealed class VolumeDuck : IVolumeDuck
{
    /// <summary>The share of the level kept while ducked.</summary>
    public const float DuckedShare = 0.3f;

    /// <summary>The marker's file name, in the mod's settings folder.</summary>
    public const string MarkerName = "volume-before-duck.txt";

    private readonly IAppVolume volume;
    private readonly string marker;
    private readonly object gate = new();
    private float? saved;

    /// <param name="volume">The game's volume.</param>
    /// <param name="folder">Where the marker is written: the mod's settings folder.</param>
    public VolumeDuck(IAppVolume volume, string folder)
    {
        ArgumentNullException.ThrowIfNull(volume);
        ArgumentNullException.ThrowIfNull(folder);
        this.volume = volume;
        marker = Path.Combine(folder, MarkerName);
    }

    /// <summary>At start: a marker left by a run that ended while ducked puts its level back, then goes.</summary>
    public void RestoreLeftover()
    {
        lock (gate)
        {
            if (!File.Exists(marker))
            {
                return;
            }
            // A marker that isn't a level is dropped: there is nothing to put back.
            if (ReadMarker() is not { } level || volume.Write(level))
            {
                File.Delete(marker);
            }
        }
    }

    public void Duck()
    {
        lock (gate)
        {
            if (saved is not null || volume.Read() is not { } now)
            {
                return;
            }
            // A leftover marker the start couldn't restore holds the level from before that duck, not the lowered one
            // read now, so it is kept (security review, 2026-11-24).
            var level = File.Exists(marker) && ReadMarker() is { } before ? before : now;
            // The marker is on disk before the level drops, so no moment is ducked without it.
            AtomicFile.WriteAllText(marker, level.ToString("R", CultureInfo.InvariantCulture));
            if (volume.Write(now * DuckedShare))
            {
                saved = level;
            }
            else
            {
                File.Delete(marker);
            }
        }
    }

    public void Restore()
    {
        lock (gate)
        {
            if (saved is not { } level || !volume.Write(level))
            {
                return;
            }
            saved = null;
            File.Delete(marker);
        }
    }

    private float? ReadMarker()
    {
        var text = File.ReadAllText(marker, System.Text.Encoding.UTF8).Trim();
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var level)
            && level is >= 0f and <= 1f
                ? level
                : null;
    }
}
