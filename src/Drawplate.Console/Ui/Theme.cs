using System;

namespace Drawplate.Console.Ui;

internal sealed record Theme(bool Colors, bool Utf8Glyphs, int Width, bool IsInteractive)
{
    public static Theme Detect(bool outputIsTerminal, bool inputIsTerminal, bool noColor, int outputCodePage, string? term, int? consoleWidth)
    {
        var colors = outputIsTerminal && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
        var utf8 = outputIsTerminal && outputCodePage == 65001 && !string.Equals(term, "dumb", StringComparison.OrdinalIgnoreCase);
        var width = consoleWidth ?? 80;
        var interactive = inputIsTerminal && outputIsTerminal;
        return new Theme(colors, utf8, width, interactive);
    }
}
