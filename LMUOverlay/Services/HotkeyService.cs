using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using LMUOverlay.Models;
using SharpDX.DirectInput;

namespace LMUOverlay.Services
{
    /// <summary>
    /// Global hotkey service — clavier, souris et volant/joystick DirectInput.
    /// Chaque overlay peut avoir un binding qui toggle sa visibilité, même quand le jeu est au premier plan.
    /// </summary>
    public class HotkeyService : IDisposable
    {
        // ── Win32 P/Invoke ────────────────────────────────────────────────────
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public UIntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public System.Drawing.Point pt;
            public uint mouseData, flags, time;
            public UIntPtr dwExtraInfo;
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL    = 14;
        private const int WM_KEYDOWN     = 0x0100;
        private const int WM_SYSKEYDOWN  = 0x0104;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_XBUTTONDOWN = 0x020B;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly OverlayManager _mgr;
        private readonly List<(string Key, HotkeyBinding Binding)> _bindings = [];

        private IntPtr  _keyboardHook = IntPtr.Zero;
        private IntPtr  _mouseHook    = IntPtr.Zero;
        private HookProc? _keyboardProc; // keep alive — GC guard
        private HookProc? _mouseProc;

        // Capture mode
        private volatile bool                _captureActive;
        private Action<HotkeyBinding>?       _captureCallback;

        // DirectInput
        private DirectInput?                 _directInput;
        private List<Joystick>               _joysticks    = [];
        private Dictionary<Joystick, bool[]> _prevButtons  = [];
        private Thread?                      _jsThread;
        private volatile bool                _jsRunning;

        // ── Constructor ───────────────────────────────────────────────────────

        public HotkeyService(OverlayManager mgr)
        {
            _mgr = mgr;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Initialize(IEnumerable<(string Key, OverlaySettings Settings)> overlays)
        {
            RefreshBindings(overlays);
            InstallHooks();
            StartDirectInput();
        }

        public void RefreshBindings(IEnumerable<(string Key, OverlaySettings Settings)> overlays)
        {
            lock (_bindings)
            {
                _bindings.Clear();
                foreach (var (key, s) in overlays)
                    if (s.Hotkey != null && !s.Hotkey.IsEmpty)
                        _bindings.Add((key, s.Hotkey));
            }
        }

        /// <summary>
        /// Enters capture mode: the next non-trivial input fires <paramref name="callback"/>
        /// with the captured binding. A 200 ms delay prevents capturing the click on CAPTURER itself.
        /// </summary>
        public void StartCapture(Action<HotkeyBinding> callback)
        {
            _captureCallback = callback;
            _captureActive   = false;
            Task.Delay(200).ContinueWith(_ => _captureActive = true);
        }

        public void CancelCapture()
        {
            _captureActive   = false;
            _captureCallback = null;
        }

        // ── Win32 hooks ───────────────────────────────────────────────────────

        private void InstallHooks()
        {
            using var proc = Process.GetCurrentProcess();
            using var mod  = proc.MainModule!;
            IntPtr hMod    = GetModuleHandle(mod.ModuleName);

            _keyboardProc = KeyboardProc;
            _mouseProc    = MouseProc;
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            _mouseHook    = SetWindowsHookEx(WH_MOUSE_LL,    _mouseProc,    hMod, 0);
        }

        private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                var ks = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int vk = (int)ks.vkCode;

                // Ignore pure modifier keys
                if (vk is not (0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5))
                {
                    int mods = BuildModifiers();
                    var binding = new HotkeyBinding
                    {
                        Type      = HotkeyInputType.Keyboard,
                        Code      = vk,
                        Modifiers = mods,
                        Display   = BuildKeyDisplay(vk, mods),
                    };
                    HandleBinding(binding);
                }
            }
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var ms   = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int msg  = (int)wParam;
                int code = 0;
                string? display = null;

                switch (msg)
                {
                    case WM_LBUTTONDOWN: code = WM_LBUTTONDOWN; display = "Clic gauche";  break;
                    case WM_RBUTTONDOWN: code = WM_RBUTTONDOWN; display = "Clic droit";   break;
                    case WM_MBUTTONDOWN: code = WM_MBUTTONDOWN; display = "Molette";       break;
                    case WM_XBUTTONDOWN:
                        int xBtn = (int)(ms.mouseData >> 16) & 0xFFFF;
                        if (xBtn == 1) { code = 1; display = "Souris X1"; }
                        else           { code = 2; display = "Souris X2"; }
                        break;
                }

                if (display != null)
                {
                    var binding = new HotkeyBinding
                    {
                        Type    = HotkeyInputType.Mouse,
                        Code    = code,
                        Display = display,
                    };
                    HandleBinding(binding);
                }
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        // ── DirectInput (joystick / volant) ───────────────────────────────────

