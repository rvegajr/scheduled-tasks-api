using ScheduledTasksApi.Extensions;

namespace ScheduledTasksApi.Tests.Extensions;

public class WildcardFilterExtensionsTests
{
    [Theory]
    [InlineData("*", "anything", true)]
    [InlineData("*Google*", "GoogleUpdate", true)]
    [InlineData("*Google*", "MyGoogleTask", true)]
    [InlineData("*Google*", "NotHere", false)]
    [InlineData("Exact", "Exact", true)]
    [InlineData("Exact", "NotExact", false)]
    [InlineData("Test?", "Test1", true)]
    [InlineData("Test?", "Test12", false)]
    public void ToWildcardRegex_MatchesCorrectly(string pattern, string input, bool shouldMatch)
    {
        var regex = pattern.ToWildcardRegex();
        Assert.Equal(shouldMatch, regex.IsMatch(input));
    }

    [Fact]
    public void FilterByWildcard_FiltersItems()
    {
        var items = new[] { "GoogleTask", "AppleTask", "GoogleSync", "MicrosoftTask" };

        var result = items.FilterByWildcard("*Google*,*Microsoft*", i => [i]).ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains("GoogleTask", result);
        Assert.Contains("GoogleSync", result);
        Assert.Contains("MicrosoftTask", result);
    }

    [Fact]
    public void FilterByWildcard_EmptyFilter_ReturnsEmpty()
    {
        var items = new[] { "A", "B" };
        var result = items.FilterByWildcard("", i => [i]).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void FilterByWildcard_MultipleNames_MatchesAny()
    {
        var items = new[] { ("svc1", "Display One"), ("svc2", "Display Two") };

        var result = items.FilterByWildcard("*Two*", i => [i.Item1, i.Item2]).ToList();

        Assert.Single(result);
        Assert.Equal("svc2", result[0].Item1);
    }
}
