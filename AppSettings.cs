using System.Globalization;
using System.Text.Json.Serialization;

namespace ThemeTray;

internal sealed class AppSettings
{
    private const int MaxSunriseSunsetCacheEntries = 3;
    private const int MaxSavedLocationEntries = 10;

    public bool AutoSwitchEnabled { get; set; }

    public string AutoSwitchMode { get; set; } = ThemeTray.AutoSwitchMode.FixedTime.ToString();

    public string LightTime { get; set; } = "07:00";

    public string DarkTime { get; set; } = "19:00";

    public string PresetName { get; set; } = SchedulePreset.StandardName;

    public List<SunriseSunsetInfo> SunriseSunsetCache { get; set; } = [];

    public List<LocationInfo> SavedLocations { get; set; } = [];

    public string? LastSunriseSunsetFetch { get; set; }

    public string? LastSunriseSunsetError { get; set; }

    public bool StartWithWindows { get; set; }

    [JsonIgnore]
    public TimeOnly LightTimeValue => ParseTimeOrDefault(LightTime, new TimeOnly(7, 0));

    [JsonIgnore]
    public TimeOnly DarkTimeValue => ParseTimeOrDefault(DarkTime, new TimeOnly(19, 0));

    [JsonIgnore]
    public ThemeTray.AutoSwitchMode AutoSwitchModeValue => Enum.TryParse<ThemeTray.AutoSwitchMode>(AutoSwitchMode, ignoreCase: true, out var mode)
        ? mode
        : ThemeTray.AutoSwitchMode.FixedTime;

    [JsonIgnore]
    public SunriseSunsetInfo? TodaySunriseSunsetInfo => GetSunriseSunsetInfo(DateOnly.FromDateTime(DateTime.Now));

    public SunriseSunsetInfo? GetSunriseSunsetInfo(DateOnly date)
    {
        return SunriseSunsetCache
            .Where(info => info.Date == date)
            .OrderByDescending(info => info.FetchedAt)
            .FirstOrDefault();
    }

    public void SetSunriseSunsetInfo(SunriseSunsetInfo info)
    {
        SunriseSunsetCache.RemoveAll(existing => existing.Date == info.Date && AreSameLocation(existing, info));
        SunriseSunsetCache.Add(info with
        {
            FetchedAt = DateTimeOffset.Now
        });
        TrimSunriseSunsetCache();

        LastSunriseSunsetFetch = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
        LastSunriseSunsetError = null;
    }

    public void SetSunriseSunsetError(string error)
    {
        LastSunriseSunsetError = error;
        LastSunriseSunsetFetch = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
    }

    public void AddOrUpdateLocation(LocationInfo location)
    {
        var normalized = NormalizeLocation(location);
        var existingIndex = SavedLocations.FindIndex(existing =>
            string.Equals(existing.Id, normalized.Id, StringComparison.OrdinalIgnoreCase) ||
            AreSameLocation(existing, normalized));

        if (existingIndex >= 0)
        {
            var old = SavedLocations[existingIndex];
            SavedLocations[existingIndex] = normalized with
            {
                Id = old.Id,
                CreatedAt = old.CreatedAt,
                LastUsedAt = DateTimeOffset.Now
            };
        }
        else
        {
            SavedLocations.Add(normalized with
            {
                Id = string.IsNullOrWhiteSpace(normalized.Id) ? Guid.NewGuid().ToString("N") : normalized.Id,
                CreatedAt = DateTimeOffset.Now,
                LastUsedAt = DateTimeOffset.Now
            });
        }

        TrimSavedLocations();
    }

