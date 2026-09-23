using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PCDoctor.Core.Enums;

namespace PCDoctor.App.Views;

public partial class OverlayWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExToolwindow = 0x00000080;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoactivate = 0x08000000;
    private const int WsExTopmost = 0x00000008;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        SizeChanged += (_, _) => Place();
        LocationChanged += (_, _) => Place();
    }

    public OverlayCorner Corner { get; set; } = OverlayCorner.TopRight;

    public void Place()
    {
        if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var work = SystemParameters.WorkArea;
        const double margin = 18;
        Left = Corner switch
        {
            OverlayCorner.TopLeft or OverlayCorner.BottomLeft => work.Left + margin,
            _ => work.Right - ActualWidth - margin
        };
        Top = Corner switch
        {
            OverlayCorner.TopLeft or OverlayCorner.TopRight => work.Top + margin,
            _ => work.Bottom - ActualHeight - margin
        };
        KeepOnTop();
    }

    public void KeepOnTop()
    {
        Topmost = false;
        Topmost = true;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(hwnd, GwlExstyle);
        SetWindowLong(hwnd, GwlExstyle, style | WsExToolwindow | WsExTransparent | WsExNoactivate | WsExTopmost);
        KeepOnTop();
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
