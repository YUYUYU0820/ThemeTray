namespace ThemeTray;

internal sealed record LocationInfo
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; init; } = "常驻地点";

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public string? TimeZoneId { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset LastUsedAt { get; init; } = DateTimeOffset.Now;
}
