using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ThemeTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const double SavedLocationMatchDistanceKilometers = 5.0;
    private const int MaxNotificationErrorLength = 180;
    private readonly SettingsService _settingsService = new();
    private readonly ThemeService _themeService = new();
    private readonly StartupService _startupService = new();
    private readonly ScheduleService _scheduleService = new();
    private readonly SunriseSunsetService _sunriseSunsetService = new();
    private readonly NotificationService _notificationService = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu = new();
    private readonly Form _menuOwnerWindow;
    private Icon _currentIcon;
    private ThemeMode _currentMode;
    private bool _isFetchingSunriseSunset;
    private bool _isFetchingLocation;
    private bool _isInitialScheduleCheck;
    private bool _startupNotificationShown;

    public TrayApplicationContext()
    {
        _settingsService.Load();
        _settingsService.Settings.StartWithWindows = _startupService.IsEnabled();
        _settingsService.Save();

        _menuOwnerWindow = CreateMenuOwnerWindow();
        _currentMode = _themeService.GetCurrentMode();
        _currentIcon = CreateTrayIcon(_currentMode);

        _notifyIcon = new NotifyIcon
        {
            Icon = _currentIcon,
            Text = "ThemeTray - Windows 深浅色切换",
            Visible = true
        };

        _notifyIcon.MouseUp += NotifyIconOnMouseUp;
        _scheduleService.ThemeDue += ScheduleServiceOnThemeDue;
        _notificationService.DeliveryFailed += NotificationServiceOnDeliveryFailed;
        _notificationService.Initialize();
        BuildMenu();

        _isInitialScheduleCheck = true;
        try
        {
            _scheduleService.Start(_settingsService.Settings);
        }
        finally
        {
            _isInitialScheduleCheck = false;
        }

        Application.Idle += ApplicationOnIdle;
        _ = RefreshSunriseSunsetOnStartupAsync();
    }

    private void ApplicationOnIdle(object? sender, EventArgs e)
    {
        if (_startupNotificationShown)
        {
            return;
        }

        _startupNotificationShown = true;
        Application.Idle -= ApplicationOnIdle;

        var settings = _settingsService.Settings;
        ShowNotification(
            "ThemeTray 已启动",
            ToolTipIcon.Info,
            $"当前模式：{GetModeText(_currentMode)}",
            GetStartupAutoSwitchText(settings));
    }

    private void NotifyIconOnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button is MouseButtons.Left or MouseButtons.Right)
        {
            ToggleMenu();
        }
    }

    private void ToggleMenu()
    {
        if (_menu.Visible)
        {
            _menu.Close(ToolStripDropDownCloseReason.AppClicked);
            return;
        }

        ShowMenu();
    }

    private void ShowMenu()
    {
        BuildMenu();

        if (!_menuOwnerWindow.IsHandleCreated)
        {
            _menuOwnerWindow.CreateControl();
        }

        SetForegroundWindow(_menuOwnerWindow.Handle);
        _menu.Show(_menuOwnerWindow, _menuOwnerWindow.PointToClient(Cursor.Position));
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();

        var currentMode = _themeService.GetCurrentMode();
        var settings = _settingsService.Settings;

        _menu.Items.Add(new ToolStripMenuItem($"当前模式：{GetModeText(currentMode)}") { Enabled = false });
        foreach (var statusItem in BuildScheduleStatusItems(settings))
        {
            _menu.Items.Add(statusItem);
        }
        _menu.Items.Add(new ToolStripSeparator());

        _menu.Items.Add(new ToolStripMenuItem("切换为浅色", null, (_, _) => ApplyTheme(ThemeMode.Light, ThemeChangeSource.Manual)) { Checked = currentMode == ThemeMode.Light });
        _menu.Items.Add(new ToolStripMenuItem("切换为深色", null, (_, _) => ApplyTheme(ThemeMode.Dark, ThemeChangeSource.Manual)) { Checked = currentMode == ThemeMode.Dark });
        _menu.Items.Add(new ToolStripSeparator());

        _menu.Items.Add(new ToolStripMenuItem("自动切换", null, (_, _) => ToggleAutoSwitch()) { Checked = settings.AutoSwitchEnabled });

        var modeMenu = new ToolStripMenuItem("自动切换方式");
        modeMenu.DropDownItems.Add(new ToolStripMenuItem("固定时间", null, (_, _) => SetAutoSwitchMode(AutoSwitchMode.FixedTime)) { Checked = settings.AutoSwitchModeValue == AutoSwitchMode.FixedTime });
        modeMenu.DropDownItems.Add(new ToolStripMenuItem("日出日落", null, (_, _) => SetAutoSwitchMode(AutoSwitchMode.SunriseSunset)) { Checked = settings.AutoSwitchModeValue == AutoSwitchMode.SunriseSunset });
        _menu.Items.Add(modeMenu);
        _menu.Items.Add(new ToolStripSeparator());

        if (settings.AutoSwitchModeValue == AutoSwitchMode.SunriseSunset)
        {
            _menu.Items.Add(BuildSunriseSunsetMenu(settings));
            _menu.Items.Add(BuildSavedLocationsMenu(settings));
        }
        else
        {
            _menu.Items.Add(BuildFixedTimePresetMenu(settings));
            _menu.Items.Add(new ToolStripMenuItem($"自定义固定时间...（浅 {settings.LightTime} / 深 {settings.DarkTime}）", null, (_, _) => EditCustomTimes()));
        }

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("开机自启", null, (_, _) => ToggleStartup()) { Checked = settings.StartWithWindows });
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitApplication()));
    }

    private IEnumerable<ToolStripItem> BuildScheduleStatusItems(AppSettings settings)
    {
        if (settings.AutoSwitchModeValue == AutoSwitchMode.SunriseSunset)
        {
            var cached = settings.TodaySunriseSunsetInfo;
            if (cached is null)
            {
                yield return new ToolStripMenuItem($"当前自动方式：日出日落（今日未获取，临时使用固定时间）") { Enabled = false };
                yield return new ToolStripMenuItem($"临时时间：浅 {settings.LightTime} / 深 {settings.DarkTime}") { Enabled = false };
                yield break;
            }

            yield return new ToolStripMenuItem($"当前自动方式：日出日落") { Enabled = false };
            yield return new ToolStripMenuItem($"当前使用地址：{GetLocationDisplayName(settings, cached)}") { Enabled = false };
            yield return new ToolStripMenuItem($"当前日出日落：日出 {AppSettings.FormatTime(cached.Sunrise)} / 日落 {AppSettings.FormatTime(cached.Sunset)}") { Enabled = false };
            yield break;
        }

        yield return new ToolStripMenuItem($"当前自动方式：固定时间") { Enabled = false };
        yield return new ToolStripMenuItem($"当前固定时间：浅 {settings.LightTime} / 深 {settings.DarkTime}") { Enabled = false };
    }

    private ToolStripMenuItem BuildFixedTimePresetMenu(AppSettings settings)
    {
        var presetMenu = new ToolStripMenuItem("预设固定时间方案");
        foreach (var preset in SchedulePreset.All)
        {
            presetMenu.DropDownItems.Add(new ToolStripMenuItem(
                $"{preset.Name}（浅 {AppSettings.FormatTime(preset.LightTime)} / 深 {AppSettings.FormatTime(preset.DarkTime)}）",
                null,
                (_, _) => ApplyPreset(preset))
            {
                Checked = string.Equals(settings.PresetName, preset.Name, StringComparison.Ordinal)
            });
        }

        return presetMenu;
    }
    private static string GetLocationDisplayName(AppSettings settings, SunriseSunsetInfo info)
    {
        var location = settings.SavedLocations.FirstOrDefault(location =>
            string.Equals(location.Id, info.LocationId, StringComparison.OrdinalIgnoreCase));
        if (location is not null)
        {
            return $"{location.Name}（{location.Latitude:F4}, {location.Longitude:F4}）";
        }

        return $"{info.Name}（{info.Latitude:F4}, {info.Longitude:F4}）";
    }
    private ToolStripMenuItem BuildSunriseSunsetMenu(AppSettings settings)
    {
        var sunMenu = new ToolStripMenuItem("日出日落时间缓存");
        var cached = settings.TodaySunriseSunsetInfo;
        if (cached is null)
        {
            sunMenu.DropDownItems.Add(new ToolStripMenuItem("今日尚未获取，日出日落模式会临时使用固定时间") { Enabled = false });
        }
        else
        {
            sunMenu.DropDownItems.Add(new ToolStripMenuItem($"今日使用：{cached.Name}") { Enabled = false });
            sunMenu.DropDownItems.Add(new ToolStripMenuItem($"日出：{AppSettings.FormatTime(cached.Sunrise)} / 日落：{AppSettings.FormatTime(cached.Sunset)}") { Enabled = false });
            sunMenu.DropDownItems.Add(new ToolStripMenuItem($"位置：{cached.Latitude:F4}, {cached.Longitude:F4}") { Enabled = false });
        }

        if (!string.IsNullOrWhiteSpace(settings.LastSunriseSunsetError))
        {
            sunMenu.DropDownItems.Add(new ToolStripMenuItem($"上次错误：{settings.LastSunriseSunsetError}") { Enabled = false });
        }

        sunMenu.DropDownItems.Add(new ToolStripSeparator());
        sunMenu.DropDownItems.Add(new ToolStripMenuItem(
            _isFetchingSunriseSunset ? "正在获取..." : "手动获取当前位置今日日出/日落时间...",
            null,
            async (_, _) => await FetchSunriseSunsetFromCurrentLocationAsync())
        {
            Enabled = !_isFetchingSunriseSunset && !_isFetchingLocation
        });

        var cacheListMenu = new ToolStripMenuItem($"缓存记录（最多 3 组，当前 {settings.SunriseSunsetCache.Count} 组）");
        if (settings.SunriseSunsetCache.Count == 0)
        {
            cacheListMenu.DropDownItems.Add(new ToolStripMenuItem("暂无缓存记录") { Enabled = false });
        }
        else
        {
            foreach (var info in settings.SunriseSunsetCache.OrderByDescending(info => info.Date).ThenByDescending(info => info.FetchedAt))
            {
                var item = new ToolStripMenuItem($"{info.Name}｜{info.Date:yyyy-MM-dd}｜{AppSettings.FormatTime(info.Sunrise)}/{AppSettings.FormatTime(info.Sunset)}");
                item.DropDownItems.Add(new ToolStripMenuItem($"位置：{info.Latitude:F4}, {info.Longitude:F4}") { Enabled = false });
                item.DropDownItems.Add(new ToolStripMenuItem("重命名...", null, (_, _) => RenameSunriseSunsetRecord(info.Id)));
                cacheListMenu.DropDownItems.Add(item);
            }
        }
        sunMenu.DropDownItems.Add(cacheListMenu);
        sunMenu.DropDownItems.Add(new ToolStripMenuItem("数据来源：sunrise-sunset.org", null, (_, _) => OpenUrl("https://sunrise-sunset.org/")));
        return sunMenu;
    }

    private ToolStripMenuItem BuildSavedLocationsMenu(AppSettings settings)
    {
        var locationsMenu = new ToolStripMenuItem($"常驻地点（最多 10 组，当前 {settings.SavedLocations.Count} 组）");
        locationsMenu.DropDownItems.Add(new ToolStripMenuItem(
            _isFetchingLocation ? "正在获取当前位置..." : "添加当前位置为常驻地点...",
            null,
            async (_, _) => await AddCurrentLocationAsync())
        {
            Enabled = !_isFetchingLocation && !_isFetchingSunriseSunset
        });
        locationsMenu.DropDownItems.Add(new ToolStripMenuItem("手动添加常驻地点...", null, (_, _) => AddManualLocation()));
        locationsMenu.DropDownItems.Add(new ToolStripSeparator());

        if (settings.SavedLocations.Count == 0)
        {
            locationsMenu.DropDownItems.Add(new ToolStripMenuItem("暂无常驻地点") { Enabled = false });
            return locationsMenu;
        }

        foreach (var location in settings.SavedLocations.OrderByDescending(location => location.LastUsedAt))
        {
            var locationMenu = new ToolStripMenuItem($"{location.Name}｜{location.Latitude:F4}, {location.Longitude:F4}");
            locationMenu.DropDownItems.Add(new ToolStripMenuItem("获取此地点今日日出/日落...", null, async (_, _) => await FetchSunriseSunsetFromLocationAsync(location))
            {
                Enabled = !_isFetchingSunriseSunset
            });
            locationMenu.DropDownItems.Add(new ToolStripMenuItem("编辑地点...", null, (_, _) => EditLocation(location)));
            locationMenu.DropDownItems.Add(new ToolStripMenuItem("删除地点", null, (_, _) => DeleteLocation(location)));
            locationsMenu.DropDownItems.Add(locationMenu);
        }

        return locationsMenu;
    }


    private async Task RefreshSunriseSunsetOnStartupAsync()
    {
        var settings = _settingsService.Settings;
        if (settings.AutoSwitchModeValue != AutoSwitchMode.SunriseSunset)
        {
            return;
        }

        if (_isFetchingSunriseSunset || _isFetchingLocation)
        {
            return;
        }

        _isFetchingSunriseSunset = true;
        _isFetchingLocation = true;
        BuildMenu();

        try
        {
            var currentLocation = await _sunriseSunsetService.GetCurrentLocationAsync("当前位置");
            settings = _settingsService.Settings;
            var savedLocation = settings.FindNearestSavedLocation(
                currentLocation.Latitude,
                currentLocation.Longitude,
                SavedLocationMatchDistanceKilometers);

            var effectiveLocation = savedLocation ?? currentLocation;
            var recordName = savedLocation is null
                ? $"当前位置自动刷新 {DateTime.Now:yyyy-MM-dd}"
                : $"{savedLocation.Name} 自动刷新 {DateTime.Now:yyyy-MM-dd}";

            var info = await _sunriseSunsetService.FetchTodayAsync(effectiveLocation, recordName);
            settings.SetSunriseSunsetInfo(info);

            if (savedLocation is not null)
            {
                settings.MarkLocationUsed(savedLocation.Id);
            }

            settings.AutoSwitchMode = AutoSwitchMode.SunriseSunset.ToString();
            SaveSettings();
            _scheduleService.Restart(settings);
            BuildMenu();
            ShowSunriseSunsetSuccess(info);
        }
        catch (Exception ex)
        {
            _settingsService.Settings.SetSunriseSunsetError(ex.Message);
            SaveSettings();
            BuildMenu();
            ShowSunriseSunsetFailure("启动自动刷新未完成。", ex);
        }
        finally
        {
            _isFetchingLocation = false;
            _isFetchingSunriseSunset = false;
            BuildMenu();
        }
    }

    private void ApplyTheme(ThemeMode mode, ThemeChangeSource source)
    {
        try
        {
            if (_themeService.GetCurrentMode() == mode)
            {
                return;
            }

            _themeService.ApplyTheme(mode);
            UpdateIcon(mode);
            BuildMenu();

            if (!_isInitialScheduleCheck)
            {
                var message = source == ThemeChangeSource.Schedule
                    ? $"已按计划切换为{GetModeText(mode)}模式。"
                    : $"已切换为{GetModeText(mode)}模式。";
                ShowNotification("Windows 主题已更新", ToolTipIcon.Info, message);
            }
        }
        catch (Exception ex)
        {
            ShowError("切换主题失败", ex);
        }
    }

    private void ScheduleServiceOnThemeDue(object? sender, ThemeMode mode) => ApplyTheme(mode, ThemeChangeSource.Schedule);

    private void ToggleAutoSwitch()
    {
        var settings = _settingsService.Settings;
        settings.AutoSwitchEnabled = !settings.AutoSwitchEnabled;
        SaveSettings();
        _scheduleService.Restart(settings);
        BuildMenu();
    }

    private void SetAutoSwitchMode(AutoSwitchMode mode)
    {
        var settings = _settingsService.Settings;
        settings.AutoSwitchMode = mode.ToString();
        SaveSettings();
        _scheduleService.Restart(settings);
        BuildMenu();

        if (mode == AutoSwitchMode.SunriseSunset && settings.TodaySunriseSunsetInfo?.Date != DateOnly.FromDateTime(DateTime.Now))
        {
            ShowNotification(
                "日出日落模式",
                ToolTipIcon.Info,
                "已切换到日出日落模式。",
                "获取今日数据前将临时使用固定时间。");
        }
    }

    private void ApplyPreset(SchedulePreset preset)
    {
        var settings = _settingsService.Settings;
        settings.PresetName = preset.Name;
        settings.LightTime = AppSettings.FormatTime(preset.LightTime);
        settings.DarkTime = AppSettings.FormatTime(preset.DarkTime);
        settings.AutoSwitchMode = AutoSwitchMode.FixedTime.ToString();
        SaveSettings();
        _scheduleService.Restart(settings);
        BuildMenu();
    }

    private void EditCustomTimes()
    {
        using var form = new TimeSettingsForm(_settingsService.Settings.LightTimeValue, _settingsService.Settings.DarkTimeValue);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        var settings = _settingsService.Settings;
        settings.PresetName = SchedulePreset.CustomName;
        settings.LightTime = AppSettings.FormatTime(form.LightTime);
        settings.DarkTime = AppSettings.FormatTime(form.DarkTime);
        settings.AutoSwitchMode = AutoSwitchMode.FixedTime.ToString();
        SaveSettings();
        _scheduleService.Restart(settings);
        BuildMenu();
    }

    private async Task FetchSunriseSunsetFromCurrentLocationAsync()
    {
        var recordName = PromptText("日出日落缓存名称", "请输入这组日出日落时间的名称：", $"当前位置 {DateTime.Now:yyyy-MM-dd}");
        if (recordName is null)
        {
            return;
        }

        await FetchSunriseSunsetAsync(null, recordName);
    }

    private async Task FetchSunriseSunsetFromLocationAsync(LocationInfo location)
    {
        var recordName = PromptText("日出日落缓存名称", "请输入这组日出日落时间的名称：", $"{location.Name} {DateTime.Now:yyyy-MM-dd}");
        if (recordName is null)
        {
            return;
        }

        await FetchSunriseSunsetAsync(location, recordName);
    }

    private async Task FetchSunriseSunsetAsync(LocationInfo? location, string recordName)
    {
        if (_isFetchingSunriseSunset)
        {
            return;
        }

        _isFetchingSunriseSunset = true;
        BuildMenu();

        try
        {
            var info = location is null
                ? await _sunriseSunsetService.FetchTodayAsync(recordName)
                : await _sunriseSunsetService.FetchTodayAsync(location, recordName);

            var settings = _settingsService.Settings;
            settings.SetSunriseSunsetInfo(info);
            if (location is not null)
            {
                settings.MarkLocationUsed(location.Id);
            }
            settings.AutoSwitchMode = AutoSwitchMode.SunriseSunset.ToString();
            SaveSettings();
            _scheduleService.Restart(settings);
            BuildMenu();

            ShowSunriseSunsetSuccess(info);
        }
        catch (Exception ex)
        {
            _settingsService.Settings.SetSunriseSunsetError(ex.Message);
            SaveSettings();
            BuildMenu();
            ShowSunriseSunsetFailure("手动获取未完成。", ex);
        }
        finally
        {
            _isFetchingSunriseSunset = false;
            BuildMenu();
        }
    }

    private async Task AddCurrentLocationAsync()
    {
        var locationName = PromptText("常驻地点名称", "请输入常驻地点名称：", "当前位置");
        if (locationName is null)
        {
            return;
        }

        if (_isFetchingLocation)
        {
            return;
        }

        _isFetchingLocation = true;
        BuildMenu();

        try
        {
            var location = await _sunriseSunsetService.GetCurrentLocationAsync(locationName);
            _settingsService.Settings.AddOrUpdateLocation(location);
            SaveSettings();
            BuildMenu();
            ShowNotification("常驻地点已添加", ToolTipIcon.Info, $"已添加“{locationName}”。");
        }
        catch (Exception ex)
        {
            ShowError("添加当前位置失败", ex);
        }
        finally
        {
            _isFetchingLocation = false;
            BuildMenu();
        }
    }

    private void AddManualLocation()
    {
        using var form = new LocationEditForm("手动添加常驻地点");
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _settingsService.Settings.AddOrUpdateLocation(new LocationInfo
        {
            Name = form.LocationName,
            Latitude = form.Latitude,
            Longitude = form.Longitude,
            TimeZoneId = TimeZoneInfo.Local.Id
        });
        SaveSettings();
        BuildMenu();
    }

    private void EditLocation(LocationInfo location)
    {
        using var form = new LocationEditForm("编辑常驻地点", location);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _settingsService.Settings.AddOrUpdateLocation(location with
        {
            Name = form.LocationName,
            Latitude = form.Latitude,
            Longitude = form.Longitude,
            TimeZoneId = TimeZoneInfo.Local.Id,
            LastUsedAt = DateTimeOffset.Now
        });
        SaveSettings();
        BuildMenu();
    }

    private void DeleteLocation(LocationInfo location)
    {
        var result = MessageBox.Show($"确定删除常驻地点“{location.Name}”吗？", "删除常驻地点", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes)
        {
            return;
        }

        _settingsService.Settings.RemoveLocation(location.Id);
        SaveSettings();
        BuildMenu();
    }

    private void RenameSunriseSunsetRecord(string recordId)
    {
        var settings = _settingsService.Settings;
        var index = settings.SunriseSunsetCache.FindIndex(info => string.Equals(info.Id, recordId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        var current = settings.SunriseSunsetCache[index];
        var name = PromptText("重命名日出日落缓存", "请输入新的缓存名称：", current.Name);
        if (name is null)
        {
            return;
        }

        settings.SunriseSunsetCache[index] = current with { Name = name };
        SaveSettings();
        BuildMenu();
    }

    private void ToggleStartup()
    {
        try
        {
            var settings = _settingsService.Settings;
            settings.StartWithWindows = !settings.StartWithWindows;
            _startupService.SetEnabled(settings.StartWithWindows);
            settings.StartWithWindows = _startupService.IsEnabled();
            SaveSettings();
            BuildMenu();
        }
        catch (Exception ex)
        {
            ShowError("更新开机自启失败", ex);
        }
    }

    private void SaveSettings()
    {
        try
        {
            _settingsService.Save();
        }
        catch (Exception ex)
        {
            ShowError("保存配置失败", ex);
        }
    }

    private void UpdateIcon(ThemeMode mode)
    {
        var newIcon = CreateTrayIcon(mode);
        var oldIcon = _currentIcon;
        _currentMode = mode;
        _currentIcon = newIcon;
        _notifyIcon.Icon = _currentIcon;
        oldIcon.Dispose();
    }

    private void ShowSunriseSunsetSuccess(SunriseSunsetInfo info)
    {
        ShowNotification(
            "日出日落已更新",
            ToolTipIcon.Info,
            $"地点：{info.Name}",
            $"日出 {AppSettings.FormatTime(info.Sunrise)}，日落 {AppSettings.FormatTime(info.Sunset)}。");
    }

    private void ShowSunriseSunsetFailure(string operation, Exception ex)
    {
        ShowNotification(
            "获取日出日落失败",
            ToolTipIcon.Error,
            operation,
            TruncateNotificationError(ex.Message));
    }

    private void ShowNotification(string title, ToolTipIcon fallbackIcon, params string[] textLines)
    {
        var nonEmptyLines = textLines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (_notificationService.TryQueue(_currentMode, title, fallbackIcon, nonEmptyLines))
        {
            return;
        }

        var fallbackText = nonEmptyLines.Length > 0
            ? string.Join(Environment.NewLine, nonEmptyLines)
            : title;
        ShowFallbackBalloon(title, fallbackText, fallbackIcon);
    }

    private void NotificationServiceOnDeliveryFailed(
        object? sender,
        NotificationDeliveryFailedEventArgs e)
    {
        ShowFallbackBalloon(e.Title, string.IsNullOrWhiteSpace(e.Body) ? e.Title : e.Body, e.FallbackIcon);
    }

    private void ShowFallbackBalloon(string title, string text, ToolTipIcon icon)
    {
        try
        {
            _notifyIcon.ShowBalloonTip(4000, title, text, icon);
        }
        catch
        {
            // Notification delivery is best effort and must not interrupt the requested action.
        }
    }

    private static string GetStartupAutoSwitchText(AppSettings settings)
    {
        if (!settings.AutoSwitchEnabled)
        {
            return "自动切换：未启用";
        }

        if (settings.AutoSwitchModeValue == AutoSwitchMode.FixedTime)
        {
            return $"自动切换：固定时间（浅 {settings.LightTime} / 深 {settings.DarkTime}）";
        }

        var info = settings.TodaySunriseSunsetInfo;
        return info is null
            ? "自动切换：日出日落（暂用固定时间）"
            : $"自动切换：日出 {AppSettings.FormatTime(info.Sunrise)} / 日落 {AppSettings.FormatTime(info.Sunset)}";
    }

    private static string TruncateNotificationError(string? error)
    {
        var normalized = string.IsNullOrWhiteSpace(error)
            ? "未知错误"
            : error.Replace('\r', ' ').Replace('\n', ' ').Trim();

        return normalized.Length <= MaxNotificationErrorLength
            ? normalized
            : $"{normalized[..(MaxNotificationErrorLength - 3)]}...";
    }

    private static string? PromptText(string title, string prompt, string defaultValue)
    {
        using var form = new TextInputForm(title, prompt, defaultValue);
        return form.ShowDialog() == DialogResult.OK ? form.InputText : null;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignore URL launch failures; this menu item is only attribution/help.
        }
    }

    private void ExitApplication()
    {
        _notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Application.Idle -= ApplicationOnIdle;
            _scheduleService.ThemeDue -= ScheduleServiceOnThemeDue;
            _scheduleService.Dispose();
            _notificationService.Dispose();
            _notificationService.DeliveryFailed -= NotificationServiceOnDeliveryFailed;
            _notifyIcon.MouseUp -= NotifyIconOnMouseUp;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _menuOwnerWindow.Dispose();
            _currentIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Form CreateMenuOwnerWindow()
    {
        var form = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Size = new Size(1, 1),
            Location = new Point(-32000, -32000),
            Opacity = 0,
            Text = "ThemeTray Menu Owner"
        };

        form.Load += (_, _) => form.Hide();
        form.Show();
        form.Hide();
        return form;
    }

    private static Icon CreateTrayIcon(ThemeMode mode)
    {
        var fileName = mode == ThemeMode.Light ? "light.png" : "dark.png";
        var assembly = typeof(TrayApplicationContext).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith($".picture.{fileName}", StringComparison.OrdinalIgnoreCase));

        if (resourceName is not null)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                return CreateIconFromImage(stream);
            }
        }

        return CreateFallbackTrayIcon(mode);
    }

    private static Icon CreateIconFromImage(Stream stream)
    {
        using var source = Image.FromStream(stream);
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        }

        return CreateIconFromBitmap(bitmap);
    }

    private static Icon CreateFallbackTrayIcon(ThemeMode mode)
    {
        using var bitmap = new Bitmap(16, 16);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var isLight = mode == ThemeMode.Light;
            using var backgroundBrush = new SolidBrush(isLight ? Color.White : Color.FromArgb(32, 32, 32));
            using var borderPen = new Pen(isLight ? Color.FromArgb(35, 35, 35) : Color.White, 1.5f);
            using var accentBrush = new SolidBrush(isLight ? Color.Goldenrod : Color.FromArgb(120, 180, 255));

            graphics.FillEllipse(backgroundBrush, 1, 1, 14, 14);
            graphics.DrawEllipse(borderPen, 1, 1, 14, 14);
            graphics.FillEllipse(accentBrush, isLight ? 5 : 7, 4, 5, 5);
        }

        return CreateIconFromBitmap(bitmap);
    }

    private static Icon CreateIconFromBitmap(Bitmap bitmap)
    {
        var iconHandle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(iconHandle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private static string GetModeText(ThemeMode mode) => mode == ThemeMode.Light ? "浅色" : "深色";

    private enum ThemeChangeSource
    {
        Manual,
        Schedule
    }

    private static void ShowError(string title, Exception ex)
    {
        MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}



