using System.Runtime.InteropServices;

namespace PortProxyGUI.UI;

/// <summary>Paints native group headers after the ListView has drawn its contents.</summary>
internal sealed class ThemedListView : ListView
{
    private const int WmPaint = 0x000F;
    private const int WmPrintClient = 0x0318;
    private const int LvmGetGroupCount = 0x1000 + 152;
    private const int LvmGetGroupInfoByIndex = 0x1000 + 153;
    private const int LvmGetGroupRect = 0x1000 + 98;
    private const int LvmGetHeader = 0x1000 + 31;

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (ShowGroups && WindowsTheme.IsDarkMode())
        {
            if (message.Msg == WmPaint) DrawGroupHeaders(IntPtr.Zero);
            else if (message.Msg == WmPrintClient) DrawGroupHeaders(message.WParam);
        }
    }

    private void DrawGroupHeaders(IntPtr deviceContext)
    {
        var count = SendMessage(Handle, LvmGetGroupCount, IntPtr.Zero, IntPtr.Zero).ToInt32();
        if (count <= 0) return;

        using var graphics = deviceContext == IntPtr.Zero
            ? Graphics.FromHwnd(Handle) : Graphics.FromHdc(deviceContext);
        using var font = new Font(Font, FontStyle.Bold);
        using var background = new SolidBrush(Color.FromArgb(52, 52, 56));
        using var separator = new Pen(Color.FromArgb(100, 100, 106));
        var viewport = ClientRectangle;
        var header = SendMessage(Handle, LvmGetHeader, IntPtr.Zero, IntPtr.Zero);
        if (GetWindowRect(header, out var headerRect))
            viewport.Y = headerRect.Bottom - headerRect.Top;
        viewport.Height = Math.Max(0, ClientSize.Height - viewport.Y);
        graphics.SetClip(viewport);

        var buffer = Marshal.AllocHGlobal(2048);
        try
        {
            for (var index = 0; index < count; index++)
            {
                var group = new NativeGroup
                {
                    Size = (uint)Marshal.SizeOf<NativeGroup>(),
                    Mask = 0x00000001 | 0x00000010, // LVGF_HEADER | LVGF_GROUPID
                    Header = buffer,
                    HeaderLength = 1024
                };
                if (GetGroupInfo(Handle, LvmGetGroupInfoByIndex, index, ref group) == IntPtr.Zero) continue;
                var rect = new NativeRect { Top = 1 }; // LVGGR_HEADER
                if (GetGroupRect(Handle, LvmGetGroupRect, group.Id, ref rect) == IntPtr.Zero) continue;
                var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                if (bounds.Width <= 0 || bounds.Height <= 0 || !bounds.IntersectsWith(viewport)) continue;

                graphics.FillRectangle(background, bounds);
                graphics.DrawLine(separator, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
                var padding = Math.Max(8, DeviceDpi * 8 / 96);
                var textBounds = Rectangle.Inflate(bounds, -padding, 0);
                TextRenderer.DrawText(graphics, Marshal.PtrToStringUni(buffer) ?? "", font,
                    textBounds, Color.FromArgb(250, 250, 250),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping);
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    // Full Vista LVGROUP layout. RECT uses right/bottom coordinates, not width/height.
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
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr GetGroupInfo(IntPtr window, int message, int index, ref NativeGroup group);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr GetGroupRect(IntPtr window, int message, int id, ref NativeRect rect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
}
