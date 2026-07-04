namespace ApiPlatform.Contracts;

// ── Insight models (mirrors /spec/insights.tsp) ───────────────────────────────
// A neutral analytics row — produced by the Databricks connector (stub-default).

public class Insight
{
    public string Metric { get; set; } = string.Empty;
    public string? Dimension { get; set; }
    public decimal Value { get; set; }
    public DateOnly? AsOf { get; set; }
}

public class InsightList
{
    public List<Insight> Data { get; set; } = [];
    public string? NextCursor { get; set; }
}
