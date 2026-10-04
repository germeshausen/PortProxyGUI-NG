using System.Reflection;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        // Do not show the main window: its startup refresh accesses shared configuration.
        using var owner = new PortProxyGUI.PortProxyGUI();
        if (owner.Text != "PortProxyGUI NG") throw new Exception("Fork name missing from main window.");
        using var about = new PortProxyGUI.About(owner);
        about.Show();
        Application.DoEvents();
        var controls = Descendants(about).ToArray();
        foreach (var expected in new[] { "https://github.com/zmjack/PortProxyGUI", "https://github.com/germeshausen/PortProxyGUI-NG" })
            if (!controls.OfType<LinkLabel>().Any(link => link.Text == expected && link.Visible && link.TabStop))
                throw new Exception($"Repository link missing: {expected}");
        if (!controls.OfType<Label>().Any(label => label.Text.Contains("enhancements by germeshausen")))
            throw new Exception("Fork attribution missing.");
        if (!controls.OfType<Label>().Any(label => label.Text.Contains("by zmjack")))
            throw new Exception("Upstream attribution missing.");
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts");
        Directory.CreateDirectory(output);
        var apply = typeof(PortProxyGUI.About).Assembly.GetType("PortProxyGUI.UI.WindowsTheme")!
            .GetMethod("ApplyControl", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (var dark in new[] { false, true })
        {
            apply.Invoke(null, [about, dark]);
            about.PerformLayout();
            Application.DoEvents();
            foreach (var control in controls.Where(control => control.Visible))
                if (!control.Parent!.ClientRectangle.Contains(control.Bounds))
                    throw new Exception($"About control clipped: {control.Name} {control.Bounds}");
            using var bitmap = new Bitmap(about.Width, about.Height);
            about.DrawToBitmap(bitmap, new Rectangle(Point.Empty, about.Size));
            bitmap.Save(Path.Combine(output, dark ? "about-dark.png" : "about-light.png"));
        }
        about.Close();
        Console.WriteLine($"About attribution, repository links and layout passed. Previews: {output}");
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            yield return control;
            foreach (var descendant in Descendants(control)) yield return descendant;
        }
    }
}
