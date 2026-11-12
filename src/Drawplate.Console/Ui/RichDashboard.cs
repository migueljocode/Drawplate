using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Drawplate.Models.Formatting;

namespace Drawplate.Console.Ui;

/// <summary>Full-screen alternate-buffer Aurora dashboard for startup probing and live conversion, mirroring -h.</summary>
internal static class RichDashboard
{
    private const int WheelScrollLines = 3;

    public static T RunStartupTask<T>(
        TextWriter stdout,
        Theme theme,
        Stopwatch clock,
        string label,
        Func<CancellationToken, Task<T>> work,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(work);

        var task = Task.Run(() => work(ct), ct);
        while (!task.IsCompleted)
        {
            var (width, height) = HelpPager.SafeWindowSize(theme.Width);
            var elapsed = clock.Elapsed.TotalSeconds;
            var reveal = Math.Clamp((elapsed + 0.08) / 0.32, 0.25, 1.0);
            var headerLines = BrandLogo.RenderLinesAt(width, theme.Utf8Glyphs, elapsed, reveal, topMargin: BrandLogo.TopMargin);

            var spin = Gradient.SpinnerAt(elapsed);
            var spinColor = Gradient.Wave(Palette.AuroraBright, elapsed * 0.4, 0.0);
            var bar = Gradient.IndeterminateBarAt(Math.Min(36, Math.Max(18, width - 16)), elapsed, theme.Utf8Glyphs);
            var body = PanelBuilder.Build(
                "INITIALIZING",
                [
                    $"{spinColor.Fg()}{spin}{Rgb.Reset} {Palette.TextPrimary.Fg()}{label}{Rgb.Reset}",
                    bar,
                ],
                Math.Min(width, 96),
                theme.Utf8Glyphs,
                Palette.BorderPrimary);

            DrawScreen(stdout, headerLines, body, width, height, theme.Utf8Glyphs, "Starting up  ·  Ctrl+C cancel");
            Thread.Sleep(22);
        }

        return task.GetAwaiter().GetResult();
    }

    public static (int ExitCode, SummaryInfo? Summary) RunConversionLoop(
        TextWriter stdout,
        Theme theme,
        Stopwatch clock,
        Func<IRenderer, CancellationToken, int> runConversion,
        CancellationToken outerCt)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(runConversion);

        var renderer = new DashboardRenderer(theme.Utf8Glyphs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        var worker = Task.Run(() => runConversion(renderer, linked.Token), CancellationToken.None);

        var shownFraction = 0.0;
        var lastTick = clock.Elapsed.TotalSeconds;
        var activeFileIndex = -1;
        var feedOffset = 0;
        var followTail = true;

        while (!worker.IsCompleted)
        {
            var (width, height) = HelpPager.SafeWindowSize(theme.Width);
            var now = clock.Elapsed.TotalSeconds;
            var dt = Math.Max(0.005, now - lastTick);
            lastTick = now;

            var snap = renderer.Snapshot();
            if (snap.ActiveIndex != activeFileIndex)
            {
                activeFileIndex = snap.ActiveIndex;
                shownFraction = 0.0;
            }

            if (snap.ActiveFraction.HasValue)
            {
                shownFraction = snap.ActiveFraction.Value <= 0.0
                    ? 0.0
                    : Gradient.Glide(shownFraction, snap.ActiveFraction.Value, dt, rate: 10.0);
            }

            var headerLines = BrandLogo.RenderLinesAt(width, theme.Utf8Glyphs, now, reveal: 1.0, topMargin: BrandLogo.TopMargin);
            var content = BuildDashboardBody(snap, width, height - headerLines.Count - 2, now, shownFraction, theme.Utf8Glyphs, ref feedOffset, ref followTail);

            DrawScreen(stdout, headerLines, content, width, height, theme.Utf8Glyphs, "↑/↓ or wheel scroll  ·  q / Esc / Ctrl+C cancel");

            for (var poll = 0; poll < 2 && !worker.IsCompleted; poll++)
            {
                if (TerminalInput.KeyAvailableSafe())
                {
                    var ev = TerminalInput.ReadEvent();
                    if (ev is { } input)
                    {
                        switch (input.Action)
                        {
                            case UiKeyAction.Quit:
                                try
                                {
                                    linked.Cancel();
                                }
                                catch (ObjectDisposedException)
                                {
                                }
                                break;
                            case UiKeyAction.Up:
                                followTail = false;
                                feedOffset = Math.Max(0, feedOffset - 1);
                                break;
                            case UiKeyAction.WheelUp:
                                followTail = false;
                                feedOffset = Math.Max(0, feedOffset - WheelScrollLines);
                                break;
                            case UiKeyAction.Down:
                                feedOffset++;
                                break;
                            case UiKeyAction.WheelDown:
                                feedOffset += WheelScrollLines;
                                break;
                            case UiKeyAction.PageUp:
                                followTail = false;
                                feedOffset = Math.Max(0, feedOffset - 6);
                                break;
                            case UiKeyAction.PageDown:
                                feedOffset += 6;
                                break;
                            case UiKeyAction.Home:
                                followTail = false;
                                feedOffset = 0;
                                break;
                            case UiKeyAction.End:
                                followTail = true;
                                break;
                        }
                    }

                    break;
                }

                Thread.Sleep(11);
            }
        }

        var code = worker.GetAwaiter().GetResult();
        return (code, renderer.CompletedSummary);
    }

