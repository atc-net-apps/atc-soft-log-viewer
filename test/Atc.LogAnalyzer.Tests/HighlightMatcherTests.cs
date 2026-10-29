namespace Atc.LogAnalyzer.Tests;

public sealed class HighlightMatcherTests
{
    [Fact]
    public void FindMatch_WithoutRules_ReturnsNoMatch()
    {
        // Arrange
        var sut = new HighlightMatcher([]);

        // Act
        var actual = sut.FindMatch("Connection lost");

        // Assert
        actual.Should().Be(HighlightMatcher.NoMatch);
    }

    [Fact]
    public void FindMatch_ReturnsTheIndexOfTheMatchingRule()
    {
        // Arrange
        var sut = new HighlightMatcher(
        [
            new HighlightRule("timeout"),
            new HighlightRule("lost"),
        ]);

        // Act
        var actual = sut.FindMatch("Connection lost");

        // Assert
        actual.Should().Be(1);
    }

    [Fact]
    public void FindMatch_WhenSeveralRulesMatch_ReturnsTheFirst()
    {
        // Arrange
        var sut = new HighlightMatcher(
        [
            new HighlightRule("Connection"),
            new HighlightRule("lost"),
        ]);

        // Act
        var actual = sut.FindMatch("Connection lost");

        // Assert
        actual.Should().Be(0);
    }

    [Fact]
    public void FindMatch_IsCaseSensitiveByDefault()
    {
        // Arrange
        var sut = new HighlightMatcher([new HighlightRule("LOST")]);

        // Act
        var actual = sut.FindMatch("Connection lost");

        // Assert
        actual.Should().Be(HighlightMatcher.NoMatch);
    }

    [Fact]
    public void FindMatch_WithIgnoreCase_MatchesOtherCasing()
    {
        // Arrange
        var sut = new HighlightMatcher([new HighlightRule("LOST", IgnoreCase: true)]);

        // Act
        var actual = sut.FindMatch("Connection lost");

        // Assert
        actual.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void FindMatch_RuleWithoutText_NeverMatches(string? text)
    {
        // Arrange
        var sut = new HighlightMatcher(
        [
            new HighlightRule(text!),
            new HighlightRule("lost"),
        ]);

        // Act
        var actual = sut.FindMatch("Connection lost");

        // Assert
        actual.Should().Be(1);
    }

    [Fact]
    public void FindMatch_Entry_MatchesOnTheFullMessage()
    {
        // Arrange
        var sut = new HighlightMatcher([new HighlightRule("NullReferenceException")]);
        var entry = new AtcLogEntry(
            SourceIdentifier: "app.log",
            SourceSystem: "App",
            LineNumber: 1,
            TimeStamp: new DateTime(2024, 2, 16, 9, 15, 0, DateTimeKind.Unspecified),
            LogLevel: LogLevel.Error,
            MessageShort: "Request failed",
            MessageFull: "Request failed" + Environment.NewLine + "System.NullReferenceException: Object reference not set");

        // Act
        var actual = sut.FindMatch(entry);

        // Assert
        actual.Should().Be(0);
    }
}