    public void MarkLocationUsed(string locationId)
    {
        var index = SavedLocations.FindIndex(location => string.Equals(location.Id, locationId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            SavedLocations[index] = SavedLocations[index] with
            {
                LastUsedAt = DateTimeOffset.Now
            };
        }
    }

    public void RemoveLocation(string locationId)
    {
        SavedLocations.RemoveAll(location => string.Equals(location.Id, locationId, StringComparison.OrdinalIgnoreCase));
    }

    public LocationInfo? FindNearestSavedLocation(double latitude, double longitude, double maxDistanceKilometers)
    {
        return SavedLocations
            .Select(location => new
            {
                Location = location,
                Distance = CalculateDistanceKilometers(latitude, longitude, location.Latitude, location.Longitude)
            })
            .Where(candidate => candidate.Distance <= maxDistanceKilometers)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
    }

    public void Normalize()
    {
        LightTime = FormatTime(LightTimeValue);
        DarkTime = FormatTime(DarkTimeValue);

        if (!Enum.TryParse<ThemeTray.AutoSwitchMode>(AutoSwitchMode, ignoreCase: true, out _))
        {
            AutoSwitchMode = ThemeTray.AutoSwitchMode.FixedTime.ToString();
        }

        if (string.IsNullOrWhiteSpace(PresetName))
        {
            PresetName = SchedulePreset.CustomName;
        }

        SunriseSunsetCache.RemoveAll(info => info.Date == default || info.Sunrise == default || info.Sunset == default);
        SunriseSunsetCache = SunriseSunsetCache.Select(NormalizeSunriseSunsetInfo).ToList();
        TrimSunriseSunsetCache();

        SavedLocations.RemoveAll(location => !IsValidLatitude(location.Latitude) || !IsValidLongitude(location.Longitude));
        SavedLocations = SavedLocations.Select(NormalizeLocation).ToList();
        TrimSavedLocations();
    }

    public static string FormatTime(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static bool IsValidLatitude(double latitude) => latitude is >= -90 and <= 90;

    public static bool IsValidLongitude(double longitude) => longitude is >= -180 and <= 180;

    private void TrimSunriseSunsetCache()
    {
        SunriseSunsetCache = SunriseSunsetCache
            .OrderByDescending(info => info.Date)
            .ThenByDescending(info => info.FetchedAt)
            .Take(MaxSunriseSunsetCacheEntries)
            .ToList();
    }

    private void TrimSavedLocations()
    {
        SavedLocations = SavedLocations
            .OrderByDescending(location => location.LastUsedAt)
            .ThenByDescending(location => location.CreatedAt)
            .Take(MaxSavedLocationEntries)
            .ToList();
    }

    private static SunriseSunsetInfo NormalizeSunriseSunsetInfo(SunriseSunsetInfo info)
    {
        var fallbackName = $"{info.Date:yyyy-MM-dd} 日出日落";
        return info with
        {
            Id = string.IsNullOrWhiteSpace(info.Id) ? Guid.NewGuid().ToString("N") : info.Id,
            Name = string.IsNullOrWhiteSpace(info.Name) ? fallbackName : info.Name.Trim(),
            TimeZoneId = string.IsNullOrWhiteSpace(info.TimeZoneId) ? TimeZoneInfo.Local.Id : info.TimeZoneId
        };
    }

    private static LocationInfo NormalizeLocation(LocationInfo location)
    {
        return location with
        {
            Id = string.IsNullOrWhiteSpace(location.Id) ? Guid.NewGuid().ToString("N") : location.Id,
            Name = string.IsNullOrWhiteSpace(location.Name) ? "常驻地点" : location.Name.Trim(),
            TimeZoneId = string.IsNullOrWhiteSpace(location.TimeZoneId) ? TimeZoneInfo.Local.Id : location.TimeZoneId
        };
    }

    private static bool AreSameLocation(SunriseSunsetInfo left, SunriseSunsetInfo right)
    {
        return Math.Abs(left.Latitude - right.Latitude) < 0.0001 && Math.Abs(left.Longitude - right.Longitude) < 0.0001;
    }

    private static bool AreSameLocation(LocationInfo left, LocationInfo right)
    {
        return Math.Abs(left.Latitude - right.Latitude) < 0.0001 && Math.Abs(left.Longitude - right.Longitude) < 0.0001;
    }

    private static double CalculateDistanceKilometers(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadiusKilometers = 6371.0;
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var lat1 = DegreesToRadians(latitude1);
        var lat2 = DegreesToRadians(latitude2);

        var a = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKilometers * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static TimeOnly ParseTimeOrDefault(string? value, TimeOnly defaultValue)
    {
        return TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : defaultValue;
    }
}

