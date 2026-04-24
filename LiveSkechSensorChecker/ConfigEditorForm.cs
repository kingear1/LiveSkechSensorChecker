namespace LiveSkechSensorChecker;

internal sealed class ConfigEditorForm : Form
{
    private readonly string _configPath;
    private readonly TextBox _editor;

    public bool IsSaved { get; private set; }

    public ConfigEditorForm(string configPath, string configJson)
    {
        _configPath = configPath;

        Text = "설정 편집";
        StartPosition = FormStartPosition.CenterParent;
        Width = 900;
        Height = 700;

        var helpLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Text = "config.json 내용을 직접 수정할 수 있습니다. 저장 시 형식/필수값을 검증합니다.",
            Padding = new Padding(8, 12, 8, 8)
        };

        _editor = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 10),
            WordWrap = false,
            Text = configJson
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };

        var saveButton = new Button
        {
            Width = 120,
            Height = 32,
            Text = "저장"
        };
        saveButton.Click += SaveButton_Click;

        var cancelButton = new Button
        {
            Width = 120,
            Height = 32,
            Text = "취소"
        };
        cancelButton.Click += (_, _) => Close();

        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);

        Controls.Add(_editor);
        Controls.Add(buttonPanel);
        Controls.Add(helpLabel);
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var parsed = AppConfig.Parse(_editor.Text);
            parsed.Save(_configPath);

            IsSaved = true;
            MessageBox.Show("설정이 저장되었습니다.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"설정 저장 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
