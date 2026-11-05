namespace Drawplate.Services.Interaction;

public interface IPrompt
{
    bool IsInteractive { get; }
    bool Confirm(string question, bool defaultYes);
    string? Ask(string label, string? current);
}
