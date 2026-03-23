using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LMUOverlay.Models;
using LMUOverlay.Views.Overlays;

namespace LMUOverlay.VR
{
    /// <summary>
    /// Manages SteamVR overlay rendering for all active overlays.
    /// Captures WPF visual content and submits it as VR overlay textures.
    /// </summary>
    public class VROverlayService : IDisposable
    {
        private VROverlayInterface? _overlay;
        private readonly Dictionary<string, VROverlayInstance> _vrOverlays = new();
        private bool _vrActive;

        public bool IsVRActive => _vrActive;
        public string LastError { get; private set; } = "";

        public event EventHandler<bool>? VRStatusChanged;

        // ================================================================
        // VR OVERLAY POSITIONING PRESETS
        // ================================================================

        /// <summary>
        /// Default positions for overlays in VR space.
        /// HMD-relative: x=right, y=up, z=negative=toward face
        /// </summary>
        private static readonly Dictionary<string, (float x, float y, float z, float width)> DefaultVRPositions = new()
        {
            ["ProximityRadar"]       = (-0.4f, -0.2f, -0.8f, 0.20f),
            ["StandingsOverall"]     = (-0.5f,  0.1f, -1.0f, 0.40f),
            ["StandingsRelative"]    = (-0.5f, -0.1f, -1.0f, 0.30f),
            ["TrackMap"]             = ( 0.5f,  0.1f, -1.0f, 0.25f),
            ["InputGraph"]           = ( 0.0f, -0.4f, -0.8f, 0.30f),
            ["GapTimer"]             = ( 0.3f, -0.2f, -0.8f, 0.20f),
            ["Weather"]              = ( 0.5f, -0.2f, -0.8f, 0.15f),
            ["Flags"]                = ( 0.0f,  0.3f, -0.7f, 0.15f),
            ["TireInfo"]             = ( 0.4f, -0.3f, -0.9f, 0.25f),
            ["FuelInfo"]             = (-0.3f, -0.3f, -0.9f, 0.18f),
            ["DeltaTime"]            = ( 0.0f,  0.2f, -0.6f, 0.18f),
            ["PitStrategy"]          = (-0.4f, -0.1f, -0.9f, 0.22f),
            ["Damage"]               = ( 0.4f,  0.1f, -0.9f, 0.18f),
            ["LapHistory"]           = (-0.5f,  0.2f, -1.0f, 0.30f),
            ["GForce"]               = ( 0.3f, -0.3f, -0.8f, 0.18f),
            ["Dashboard"]            = ( 0.0f, -0.3f, -0.6f, 0.25f),
            ["RelativeAheadBehind"]  = ( 0.0f,  0.15f, -0.7f, 0.22f),
            ["TrackLimits"]          = (-0.3f,  0.2f, -0.9f, 0.20f),
        };

        // ================================================================
        // INIT / SHUTDOWN
        // ================================================================

        public bool Initialize()
        {
            if (!OpenVRInterop.Init(out string error))
            {
                LastError = error;
                return false;
            }

            _overlay = OpenVRInterop.GetOverlayInterface(out error);
            if (_overlay == null)
            {
                LastError = error;
                OpenVRInterop.Shutdown();
                return false;
            }

            _vrActive = true;
            VRStatusChanged?.Invoke(this, true);
            return true;
        }

        public void Shutdown()
        {
            foreach (var instance in _vrOverlays.Values)
                DestroyVROverlay(instance);

            _vrOverlays.Clear();

            OpenVRInterop.Shutdown();
            _vrActive = false;
            _overlay = null;
            VRStatusChanged?.Invoke(this, false);
        }

        // ================================================================
        // OVERLAY MANAGEMENT
        // ================================================================

        /// <summary>
        /// Register a WPF overlay window for VR rendering.
        /// </summary>
        public void RegisterOverlay(string key, BaseOverlayWindow window, OverlaySettings settings)
        {
            if (!_vrActive || _overlay == null) return;

            if (_vrOverlays.ContainsKey(key))
                return;

            string vrKey = $"douze.assistance.{key.ToLowerInvariant()}";
            string vrName = $"Douze - {settings.Name}";

            ulong handle = 0;
            var err = _overlay.CreateOverlay(vrKey, vrName, ref handle);
            if (err != EVROverlayError.None)
            {
                LastError = $"Erreur création overlay VR '{key}': {err}";
                return;
            }

            // Get position from defaults or use center
            var pos = DefaultVRPositions.GetValueOrDefault(key, (0, 0, -1.0f, 0.25f));

            // Set size
            _overlay.SetOverlayWidthInMeters(handle, pos.width);

            // Set alpha
            _overlay.SetOverlayAlpha(handle, (float)settings.Opacity);

            // Position relative to HMD (device 0)
            var transform = HmdMatrix34_t.FromPosition(pos.x, pos.y, pos.z);
            _overlay.SetOverlayTransformTrackedDeviceRelative(handle, 0, ref transform);

            _overlay.ShowOverlay(handle);

            _vrOverlays[key] = new VROverlayInstance
            {
                Key = key,
                Handle = handle,
                Window = window,
                Settings = settings,
                Position = (pos.x, pos.y, pos.z),
                WidthMeters = pos.width
            };
        }

