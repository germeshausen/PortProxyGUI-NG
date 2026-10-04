using PortProxyGUI.Data;

namespace PortProxyGUI;

internal static class Program
{
    public static ApplicationDbScope Database { get; private set; } = null!;

    [STAThread]
    public static int Main(string[] args)
    {
        var smokeTest = args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
        try
        {
            Database = ApplicationDbScope.OpenShared();
            ApplicationConfiguration.Initialize();
            if (smokeTest)
            {
                using var window = new PortProxyGUI();
                return 0;
            }
            Application.Run(new PortProxyGUI());
            return 0;
        }
        catch (Exception ex)
        {
            if (!smokeTest)
                MessageBox.Show(ex.Message, $"{AppIdentity.Name} – Startfehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally
        {
            Database?.Dispose();
        }
    }
}
