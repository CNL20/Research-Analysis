namespace ScholarTrend.Application.Services.Keywords;

public static class ArxivCategoryMapper
{
    private static readonly Dictionary<string, string> CategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cs.AI"] = "Artificial Intelligence",
        ["cs.LG"] = "Machine Learning",
        ["cs.CV"] = "Computer Vision",
        ["cs.CL"] = "Natural Language Processing",
        ["cs.NE"] = "Deep Learning",
        ["cs.CR"] = "Cybersecurity",
        ["cs.DC"] = "Cloud Computing",
        ["cs.IR"] = "Data Mining",
        ["stat.ML"] = "Machine Learning"
    };

    public static IReadOnlyList<string> MapCategories(IEnumerable<string> categories)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in categories)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                continue;
            }

            var mapped = CategoryMap.TryGetValue(category.Trim(), out var seed)
                ? seed
                : KeywordLinkRules.ToDisplayName(category.Replace('.', ' '));

            if (seen.Add(mapped))
            {
                result.Add(mapped);
            }
        }

        return result;
    }
}
