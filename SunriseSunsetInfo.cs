namespace ThemeTray;

internal sealed record SunriseSunsetInfo
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; init; } = "日出日落时间";

    public string? LocationId { get; init; }

    public DateOnly Date { get; init; }

    public TimeOnly Sunrise { get; init; }

    public TimeOnly Sunset { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public string? TimeZoneId { get; init; }

    public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.Now;
}
