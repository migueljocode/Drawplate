using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Drawplate.Console.Ui;

/// <summary>
/// Windows-safe interactive help pager with animated Aurora header, resize awareness,
/// live search (/), and explicit cursor positioning (never triggers bottom-row scroll).
/// </summary>
internal static class HelpPager
{
    public static void Show(TextWriter stdout, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(theme);

        using var consoleGuard = ConsoleGuard.EnterInteractive();
        var effectiveUtf8 = theme.Utf8Glyphs || !global::System.Console.IsOutputRedirected && global::System.Console.OutputEncoding.CodePage == 65001;
        if (OperatingSystem.IsWindows() && !consoleGuard.VtEnabled)
        {
            stdout.Write(Cli.HelpText.Render());
            return;
        }

        var (width, height) = SafeWindowSize(theme.Width);
        var headerLines = BrandLogo.RenderLines(width, effectiveUtf8, frame: 0, reveal: 1.0);
        var bodyLines = HelpView.BuildBodyLines(width, effectiveUtf8);

        var canInteractivePage = theme.IsInteractive
            && !global::System.Console.IsInputRedirected
            && !global::System.Console.IsOutputRedirected;

        // If everything fits on one screen (or input is not interactive), play a crisp animated reveal and exit cleanly.
        if (!canInteractivePage || headerLines.Count + 1 + bodyLines.Count <= Math.Max(12, height - 1))
        {
            RenderInlineAnimated(stdout, width, effectiveUtf8, bodyLines, canInteractivePage);
            return;
        }

        RunAlternatePager(stdout, effectiveUtf8, canInteractivePage);
    }

