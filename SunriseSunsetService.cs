using System.Globalization;
using System.Net.Http.Json;
using Windows.Devices.Geolocation;

namespace ThemeTray;

internal sealed class SunriseSunsetService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<LocationInfo> GetCurrentLocationAsync(string name = "当前位置", CancellationToken cancellationToken = default)
    {
        var access = await Geolocator.RequestAccessAsync();
        if (access != GeolocationAccessStatus.Allowed)
        {
            throw new InvalidOperationException(access switch
            {
                GeolocationAccessStatus.Denied => "Windows 定位权限被拒绝。请在 设置 > 隐私和安全性 > 位置 中允许此设备和应用访问位置。",
                GeolocationAccessStatus.Unspecified => "Windows 定位权限状态未知，无法获取当前位置。",
                _ => "Windows 定位权限不可用。"
            });
        }

        var geolocator = new Geolocator
        {
            DesiredAccuracy = PositionAccuracy.Default,
            MovementThreshold = 1000
        };

        var position = await geolocator.GetGeopositionAsync(
            maximumAge: TimeSpan.FromHours(6),
            timeout: TimeSpan.FromSeconds(15)).AsTask(cancellationToken);

        return new LocationInfo
        {
            Name = name,
            Latitude = position.Coordinate.Point.Position.Latitude,
            Longitude = position.Coordinate.Point.Position.Longitude,
            TimeZoneId = TimeZoneInfo.Local.Id,
            CreatedAt = DateTimeOffset.Now,
            LastUsedAt = DateTimeOffset.Now
        };
    }

    public async Task<SunriseSunsetInfo> FetchTodayAsync(string recordName = "当前位置日出日落", CancellationToken cancellationToken = default)
    {
        var location = await GetCurrentLocationAsync("当前位置", cancellationToken);
        return await FetchTodayAsync(location, recordName, cancellationToken);
    }

    public async Task<SunriseSunsetInfo> FetchTodayAsync(LocationInfo location, string recordName, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"https://api.sunrise-sunset.org/json?lat={location.Latitude}&lng={location.Longitude}&date={today:yyyy-MM-dd}&formatted=0");

        var response = await HttpClient.GetFromJsonAsync<SunriseSunsetApiResponse>(url, cancellationToken);
        if (response is null)
        {
            throw new InvalidOperationException("Sunrise-Sunset.org 返回了空响应。");
        }

        if (!string.Equals(response.Status, "OK", StringComparison.OrdinalIgnoreCase) || response.Results is null)
        {
            throw new InvalidOperationException($"Sunrise-Sunset.org 请求失败：{response.Status ?? "未知错误"}");
        }

        if (!DateTimeOffset.TryParse(response.Results.Sunrise, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var sunriseUtc) ||
            !DateTimeOffset.TryParse(response.Results.Sunset, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var sunsetUtc))
        {
            throw new InvalidOperationException("Sunrise-Sunset.org 返回的日出日落时间格式无法识别。");
        }

        return new SunriseSunsetInfo
        {
            Name = string.IsNullOrWhiteSpace(recordName) ? $"{location.Name} {today:yyyy-MM-dd}" : recordName.Trim(),
            LocationId = location.Id,
            Date = today,
            Sunrise = TimeOnly.FromDateTime(sunriseUtc.ToLocalTime().DateTime),
            Sunset = TimeOnly.FromDateTime(sunsetUtc.ToLocalTime().DateTime),
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            TimeZoneId = location.TimeZoneId ?? TimeZoneInfo.Local.Id,
            FetchedAt = DateTimeOffset.Now
        };
    }

    private sealed class SunriseSunsetApiResponse
    {
        public SunriseSunsetApiResults? Results { get; set; }

        public string? Status { get; set; }
    }

    private sealed class SunriseSunsetApiResults
    {
        public string? Sunrise { get; set; }

        public string? Sunset { get; set; }
    }
}
