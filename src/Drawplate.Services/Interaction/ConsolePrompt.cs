using System;
using System.IO;

namespace Drawplate.Services.Interaction;

public sealed class ConsolePrompt : IPrompt
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly bool _inputIsTerminal;
    private readonly bool _outputIsTerminal;

    public ConsolePrompt(TextReader input, TextWriter output, bool inputIsTerminal, bool outputIsTerminal)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        _input = input;
        _output = output;
        _inputIsTerminal = inputIsTerminal;
        _outputIsTerminal = outputIsTerminal;
    }

    public bool IsInteractive => _inputIsTerminal && _outputIsTerminal;

    public bool Confirm(string question, bool defaultYes)
    {
        ArgumentNullException.ThrowIfNull(question);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var prompt = defaultYes ? $"{question} [Y/n] " : $"{question} [y/N] ";
            _output.Write(prompt);
            var answer = _input.ReadLine();
            if (answer is null)
            {
                return defaultYes;
            }
            var trimmed = answer.Trim().ToLowerInvariant();
            if (trimmed.Length == 0)
            {
                return defaultYes;
            }
            if (trimmed is "y" or "yes")
            {
                return true;
            }
            if (trimmed is "n" or "no")
            {
                return false;
            }
            _output.WriteLine("Invalid choice.");
        }
        return false;
    }

    public string? Ask(string label, string? current)
    {
        ArgumentNullException.ThrowIfNull(label);
        var display = current is null ? $"{label}: " : $"{label} [{current}]: ";
        _output.Write(display);
        var answer = _input.ReadLine();
        if (answer is null || answer.Length == 0)
        {
            return current;
        }
        return answer;
    }
}
