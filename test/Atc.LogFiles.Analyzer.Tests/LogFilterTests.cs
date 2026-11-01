namespace Atc.LogFiles.Analyzer.Tests;

public sealed class LogFilterTests
{
    private static readonly string[] Available = ["Api", "Scheduler", "Worker"];

    [Fact]
    public void SourceSystemsToShow_SomeTicked_ShowsThose()
    {
        // Act
        var actual = LogFilter.SourceSystemsToShow(["Worker", "Api"], Available);

        // Assert
        actual.Should().BeEquivalentTo("Api", "Worker");
    }

    [Fact]
    public void SourceSystemsToShow_NoneTicked_ShowsAll()
    {
        // Act
        var actual = LogFilter.SourceSystemsToShow([], Available);

        // Assert
        actual.Should().BeEmpty("an empty set shows every source system");
    }

    [Fact]
    public void SourceSystemsToShow_AllTicked_ShowsAllAlsoSystemsThatAppearLater()
    {
        // Act
        var actual = LogFilter.SourceSystemsToShow(Available, Available);

        // Assert
        actual.Should().BeEmpty("with every system ticked, a system read later must show too");
    }
}