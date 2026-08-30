using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace ThemeTray;

internal sealed class LocationEditForm : Form
{
    private readonly TextBox _nameTextBox;
    private readonly TextBox _latitudeTextBox;
    private readonly TextBox _longitudeTextBox;

    public LocationEditForm(string title, LocationInfo? location = null)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 190);
        Font = SystemFonts.MessageBoxFont;

        Controls.Add(new Label { AutoSize = true, Location = new Point(18, 22), Text = "名称：" });
        _nameTextBox = new TextBox
        {
            Location = new Point(105, 18),
            Size = new Size(230, 24),
            Text = location?.Name ?? "常驻地点"
        };

        Controls.Add(new Label { AutoSize = true, Location = new Point(18, 62), Text = "纬度：" });
        _latitudeTextBox = new TextBox
        {
            Location = new Point(105, 58),
            Size = new Size(230, 24),
            Text = location?.Latitude.ToString("F6", CultureInfo.InvariantCulture) ?? string.Empty
        };

        Controls.Add(new Label { AutoSize = true, Location = new Point(18, 102), Text = "经度：" });
        _longitudeTextBox = new TextBox
        {
            Location = new Point(105, 98),
            Size = new Size(230, 24),
            Text = location?.Longitude.ToString("F6", CultureInfo.InvariantCulture) ?? string.Empty
        };

        var okButton = new Button
        {
            DialogResult = DialogResult.OK,
            Location = new Point(179, 145),
            Size = new Size(75, 27),
            Text = "确定"
        };
        okButton.Click += OkButtonOnClick;

        var cancelButton = new Button
        {
            DialogResult = DialogResult.Cancel,
            Location = new Point(260, 145),
            Size = new Size(75, 27),
            Text = "取消"
        };

        Controls.AddRange([_nameTextBox, _latitudeTextBox, _longitudeTextBox, okButton, cancelButton]);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public string LocationName => string.IsNullOrWhiteSpace(_nameTextBox.Text) ? "常驻地点" : _nameTextBox.Text.Trim();

    public double Latitude => double.Parse(_latitudeTextBox.Text.Trim(), CultureInfo.InvariantCulture);

    public double Longitude => double.Parse(_longitudeTextBox.Text.Trim(), CultureInfo.InvariantCulture);

    private void OkButtonOnClick(object? sender, EventArgs e)
    {
        if (!double.TryParse(_latitudeTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !AppSettings.IsValidLatitude(latitude))
        {
            MessageBox.Show("纬度必须是 -90 到 90 之间的数字。", "地点信息无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (!double.TryParse(_longitudeTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
            !AppSettings.IsValidLongitude(longitude))
        {
            MessageBox.Show("经度必须是 -180 到 180 之间的数字。", "地点信息无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
        }
    }
}
