// OpenXR overlay backend for Douze Assistance.
//
// Uses XR_EXTX_overlay to run as a secondary overlay session on top of
// the main XR application (Le Mans Ultimate).
//
// Supported runtimes:
//   ✔ Meta / Oculus (native OpenXR runtime)
//   ✔ SteamVR configured as OpenXR runtime
//   ✔ Any runtime that advertises XR_EXTX_overlay
//
// Dependencies:
//   Silk.NET.OpenXR  (NuGet)  – OpenXR API bindings
//   D3D11 helpers   (P/Invoke) – no extra NuGet package required

using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LMUOverlay.Models;
using LMUOverlay.Views.Overlays;
using Microsoft.Win32;
using Silk.NET.Core;
using Silk.NET.OpenXR;

namespace LMUOverlay.VR
{
    /// <summary>
    /// OpenXR VR backend.  Runs a dedicated frame-loop thread and submits
    /// each WPF overlay as an XrCompositionLayerQuad.
    /// </summary>
    public sealed class OpenXRService : IVRService
    {
        // ── OpenXR handles ────────────────────────────────────────────────────
        private XR?      _xr;
        private Instance _instance;
        private Session  _session;
        private Space    _localSpace;
        private ulong    _systemId;

        // ── D3D11 (raw COM pointers via P/Invoke) ──────────────────────────────
        private IntPtr _d3dDevice;   // ID3D11Device*
        private IntPtr _d3dContext;  // ID3D11DeviceContext*

        // ── Per-overlay state ─────────────────────────────────────────────────
        private sealed class XROvl
        {
            public string            Key      = "";
            public BaseOverlayWindow Window   = null!;
            public OverlaySettings   Settings = null!;
            public Swapchain         Swapchain;
            public IntPtr[]          Images   = [];  // ID3D11Texture2D* per swapchain slot
            public int W, H;
            public (float x, float y, float z, float widthM) Pos;

            // Double-buffered pixel data: WPF UI thread writes, XR frame thread reads
            public byte[]? PixelBuf;
            public bool    Dirty;
            public readonly object Lock = new();
        }

        private readonly Dictionary<string, XROvl> _ovls = new();
        private Thread?       _frameThread;
        private volatile bool _running;

        // ── IVRService ────────────────────────────────────────────────────────
        public bool   IsVRActive { get; private set; }
        public string LastError  { get; private set; } = "";
        public event EventHandler<bool>? VRStatusChanged;

        // ── Default VR positions (mirrors VROverlayService) ───────────────────
        private static readonly Dictionary<string, (float x, float y, float z, float w)>
            DefaultPos = new()
            {
                ["ProximityRadar"]      = (-0.4f, -0.2f, -0.8f, 0.20f),
                ["StandingsOverall"]    = (-0.5f,  0.1f, -1.0f, 0.40f),
                ["StandingsRelative"]   = (-0.5f, -0.1f, -1.0f, 0.30f),
                ["TrackMap"]            = ( 0.5f,  0.1f, -1.0f, 0.25f),
                ["InputGraph"]          = ( 0.0f, -0.4f, -0.8f, 0.30f),
                ["GapTimer"]            = ( 0.3f, -0.2f, -0.8f, 0.20f),
                ["Weather"]             = ( 0.5f, -0.2f, -0.8f, 0.15f),
                ["Flags"]               = ( 0.0f,  0.3f, -0.7f, 0.15f),
                ["TireInfo"]            = ( 0.4f, -0.3f, -0.9f, 0.25f),
                ["FuelInfo"]            = (-0.3f, -0.3f, -0.9f, 0.18f),
                ["DeltaTime"]           = ( 0.0f,  0.2f, -0.6f, 0.18f),
                ["PitStrategy"]         = (-0.4f, -0.1f, -0.9f, 0.22f),
                ["Damage"]              = ( 0.4f,  0.1f, -0.9f, 0.18f),
                ["LapHistory"]          = (-0.5f,  0.2f, -1.0f, 0.30f),
                ["GForce"]              = ( 0.3f, -0.3f, -0.8f, 0.18f),
                ["Dashboard"]           = ( 0.0f, -0.3f, -0.6f, 0.25f),
                ["RelativeAheadBehind"] = ( 0.0f,  0.15f,-0.7f, 0.22f),
                ["TrackLimits"]         = (-0.3f,  0.2f, -0.9f, 0.20f),
            };

        // =====================================================================
        // Runtime detection
        // =====================================================================

