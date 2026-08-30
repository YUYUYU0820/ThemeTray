namespace ThemeTray;

internal sealed record SchedulePreset(string Name, TimeOnly LightTime, TimeOnly DarkTime)
{
    public const string StandardName = "标准";
    public const string OfficeName = "办公";
    public const string NightName = "夜间";
    public const string CustomName = "自定义";

    public static readonly SchedulePreset[] All =
    [
        new(StandardName, new TimeOnly(7, 0), new TimeOnly(19, 0)),
        new(OfficeName, new TimeOnly(8, 0), new TimeOnly(18, 0)),
        new(NightName, new TimeOnly(9, 0), new TimeOnly(22, 0)),
    ];
}
