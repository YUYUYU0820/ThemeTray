using System.Drawing;
using System.Windows.Forms;

namespace ThemeTray;

internal sealed class TextInputForm : Form
{
    private readonly TextBox _textBox;

    public TextInputForm(string title, string prompt, string defaultValue = "")
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 130);
        Font = SystemFonts.MessageBoxFont;

        var label = new Label
        {
            AutoSize = true,
            Location = new Point(16, 18),
            Text = prompt
        };

        _textBox = new TextBox
        {
            Location = new Point(18, 45),
            Size = new Size(344, 24),
            Text = defaultValue
        };

        var okButton = new Button
        {
            DialogResult = DialogResult.OK,
            Location = new Point(206, 88),
            Size = new Size(75, 27),
            Text = "确定"
        };

        var cancelButton = new Button
        {
            DialogResult = DialogResult.Cancel,
            Location = new Point(287, 88),
            Size = new Size(75, 27),
            Text = "取消"
        };

        Controls.AddRange([label, _textBox, okButton, cancelButton]);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public string InputText => _textBox.Text.Trim();

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _textBox.SelectAll();
        _textBox.Focus();
    }
}
