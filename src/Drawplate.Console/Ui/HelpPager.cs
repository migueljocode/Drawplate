using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Drawplate.Console.Ui;

/// <summary>Full-screen alternate-buffer Aurora help pager with mouse-wheel, search, top margin, and Windows VT safety.</summary>
internal static class HelpPager
{
    private const int WheelScrollLines = 3;

    public static void Show(TextWriter stdout, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(theme);

        using var consoleGuard = ReferenceEquals(stdout, global::System.Console.Out) && !global::System.Console.IsOutputRedirected
            ? ConsoleGuard.EnterInteractive()
            : null;

        if (consoleGuard is not null && !consoleGuard.VtEnabled)
        {
            stdout.Write(Cli.HelpText.Render());
            return;
        }

        var effectiveUtf8 = theme.Utf8Glyphs || (OperatingSystem.IsWindows() && (consoleGuard?.VtEnabled ?? false));
        if (!theme.Colors)
        {
            stdout.Write(Cli.HelpText.Render());
            return;
        }

        var canInteractivePage = theme.IsInteractive
            && !global::System.Console.IsInputRedirected
            && !global::System.Console.IsOutputRedirected;

        // When stdout/stdin are non-interactive (piped/redirected), play inline instead of entering alternate screen.
        if (!canInteractivePage)
        {
            var (width, _) = SafeWindowSize(theme.Width);
            var bodyLines = HelpView.BuildBodyLines(width, effectiveUtf8);
            RenderInlineAnimated(stdout, width, effectiveUtf8, bodyLines, allowSkip: false);
            return;
        }

        RunAlternatePager(stdout, effectiveUtf8);
    }

    private static void RenderInlineAnimated(TextWriter stdout, int width, bool utf8, IReadOnlyList<string> bodyLines, bool allowSkip)
    {
        var finalHeader = BrandLogo.RenderLinesAt(width, utf8, timeSeconds: 0.3, reveal: 1.0, topMargin: BrandLogo.TopMargin);
        var headerHeight = finalHeader.Count;
        var started = Stopwatch.GetTimestamp();

        try
        {
            stdout.Write("\u001b[?25l");
            for (var step = 1; step <= 10; step++)
            {
                if (allowSkip && TerminalInput.KeyAvailableSafe())
                {
                    _ = TerminalInput.ReadEvent();
                    break;
                }

                var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
                var frameLines = BrandLogo.RenderLinesAt(width, utf8, elapsed, reveal: step / 10.0, topMargin: BrandLogo.TopMargin);
                var sb = new StringBuilder(512);
                sb.Append("\u001b[?2026h");
                if (step > 1)
                {
                    sb.Append(CultureInfo.InvariantCulture, $"\u001b[{headerHeight}A");
                }

                foreach (var line in frameLines)
                {
                    sb.Append('\r').Append(AnsiText.ClipVisible(line, Math.Max(10, width - 1))).Append("\u001b[K\n");
                }

                sb.Append("\u001b[?2026l");
                stdout.Write(sb.ToString());
                stdout.Flush();
                Thread.Sleep(16);
            }
        }
        finally
        {
            stdout.Write("\u001b[?25h");
        }

        stdout.WriteLine();
        foreach (var line in bodyLines)
        {
            stdout.WriteLine(AnsiText.ClipVisible(line, Math.Max(10, width - 1)));
        }

        stdout.Flush();
    }