    private static void RenderInlineAnimated(TextWriter stdout, int width, bool utf8, IReadOnlyList<string> bodyLines, bool allowSkip)
    {
        var finalHeader = BrandLogo.RenderLines(width, utf8, frame: 8, reveal: 1.0);
        var headerHeight = finalHeader.Count;

        try
        {
            stdout.Write("\u001b[?25l");
            for (var step = 1; step <= 8; step++)
            {
                if (allowSkip && KeyAvailableSafe())
                {
                    _ = ReadKeySafe();
                    break;
                }

                var frameLines = BrandLogo.RenderLines(width, utf8, frame: step, reveal: step / 8.0);
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
                Thread.Sleep(24);
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

    private static void RunAlternatePager(TextWriter stdout, bool utf8, bool canInteractive)
    {
        using var screen = ScreenGuard.TryEnter(stdout, new Theme(true, utf8, 80, canInteractive, true, true));
        var (width, height) = SafeWindowSize(100);
        var offset = 0;
        var frame = 0;
        string? searchQuery = null;
        var lastWidth = width;
        var bodyLines = HelpView.BuildBodyLines(width, utf8, searchQuery);

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

                var headerLines = BrandLogo.RenderLines(width, utf8, frame, reveal: Math.Min(1.0, (frame + 2) / 4.0));
                var headerRows = headerLines.Count + 1;
                var bodyRows = Math.Max(4, height - headerRows - 1);
                var maxOffset = Math.Max(0, bodyLines.Count - bodyRows);
                offset = Math.Clamp(offset, 0, maxOffset);

                DrawFrame(stdout, headerLines, bodyLines, offset, bodyRows, width, height, utf8, searchQuery);

                // Animate header shimmer gently at ~10 Hz while polling for key input.
                var ticked = false;
                for (var wait = 0; wait < 6; wait++)
                {
                    if (KeyAvailableSafe())
                    {
                        var key = ReadKeySafe();
                        if (key is null)
                        {
                            break;
                        }

                        var k = key.Value;
                        if (k.Key is ConsoleKey.Q or ConsoleKey.Escape)
                        {
                            return;
                        }

                        if (k.Key == ConsoleKey.DownArrow || k.KeyChar == 'j')
                        {
                            offset = Math.Min(maxOffset, offset + 1);
                        }
                        else if (k.Key == ConsoleKey.UpArrow || k.KeyChar == 'k')
                        {
                            offset = Math.Max(0, offset - 1);
                        }
                        else if (k.Key is ConsoleKey.PageDown or ConsoleKey.Spacebar || k.KeyChar == 'f')
                        {
                            offset = Math.Min(maxOffset, offset + bodyRows);
                        }
                        else if (k.Key == ConsoleKey.PageUp || k.KeyChar == 'b')
                        {
                            offset = Math.Max(0, offset - bodyRows);
                        }
                        else if (k.Key == ConsoleKey.Home || k.KeyChar == 'g')
                        {
                            offset = 0;
                        }
                        else if (k.Key == ConsoleKey.End || k.KeyChar == 'G')
                        {
                            offset = maxOffset;
                        }
                        else if (k.KeyChar == '/')
                        {
                            searchQuery = PromptSearch(stdout, height, width, utf8);
                            bodyLines = HelpView.BuildBodyLines(width, utf8, searchQuery);
                            if (!string.IsNullOrEmpty(searchQuery))
                            {
                                var hit = FindNextMatch(bodyLines, 0, searchQuery, forward: true);
                                if (hit >= 0)
                                {
                                    offset = Math.Clamp(hit, 0, Math.Max(0, bodyLines.Count - bodyRows));
                                }
                            }
                        }
                        else if (k.KeyChar == 'n' && !string.IsNullOrEmpty(searchQuery))
                        {
                            var hit = FindNextMatch(bodyLines, offset + 1, searchQuery, forward: true);
                            if (hit >= 0)
                            {
                                offset = Math.Clamp(hit, 0, Math.Max(0, bodyLines.Count - bodyRows));
                            }
                        }
                        else if (k.KeyChar == 'N' && !string.IsNullOrEmpty(searchQuery))
                        {
                            var hit = FindNextMatch(bodyLines, offset - 1, searchQuery, forward: false);
                            if (hit >= 0)
                            {
                                offset = Math.Clamp(hit, 0, Math.Max(0, bodyLines.Count - bodyRows));
                            }
                        }

                        ticked = true;
                        break;
                    }

                    Thread.Sleep(20);
                }

                frame++;
                if (!ticked && frame % 1 != 0)
                {
                    continue;
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
        // Always stay strictly within (width - 1) columns and position rows explicitly to prevent Windows wrap/scroll bugs.
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

        var end = Math.Min(bodyLines.Count, offset + bodyRows);
        for (var i = offset; i < end && row < height; i++, row++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\u001b[{row};1H")
              .Append(AnsiText.ClipVisible(bodyLines[i], maxCols))
              .Append("\u001b[0m\u001b[K");
        }

        while (row < height)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\u001b[{row};1H\u001b[K");
            row++;
        }

        // Status bar on bottom row, strictly <= maxCols characters.
        var shownEnd = Math.Min(bodyLines.Count, offset + bodyRows);
        var pct = bodyLines.Count <= bodyRows ? 100 : (int)Math.Round(100.0 * shownEnd / bodyLines.Count, MidpointRounding.AwayFromZero);
        var pctLabel = shownEnd >= bodyLines.Count ? "END" : $"{pct.ToString(CultureInfo.InvariantCulture)}%";
        var dot = utf8 ? "·" : "-";
        var diamond = utf8 ? "◆" : "*";
        var searchNote = string.IsNullOrEmpty(searchQuery) ? string.Empty : $"  {dot}  /{searchQuery} (n/N)";
        var statusRaw = $"  {diamond} Drawplate Help  {offset + 1}-{shownEnd}/{bodyLines.Count}  {pctLabel}  {dot}  j/k  space/b  g/G  /search  q{searchNote}";
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

                var key = ReadKeySafe();
                if (key is null)
                {
                    return null;
                }

                var k = key.Value;
                if (k.Key == ConsoleKey.Escape)
                {
                    return null;
                }

                if (k.Key == ConsoleKey.Enter)
                {
                    var text = query.ToString().Trim();
                    return text.Length == 0 ? null : text;
                }

                if (k.Key == ConsoleKey.Backspace)
                {
                    if (query.Length > 0)
                    {
                        query.Length--;
                    }
                }
                else if (!char.IsControl(k.KeyChar))
                {
                    query.Append(k.KeyChar);
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

    private static (int Width, int Height) SafeWindowSize(int fallbackWidth)
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

    private static bool KeyAvailableSafe()
    {
        try
        {
            return global::System.Console.KeyAvailable;
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static ConsoleKeyInfo? ReadKeySafe()
    {
        try
        {
            return global::System.Console.ReadKey(intercept: true);
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
