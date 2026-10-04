using System;
using System.Diagnostics;
using System.Windows.Forms;
using PortProxyGUI.UI;

namespace PortProxyGUI;

public partial class About : Form
{
    public readonly PortProxyGUI PortProxyGUI;

    public About(PortProxyGUI portProxyGUI)
    {
        PortProxyGUI = portProxyGUI;

        InitializeComponent();
        WindowsTheme.Track(this);
        Text = $"About {AppIdentity.Name}";
        label_version.Text = $"{AppIdentity.Name}  v{typeof(About).Assembly.GetName().Version?.ToString(3)}";
        label1.Text = "Based on PortProxyGUI by zmjack and the original contributors.";
        linkLabel1.Text = AppIdentity.UpstreamUrl;
        label_Star.Text = $".NET 10 migration and functional enhancements by {AppIdentity.Maintainer}.\n"
            + "Shared JSON configuration, safe saving, cross-session locking,\n"
            + "rule reconciliation (Delete / Restore / Skip) and Windows theme support.";
        linkLabelFork.Text = AppIdentity.RepositoryUrl;
    }

    private void linkLabel1_Click(object sender, EventArgs e)
    {
        if (sender is LinkLabel _sender)
        {
            Process.Start(new ProcessStartInfo(_sender.Text) { UseShellExecute = true });
        }
    }

    private void About_FormClosing(object sender, FormClosingEventArgs e)
    {
        PortProxyGUI.AboutForm = null;
    }
}
