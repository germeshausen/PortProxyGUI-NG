using System.Reflection;
using System.Text.Json;
using PortProxyGUI.Data;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var directory = Path.Combine(Path.GetTempPath(), "PortProxyGUI-reconciliation-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "config.json");
        // Exercise persistence in an isolated directory, never ProgramData or the registry.
        using var store = (ApplicationDbScope)Activator.CreateInstance(typeof(ApplicationDbScope),
            BindingFlags.Instance | BindingFlags.NonPublic, null, [file, false, null], null)!;
        var rule = new Rule { Type = "v4tov4", ListenOn = "127.0.0.1", ListenPort = 8080, ConnectTo = "127.0.0.2", ConnectPort = 80 };
        store.Add(rule);
        Check(RuleReconciliation.GetStatus(rule, []), RuleStatus.Warning, "Missing rule");
        store.SetInactive([rule.Id], true);
        using (var reloaded = ApplicationDbScope.FromFile(file))
            Check(RuleReconciliation.GetStatus(reloaded.Rules.Single(), []), RuleStatus.Inactive, "Skip survives reload");
        var systemRule = new Rule { Type = rule.Type, ListenOn = rule.ListenOn, ListenPort = rule.ListenPort, ConnectTo = "127.0.0.3", ConnectPort = 81 };
        Check(RuleReconciliation.GetStatus(rule, [systemRule]), RuleStatus.Active, "System presence takes precedence");
        systemRule.Id = rule.Id;
        store.Update(systemRule);
        Check(RuleReconciliation.GetStatus(rule, []), RuleStatus.Warning, "Externally restored rule missing again");
        Check(rule.ConnectPort, 81, "System target synchronized");
        store.SetInactive([rule.Id], true);
        Check(File.Exists(file + ".bak"), true, "Atomic save retains backup");
        using (var json = JsonDocument.Parse(File.ReadAllText(file)))
            Check(json.RootElement.GetProperty("rules")[0].GetProperty("isInactive").GetBoolean(), true, "Inactive stored in JSON");
        var legacyRule = JsonSerializer.Deserialize<Rule>("{\"Type\":\"v4tov4\",\"ListenPort\":8080}")!;
        Check(RuleReconciliation.GetStatus(legacyRule, []), RuleStatus.Warning, "Legacy JSON without inactive marker");
        ApplicationConfiguration.Initialize();
        var iconType = typeof(Rule).Assembly.GetType("PortProxyGUI.UI.StatusIcons")!;
        var populate = iconType.GetMethod("Populate", BindingFlags.Static | BindingFlags.NonPublic)!;
        using (var images = new ImageList())
        {
            foreach (var dpi in new[] { 96, 144, 192, 96 })
            {
                populate.Invoke(null, [images, dpi]);
                // Force the delayed native creation, just as the ListView does at startup.
                Check(images.Handle != IntPtr.Zero, true, "Native status image list created");
                Check(images.Images.Count, 3, "All status icons survive native creation and DPI changes");
                foreach (var status in Enum.GetValues<RuleStatus>())
                {
                    using var image = images.Images[(int)status];
                    Check(image.Size, images.ImageSize, "Native icon readable after source bitmap disposal");
                }
            }
        }
        using (var window = new PortProxyGUI.PortProxyGUI())
        {
            var missing = new Rule { Id = "missing", Type = "v4tov4", ListenPort = 9001 };
            var inactive = new Rule { Id = "inactive", Type = "v4tov4", ListenPort = 9002, IsInactive = true };
            var active = new Rule { Id = "active", Type = "v4tov4", ListenPort = 9003 };
            typeof(PortProxyGUI.PortProxyGUI).GetMethod("InitProxyItems", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [new[] { missing, inactive, active }, new[] { active }]);
            var list = (ListView)typeof(PortProxyGUI.PortProxyGUI).GetField("listViewProxies", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            Check(window.Handle != IntPtr.Zero, true, "Main window native handle created");
            Check(list.Handle != IntPtr.Zero, true, "Main list native handle created");
            Check(list.Items[0].ImageIndex, (int)RuleStatus.Warning, "Warning displayed in list");
            Check(list.Items[1].ImageIndex, (int)RuleStatus.Inactive, "Inactive displayed in list");
            Check(list.Items[2].ImageIndex, (int)RuleStatus.Active, "Active displayed in list");
            Check(list.Items[0].ToolTipText.Contains("Restore"), true, "Warning tooltip explains action");
            Check(list.SmallImageList!.Images.Count, 3, "Three status icons installed");
        }
        store.Remove(rule);
        using (var reloaded = ApplicationDbScope.FromFile(file))
            Check(reloaded.Rules.Count(), 0, "Delete removes JSON rule");

        var create = iconType.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var preview = new Bitmap(240, 100);
        using var graphics = Graphics.FromImage(preview);
        for (var row = 0; row < 2; row++)
        {
            using var background = new SolidBrush(row == 0 ? Color.White : Color.FromArgb(37, 37, 38));
            graphics.FillRectangle(background, 0, row * 50, 240, 50);
            foreach (var status in Enum.GetValues<RuleStatus>())
            {
                using var icon = (Bitmap)create.Invoke(null, [status, 32])!;
                Check(icon.GetPixel(0, 0).A, (byte)0, "Transparent icon background");
                graphics.DrawImage(icon, 20 + (int)status * 75, row * 50 + 9);
            }
        }
        var previewPath = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/status-icons-preview.png");
        Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
        preview.Save(previewPath);
        Console.WriteLine($"Reconciliation and JSON persistence passed. Status icon preview: {previewPath}");
        Console.WriteLine($"Isolated test data: {directory}");
    }

    private static void Check<T>(T actual, T expected, string description)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw new Exception($"{description}: expected {expected}, got {actual}");
    }
}