        /// <summary>
        /// Unregister and destroy a VR overlay.
        /// </summary>
        public void UnregisterOverlay(string key)
        {
            if (_vrOverlays.TryGetValue(key, out var instance))
            {
                DestroyVROverlay(instance);
                _vrOverlays.Remove(key);
            }
        }

        /// <summary>
        /// Update all VR overlays: capture WPF visuals and submit as textures.
        /// Call this from the UI thread on each update tick.
        /// </summary>
        public void UpdateAll()
        {
            if (!_vrActive || _overlay == null) return;

            foreach (var kvp in _vrOverlays)
            {
                var inst = kvp.Value;
                if (!inst.Settings.IsEnabled)
                {
                    _overlay.HideOverlay(inst.Handle);
                    continue;
                }

                _overlay.ShowOverlay(inst.Handle);
                _overlay.SetOverlayAlpha(inst.Handle, (float)inst.Settings.Opacity);

                try
                {
                    CaptureAndSubmit(inst);
                }
                catch
                {
                    // Skip frame on error
                }
            }
        }

        /// <summary>
        /// Reposition a specific overlay in VR space.
        /// </summary>
        public void SetOverlayPosition(string key, float x, float y, float z, float widthMeters = -1)
        {
            if (!_vrActive || _overlay == null) return;
            if (!_vrOverlays.TryGetValue(key, out var inst)) return;

            inst.Position = (x, y, z);
            var transform = HmdMatrix34_t.FromPosition(x, y, z);
            _overlay.SetOverlayTransformTrackedDeviceRelative(inst.Handle, 0, ref transform);

            if (widthMeters > 0)
            {
                inst.WidthMeters = widthMeters;
                _overlay.SetOverlayWidthInMeters(inst.Handle, widthMeters);
            }
        }

        // ================================================================
        // WPF VISUAL CAPTURE → VR TEXTURE
        // ================================================================

        private void CaptureAndSubmit(VROverlayInstance inst)
        {
            var window = inst.Window;

            // Get the actual content element
            if (window.Content is not FrameworkElement content) return;

            double w = content.ActualWidth;
            double h = content.ActualHeight;
            if (w < 1 || h < 1) return;

            // Scale factor for VR readability (render at higher resolution)
            double scale = Math.Max(1.0, inst.Settings.Scale);
            int pixelW = (int)(w * scale);
            int pixelH = (int)(h * scale);

            if (pixelW < 1 || pixelH < 1) return;

            // Render WPF visual to bitmap
            var rtb = new RenderTargetBitmap(pixelW, pixelH, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            rtb.Render(content);

            // Convert to BGRA byte array
            int stride = pixelW * 4;
            byte[] pixels = new byte[pixelH * stride];
            rtb.CopyPixels(pixels, stride, 0);

            // Convert BGRA → RGBA (OpenVR expects RGBA)
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i];
                pixels[i] = pixels[i + 2];     // R
                pixels[i + 2] = b;              // B
            }

            // Pin and submit to OpenVR
            GCHandle pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                _overlay!.SetOverlayRaw(inst.Handle, pinned.AddrOfPinnedObject(),
                    (uint)pixelW, (uint)pixelH, 4);
            }
            finally
            {
                pinned.Free();
            }
        }

        private void DestroyVROverlay(VROverlayInstance instance)
        {
            if (_overlay != null)
            {
                _overlay.HideOverlay(instance.Handle);
                _overlay.DestroyOverlay(instance.Handle);
            }
        }

        public void Dispose()
        {
            if (_vrActive) Shutdown();
        }

        // ================================================================
        // INTERNAL STATE
        // ================================================================

        private class VROverlayInstance
        {
            public string Key { get; set; } = "";
            public ulong Handle { get; set; }
            public BaseOverlayWindow Window { get; set; } = null!;
            public OverlaySettings Settings { get; set; } = null!;
            public (float x, float y, float z) Position { get; set; }
            public float WidthMeters { get; set; }
        }
    }
}
