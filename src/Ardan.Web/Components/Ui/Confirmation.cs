namespace Ardan.Web.Components.Ui;

/// <summary>A question to confirm before a command runs. The text says what will happen.</summary>
public sealed record Confirmation(string Title, string Text, string ConfirmLabel, Func<Task> Confirm, string Tone = "primary");

/// <summary>One option in a segmented control.</summary>
public sealed record Choice(string Value, string Label, string? Hint = null);
