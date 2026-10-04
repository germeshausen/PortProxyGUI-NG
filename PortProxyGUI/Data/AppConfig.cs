using System.Drawing;

namespace PortProxyGUI.Data;

public sealed class AppConfig
{
    public Size MainWindowSize = new(720, 500);
    public int[] PortProxyColumnWidths = [24, 64, 140, 100, 140, 100, 100];
}