        /// <summary>
        /// Returns true when an OpenXR runtime JSON is registered in HKLM.
        /// Does NOT guarantee XR_EXTX_overlay support.
        /// </summary>
        public static bool IsRuntimeAvailable()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Khronos\OpenXR\1");
                if (key == null) return false;
                var path = key.GetValue("ActiveRuntime") as string;
                return !string.IsNullOrEmpty(path) && File.Exists(path);
            }
            catch { return false; }
        }

        // =====================================================================
        // Initialize
        // =====================================================================

        public bool Initialize()
        {
            try
            {
                _xr = XR.GetApi();

                // 1. Check required extension
                if (!HasExtension("XR_EXTX_overlay"))
                {
                    LastError = "Le runtime OpenXR actif ne supporte pas XR_EXTX_overlay.\n" +
                                "Essayez un runtime Meta/Oculus ou SteamVR récent.";
                    return false;
                }

                // 2. Create XrInstance
                if (!CreateInstance()) return false;

                // 3. Get XrSystemId (HMD)
                if (!GetSystem()) return false;

                // 4. D3D11 device (required by XrGraphicsBindingD3D11KHR)
                if (!CreateD3D11()) return false;

                // 5. Satisfy xrGetD3D11GraphicsRequirementsKHR requirement
                SatisfyD3D11Requirements();

                // 6. XrSession with overlay + D3D11 binding chained
                if (!CreateSession()) return false;

                // 7. Reference space
                if (!CreateSpace()) return false;

                // 8. Begin session
                BeginSession();

                // 9. Start frame-loop thread
                _running     = true;
                _frameThread = new Thread(FrameLoop)
                    { IsBackground = true, Name = "XR-FrameLoop" };
                _frameThread.Start();

                IsVRActive = true;
                VRStatusChanged?.Invoke(this, true);
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"OpenXR Init: {ex.Message}";
                return false;
            }
        }

        // ── Extension enumeration ─────────────────────────────────────────────

        private unsafe bool HasExtension(string name)
        {
            uint count = 0;
            _xr!.EnumerateInstanceExtensionProperties((byte*)null, 0, ref count, null);
            if (count == 0) return false;

            var props = new ExtensionProperties[count];
            for (uint i = 0; i < count; i++)
                props[i] = new ExtensionProperties { Type = StructureType.ExtensionProperties };

            fixed (ExtensionProperties* p = props)
                _xr.EnumerateInstanceExtensionProperties((byte*)null, count, ref count, p);

            foreach (var prop in props)
            {
                // ExtensionName is a fixed-size buffer inside a struct — access via pointer arithmetic
                var extName = new string((sbyte*)prop.ExtensionName, 0, 128).TrimEnd('\0');
                if (extName == name) return true;
            }
            return false;
        }

        // ── Instance ──────────────────────────────────────────────────────────

        private unsafe bool CreateInstance()
        {
            var exts = new[] { "XR_EXTX_overlay", "XR_KHR_D3D11_enable" };
            var extPtrs = exts.Select(e =>
            {
                byte[] b = Encoding.ASCII.GetBytes(e + "\0");
                IntPtr p = Marshal.AllocHGlobal(b.Length);
                Marshal.Copy(b, 0, p, b.Length);
                return p;
            }).ToArray();

            try
            {
                var appInfo = new ApplicationInfo { ApiVersion = new Version64(1, 0, 0) };
                CopyAsciiFixed(Encoding.ASCII.GetBytes("Douze Assistance"), appInfo.ApplicationName, 128);
                CopyAsciiFixed(Encoding.ASCII.GetBytes("LMUOverlay"),       appInfo.EngineName,      128);
                appInfo.ApplicationVersion = 1;
                appInfo.EngineVersion      = 1;

                fixed (IntPtr* ep = extPtrs)
                {
                    var ci = new InstanceCreateInfo
                    {
                        Type                  = StructureType.InstanceCreateInfo,
                        ApplicationInfo       = appInfo,
                        EnabledExtensionCount = (uint)exts.Length,
                        EnabledExtensionNames = (byte**)ep
                    };
                    var result = _xr!.CreateInstance(ref ci, ref _instance);
                    if (result != Result.Success)
                    { LastError = $"xrCreateInstance: {result}"; return false; }
                }
                return true;
            }
            finally { foreach (var p in extPtrs) Marshal.FreeHGlobal(p); }
        }

        // ── System ────────────────────────────────────────────────────────────

        private bool GetSystem()
        {
            var gi = new SystemGetInfo
            {
                Type       = StructureType.SystemGetInfo,
                FormFactor = FormFactor.HeadMountedDisplay
            };
            var result = _xr!.GetSystem(_instance, ref gi, ref _systemId);
            if (result != Result.Success)
            { LastError = $"xrGetSystem: {result}"; return false; }
            return true;
        }

        // ── D3D11 device (raw P/Invoke — no extra NuGet required) ─────────────

        private bool CreateD3D11()
        {
            try
            {
                (_d3dDevice, _d3dContext) = D3D11Native.CreateDevice();
                return true;
            }
            catch (Exception ex)
            { LastError = $"D3D11CreateDevice: {ex.Message}"; return false; }
        }

        // ── Satisfy xrGetD3D11GraphicsRequirementsKHR (required by spec) ──────

        private unsafe void SatisfyD3D11Requirements()
        {
            // Dynamically load the extension function via PfnVoidFunction
            var pfn = new Silk.NET.Core.PfnVoidFunction();
            byte[] nameBytes = Encoding.ASCII.GetBytes("xrGetD3D11GraphicsRequirementsKHR\0");
            fixed (byte* nb = nameBytes)
                _xr!.GetInstanceProcAddr(_instance, nb, &pfn);

            var fnPtr = (IntPtr)(void*)pfn;
            if (fnPtr == IntPtr.Zero) return;

            var reqs = new XrGraphicsRequirementsD3D11KHR
            {
                Type = StructureType.GraphicsRequirementsD3D11Khr,
                Next = null
            };
            var fn = Marshal.GetDelegateForFunctionPointer<GetD3D11GraphicsRequirementsFn>(fnPtr);
            fn(_instance, _systemId, ref reqs);
            // We accept any feature level; just calling the function satisfies the spec.
        }

        // ── Session (overlay + D3D11 binding chained via Next pointer) ─────────

        private unsafe bool CreateSession()
        {
            var overlayInfo = new SessionCreateInfoOverlayEXTX
            {
                Type                  = StructureType.SessionCreateInfoOverlayExtx,
                Next                  = null,
                SessionLayersPlacement = 1   // z-order above main session
            };

            // D3D11 binding — uses our manual struct to avoid Silk.NET.Direct3D11 dependency
            var gfxBinding = new XrGraphicsBindingD3D11KHR
            {
                Type   = StructureType.GraphicsBindingD3D11Khr,
                Next   = &overlayInfo,
                Device = _d3dDevice
            };

            var si = new SessionCreateInfo
            {
                Type     = StructureType.SessionCreateInfo,
                Next     = &gfxBinding,
                SystemId = _systemId
            };

            var result = _xr!.CreateSession(_instance, ref si, ref _session);
            if (result != Result.Success)
            { LastError = $"xrCreateSession: {result}"; return false; }
            return true;
        }

        // ── Reference space ───────────────────────────────────────────────────

        private bool CreateSpace()
        {
            var sci = new ReferenceSpaceCreateInfo
            {
                Type               = StructureType.ReferenceSpaceCreateInfo,
                ReferenceSpaceType = ReferenceSpaceType.Local,
                PoseInReferenceSpace = new Posef
                {
                    Orientation = new Quaternionf(0, 0, 0, 1),
                    Position    = new Vector3f(0, 0, 0)
                }
            };
            var result = _xr!.CreateReferenceSpace(_session, ref sci, ref _localSpace);
            if (result != Result.Success)
            { LastError = $"xrCreateReferenceSpace: {result}"; return false; }
            return true;
        }

        private void BeginSession()
        {
            var bi = new SessionBeginInfo
            {
                Type = StructureType.SessionBeginInfo,
                PrimaryViewConfigurationType = ViewConfigurationType.PrimaryStereo
            };
            _xr!.BeginSession(_session, ref bi);
        }

        // =====================================================================
        // Overlay registration
        // =====================================================================

        public void RegisterOverlay(string key, BaseOverlayWindow window, OverlaySettings settings)
        {
            if (_ovls.ContainsKey(key)) return;
            var pos = DefaultPos.GetValueOrDefault(key, (0f, 0f, -1.0f, 0.25f));
            _ovls[key] = new XROvl
                { Key = key, Window = window, Settings = settings, Pos = pos };
        }

        public void UnregisterOverlay(string key)
        {
            if (_ovls.TryGetValue(key, out var ovl))
            {
                DestroySwapchain(ovl);
                _ovls.Remove(key);
            }
        }

        /// <summary>Called from OverlayManager on the UI thread each tick.</summary>
        public void EnsureSwapchainForOverlay(string key, BaseOverlayWindow _)
        {
            if (_ovls.TryGetValue(key, out var ovl))
                EnsureSwapchain(ovl);
        }

        // ── Create swapchain once the window has a valid pixel size ───────────

        private unsafe bool EnsureSwapchain(XROvl ovl)
        {
            if (ovl.Swapchain.Handle != 0) return true;

            var content = ovl.Window.Content as FrameworkElement;
            if (content == null) return false;

            double scale = Math.Max(1.0, ovl.Settings.Scale);
            int w = Math.Max(1, (int)(content.ActualWidth  * scale));
            int h = Math.Max(1, (int)(content.ActualHeight * scale));
            if (w < 1 || h < 1) return false;

            ovl.W = w; ovl.H = h;

            var sci = new SwapchainCreateInfo
            {
                Type        = StructureType.SwapchainCreateInfo,
                UsageFlags  = SwapchainUsageFlags.ColorAttachmentBit |
                              SwapchainUsageFlags.TransferDstBit,
                Format      = 28L,  // DXGI_FORMAT_R8G8B8A8_UNORM = 28
                SampleCount = 1,
                Width       = (uint)w,
                Height      = (uint)h,
                FaceCount   = 1,
                ArraySize   = 1,
                MipCount    = 1
            };
            if (_xr!.CreateSwapchain(_session, ref sci, ref ovl.Swapchain) != Result.Success)
                return false;

            // Enumerate swapchain images
            uint imgCount = 0;
            _xr.EnumerateSwapchainImages(ovl.Swapchain, 0, ref imgCount, null);

            var xrImgs = new XrSwapchainImageD3D11KHR[imgCount];
            for (uint i = 0; i < imgCount; i++)
                xrImgs[i] = new XrSwapchainImageD3D11KHR
                    { Type = StructureType.SwapchainImageD3D11Khr, Next = null };

            fixed (XrSwapchainImageD3D11KHR* p = xrImgs)
                _xr.EnumerateSwapchainImages(ovl.Swapchain, imgCount, ref imgCount,
                    (SwapchainImageBaseHeader*)p);

            ovl.Images = xrImgs.Select(img => img.Texture).ToArray();
            return true;
        }

        private void DestroySwapchain(XROvl ovl)
        {
            if (ovl.Swapchain.Handle != 0 && _xr != null)
            {
                _xr.DestroySwapchain(ovl.Swapchain);
                ovl.Swapchain = default;
            }
            ovl.Images = [];
        }

        // =====================================================================
        // UpdateAll  (UI thread — captures WPF bitmaps)
        // =====================================================================

        public void UpdateAll()
        {
            if (!IsVRActive) return;

            foreach (var ovl in _ovls.Values)
            {
                if (!ovl.Settings.IsEnabled) continue;

                var content = ovl.Window.Content as FrameworkElement;
                if (content == null) continue;

                double scale = Math.Max(1.0, ovl.Settings.Scale);
                int w = Math.Max(1, (int)(content.ActualWidth  * scale));
                int h = Math.Max(1, (int)(content.ActualHeight * scale));
                if (w < 1 || h < 1) continue;

                var rtb    = new RenderTargetBitmap(w, h, 96*scale, 96*scale,
                                 System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(content);

                int    stride = w * 4;
                byte[] pixels = new byte[h * stride];
                rtb.CopyPixels(pixels, stride, 0);

                // BGRA → RGBA
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    byte b = pixels[i];
                    pixels[i]     = pixels[i + 2];
                    pixels[i + 2] = b;
                }

                lock (ovl.Lock) { ovl.PixelBuf = pixels; ovl.Dirty = true; }
            }
        }

        // =====================================================================
        // SetOverlayPosition
        // =====================================================================

        public void SetOverlayPosition(string key, float x, float y, float z, float widthMeters = -1)
        {
            if (_ovls.TryGetValue(key, out var ovl))
            {
                float wm = widthMeters > 0 ? widthMeters : ovl.Pos.widthM;
                ovl.Pos = (x, y, z, wm);
            }
        }

        // =====================================================================
        // Frame loop  (dedicated background thread)
        // =====================================================================

        private unsafe void FrameLoop()
        {
            while (_running)
            {
                try
                {
                    PollEvents();

                    var frameState = new FrameState { Type = StructureType.FrameState };
                    if (_xr!.WaitFrame(_session, null, ref frameState) != Result.Success)
                    { Thread.Sleep(16); continue; }

                    _xr.BeginFrame(_session, null);

                    // Build composition layer list
                    int  maxLayers = _ovls.Count;
                    var  quadBuf   = stackalloc CompositionLayerQuad[maxLayers];
                    var  ptrBuf    = stackalloc CompositionLayerBaseHeader*[maxLayers];
                    int  layerCnt  = 0;

                    foreach (var ovl in _ovls.Values)
                    {
                        if (!ovl.Settings.IsEnabled || ovl.Swapchain.Handle == 0) continue;

                        byte[]? pixels; bool dirty;
                        lock (ovl.Lock) { pixels = ovl.PixelBuf; dirty = ovl.Dirty; ovl.Dirty = false; }
                        if (dirty && pixels != null) UploadPixels(ovl, pixels);

                        float aspect  = ovl.H > 0 ? (float)ovl.W / ovl.H : 1f;
                        float heightM = ovl.Pos.widthM / aspect;

                        quadBuf[layerCnt] = new CompositionLayerQuad
                        {
                            Type          = StructureType.CompositionLayerQuad,
                            Space         = _localSpace,
                            EyeVisibility = EyeVisibility.Both,
                            SubImage = new SwapchainSubImage
                            {
                                Swapchain       = ovl.Swapchain,
                                ImageArrayIndex = 0,
                                ImageRect = new Rect2Di
                                {
                                    Offset = new Offset2Di(0, 0),
                                    Extent = new Extent2Di(ovl.W, ovl.H)
                                }
                            },
                            Pose = new Posef
                            {
                                Orientation = new Quaternionf(0f, 0f, 0f, 1f),
                                Position    = new Vector3f(ovl.Pos.x, ovl.Pos.y, ovl.Pos.z)
                            },
                            Size = new Extent2Df(ovl.Pos.widthM, heightM)
                        };
                        ptrBuf[layerCnt] = (CompositionLayerBaseHeader*)(quadBuf + layerCnt);
                        layerCnt++;
                    }

                    var endInfo = new FrameEndInfo
                    {
                        Type                 = StructureType.FrameEndInfo,
                        DisplayTime          = frameState.PredictedDisplayTime,
                        EnvironmentBlendMode = EnvironmentBlendMode.Opaque,
                        LayerCount           = (uint)layerCnt,
                        Layers               = layerCnt > 0 ? ptrBuf : null
                    };
                    _xr.EndFrame(_session, ref endInfo);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OpenXR] FrameLoop: {ex.Message}");
                    Thread.Sleep(16);
                }
            }
        }

        // ── Upload pixel buffer to D3D11 texture via COM vtable ───────────────

        private unsafe void UploadPixels(XROvl ovl, byte[] pixels)
        {
            if (ovl.Images.Length == 0 || _d3dContext == IntPtr.Zero) return;

            uint imgIdx = 0;
            var  acqI   = new SwapchainImageAcquireInfo
                { Type = StructureType.SwapchainImageAcquireInfo };
            if (_xr!.AcquireSwapchainImage(ovl.Swapchain, ref acqI, ref imgIdx) != Result.Success)
                return;

            var waitI = new SwapchainImageWaitInfo
                { Type = StructureType.SwapchainImageWaitInfo, Timeout = 100_000_000L };
            if (_xr.WaitSwapchainImage(ovl.Swapchain, ref waitI) != Result.Success)
            {
                var r2 = new SwapchainImageReleaseInfo { Type = StructureType.SwapchainImageReleaseInfo };
                _xr.ReleaseSwapchainImage(ovl.Swapchain, ref r2);
                return;
            }

            if (imgIdx < ovl.Images.Length)
            {
                fixed (byte* pb = pixels)
                    D3D11Native.UpdateSubresource(
                        _d3dContext, ovl.Images[imgIdx], (IntPtr)pb, (uint)(ovl.W * 4));
            }

            var relI = new SwapchainImageReleaseInfo { Type = StructureType.SwapchainImageReleaseInfo };
            _xr.ReleaseSwapchainImage(ovl.Swapchain, ref relI);
        }

        // ── Event polling ──────────────────────────────────────────────────────

        private void PollEvents()
        {
            var buf = new EventDataBuffer { Type = StructureType.EventDataBuffer };
            while (_xr!.PollEvent(_instance, ref buf) == Result.Success)
                buf = new EventDataBuffer { Type = StructureType.EventDataBuffer };
        }

        // =====================================================================
        // Shutdown / Dispose
        // =====================================================================

        public void Shutdown()
        {
            _running = false;
            _frameThread?.Join(2000);

            foreach (var ovl in _ovls.Values) DestroySwapchain(ovl);
            _ovls.Clear();

            if (_xr != null)
            {
                if (_session.Handle    != 0) { _xr.EndSession(_session); _xr.DestroySession(_session); }
                if (_localSpace.Handle != 0) _xr.DestroySpace(_localSpace);
                if (_instance.Handle   != 0) _xr.DestroyInstance(_instance);
            }

            D3D11Native.Release(_d3dContext);
            D3D11Native.Release(_d3dDevice);
            _d3dContext = IntPtr.Zero;
            _d3dDevice  = IntPtr.Zero;

            _xr?.Dispose();
            _xr = null;

            IsVRActive = false;
            VRStatusChanged?.Invoke(this, false);
        }

        public void Dispose() => Shutdown();

        // =====================================================================
        // Helpers
        // =====================================================================

        private static unsafe void CopyAsciiFixed(byte[] src, byte* dest, int maxLen)
        {
            int len = Math.Min(src.Length, maxLen - 1);
            for (int i = 0; i < len; i++) dest[i] = src[i];
            dest[len] = 0;
        }

        // ── D3D11 minimal P/Invoke (no extra NuGet package) ───────────────────

        private static class D3D11Native
        {
            private const uint SDK_VERSION = 7;

            [DllImport("d3d11.dll", CallingConvention = CallingConvention.Winapi)]
            private static extern int D3D11CreateDevice(
                IntPtr pAdapter, int DriverType, IntPtr Software, uint Flags,
                IntPtr pFeatureLevels, uint FeatureLevels, uint SDKVersion,
                out IntPtr ppDevice, out uint pFeatureLevel, out IntPtr ppContext);

            public static (IntPtr device, IntPtr context) CreateDevice()
            {
                int hr = D3D11CreateDevice(
                    IntPtr.Zero, 1 /* D3D_DRIVER_TYPE_HARDWARE */, IntPtr.Zero,
                    0, IntPtr.Zero, 0, SDK_VERSION,
                    out var dev, out _, out var ctx);
                if (hr < 0) throw new COMException("D3D11CreateDevice", hr);
                return (dev, ctx);
            }

            // ID3D11DeviceContext vtable slot 48 = UpdateSubresource
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate void UpdateSubresourceFn(
                IntPtr self, IntPtr pDstResource, uint DstSubresource,
                IntPtr pDstBox, IntPtr pSrcData, uint SrcRowPitch, uint SrcDepthPitch);

            public static void UpdateSubresource(
                IntPtr ctx, IntPtr resource, IntPtr data, uint rowPitch)
            {
                var vtable = Marshal.ReadIntPtr(ctx);
                var fn     = Marshal.GetDelegateForFunctionPointer<UpdateSubresourceFn>(
                                 Marshal.ReadIntPtr(vtable, 48 * IntPtr.Size));
                fn(ctx, resource, 0, IntPtr.Zero, data, rowPitch, 0);
            }

            // IUnknown::Release = vtable slot 2
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate uint ReleaseFn(IntPtr self);

            public static void Release(IntPtr obj)
            {
                if (obj == IntPtr.Zero) return;
                var vtable = Marshal.ReadIntPtr(obj);
                Marshal.GetDelegateForFunctionPointer<ReleaseFn>(
                    Marshal.ReadIntPtr(vtable, 2 * IntPtr.Size))(obj);
            }
        }

        // ── Custom structs to avoid Silk.NET.Direct3D11 dependency ────────────

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct XrGraphicsBindingD3D11KHR
        {
            public StructureType           Type;
            public SessionCreateInfoOverlayEXTX* Next;
            public IntPtr                  Device;   // ID3D11Device*
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct XrSwapchainImageD3D11KHR
        {
            public StructureType Type;
            public void*         Next;
            public IntPtr        Texture;  // ID3D11Texture2D*
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct XrGraphicsRequirementsD3D11KHR
        {
            public StructureType Type;
            public void*         Next;
            public ulong         AdapterLuid;      // LUID (8 bytes)
            public int           MinFeatureLevel;  // D3D_FEATURE_LEVEL
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetD3D11GraphicsRequirementsFn(
            Instance instance, ulong systemId, ref XrGraphicsRequirementsD3D11KHR requirements);
    }
}
