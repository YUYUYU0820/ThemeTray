namespace ThemeTray;

internal sealed class ScheduleService : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private DateTime _lastAutoApplyMinute = DateTime.MinValue;
    private ThemeMode? _lastAutoApplyMode;

    public ScheduleService()
    {
        _timer = new System.Windows.Forms.Timer
        {
            Interval = 60_000
        };
    }

    public event EventHandler<ThemeMode>? ThemeDue;

    public AppSettings? Settings { get; private set; }

    public void Start(AppSettings settings)
    {
        Settings = settings;
        _timer.Tick -= TimerOnTick;
        _timer.Tick += TimerOnTick;
        _timer.Start();
        CheckNow(force: true);
    }

    public void Restart(AppSettings settings)
    {
        Settings = settings;
        _lastAutoApplyMinute = DateTime.MinValue;
        _lastAutoApplyMode = null;
        CheckNow(force: true);
    }

    public void Stop()
    {
        _timer.Stop();
    }

    public ThemeMode GetScheduledMode(DateTime now)
    {
        var (lightTime, darkTime, _) = GetEffectiveSchedule(now);
        return GetScheduledMode(now, lightTime, darkTime);
    }

    public (TimeOnly LightTime, TimeOnly DarkTime, bool UsesSunriseSunset) GetEffectiveSchedule(DateTime now)
    {
        if (Settings is null)
        {
            return (new TimeOnly(7, 0), new TimeOnly(19, 0), false);
        }

        if (Settings.AutoSwitchModeValue == AutoSwitchMode.SunriseSunset)
        {
            var cached = Settings.GetSunriseSunsetInfo(DateOnly.FromDateTime(now));
            if (cached is not null && cached.Date == DateOnly.FromDateTime(now))
            {
                return (cached.Sunrise, cached.Sunset, true);
            }
        }

        return (Settings.LightTimeValue, Settings.DarkTimeValue, false);
    }

    public static ThemeMode GetScheduledMode(DateTime now, TimeOnly lightTime, TimeOnly darkTime)
    {
        var current = TimeOnly.FromDateTime(now);

        if (lightTime == darkTime)
        {
            return ThemeMode.Light;
        }

        if (lightTime < darkTime)
        {
            return current >= lightTime && current < darkTime ? ThemeMode.Light : ThemeMode.Dark;
        }

        return current >= lightTime || current < darkTime ? ThemeMode.Light : ThemeMode.Dark;
    }

    public void CheckNow(bool force = false)
    {
        if (Settings is null || !Settings.AutoSwitchEnabled)
        {
            return;
        }

        var now = DateTime.Now;
        var minute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        var mode = GetScheduledMode(now);

        if (!force && _lastAutoApplyMinute == minute && _lastAutoApplyMode == mode)
        {
            return;
        }

        _lastAutoApplyMinute = minute;
        _lastAutoApplyMode = mode;
        ThemeDue?.Invoke(this, mode);
    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        CheckNow();
    }

    public void Dispose()
    {
        _timer.Tick -= TimerOnTick;
        _timer.Dispose();
    }
}

