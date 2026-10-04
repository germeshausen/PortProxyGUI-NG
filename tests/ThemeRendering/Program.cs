using System.Reflection;
using System.Runtime.InteropServices;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var assembly = typeof(PortProxyGUI.Data.Rule).Assembly;
        using var list = (ListView)Activator.CreateInstance(assembly.GetType("PortProxyGUI.UI.ThemedListView"), true);
        using var form = new Form { ClientSize = new Size(760, 320) };
        list.Dock = DockStyle.Fill;
        list.View = View.Details;
        list.Columns.Add("Typ", 100);
        list.Columns.Add("Adresse", 200);
        list.Columns.Add("Kommentar", 380);
        foreach (var name in new[] { "Webserver", "Interne Dienste" })
        {
            var displayGroup = new ListViewGroup(name);
            list.Groups.Add(displayGroup);
            list.Items.Add(new ListViewItem(new[] { "v4tov4", "127.0.0.1:8080", "Darstellungstest – keine Portproxy-Änderung" }) { Group = displayGroup });
        }
        form.Controls.Add(list);
        form.Show();
        list.BackColor = Color.FromArgb(37, 37, 38);
        list.ForeColor = Color.FromArgb(241, 241, 241);
        assembly.GetType("PortProxyGUI.UI.WindowsTheme").GetMethod("ApplyListView", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { list, true });
        Application.DoEvents();

        using var bitmap = new Bitmap(list.Width, list.Height);
        list.DrawToBitmap(bitmap, list.ClientRectangle);
        // Native group rectangles must use Win32 IDs, not managed collection indices.
        var group = new NativeGroup { Size = (uint)Marshal.SizeOf<NativeGroup>(), Mask = 0x10 };
        if (GetGroupInfo(list.Handle, 0x1000 + 153, 0, ref group) == IntPtr.Zero)
            throw new Exception("Native group lookup failed.");
        var rect = new NativeRect { Top = 1 };
        if (GetGroupRect(list.Handle, 0x1000 + 98, group.Id, ref rect) == IntPtr.Zero)
            throw new Exception("Native group header rectangle lookup failed.");
        var sample = bitmap.GetPixel(rect.Right - 15, (rect.Top + rect.Bottom) / 2);
        if (sample.R != 52 || sample.G != 52 || sample.B != 56)
            throw new Exception($"Unexpected group header background: {sample}");
        var brightPixels = 0;
        for (var y = rect.Top; y < rect.Bottom; y++)
            for (var x = rect.Left + 8; x < Math.Min(rect.Right, rect.Left + 150); x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.R > 200 && pixel.G > 200 && pixel.B > 200) brightPixels++;
            }
        if (brightPixels < 20) throw new Exception("Group caption was not drawn with readable contrast.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
        bitmap.Save(args[0], System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Group rendering passed: {brightPixels} bright caption pixels. Preview: {args[0]}");
        form.Close();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeGroup
    {
        public uint Size, Mask;
        public IntPtr Header;
        public int HeaderLength;
        public IntPtr Footer;
        public int FooterLength, Id;
        public uint StateMask, State, Alignment;
        public IntPtr Subtitle;
        public uint SubtitleLength;
        public IntPtr Task;
        public uint TaskLength;
        public IntPtr DescriptionTop;
        public uint DescriptionTopLength;
        public IntPtr DescriptionBottom;
        public uint DescriptionBottomLength;
        public int TitleImage, ExtendedImage, FirstItem;
        public uint Items;
        public IntPtr SubsetTitle;
        public uint SubsetTitleLength;
    }
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr GetGroupInfo(IntPtr window, int message, int index, ref NativeGroup group);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr GetGroupRect(IntPtr window, int message, int id, ref NativeRect rect);
}
