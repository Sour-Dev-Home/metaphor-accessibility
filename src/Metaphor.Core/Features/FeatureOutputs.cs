using Metaphor.Core.Announcements;
using Metaphor.Core.Cues;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Features;

/// <summary>The one factory for a feature's outputs (#723): <see cref="For"/> hands a feature its speech, through the
/// reader switch or straight away, and its cue player, each already behind the feature's setting (#661), read at each
/// line and cue so a changed file applies without a restart. A feature module takes every announcer and cue player it
/// hands a feature class from here, so rule 18 (every feature can be turned off) holds by construction; the
/// architecture tests fail a module that reaches past it.</summary>
/// <remarks>Built once, on the mod's start-up thread; the outputs it hands out are safe from any thread.</remarks>
public sealed class FeatureOutputs
{
    private readonly SettingsReloader settings;
    private readonly IAnnouncer now;
    private readonly IAnnouncer reader;
    private readonly ICuePlayer cues;

    /// <summary>Outputs over <paramref name="now"/> (straight to speech), <paramref name="reader"/> (the reader
    /// switch, #58) and <paramref name="cues"/>, gated by <paramref name="settings"/>.</summary>
    public FeatureOutputs(SettingsReloader settings, IAnnouncer now, IAnnouncer reader, ICuePlayer cues)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(now);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(cues);
        this.settings = settings;
        this.now = now;
        this.reader = reader;
        this.cues = cues;
    }

    /// <summary>The outputs of a feature class that serves <paramref name="features"/>: on while any of them is on.
    /// A class that serves several, such as the field controls, still checks each one itself.</summary>
    public FeatureOutput For(params Enum[] features)
    {
        ArgumentNullException.ThrowIfNull(features);
        if (features.Length == 0)
        {
            throw new ArgumentException("a feature's outputs need at least one feature", nameof(features));
        }
        Enum[] served = [.. features];
        return new FeatureOutput(() => Array.Exists(served, settings.Current.IsOn), now, reader, cues);
    }
}

/// <summary>A feature's outputs (#723), from <see cref="FeatureOutputs.For"/>: each says or plays nothing while the
/// feature is off.</summary>
public sealed class FeatureOutput
{
    private readonly Func<bool> on;

    internal FeatureOutput(Func<bool> on, IAnnouncer now, IAnnouncer reader, ICuePlayer cues)
    {
        this.on = on;
        Now = new GatedSpeech(on, now);
        Reader = new GatedSpeech(on, reader);
        Cues = new GatedCues(on, cues);
    }

    /// <summary>Whether the feature is on now.</summary>
    public bool IsOn => on();

    /// <summary>Speech straight away, outside the reader switch.</summary>
    public IAnnouncer Now { get; }

    /// <summary>Speech through the reader switch (#58): automatic speech.</summary>
    public IAnnouncer Reader { get; }

    /// <summary>The mod's sound cues (#636). Silencing a held tone always passes, so turning the feature off never
    /// leaves a tone playing.</summary>
    public ICuePlayer Cues { get; }

    /// <summary>Speech through <paramref name="route"/>, a route built over this feature's own or another output
    /// (the menus' prompt hold, the dialogue window's stoppable source, another feature's gate), behind this feature's
    /// setting too.</summary>
    public IAnnouncer Through(IAnnouncer route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return new GatedSpeech(on, route);
    }

    private sealed class GatedSpeech(Func<bool> on, IAnnouncer inner) : IAnnouncer
    {
        public void Announce(Announcement announcement)
        {
            if (on())
            {
                inner.Announce(announcement);
            }
        }
    }

    private sealed class GatedCues(Func<bool> on, ICuePlayer inner) : ICuePlayer
    {
        public void Play(Cue cue)
        {
            if (on())
            {
                inner.Play(cue);
            }
        }

        public void Play(Cue cue, double pan)
        {
            if (on())
            {
                inner.Play(cue, pan);
            }
        }

        public void Hold(ToneChannel channel, HeldTone tone)
        {
            if (on())
            {
                inner.Hold(channel, tone);
            }
        }

        public void Silence(ToneChannel channel) => inner.Silence(channel);
    }
}
