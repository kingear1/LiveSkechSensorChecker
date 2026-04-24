using System.Diagnostics;

namespace LiveSkechSensorChecker;

internal sealed class ProcessPickerForm : Form
{
    private readonly CheckedListBox _processList;
    private readonly TextBox _filterText;
    private readonly List<string> _allProcesses;

    public IReadOnlyList<string> SelectedProcesses { get; private set; } = [];

    public ProcessPickerForm(IEnumerable<string>? preselected = null)
    {
        Text = "실행 중인 프로세스 선택";
        Width = 520;
        Height = 640;
        StartPosition = FormStartPosition.CenterParent;

        var preselectedSet = new HashSet<string>(preselected ?? [], StringComparer.OrdinalIgnoreCase);
        _allProcesses = Process.GetProcesses()
            .Select(p => p.ProcessName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();

        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8)
        };
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _filterText = new TextBox { PlaceholderText = "프로세스 검색...", Dock = DockStyle.Fill };
        _filterText.TextChanged += (_, _) => RefreshList(preselectedSet);

        _processList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44
        };

        var okButton = new Button { Text = "선택", Width = 100, Height = 30 };
        var cancelButton = new Button { Text = "취소", Width = 100, Height = 30 };
        var selectAllButton = new Button { Text = "전체선택", Width = 100, Height = 30 };

        okButton.Click += (_, _) =>
        {
            SelectedProcesses = _processList.CheckedItems.Cast<string>().ToList();
            DialogResult = DialogResult.OK;
            Close();
        };
        cancelButton.Click += (_, _) => Close();
        selectAllButton.Click += (_, _) =>
        {
            for (var i = 0; i < _processList.Items.Count; i++)
            {
                _processList.SetItemChecked(i, true);
            }
        };

        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(selectAllButton);

        container.Controls.Add(_filterText, 0, 0);
        container.Controls.Add(_processList, 0, 1);
        container.Controls.Add(buttons, 0, 2);
        Controls.Add(container);

        RefreshList(preselectedSet);
    }

    private void RefreshList(HashSet<string> preselected)
    {
        var q = _filterText.Text.Trim();
        var candidates = string.IsNullOrWhiteSpace(q)
            ? _allProcesses
            : _allProcesses.Where(p => p.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        _processList.BeginUpdate();
        _processList.Items.Clear();
        foreach (var name in candidates)
        {
            var idx = _processList.Items.Add(name);
            if (preselected.Contains(name))
            {
                _processList.SetItemChecked(idx, true);
            }
        }
        _processList.EndUpdate();
    }
}
