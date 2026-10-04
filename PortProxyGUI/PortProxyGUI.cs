using PortProxyGUI.Data;
using PortProxyGUI.UI;
using PortProxyGUI.Utils;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static System.Windows.Forms.ListViewItem;

namespace PortProxyGUI;

public partial class PortProxyGUI : Form
{
    private readonly ListViewColumnSorter lvwColumnSorter = new ListViewColumnSorter();

    public SetProxy? SetProxyForm;
    public About? AboutForm;
    private AppConfig AppConfig = new();
    private readonly ToolStripMenuItem restoreMenuItem = new("Restore (from JSON)");
    private readonly ToolStripMenuItem skipMenuItem = new("Skip (keep inactive)");

    public PortProxyGUI()
    {
        InitializeComponent();
        Text = AppIdentity.Name;
        StatusIcons.Populate(imageListProxies, DeviceDpi);
        DpiChanged += (_, _) => StatusIcons.Populate(imageListProxies, DeviceDpi);
        listViewProxies.ShowItemToolTips = true;
        columnHeader1.Text = "Status";
        contextMenuStrip_RightClick.Items.Insert(2, restoreMenuItem);
        contextMenuStrip_RightClick.Items.Insert(3, skipMenuItem);
        restoreMenuItem.Click += (_, _) => EnableSelectedProxies(warningsOnly: true);
        skipMenuItem.Click += (_, _) => SkipSelectedProxies();
        contextMenuStrip_RightClick.Opening += (_, _) => UpdateActionAvailability();
        WindowsTheme.Track(this);
        listViewProxies.ListViewItemSorter = lvwColumnSorter;
        saveFileDialog_Export.Filter = "JSON configuration (*.json)|*.json";
        saveFileDialog_Export.DefaultExt = "json";
        openFileDialog_Import.Filter = "JSON configuration (*.json)|*.json";
    }


    private void PortProxyGUI_Load(object sender, EventArgs e)
    {
        AppConfig = Program.Database.GetAppConfig();

        var size = AppConfig.MainWindowSize;
        Left -= (size.Width - Width) / 2;
        Top -= (size.Height - Height) / 2;

        ResetWindowSize();
    }

    private void PortProxyGUI_Shown(object sender, EventArgs e)
    {
        RefreshProxyList();
    }

    private void ResetWindowSize()
    {
        Size = AppConfig.MainWindowSize;

        if (AppConfig.PortProxyColumnWidths.Length != listViewProxies.Columns.Count)
        {
            Array.Resize(ref AppConfig.PortProxyColumnWidths, listViewProxies.Columns.Count);
        }

        foreach (var (column, configWidth) in listViewProxies.Columns.OfType<ColumnHeader>().Zip(AppConfig.PortProxyColumnWidths))
        {
            column.Width = configWidth;
        }
        columnHeader1.Width = Math.Max(columnHeader1.Width, imageListProxies.ImageSize.Width + 12);
    }

    private Data.Rule ParseRule(ListViewItem item)
    {
        var subItems = item.SubItems.OfType<ListViewSubItem>().ToArray();
        int listenPort, connectPort;

        listenPort = Data.Rule.ParsePort(subItems[3].Text);
        connectPort = Data.Rule.ParsePort(subItems[5].Text);

        var rule = new Data.Rule
        {
            Id = item.Tag?.ToString(),
            Type = subItems[1].Text.Trim(),
            ListenOn = subItems[2].Text.Trim(),
            ListenPort = listenPort,
            ConnectTo = subItems[4].Text.Trim(),
            ConnectPort = connectPort,
            Comment = subItems[6].Text.Trim(),
            Group = item.Group?.Header.Trim() ?? "",
        };
        return rule;
    }

