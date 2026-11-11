using System.Collections.Generic;

using Drawplate.Models.Encoding;
using Drawplate.Services.Interaction;

using Xunit;

namespace Drawplate.Tests.Setup;

public sealed class MenuTests
{
    [Fact]
    [Trait("Tier", "T0")]
    public void Defaults_AcceptedWithEmptyAnswers()
    {
        var prompt = new ScriptedPrompt(["", "", ""]);
        var result = new MenuService(prompt).Run([EncoderKind.Nvenc, EncoderKind.Cpu], 50, SizeCap.None);

        Assert.Null(result.Error);
        Assert.Null(result.Encoder);
        Assert.Equal(50, result.Quality);
        Assert.Equal(SizeCap.None, result.MaxSize);
    }

    [Fact]
    [Trait("Tier", "T0")]
    public void ChoosesQsv()
    {
        var prompt = new ScriptedPrompt(["3", "75", "hd"]);
        var result = new MenuService(prompt).Run([EncoderKind.Nvenc, EncoderKind.Qsv, EncoderKind.Cpu], 50, SizeCap.None);

        Assert.Null(result.Error);
        Assert.Equal(EncoderKind.Qsv, result.Encoder);
        Assert.Equal(75, result.Quality);
        Assert.Equal(SizeCap.Hd, result.MaxSize);
    }

    [Fact]
    [Trait("Tier", "T0")]
    public void InvalidChoice_ReturnsE_MENU_01()
    {
        var result = new MenuService(new ScriptedPrompt(["99"])).Run([EncoderKind.Cpu], 50, SizeCap.None);

        Assert.Equal("Invalid choice.", result.Error!.Text);
        Assert.Equal(2, result.Error!.ExitCode);
    }

    [Fact]
    [Trait("Tier", "T0")]
    public void InvalidQuality_ReturnsE_USAGE_05()
    {
        var result = new MenuService(new ScriptedPrompt(["1", "101"])).Run([EncoderKind.Cpu], 50, SizeCap.None);

        Assert.Equal("Quality must be a number from 1 to 100 (higher = better).", result.Error!.Text);
    }

    [Fact]
    [Trait("Tier", "T0")]
    public void InvalidSize_ReturnsE_MENU_02()
    {
        var result = new MenuService(new ScriptedPrompt(["1", "50", "8k"])).Run([EncoderKind.Cpu], 50, SizeCap.None);

        Assert.Equal("Invalid max size '8k'.", result.Error!.Text);
        Assert.Equal("Choose one of: sd, hd, fullhd, 4k, none.", result.Error!.Hint);
    }

    [Fact]
    [Trait("Tier", "T0")]
    public void NotInteractive_ThrowsBeforeAnyPrompt()
    {
        var prompt = new ScriptedPrompt([], false);

        Assert.Throws<System.InvalidOperationException>(() =>
            new MenuService(prompt).Run([EncoderKind.Cpu], 50, SizeCap.None));
        Assert.Empty(prompt.Asked);
    }

    private sealed class ScriptedPrompt(IEnumerable<string?> answers, bool interactive = true) : IPrompt
    {
        private readonly Queue<string?> _answers = new(answers);
        public List<string> Asked { get; } = [];
        public bool IsInteractive { get; } = interactive;

        public bool Confirm(string question, bool defaultYes) => defaultYes;

        public string? Ask(string label, string? current)
        {
            Asked.Add(label);
            var answer = _answers.Count == 0 ? null : _answers.Dequeue();
            return string.IsNullOrEmpty(answer) ? current : answer;
        }
    }
}
