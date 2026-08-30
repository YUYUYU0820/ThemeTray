using System.Drawing;
using System.Windows.Forms;

namespace ThemeTray;

internal sealed class TimeSettingsForm : Form
{
    private readonly DateTimePicker _lightTimePicker;
    private readonly DateTimePicker _darkTimePicker;

    public TimeSettingsForm(TimeOnly lightTime, TimeOnly darkTime)
    {
        Text = "自定义切换时间";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(300, 150);
        Font = SystemFonts.MessageBoxFont;

        var lightLabel = new Label
        {
            AutoSize = true,
            Location = new Point(22, 25),
            Text = "浅色时间："
        };

        _lightTimePicker = CreateTimePicker(lightTime, new Point(120, 20));

        var darkLabel = new Label
        {
            AutoSize = true,
            Location = new Point(22, 65),
            Text = "深色时间："
        };

        _darkTimePicker = CreateTimePicker(darkTime, new Point(120, 60));

        var okButton = new Button
        {
            DialogResult = DialogResult.OK,
            Location = new Point(120, 105),
            Size = new Size(75, 27),
            Text = "确定"
        };

        var cancelButton = new Button
        {
            DialogResult = DialogResult.Cancel,
            Location = new Point(205, 105),
            Size = new Size(75, 27),
            Text = "取消"
        };

        Controls.AddRange([lightLabel, _lightTimePicker, darkLabel, _darkTimePicker, okButton, cancelButton]);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public TimeOnly LightTime => TimeOnly.FromDateTime(_lightTimePicker.Value);

    public TimeOnly DarkTime => TimeOnly.FromDateTime(_darkTimePicker.Value);

    private static DateTimePicker CreateTimePicker(TimeOnly time, Point location)
    {
        return new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Location = location,
            Size = new Size(90, 25),
            Value = DateTime.Today.Add(time.ToTimeSpan())
        };
    }
}
