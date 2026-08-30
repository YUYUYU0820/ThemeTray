using System.Windows.Forms;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace ThemeTray;

internal sealed class NotificationService : IDisposable
{
    private const int AggregationDelayMilliseconds = 3000;
    private const string ApplicationDisplayName = "ThemeTray";
    private readonly AppNotificationManager _manager = AppNotificationManager.Default;
    private readonly System.Windows.Forms.Timer _aggregationTimer;
    private readonly List<PendingNotification> _pendingNotifications = [];
    private bool _isRegistered;
    private bool _isActivationHandlerAttached;

    public NotificationService()
    {
        _aggregationTimer = new System.Windows.Forms.Timer
        {
            Interval = AggregationDelayMilliseconds
        };
        _aggregationTimer.Tick += AggregationTimerOnTick;
    }

    public event EventHandler<NotificationDeliveryFailedEventArgs>? DeliveryFailed;

    public bool Initialize()
    {
        if (_isRegistered)
        {
            return true;
        }

        try
        {
            if (!AppNotificationManager.IsSupported())
            {
                return false;
            }

            _manager.NotificationInvoked += NotificationManagerOnNotificationInvoked;
            _isActivationHandlerAttached = true;

            var applicationIconUri = GetApplicationIconUri();
            if (applicationIconUri is null)
            {
                _manager.Register();
            }
            else
            {
                _manager.Register(ApplicationDisplayName, applicationIconUri);
            }

            _isRegistered = true;
            return true;
        }
        catch
        {
            DetachActivationHandler();
            return false;
        }
    }

    public bool TryQueue(
        ThemeMode mode,
        string title,
        ToolTipIcon fallbackIcon,
        params string[] textLines)
    {
        if (!_isRegistered)
        {
            return false;
        }

        try
        {
            _pendingNotifications.Add(new PendingNotification(
                mode,
                title,
                fallbackIcon,
                textLines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray()));
            _aggregationTimer.Stop();
            _aggregationTimer.Start();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _aggregationTimer.Stop();
        FlushPendingNotifications();
        _aggregationTimer.Tick -= AggregationTimerOnTick;
        _aggregationTimer.Dispose();

        if (_isRegistered)
        {
            try
            {
                _manager.Unregister();
            }
            catch
            {
                // Notification cleanup must not prevent the tray application from exiting.
            }

            _isRegistered = false;
        }

        DetachActivationHandler();
    }

    private void AggregationTimerOnTick(object? sender, EventArgs e)
    {
        FlushPendingNotifications();
    }

    private void FlushPendingNotifications()
    {
        _aggregationTimer.Stop();
        if (_pendingNotifications.Count == 0)
        {
            return;
        }

        var pending = _pendingNotifications.ToArray();
        _pendingNotifications.Clear();
        var combined = CombineNotifications(pending);

        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(combined.Title);

            if (!string.IsNullOrWhiteSpace(combined.Body))
            {
                builder.AddText(combined.Body);
            }

            var logoUri = GetLogoUri(combined.Mode);
            if (logoUri is not null)
            {
                builder.SetAppLogoOverride(logoUri, AppNotificationImageCrop.Default, "ThemeTray");
            }

            _manager.Show(builder.BuildNotification());
        }
        catch
        {
            try
            {
                DeliveryFailed?.Invoke(
                    this,
                    new NotificationDeliveryFailedEventArgs(
                        combined.Title,
                        combined.Body,
                        combined.FallbackIcon));
            }
            catch
            {
                // Notification fallback must not interrupt the tray application.
            }
        }
    }

    private static CombinedNotification CombineNotifications(IReadOnlyList<PendingNotification> notifications)
    {
        var latest = notifications[^1];
        var fallbackIcon = notifications.Any(notification => notification.FallbackIcon == ToolTipIcon.Error)
            ? ToolTipIcon.Error
            : notifications.Any(notification => notification.FallbackIcon == ToolTipIcon.Warning)
                ? ToolTipIcon.Warning
                : ToolTipIcon.Info;

        if (notifications.Count == 1)
        {
            return new CombinedNotification(
                latest.Mode,
                latest.Title,
                string.Join(Environment.NewLine, latest.TextLines),
                fallbackIcon);
        }

        var body = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            notifications.Select(notification =>
                string.Join(
                    Environment.NewLine,
                    new[] { notification.Title }.Concat(notification.TextLines))));

        return new CombinedNotification(
            latest.Mode,
            "ThemeTray 状态更新",
            body,
            fallbackIcon);
    }

    private static Uri? GetLogoUri(ThemeMode mode)
    {
        var fileName = mode == ThemeMode.Light ? "light.png" : "dark.png";
        var filePath = Path.Combine(AppContext.BaseDirectory, "picture", fileName);
        return File.Exists(filePath)
            ? new Uri(filePath)
            : null;
    }

    private static Uri? GetApplicationIconUri()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "picture", "ThemeTray.ico");
        return File.Exists(filePath)
            ? new Uri(filePath)
            : null;
    }

    private static void NotificationManagerOnNotificationInvoked(
        AppNotificationManager sender,
        AppNotificationActivatedEventArgs args)
    {
        // Notifications are informational only.
    }

    private void DetachActivationHandler()
    {
        if (!_isActivationHandlerAttached)
        {
            return;
        }

        _manager.NotificationInvoked -= NotificationManagerOnNotificationInvoked;
        _isActivationHandlerAttached = false;
    }

    private sealed record PendingNotification(
        ThemeMode Mode,
        string Title,
        ToolTipIcon FallbackIcon,
        string[] TextLines);

    private sealed record CombinedNotification(
        ThemeMode Mode,
        string Title,
        string Body,
        ToolTipIcon FallbackIcon);
}

internal sealed class NotificationDeliveryFailedEventArgs : EventArgs
{
    public NotificationDeliveryFailedEventArgs(string title, string body, ToolTipIcon fallbackIcon)
    {
        Title = title;
        Body = body;
        FallbackIcon = fallbackIcon;
    }

    public string Title { get; }

    public string Body { get; }

    public ToolTipIcon FallbackIcon { get; }
}