    private static List<string> BuildDashboardBody(
        DashboardSnapshot snap,
        int width,
        int availableRows,
        double timeSeconds,
        double shownFraction,
        bool utf8,
        ref int feedOffset,
        ref bool followTail)
    {
        var glyphs = utf8 ? Glyphs.Utf8 : Glyphs.Ascii;
        var panelWidth = Math.Min(width, 108);
        var innerMax = Math.Max(26, panelWidth - 6);
        var result = new List<string>(Math.Max(8, availableRows));

        if (snap.Plan is { } plan && availableRows >= 16)
        {
            var crfText = plan.Crf.HasValue
                ? $" {Palette.TextMuted.Fg()}(CRF {plan.Crf.Value.ToString(CultureInfo.InvariantCulture)}){Rgb.Reset}"
                : string.Empty;
            var planRows = new List<string>
            {
                PanelBuilder.KeyValue("Input", $"{Palette.TextPrimary.Fg()}{Sanitizer.ForDisplay(plan.Input)}{Rgb.Reset}"),
                PanelBuilder.KeyValue("Output", $"{Palette.Sky.Fg()}{Sanitizer.ForDisplay(plan.Output)}{Rgb.Reset}"),
                PanelBuilder.KeyValue("Profile", $"{Palette.Cyan.Fg()}Q{plan.Quality.ToString(CultureInfo.InvariantCulture)}{Rgb.Reset}{crfText}  {Palette.TextMuted.Fg()}{glyphs.Separator}{Rgb.Reset}  {RichRenderer.FormatEncoderChain(plan.EncoderChain ?? "[CPU]")}  {Palette.TextMuted.Fg()}{glyphs.Separator}{Rgb.Reset}  {Palette.TextPrimary.Fg()}{plan.FileCount.ToString(CultureInfo.InvariantCulture)} video(s){Rgb.Reset} ({HumanFormat.Bytes(plan.TotalSize)})"),
            };

            if (plan.MaxSize is not null)
            {
                planRows.Add(PanelBuilder.KeyValue("Max size", $"{Palette.Sky.Fg()}{Sanitizer.ForDisplay(plan.MaxSize)}{Rgb.Reset}"));
            }

            result.AddRange(PanelBuilder.Build("CONVERSION PLAN", planRows, panelWidth, utf8, Palette.BorderPrimary));
        }

        if (snap.Note is { } note)
        {
            result.Add($"{Palette.Warn.Fg()}!{Rgb.Reset} {Palette.TextSecondary.Fg()}{Sanitizer.ForDisplay(note)}{Rgb.Reset}");
        }

        if (snap.ActiveTotal > 0)
        {
            var spin = Gradient.SpinnerAt(timeSeconds);
            var spinColor = Gradient.Wave(Palette.AuroraBright, timeSeconds * 0.35, 0.0);
            var barWidth = Math.Min(40, Math.Max(16, innerMax - 32));
            var bar = snap.ActiveFraction.HasValue
                ? Gradient.BarAt(barWidth, shownFraction, Palette.Aurora, timeSeconds, utf8)
                : Gradient.IndeterminateBarAt(barWidth, timeSeconds, utf8);

            var pctText = snap.ActiveFraction.HasValue
                ? $"{Math.Round(snap.ActiveFraction.Value * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}%"
                : "--%";
            var speedText = snap.ActiveSpeed.HasValue
                ? $"{snap.ActiveSpeed.Value.ToString("0.0", CultureInfo.InvariantCulture)}x"
                : "--x";
            var elapsedText = HumanFormat.Time(snap.ActiveElapsed);
            string? etaText = null;
            if (snap.ActiveFraction is > 0 and < 1)
            {
                var etaSecs = snap.ActiveElapsed.TotalSeconds * (1.0 - snap.ActiveFraction.Value) / snap.ActiveFraction.Value;
                etaText = HumanFormat.Time(TimeSpan.FromSeconds(Math.Min(etaSecs, TimeSpan.MaxValue.TotalSeconds)));
            }

            var metrics = $"{bar}  {Palette.Cyan.Fg()}{pctText.PadLeft(4)}{Rgb.Reset}  {Palette.Emerald.Fg()}{speedText}{Rgb.Reset}  {Palette.TextMuted.Fg()}elapsed {Palette.TextSecondary.Fg()}{elapsedText}{Rgb.Reset}";
            if (etaText is not null)
            {
                metrics += $"  {Palette.TextMuted.Fg()}eta {Palette.Sky.Fg()}{etaText}{Rgb.Reset}";
            }

            var progRows = new List<string>
            {
                $"{spinColor.Fg()}{spin}{Rgb.Reset} {Palette.Sky.Fg()}[{snap.ActiveIndex.ToString(CultureInfo.InvariantCulture)}/{snap.ActiveTotal.ToString(CultureInfo.InvariantCulture)}]{Rgb.Reset} {Palette.TextPrimary.Fg()}{Sanitizer.ForDisplay(snap.ActiveName)}{Rgb.Reset}",
                metrics,
            };

            result.AddRange(PanelBuilder.Build("ACTIVE ENCODE", progRows, panelWidth, utf8, Palette.Cyan));
        }

        var remainingRows = availableRows - result.Count - 2;
        if (snap.Feed.Count > 0 && remainingRows >= 2)
        {
            var visibleCount = Math.Max(1, Math.Min(snap.Feed.Count, remainingRows));
            var maxOffset = Math.Max(0, snap.Feed.Count - visibleCount);
            if (followTail || feedOffset >= maxOffset)
            {
                feedOffset = maxOffset;
                followTail = true;
            }
            else
            {
                feedOffset = Math.Clamp(feedOffset, 0, maxOffset);
            }

            var slice = snap.Feed.GetRange(feedOffset, visibleCount);
            var title = snap.Feed.Count > visibleCount
                ? $"FILES ({(feedOffset + 1).ToString(CultureInfo.InvariantCulture)}-{Math.Min(snap.Feed.Count, feedOffset + visibleCount).ToString(CultureInfo.InvariantCulture)}/{snap.Feed.Count.ToString(CultureInfo.InvariantCulture)})"
                : $"FILES ({snap.Feed.Count.ToString(CultureInfo.InvariantCulture)})";
            result.AddRange(PanelBuilder.Build(title, slice, panelWidth, utf8, Palette.BorderSecondary));
        }

        return result;
    }