        private void StartDirectInput()
        {
            try
            {
                _directInput = new DirectInput();
                var deviceGuids = _directInput.GetDevices(DeviceType.Joystick,    DeviceEnumerationFlags.AllDevices)
                    .Concat(_directInput.GetDevices(DeviceType.Gamepad,   DeviceEnumerationFlags.AllDevices))
                    .Concat(_directInput.GetDevices(DeviceType.Driving,   DeviceEnumerationFlags.AllDevices))
                    .Concat(_directInput.GetDevices(DeviceType.Supplemental, DeviceEnumerationFlags.AllDevices))
                    .DistinctBy(d => d.InstanceGuid)
                    .ToList();

                foreach (var info in deviceGuids)
                {
                    try
                    {
                        var js = new Joystick(_directInput, info.InstanceGuid);
                        js.Acquire();
                        js.Poll();
                        var state   = js.GetCurrentState();
                        _prevButtons[js] = new bool[state.Buttons.Length];
                        _joysticks.Add(js);
                    }
                    catch { /* device not available */ }
                }

                if (_joysticks.Count == 0) return;

                _jsRunning = true;
                _jsThread  = new Thread(JoystickPollLoop)
                {
                    IsBackground = true,
                    Name         = "HotkeyService.JoystickPoll"
                };
                _jsThread.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HotkeyService] DirectInput init failed: {ex.Message}");
            }
        }

        private void JoystickPollLoop()
        {
            while (_jsRunning)
            {
                foreach (var js in _joysticks)
                {
                    try
                    {
                        js.Poll();
                        var state = js.GetCurrentState();
                        var prev  = _prevButtons[js];

                        for (int i = 0; i < state.Buttons.Length && i < prev.Length; i++)
                        {
                            if (state.Buttons[i] && !prev[i])
                            {
                                // Button just pressed (edge detection)
                                string devName = TruncateName(js.Information.ProductName, 20);
                                var binding = new HotkeyBinding
                                {
                                    Type    = HotkeyInputType.Joystick,
                                    Code    = i,
                                    Device  = js.Information.InstanceGuid.ToString(),
                                    Display = $"{devName} Btn {i + 1}",
                                };
                                HandleBinding(binding);
                            }
                            prev[i] = state.Buttons[i];
                        }
                    }
                    catch { /* device disconnected */ }
                }
                Thread.Sleep(50);
            }
        }

        // ── Core dispatch ─────────────────────────────────────────────────────

        private void HandleBinding(HotkeyBinding incoming)
        {
            if (_captureActive && _captureCallback != null)
            {
                // Capture mode — return binding to UI, don't toggle
                var cb = _captureCallback;
                _captureCallback = null;
                _captureActive   = false;
                Application.Current?.Dispatcher.Invoke(() => cb(incoming));
                return;
            }

            // Normal mode — find matching overlay and toggle
            lock (_bindings)
            {
                foreach (var (key, b) in _bindings)
                {
                    if (BindingsMatch(b, incoming))
                    {
                        Application.Current?.Dispatcher.Invoke(() => _mgr.ToggleOverlay(key));
                        break;
                    }
                }
            }
        }

        private static bool BindingsMatch(HotkeyBinding registered, HotkeyBinding incoming)
        {
            if (registered.Type != incoming.Type) return false;
            if (registered.Code != incoming.Code) return false;
            if (registered.Type == HotkeyInputType.Keyboard && registered.Modifiers != incoming.Modifiers) return false;
            if (registered.Type == HotkeyInputType.Joystick  && registered.Device != incoming.Device) return false;
            return true;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int BuildModifiers()
        {
            int m = 0;
            if ((GetAsyncKeyState(0x11) & 0x8000) != 0) m |= 1; // Ctrl
            if ((GetAsyncKeyState(0x12) & 0x8000) != 0) m |= 2; // Alt
            if ((GetAsyncKeyState(0x10) & 0x8000) != 0) m |= 4; // Shift
            return m;
        }

        private static string BuildKeyDisplay(int vk, int mods)
        {
            var parts = new List<string>();
            if ((mods & 1) != 0) parts.Add("Ctrl");
            if ((mods & 2) != 0) parts.Add("Alt");
            if ((mods & 4) != 0) parts.Add("Shift");
            parts.Add(KeyName(vk));
            return string.Join("+", parts);
        }

        private static string KeyName(int vk) => vk switch
        {
            >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",     // F1–F24
            >= 0x30 and <= 0x39 => ((char)vk).ToString(), // 0–9
            >= 0x41 and <= 0x5A => ((char)vk).ToString(), // A–Z
            0x20 => "Espace", 0x09 => "Tab",  0x0D => "Entrée",
            0x1B => "Échap",  0x08 => "Retour arrière",
            0x2E => "Suppr",  0x2D => "Inser",
            0x21 => "PgPréc", 0x22 => "PgSuiv",
            0x23 => "Fin",    0x24 => "Début",
            0x25 => "←", 0x26 => "↑", 0x27 => "→", 0x28 => "↓",
            0x6E => "Num.", 0x6A => "Num*", 0x6B => "Num+",
            0x6D => "Num-",  0x6F => "Num/",
            >= 0x60 and <= 0x69 => $"Num{vk - 0x60}",   // Numpad 0–9
            0xBB => "+",  0xBD => "-",  0xBE => ".",
            0xBC => ",",  0xBA => "Ù",  0xDB => ")",
            _ => $"0x{vk:X2}"
        };

        private static string TruncateName(string name, int max) =>
            name.Length <= max ? name.Trim() : name[..max].Trim() + "…";

        // ── Dispose ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            _jsRunning = false;

            if (_keyboardHook != IntPtr.Zero) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; }
            if (_mouseHook    != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook);    _mouseHook    = IntPtr.Zero; }

            foreach (var js in _joysticks)
            {
                try { js.Unacquire(); js.Dispose(); } catch { }
            }
            _joysticks.Clear();
            _directInput?.Dispose();
            _directInput = null;
        }
    }
}
