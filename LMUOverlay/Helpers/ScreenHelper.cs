using System.Runtime.InteropServices;

namespace LMUOverlay.Helpers
{
    /// <summary>
    /// Multi-screen helper using Win32 P/Invoke.
    /// Replaces System.Windows.Forms.Screen to avoid namespace conflicts with WPF.
    /// </summary>
    public class ScreenInfo
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsPrimary { get; set; }

        public override string ToString() => IsPrimary ? $"Écran {Index + 1} (principal)" : $"Écran {Index + 1}";

        // ================================================================
        // Win32 P/Invoke to enumerate monitors
        // ================================================================

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        private const uint MONITORINFOF_PRIMARY = 1;

        /// <summary>
        /// Returns all available screens with their positions and sizes.
        /// </summary>
        public static List<ScreenInfo> GetAllScreens()
        {
            var screens = new List<ScreenInfo>();
            int index = 0;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));

                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    screens.Add(new ScreenInfo
                    {
                        Index = index,
                        Name = mi.szDevice,
                        Left = mi.rcWork.Left,
                        Top = mi.rcWork.Top,
                        Width = mi.rcWork.Right - mi.rcWork.Left,
                        Height = mi.rcWork.Bottom - mi.rcWork.Top,
                        IsPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0
                    });
                }
                index++;
                return true;
            }, IntPtr.Zero);

            return screens;
        }
    }
}
