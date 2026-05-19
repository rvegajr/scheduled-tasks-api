using System.Text.RegularExpressions;

namespace ScheduledTasksApi.Extensions;

public static class WildcardFilterExtensions
{
    public static Regex ToWildcardRegex(this string pattern)
    {
        var escaped = Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".");
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public static IEnumerable<T> FilterByWildcard<T>(
        this IEnumerable<T> items,
        string filterCsv,
        Func<T, string[]> nameSelector)
    {
        if (string.IsNullOrWhiteSpace(filterCsv))
            return [];

        var patterns = filterCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToWildcardRegex())
            .ToList();

        return items.Where(item =>
            nameSelector(item).Any(name =>
                patterns.Any(p => p.IsMatch(name))));
    }
}