    private void RunSelectedAction(Func<ListViewItem, bool> filter, Action<Data.Rule, RuleStatus> action, bool notifySystem)
    {
        var items = listViewProxies.SelectedItems.OfType<ListViewItem>().Where(filter).ToArray();
        try
        {
            foreach (var item in items)
            {
                var rule = Program.Database.Rules.FirstOrDefault(rule => rule.Id == item.Tag?.ToString());
                if (rule is not null) action(rule, (RuleStatus)item.ImageIndex);
            }
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show(this, ex.Message, "Rule action failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (notifySystem && items.Length > 0)
            {
                try { Util.ParamChange(); }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show(this, ex.Message, "IP Helper notification failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            RefreshProxyList();
        }
    }

    private void EnableSelectedProxies(bool warningsOnly = false)
    {
        RunSelectedAction(item => item.ImageIndex == (int)(warningsOnly ? RuleStatus.Warning : RuleStatus.Inactive), (rule, _) =>
        {
            Util.AddOrUpdateProxy(rule);
            Program.Database.SetInactive([rule.Id], false);
        }, notifySystem: true);
    }

    private void DisableSelectedProxies()
    {
        RunSelectedAction(item => item.ImageIndex == (int)RuleStatus.Active, (rule, _) =>
        {
            Util.DeleteProxy(rule);
            Program.Database.SetInactive([rule.Id], true);
        }, notifySystem: true);
    }

    private void SkipSelectedProxies()
    {
        RunSelectedAction(item => item.ImageIndex == (int)RuleStatus.Warning,
            (rule, _) => Program.Database.SetInactive([rule.Id], true), notifySystem: false);
    }

    private void DeleteSelectedProxies()
    {
        RunSelectedAction(_ => true, (rule, status) =>
        {
            if (status == RuleStatus.Active) Util.DeleteProxy(rule);
            Program.Database.Remove(rule);
        }, notifySystem: listViewProxies.SelectedItems.OfType<ListViewItem>().Any(item => item.ImageIndex == (int)RuleStatus.Active));
    }

    private void SetProxyForUpdate(SetProxy form)
    {
        var item = listViewProxies.SelectedItems.OfType<ListViewItem>().FirstOrDefault();
        if (item is null) return;
        try
        {
            var rule = ParseRule(item);
            form.UseUpdateMode(item, rule);
        }
        catch (NotSupportedException ex)
        {
            MessageBox.Show(ex.Message, "Exclamation", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            return;
        }
    }

    private void InitProxyGroups(Data.Rule[] rules)
    {
        listViewProxies.Groups.Clear();
        var groups = (
            from g in rules.GroupBy(x => x.Group)
            let name = g.Key
            where !string.IsNullOrWhiteSpace(name)
            orderby name
            select new ListViewGroup(name)
        ).ToArray();
        listViewProxies.Groups.AddRange(groups);
    }

    private void InitProxyItems(Data.Rule[] rules, Data.Rule[] proxies)
    {
        listViewProxies.Items.Clear();
        foreach (var rule in rules)
        {
            var imageIndex = (int)RuleReconciliation.GetStatus(rule, proxies);

            var item = new ListViewItem();
            UpdateListViewItem(item, rule, imageIndex);
            listViewProxies.Items.Add(item);
        }
    }

    public void UpdateListViewItem(ListViewItem item, Data.Rule rule, int imageIndex)
    {
        item.ImageIndex = imageIndex;
        item.ToolTipText = (RuleStatus)imageIndex switch
        {
            RuleStatus.Active => "Active: the rule exists in Windows.",
            RuleStatus.Inactive => "Inactive: intentionally kept in JSON without a Windows rule.",
            _ => "Warning: rule is missing in Windows. Choose Delete, Restore or Skip from the context menu."
        };
        item.Tag = rule.Id;
        item.SubItems.Clear();
        item.SubItems.AddRange(new[]
        {
            new ListViewSubItem(item, rule.Type),
            new ListViewSubItem(item, rule.ListenOn),
            new ListViewSubItem(item, rule.ListenPort.ToString()) { Tag = "Number" },
            new ListViewSubItem(item, rule.ConnectTo),
            new ListViewSubItem(item, rule.ConnectPort.ToString ()) { Tag = "Number" },
            new ListViewSubItem(item, rule.Comment ?? ""),
        });

        if (string.IsNullOrWhiteSpace(rule.Group)) item.Group = null;
        else
        {
            var group = listViewProxies.Groups.OfType<ListViewGroup>().FirstOrDefault(x => x.Header == rule.Group);
            if (group == null)
            {
                group = new ListViewGroup(rule.Group);
                listViewProxies.Groups.Add(group);
            }
            item.Group = group;
        }
    }

    public void RefreshProxyList()
    {
        var proxies = Util.GetProxies();
        var rules = Program.Database.Rules.ToArray();
        foreach (var proxy in proxies)
        {
            var matchedRule = rules.FirstOrDefault(r => r.EqualsWithKeys(proxy));
            proxy.Id = matchedRule?.Id;
        }

        var pendingAdds = proxies.Where(x => x.Valid && x.Id == null);
        var pendingUpdates =
            from proxy in proxies
            let exsist = rules.FirstOrDefault(r => r.Id == proxy.Id)
            where exsist is not null
            where proxy.Valid && proxy.Id is not null
            select proxy;

        Program.Database.AddRange(pendingAdds);
        Program.Database.UpdateRange(pendingUpdates);

        rules = Program.Database.Rules.ToArray();
        InitProxyGroups(rules);
        InitProxyItems(rules, proxies);

        // CheckServiceStatus
        toolStripStatusLabel_ServiceNotRunning.Visible = !Util.IsServiceRunning();
        var warnings = listViewProxies.Items.OfType<ListViewItem>().Count(item => item.ImageIndex == (int)RuleStatus.Warning);
        toolStripStatusLabel_Status.Text = warnings > 0
            ? $"{warnings} missing system rule(s): choose Delete, Restore or Skip."
            : $"{DateTime.Now} : Refreshed.";
    }

    private void contextMenuStrip_RightClick_MouseClick(object sender, MouseEventArgs e)
    {
        if (sender is ContextMenuStrip strip)
        {
            var selected = strip.Items.OfType<ToolStripMenuItem>().Where(x => x.Selected).FirstOrDefault();
            if (selected is null || !selected.Enabled) return;

            switch (selected)
            {
                case ToolStripMenuItem item when item == toolStripMenuItem_Enable: EnableSelectedProxies(); break;
                case ToolStripMenuItem item when item == toolStripMenuItem_Disable: DisableSelectedProxies(); break;

                case ToolStripMenuItem item when item == toolStripMenuItem_New:
                    SetProxyForm ??= new SetProxy(this);
                    SetProxyForm.UseNormalMode();
                    SetProxyForm.ShowDialog();
                    break;

                case ToolStripMenuItem item when item == toolStripMenuItem_Modify:
                    SetProxyForm ??= new SetProxy(this);
                    SetProxyForUpdate(SetProxyForm);
                    SetProxyForm.ShowDialog();
                    break;

                case ToolStripMenuItem item when item == toolStripMenuItem_Refresh:
                    RefreshProxyList();
                    break;

                case ToolStripMenuItem item when item == toolStripMenuItem_FlushDnsCache:
                    DnsUtil.FlushCache();
                    toolStripStatusLabel_Status.Text = $"{DateTime.Now} : DNS cache cleared.";
                    break;

                case ToolStripMenuItem item when item == toolStripMenuItem_Delete:
                    DeleteSelectedProxies();
                    break;

                case ToolStripMenuItem item when item == toolStripMenuItem_About:
                    if (AboutForm == null)
                    {
                        AboutForm = new About(this);
                        AboutForm.Show();
                    }
                    else AboutForm.Show();
                    break;
            }
        }
    }

    private void listView1_MouseUp(object sender, MouseEventArgs e)
    {
        UpdateActionAvailability();
    }

    private void UpdateActionAvailability()
    {
        var items = listViewProxies.SelectedItems.OfType<ListViewItem>().ToArray();
        toolStripMenuItem_Enable.Enabled = items.Any(item => item.ImageIndex == (int)RuleStatus.Inactive);
        toolStripMenuItem_Disable.Enabled = items.Any(item => item.ImageIndex == (int)RuleStatus.Active);
        restoreMenuItem.Enabled = skipMenuItem.Enabled = items.Any(item => item.ImageIndex == (int)RuleStatus.Warning);
        toolStripMenuItem_Delete.Enabled = items.Length > 0;
        toolStripMenuItem_Modify.Enabled = items.Length == 1;
    }

    private void listView1_DoubleClick(object sender, EventArgs e)
    {
        if (sender is ListView listView)
        {
            var selectAny = listView.SelectedItems.OfType<ListViewItem>().Any();
            if (selectAny)
            {
                SetProxyForm ??= new SetProxy(this);
                SetProxyForUpdate(SetProxyForm);
                SetProxyForm.ShowDialog();
            }
        }
    }

    private void listView1_ColumnClick(object sender, ColumnClickEventArgs e)
    {
        // Determine if clicked column is already the column that is being sorted.
        if (e.Column == lvwColumnSorter.SortColumn)
        {
            // Reverse the current sort direction for this column.
            if (lvwColumnSorter.Order == SortOrder.Ascending)
            {
                lvwColumnSorter.Order = SortOrder.Descending;
            }
            else
            {
                lvwColumnSorter.Order = SortOrder.Ascending;
            }
        }
        else
        {
            // Set the column number that is to be sorted; default to ascending.
            lvwColumnSorter.SortColumn = e.Column;
            lvwColumnSorter.Order = SortOrder.Ascending;
        }

        // Perform the sort with these new sort options.
        listViewProxies.Sort();
    }

    private void listViewProxies_KeyUp(object sender, KeyEventArgs e)
    {
        if (sender is ListView)
        {
            if (e.KeyCode == Keys.Delete) DeleteSelectedProxies();
        }
    }

    private void listViewProxies_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
    {
        if (AppConfig is not null && sender is ListView listView)
        {
            AppConfig.PortProxyColumnWidths[e.ColumnIndex] = listView.Columns[e.ColumnIndex].Width;
        }
    }

    private void PortProxyGUI_FormClosing(object sender, FormClosingEventArgs e)
    {
        Program.Database.SaveAppConfig(AppConfig);
    }

    private void PortProxyGUI_Resize(object sender, EventArgs e)
    {
        if (AppConfig is not null && sender is Form form)
        {
            AppConfig.MainWindowSize = form.Size;
        }
    }

    private void toolStripMenuItem_Export_Click(object sender, EventArgs e)
    {
        using var dialog = saveFileDialog_Export;

        var result = dialog.ShowDialog();
        if (result == DialogResult.OK)
        {
            var fileName = dialog.FileName;
            File.Copy(ApplicationDbScope.AppDbFile, fileName, true);
        }
    }

    private void toolStripMenuItem_Import_Click(object sender, EventArgs e)
    {
        using var dialog = openFileDialog_Import;

        var result = dialog.ShowDialog();
        if (result == DialogResult.OK)
        {
            var fileName = dialog.FileName;
            using (var scope = ApplicationDbScope.FromFile(fileName))
            {
                foreach (var rule in scope.Rules)
                {
                    var exsist = Program.Database.GetRule(rule.Type, rule.ListenOn, rule.ListenPort);
                    if (exsist is null)
                    {
                        rule.Id = Guid.NewGuid().ToString();
                        Program.Database.Add(rule);
                    }
                }
            }

            RefreshProxyList();
        }
    }

    private void toolStripMenuItem_ResetWindowSize_Click(object sender, EventArgs e)
    {
        AppConfig = new AppConfig();
        ResetWindowSize();
    }

    private void toolStripStatusLabel_ServiceNotRunning_Click(object sender, EventArgs e)
    {
        Util.StartService();
        toolStripStatusLabel_ServiceNotRunning.Visible = false;
    }
}