    private static void DrawScreen(
        TextWriter stdout,
        IReadOnlyList<string> headerLines,
        IReadOnlyList<string> bodyLines,
        int width,
        int height,
        bool utf8,
        string footerHint)
    {
        var maxCols = Math.Max(10, width - 1);
        var sb = new StringBuilder(4096);
        sb.Append("\u001b[?2026h");
        var row = 1;

        for (var i = 0; i < headerLines.Count && row < height; i++, row++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\u001b[{row};1H")
              .Append(AnsiText.ClipVisible(headerLines[i], maxCols))
              .Append("\u001b[0m\u001b[K");
        }

        if (row < height)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\u001b[{row};1H\u001b[K");
            row++;
        }

        var maxBodyVis = 0;
        for (var i = 0; i < bodyLines.Count; i++)
        {
            var vis = AnsiText.VisibleLength(bodyLines[i]);
            if (vis > maxBodyVis)
            {
                maxBodyVis = vis;
            }
        }

        var bodyLeftPad = maxBodyVis > 0 && maxCols > maxBodyVis
            ? new string(' ', (maxCols - maxBodyVis) / 2)
            : string.Empty;

        for (var i = 0; i < bodyLines.Count && row < height; i++, row++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\u001b[{row};1H")
              .Append(AnsiText.ClipVisible(bodyLeftPad + bodyLines[i], maxCols))
              .Append("\u001b[0m\u001b[K");
        }

        while (row < height)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\u001b[{row};1H\u001b[K");
            row++;
        }

        var diamond = utf8 ? "◆" : "*";
        var statusRaw = $"  {diamond} Drawplate  {footerHint}";
        var statusPadded = statusRaw.Length >= maxCols ? statusRaw[..maxCols] : statusRaw.PadRight(maxCols);
        sb.Append(CultureInfo.InvariantCulture, $"\u001b[{height};1H")
          .Append(Palette.StatusBg.Bg())
          .Append(Palette.StatusFg.Fg())
          .Append(statusPadded)
          .Append("\u001b[0m\u001b[?2026l");

        stdout.Write(sb.ToString());
        stdout.Flush();
    }

    internal sealed record PlanInfo(
        string Input,
        string Output,
        int Quality,
        int? Crf,
        string? MaxSize,
        string? EncoderChain,
        int FileCount,
        long TotalSize);

    internal sealed record DashboardSnapshot(
        PlanInfo? Plan,
        string? Note,
        int ActiveIndex,
        int ActiveTotal,
        string ActiveName,
        double? ActiveFraction,
        double? ActiveSpeed,
        TimeSpan ActiveElapsed,
        List<string> Feed);

    internal sealed record SummaryInfo(
        int Converted,
        int Failed,
        int Skipped,
        long InputBytes,
        long OutputBytes,
        string EncoderCounts,
        TimeSpan Elapsed,
        string OutputFolder);

    internal sealed class DashboardRenderer(bool utf8) : IRenderer
    {
        private readonly Lock _lock = new();
        private readonly GlyphSet _glyphs = utf8 ? Glyphs.Utf8 : Glyphs.Ascii;
        private readonly List<string> _feed = [];
        private PlanInfo? _plan;
        private string? _note;
        private int _activeIndex;
        private int _activeTotal;
        private string _activeName = string.Empty;
        private double? _activeFraction;
        private double? _activeSpeed;
        private TimeSpan _activeElapsed;

        public SummaryInfo? CompletedSummary { get; private set; }
        public string? CompletedNote { get; private set; }

        public void PlanCard(string input, string output, int quality, int? crf, string? maxSize, string? encoderChain, int fileCount, long totalSize, bool timestampsKept)
        {
            lock (_lock)
            {
                _plan = new PlanInfo(input, output, quality, crf, maxSize, encoderChain, fileCount, totalSize);
            }
        }

        public void Unsupported(string name)
        {
            var shown = Sanitizer.ForDisplay(name);
            lock (_lock)
            {
                _feed.Add($"{Palette.TextMuted.Fg()}{_glyphs.Skip}{Rgb.Reset} {Palette.TextMuted.Fg()}{shown} (unsupported){Rgb.Reset}");
            }
        }

        public void FileStarted(int index, int total, string name)
        {
            lock (_lock)
            {
                _activeIndex = index;
                _activeTotal = total;
                _activeName = name;
                _activeFraction = null;
                _activeSpeed = null;
                _activeElapsed = TimeSpan.Zero;
            }
        }

        public void FileProgress(int index, int total, string name, double? fraction, double? speed, TimeSpan elapsed)
        {
            lock (_lock)
            {
                _activeIndex = index;
                _activeTotal = total;
                _activeName = name;
                _activeFraction = fraction;
                _activeSpeed = speed;
                _activeElapsed = elapsed;
            }
        }

        public void FileFinished(int index, int total, string name, bool success, string? encoder, TimeSpan elapsed, long inputSize, long outputSize, string? reason)
        {
            var shown = Sanitizer.ForDisplay(name);
            var counter = $"[{index.ToString(CultureInfo.InvariantCulture)}/{total.ToString(CultureInfo.InvariantCulture)}]";
            string line;
            if (success)
            {
                var diff = inputSize - outputSize;
                var delta = inputSize > 0
                    ? diff >= 0
                        ? $"{Palette.Emerald.Fg()}{_glyphs.Down}{(int)Math.Round(Math.Abs(diff) * 100.0 / inputSize, MidpointRounding.AwayFromZero)}%{Rgb.Reset}"
                        : $"{Palette.Warn.Fg()}{_glyphs.Up}{(int)Math.Round(Math.Abs(diff) * 100.0 / inputSize, MidpointRounding.AwayFromZero)}%{Rgb.Reset}"
                    : string.Empty;
                var badge = RichRenderer.FormatEncoderBadge(encoder ?? "[CPU]");
                line =
                    $"{Palette.Emerald.Fg()}{_glyphs.Ok}{Rgb.Reset} " +
                    $"{Palette.Sky.Fg()}{counter}{Rgb.Reset} " +
                    $"{Palette.TextPrimary.Fg()}{shown}{Rgb.Reset}  " +
                    $"{Palette.TextSecondary.Fg()}{HumanFormat.Bytes(inputSize)} {_glyphs.Arrow} {Palette.TextPrimary.Fg()}{HumanFormat.Bytes(outputSize)}{Rgb.Reset}  " +
                    $"{delta}  {badge}  {Palette.TextMuted.Fg()}{HumanFormat.Time(elapsed)}{Rgb.Reset}";
            }
            else
            {
                var why = Sanitizer.ForDisplay(reason ?? "failed");
                line =
                    $"{Palette.Error.Fg()}{_glyphs.Fail}{Rgb.Reset} " +
                    $"{Palette.Sky.Fg()}{counter}{Rgb.Reset} " +
                    $"{Palette.TextPrimary.Fg()}{shown}{Rgb.Reset}  " +
                    $"{Palette.Error.Fg()}failed: {why}{Rgb.Reset}";
            }

            lock (_lock)
            {
                _feed.Add(line);
                if (_activeIndex == index)
                {
                    _activeFraction = 1.0;
                }
            }
        }

        public void FileSkipped(int index, int total, string name, string reason)
        {
            var shown = Sanitizer.ForDisplay(name);
            var why = Sanitizer.ForDisplay(reason);
            var counter = $"[{index.ToString(CultureInfo.InvariantCulture)}/{total.ToString(CultureInfo.InvariantCulture)}]";
            lock (_lock)
            {
                _feed.Add(
                    $"{Palette.TextMuted.Fg()}{_glyphs.Skip}{Rgb.Reset} " +
                    $"{Palette.TextSecondary.Fg()}{counter}{Rgb.Reset} " +
                    $"{Palette.TextSecondary.Fg()}{shown}{Rgb.Reset}  " +
                    $"{Palette.TextMuted.Fg()}skipped: {why}{Rgb.Reset}");
            }
        }

        public void Note(string text)
        {
            lock (_lock)
            {
                _note = text;
                CompletedNote = text;
            }
        }

        public void Summary(int converted, int failed, int skipped, long inputBytes, long outputBytes, string encoderCounts, TimeSpan elapsed, string outputFolder)
        {
            lock (_lock)
            {
                CompletedSummary = new SummaryInfo(converted, failed, skipped, inputBytes, outputBytes, encoderCounts, elapsed, outputFolder);
            }
        }

        public DashboardSnapshot Snapshot()
        {
            lock (_lock)
            {
                return new DashboardSnapshot(
                    _plan,
                    _note,
                    _activeIndex,
                    _activeTotal,
                    _activeName,
                    _activeFraction,
                    _activeSpeed,
                    _activeElapsed,
                    [.. _feed]);
            }
        }
    }
}
