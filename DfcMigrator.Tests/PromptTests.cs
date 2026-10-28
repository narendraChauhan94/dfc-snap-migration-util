using Xunit;

namespace DfcMigrator.Tests;

// Interactive prompts, driven by typed answers.
[Collection("Console")]
public sealed class PromptTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("dfcprompt-").FullName;
    readonly TextReader originalIn = Console.In;
    readonly TextWriter originalOut = Console.Out;

    public void Dispose()
    {
        Console.SetIn(originalIn);
        Console.SetOut(originalOut);
        Directory.Delete(dir, recursive: true);
    }

    T Answer<T>(string typed, Func<T> prompt)
    {
        Console.SetIn(new StringReader(typed));
        Console.SetOut(new StringWriter());
        return prompt();
    }

    [Fact]
    public void DataFolder_ReAsksUntilItExists_YesUsesTheDefault_QuotesFromDragAndDropAreRemoved()
    {
        Assert.Equal(dir, Answer($"{Path.Combine(dir, "missing")}\n\"{dir}\"\n", () => Prompt.AskExistingFolder("Data folder", null)));
        Assert.Equal(dir, Answer("yes\n", () => Prompt.AskExistingFolder("Data folder", dir)));
    }

    [Fact]
    public void Q_GoesBack_AndClosedInputQuits()
    {
        Assert.Throws<BackToMenuException>(() => Answer("Q\n", () => Prompt.AskExistingFolder("Data folder", dir)));
        Assert.Throws<QuitException>(() => Answer("", () => Prompt.AskExistingFolder("Data folder", null)));
    }

    [Fact]
    public void OutputFolder_YesUsesDefault_RelativeNamesAreRejected()
    {
        Assert.Equal(dir, Answer("yes\n", () => Prompt.AskFolder("Clients folder", dir)));
        Assert.Equal(dir, Answer($"Clients\n{dir}\n", () => Prompt.AskFolder("Clients folder", Path.Combine(dir, "nope"))));
    }

    [Fact]
    public void OptionalFolderAndUrl_BlankSkips_InvalidIsReAsked()
    {
        Assert.Equal("", Answer("\n", () => Prompt.AskOptionalFolder("Reference", "")));
        Assert.Equal(dir, Answer($"nope-folder\n{dir}\n", () => Prompt.AskOptionalFolder("Reference", "")));
        Assert.Equal("", Answer("yes\n", () => Prompt.AskUrl("Site URL", "")));
        Assert.Equal("http://localhost:4200", Answer("localhost\nhttp://localhost:4200\n", () => Prompt.AskUrl("Site URL", "")));
    }
}
