using System.Text.RegularExpressions;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue;

/// <summary>Says the subtitles of animated cutscenes (#888), which reach none of the dialogue hooks: the lines of the
/// event file compiled last, in file order, one more each time the subtitle layout fades a line out. The first fade
/// says the first two lines, the one fading and the one coming in, so every line is said once. Queued, with no speaker,
/// through the dialogue's speech and history.</summary>
/// <remarks>Build 548ddc95… (Steam build 18330018), from the Debug logs of 2026-11-22 and 23
/// (docs/findings/anime-movie-subtitles-skip-dialogue-hooks.md): the opening movie's file, <c>e01_001_001.msg</c> (25
/// lines), compiled before the movie, and <c>AnimeMovieSubtitleMessageLayout</c> played animation 1032 as each line
/// faded out, 24 times. Still <c>[NEEDS VERIFICATION]</c>, for its by-ear session: that the layout shows a file's lines
/// in order with none skipped, that no other event file compiles while a movie plays, whether the layout shows a
/// speaker, and how the lines line up with the movie's voice. A skipped movie plays no more fades; the next event file
/// to compile starts from its first line.</remarks>
[FeatureSetting<DialogueFeature>(DialogueFeature.MovieSubtitles)]
public sealed partial class MovieSubtitles : ILayoutAnimationObserver, IMessageFileObserver
{
    /// <summary>The animated cutscenes' subtitle layout class.</summary>
    public const string Layout = "AnimeMovieSubtitleMessageLayout@ui@app";

    /// <summary>The animation the layout plays as a line fades out.</summary>
    public const int LineFadesOut = 1032;

    private readonly DialogueAnnouncer dialogue;
    private readonly Func<bool> on;
    private readonly Lock gate = new();
    private List<GameMessage>? lines;
    private int next;

    /// <param name="dialogue">Where the lines are said and recorded.</param>
    /// <param name="on">Whether the lines are said now; off, they still count, so the place in the movie is kept.</param>
    public MovieSubtitles(DialogueAnnouncer dialogue, Func<bool> on)
    {
        this.dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
        this.on = on ?? throw new ArgumentNullException(nameof(on));
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Layouts { get; } = [Layout];

    // The three movies' files are event scenes named like e01_001_001.msg; so are the event scenes the dialogue
    // window shows, which play no subtitle fades.
    [GeneratedRegex(@"^e\d+_\d+_\d+\.msg$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EventScene();

    /// <summary>An event scene's file becomes the one the next fades read, from its first line; any other file is
    /// ignored.</summary>
    public void Observe(MessageFileCompiled file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var name = file.Path.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        if (!EventScene().IsMatch(name))
        {
            return;
        }
        var parsed = MessageLibrary.Parse(file.Source);
        lock (gate)
        {
            lines = parsed;
            next = 0;
        }
    }

    /// <summary>A line fading out says the line coming in, and on the first fade the one going out before it.</summary>
    public void Played(LayoutAnimation played)
    {
        ArgumentNullException.ThrowIfNull(played);
        if (played.Layout != Layout || played.Animation != LineFadesOut)
        {
            return;
        }
        var due = new List<GameMessage>(2);
        lock (gate)
        {
            if (lines is null)
            {
                return;
            }
            var end = Math.Min(lines.Count, next == 0 ? 2 : next + 1);
            for (; next < end; next++)
            {
                due.Add(lines[next]);
            }
        }
        if (!on())
        {
            return;
        }
        foreach (var line in due)
        {
            dialogue.Show(null, line.Text, interrupt: false);
        }
    }
}
