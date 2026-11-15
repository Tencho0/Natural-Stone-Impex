namespace NaturalStoneImpex.Client.Components;

/// <summary>What an <see cref="InlineConfirm"/> asks: warning lines first, then the question.</summary>
public sealed record ConfirmPrompt(string Question, IReadOnlyList<string> Warnings)
{
    public ConfirmPrompt(string question) : this(question, Array.Empty<string>()) { }
}