    private static void RunAlternatePager(TextWriter stdout, bool utf8)
    {
        using var screen = ScreenGuard.TryEnter(stdout, new Theme(true, utf8, 80, true, true, true), enableMouse: true);
        var (width, height) = SafeWindowSize(100);
        var offset = 0;
        string? searchQuery = null;
        var lastWidth = width;
        var bodyLines = HelpView.BuildBodyLines(width, utf8, searchQuery);
        var clock = Stopwatch.StartNew();

        try
        {
            while (true)
            {
                (width, height) = SafeWindowSize(width);
                if (width != lastWidth)
                {
                    lastWidth = width;
                    bodyLines = HelpView.BuildBodyLines(width, utf8, searchQuery);
                }

                var elapsed = clock.Elapsed.TotalSeconds;
                var reveal = Math.Clamp((elapsed + 0.08) / 0.34, 0.2, 1.0);
                var headerLines = BrandLogo.RenderLinesAt(width, utf8, elapsed, reveal: reveal, topMargin: BrandLogo.TopMargin);
                var headerRows = headerLines.Count + 1;
                var bodyRows = Math.Max(4, height - headerRows - 1);
                var maxOffset = Math.Max(0, bodyLines.Count - bodyRows);
                offset = Math.Clamp(offset, 0, maxOffset);

                DrawFrame(stdout, headerLines, bodyLines, offset, bodyRows, width, height, utf8, searchQuery);

                // ~45 FPS smooth gradient motion while polling input at ~11ms latency.
                for (var poll = 0; poll < 2; poll++)
                {
                    if (TerminalInput.KeyAvailableSafe())
                    {
                        var ev = TerminalInput.ReadEvent();
                        if (ev is null)
                        {
                            break;
                        }

                        var input = ev.Value;
                        switch (input.Action)
                        {
                            case UiKeyAction.Quit:
                                return;
                            case UiKeyAction.Down:
                                offset = Math.Min(maxOffset, offset + 1);
                                break;
                            case UiKeyAction.Up:
                                offset = Math.Max(0, offset - 1);
                                break;
                            case UiKeyAction.WheelDown:
                                offset = Math.Min(maxOffset, offset + WheelScrollLines);
                                break;
                            case UiKeyAction.WheelUp:
                                offset = Math.Max(0, offset - WheelScrollLines);
                                break;
                            case UiKeyAction.PageDown:
                                offset = Math.Min(maxOffset, offset + bodyRows);
                                break;
                            case UiKeyAction.PageUp:
                                offset = Math.Max(0, offset - bodyRows);
                                break;
                            case UiKeyAction.Home:
                                offset = 0;
                                break;
                            case UiKeyAction.End:
                                offset = maxOffset;
                                break;
                            case UiKeyAction.Search:
                                searchQuery = PromptSearch(stdout, height, width, utf8);
                                bodyLines = HelpView.BuildBodyLines(width, utf8, searchQuery);
                                maxOffset = Math.Max(0, bodyLines.Count - bodyRows);
                                if (!string.IsNullOrEmpty(searchQuery))
                                {
                                    var hit = FindNextMatch(bodyLines, 0, searchQuery, forward: true);
                                    if (hit >= 0)
                                    {
                                        offset = Math.Clamp(hit, 0, maxOffset);
                                    }
                                }
                                break;
                            case UiKeyAction.NextMatch when !string.IsNullOrEmpty(searchQuery):
                                {
                                    var hit = FindNextMatch(bodyLines, offset + 1, searchQuery, forward: true);
                                    if (hit >= 0)
                                    {
                                        offset = Math.Clamp(hit, 0, maxOffset);
                                    }
                                    break;
                                }
                            case UiKeyAction.PrevMatch when !string.IsNullOrEmpty(searchQuery):
                                {
                                    var hit = FindNextMatch(bodyLines, offset - 1, searchQuery, forward: false);
                                    if (hit >= 0)
                                    {
                                        offset = Math.Clamp(hit, 0, maxOffset);
                                    }
                                    break;
                                }
                        }

                        break;
                    }

                    Thread.Sleep(11);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void DrawFrame(
        TextWriter stdout,
        IReadOnlyList<string> headerLines,
        IReadOnlyList<string> bodyLines,
        int offset,
        int bodyRows,
        int width,
        int height,
        bool utf8,
        string? searchQuery)
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

        var end = Math.Min(bodyLines.Count, offset + bodyRows);
        for (var i = offset; i < end && row < height; i++, row++)
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

        var shownEnd = Math.Min(bodyLines.Count, offset + bodyRows);
        var pct = bodyLines.Count <= bodyRows ? 100 : (int)Math.Round(100.0 * shownEnd / bodyLines.Count, MidpointRounding.AwayFromZero);
        var pctLabel = shownEnd >= bodyLines.Count ? "END" : $"{pct.ToString(CultureInfo.InvariantCulture)}%";
        var dot = utf8 ? "·" : "-";
        var diamond = utf8 ? "◆" : "*";
        var searchNote = string.IsNullOrEmpty(searchQuery) ? string.Empty : $"  {dot}  /{searchQuery} (n/N)";
        var statusRaw = $"  {diamond} Drawplate Help  {offset + 1}-{shownEnd}/{bodyLines.Count}  {pctLabel}  {dot}  ↑/↓ scroll  space/b  g/G  /search  q{searchNote}";
        var statusPadded = statusRaw.Length >= maxCols ? statusRaw[..maxCols] : statusRaw.PadRight(maxCols);

        sb.Append(CultureInfo.InvariantCulture, $"\u001b[{height};1H")
          .Append(Palette.StatusBg.Bg())
          .Append(Palette.StatusFg.Fg())
          .Append(statusPadded)
          .Append("\u001b[0m\u001b[?2026l");

        stdout.Write(sb.ToString());
        stdout.Flush();
    }

    private static string? PromptSearch(TextWriter stdout, int height, int width, bool utf8)
    {
        var maxCols = Math.Max(10, width - 1);
        var query = new StringBuilder();
        stdout.Write("\u001b[?25h");
        try
        {
            while (true)
            {
                var prompt = $"  {(utf8 ? "⌕" : "/")} Search: {query}";
                var clipped = prompt.Length >= maxCols ? prompt[..maxCols] : prompt.PadRight(maxCols);
                stdout.Write(string.Create(CultureInfo.InvariantCulture, $"\u001b[{height};1H{Palette.KeyBg.Bg()}{Palette.KeyFg.Fg()}{clipped}\u001b[0m"));
                stdout.Flush();

                var ev = TerminalInput.ReadEvent();
                if (ev is null)
                {
                    return null;
                }

                var input = ev.Value;
                if (input.Action == UiKeyAction.Quit && input.KeyChar != 'q')
                {
                    return null;
                }

                if (input.Action == UiKeyAction.Enter)
                {
                    var text = query.ToString().Trim();
                    return text.Length == 0 ? null : text;
                }

                if (input.Action == UiKeyAction.Backspace)
                {
                    if (query.Length > 0)
                    {
                        query.Length--;
                    }
                }
                else if (input.KeyChar != '\0' && !char.IsControl(input.KeyChar))
                {
                    query.Append(input.KeyChar);
                }
            }
        }
        finally
        {
            stdout.Write("\u001b[?25l");
        }
    }

    private static int FindNextMatch(IReadOnlyList<string> lines, int startIndex, string query, bool forward)
    {
        if (lines.Count == 0)
        {
            return -1;
        }

        if (forward)
        {
            for (var i = Math.Max(0, startIndex); i < lines.Count; i++)
            {
                if (AnsiText.Strip(lines[i]).Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        for (var i = Math.Min(lines.Count - 1, startIndex); i >= 0; i--)
        {
            if (AnsiText.Strip(lines[i]).Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    internal static (int Width, int Height) SafeWindowSize(int fallbackWidth)
    {
        var w = fallbackWidth;
        var h = 24;
        try
        {
            if (global::System.Console.WindowWidth > 0)
            {
                w = global::System.Console.WindowWidth;
            }

            if (global::System.Console.WindowHeight > 0)
            {
                h = global::System.Console.WindowHeight;
            }
        }
        catch (IOException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }

        return (Math.Max(24, w), Math.Max(10, h));
    }
}
