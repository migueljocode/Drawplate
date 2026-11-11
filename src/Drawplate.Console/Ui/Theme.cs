using System;
using System.Collections.Generic;

namespace Drawplate.Console.Ui;

internal sealed record Theme(bool Colors, bool Utf8Glyphs, int Width, bool IsInteractive, bool IsTerminal, bool CanRedraw)
{
    public static Theme Detect(
        bool isTerminal,
        IReadOnlyDictionary<string, string?> environment,
        int outputCodePage,
        int? consoleWidth,
        bool inputIsTerminal = true)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var noColor = environment.TryGetValue("NO_COLOR", out var noColorValue) && !string.IsNullOrEmpty(noColorValue);
        var termIsDumb = environment.TryGetValue("TERM", out var term)
            && string.Equals(term, "dumb", StringComparison.OrdinalIgnoreCase);
        var colors = isTerminal && !noColor && !termIsDumb;
        var utf8 = isTerminal && outputCodePage == 65001 && !termIsDumb;
        return new Theme(colors, utf8, consoleWidth ?? 80, isTerminal && inputIsTerminal, isTerminal, isTerminal && !termIsDumb);
    }
}
