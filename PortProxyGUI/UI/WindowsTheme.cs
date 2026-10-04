using Microsoft.Win32;
using System.Drawing;
using System.Runtime.InteropServices;

namespace PortProxyGUI.UI;

internal static class WindowsTheme
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int LvmFirst = 0x1000;
    private const int LvmGetHeader = LvmFirst + 31;
    private const int WmThemeChanged = 0x031A;
    private static readonly Color DarkBackground = Color.FromArgb(32, 32, 32);
    private static readonly Color DarkSurface = Color.FromArgb(45, 45, 48);
    private static readonly Color DarkInput = Color.FromArgb(37, 37, 38);
    private static readonly Color DarkForeground = Color.FromArgb(241, 241, 241);

    public static void Track(Form form)
    {
        void Apply() => ApplyTo(form);
        UserPreferenceChangedEventHandler preferenceChanged = (_, _) =>
        {
            if (!form.IsDisposed && form.IsHandleCreated) form.BeginInvoke(Apply);
        };

        form.HandleCreated += (_, _) => Apply();
        form.FormClosed += (_, _) => SystemEvents.UserPreferenceChanged -= preferenceChanged;
        SystemEvents.UserPreferenceChanged += preferenceChanged;
        if (form.IsHandleCreated) Apply();
    }

    private static void ApplyTo(Form form)
    {
        var dark = IsDarkMode();
        var enabled = dark ? 1 : 0;
        DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
        ApplyControl(form, dark);
        form.Invalidate(true);
    }

    private static void ApplyControl(Control control, bool dark)
    {
        control.ForeColor = dark ? DarkForeground : SystemColors.ControlText;
        control.BackColor = control switch
        {
            TextBoxBase or ComboBox or ListView => dark ? DarkInput : SystemColors.Window,
            Form => dark ? DarkBackground : SystemColors.Control,
            StatusStrip or ContextMenuStrip => dark ? DarkSurface : SystemColors.Control,
            _ => dark ? DarkBackground : SystemColors.Control
        };

        switch (control)
        {
            case Button button:
                button.UseVisualStyleBackColor = !dark;
                if (dark) button.BackColor = DarkSurface;
                break;
            case LinkLabel link:
                link.LinkColor = dark ? Color.FromArgb(76, 194, 255) : SystemColors.HotTrack;
                link.ActiveLinkColor = dark ? Color.FromArgb(153, 220, 255) : Color.Red;
                break;
            case ListView listView:
                ApplyListView(listView, dark);
                break;
            case ToolStrip strip:
                strip.RenderMode = ToolStripRenderMode.System;
                foreach (ToolStripItem item in strip.Items) ApplyToolStripItem(item, dark);
                break;
        }

        foreach (Control child in control.Controls) ApplyControl(child, dark);
    }

    private static void ApplyListView(ListView listView, bool dark)
    {
        SetWindowTheme(listView.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);

        var header = SendMessage(listView.Handle, LvmGetHeader, IntPtr.Zero, IntPtr.Zero);
        if (header != IntPtr.Zero)
        {
            SetWindowTheme(header, dark ? "DarkMode_ItemsView" : "Explorer", null);
            SendMessage(header, WmThemeChanged, IntPtr.Zero, IntPtr.Zero);
        }

        listView.DrawColumnHeader -= DrawColumnHeader;
        listView.DrawItem -= DrawItem;
        listView.DrawSubItem -= DrawSubItem;
        listView.OwnerDraw = dark;
        if (dark)
        {
            listView.DrawColumnHeader += DrawColumnHeader;
            listView.DrawItem += DrawItem;
            listView.DrawSubItem += DrawSubItem;
        }
    }

    private static void DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using var background = new SolidBrush(DarkSurface);
        using var separator = new Pen(Color.FromArgb(70, 70, 74));
        e.Graphics.FillRectangle(background, e.Bounds);
        e.Graphics.DrawLine(separator, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
        e.Graphics.DrawLine(separator, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        flags |= e.Header?.TextAlign switch
        {
            HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
            HorizontalAlignment.Right => TextFormatFlags.Right,
            _ => TextFormatFlags.Left
        };
        var textBounds = Rectangle.Inflate(e.Bounds, -7, 0);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", e.Font ?? SystemFonts.DefaultFont, textBounds, DarkForeground, flags);
    }

    private static void DrawItem(object? sender, DrawListViewItemEventArgs e) => e.DrawDefault = true;
    private static void DrawSubItem(object? sender, DrawListViewSubItemEventArgs e) => e.DrawDefault = true;

    private static void ApplyToolStripItem(ToolStripItem item, bool dark)
    {
        item.BackColor = dark ? DarkSurface : SystemColors.Control;
        item.ForeColor = dark ? DarkForeground : SystemColors.ControlText;
        if (item is ToolStripDropDownItem dropDown)
        {
            dropDown.DropDown.BackColor = item.BackColor;
            dropDown.DropDown.ForeColor = item.ForeColor;
            foreach (ToolStripItem child in dropDown.DropDownItems) ApplyToolStripItem(child, dark);
        }
    }

    internal static bool IsDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr window, string? subAppName, string? subIdList);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
