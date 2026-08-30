using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ThemeTray;

internal enum ThemeMode
{
    Dark,
    Light
}

internal sealed class ThemeService
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string SystemValueName = "SystemUsesLightTheme";
    private const string AppsValueName = "AppsUseLightTheme";
    private const int HwndBroadcast = 0xffff;
    private const int WmSettingChange = 0x001A;
    private const int SmtoAbortIfHung = 0x0002;

    public ThemeMode GetCurrentMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath, writable: false);
        var appValue = key?.GetValue(AppsValueName);
        return Convert.ToInt32(appValue ?? 1) == 0 ? ThemeMode.Dark : ThemeMode.Light;
    }

    public void ApplyTheme(ThemeMode mode)
    {
        var useLightTheme = mode == ThemeMode.Light ? 1 : 0;

        using var key = Registry.CurrentUser.CreateSubKey(PersonalizeKeyPath, writable: true);
        key.SetValue(SystemValueName, useLightTheme, RegistryValueKind.DWord);
        key.SetValue(AppsValueName, useLightTheme, RegistryValueKind.DWord);

        BroadcastThemeChanged();
    }

    private static void BroadcastThemeChanged()
    {
        SendMessageTimeout(
            new IntPtr(HwndBroadcast),
            WmSettingChange,
            UIntPtr.Zero,
            "ImmersiveColorSet",
            SmtoAbortIfHung,
            1000,
            out _);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        int msg,
        UIntPtr wParam,
        string lParam,
        int flags,
        int timeout,
        out UIntPtr result);
}
