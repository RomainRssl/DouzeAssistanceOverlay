// Minimal OpenVR C# interop for overlay applications.
// Only includes types/functions needed for VR overlay rendering.
// Requires openvr_api.dll from SteamVR (usually at: Steam\steamapps\common\SteamVR\bin\win64\openvr_api.dll)

using System.Runtime.InteropServices;

namespace LMUOverlay.VR
{
    // ================================================================
    // ENUMS
    // ================================================================

    public enum EVRInitError
    {
        None = 0,
        Init_InstallationNotFound = 100,
        Init_NotInitialized = 110,
    }

    public enum EVRApplicationType
    {
        VRApplication_Overlay = 2,
    }

    public enum ETrackingUniverseOrigin
    {
        TrackingUniverseSeated = 0,
        TrackingUniverseStanding = 1,
        TrackingUniverseRawAndUncalibrated = 2,
    }

    public enum EVROverlayError
    {
        None = 0,
        UnknownOverlay = 10,
        InvalidHandle = 11,
        PermissionDenied = 12,
    }

    // ================================================================
    // STRUCTS
    // ================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct HmdMatrix34_t
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public float[] m;

        public static HmdMatrix34_t Identity()
        {
            return new HmdMatrix34_t
            {
                m = new float[]
                {
                    1, 0, 0, 0,   // row 0: right
                    0, 1, 0, 0,   // row 1: up
                    0, 0, 1, 0    // row 2: forward
                }
            };
        }

        /// <summary>
        /// Creates a transform matrix positioned relative to the headset.
        /// x = right, y = up, z = away from face (negative = toward face)
        /// </summary>
        public static HmdMatrix34_t FromPosition(float x, float y, float z)
        {
            return new HmdMatrix34_t
            {
                m = new float[]
                {
                    1, 0, 0, x,
                    0, 1, 0, y,
                    0, 0, 1, z
                }
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HmdVector2_t
    {
        public float v0, v1;
    }

    // ================================================================
    // IVROverlay FUNCTION TABLE (vtable offsets for P/Invoke)
    // ================================================================

    /// <summary>
    /// Managed wrapper around the IVROverlay interface obtained from OpenVR.
    /// Uses Marshal.GetDelegateForFunctionPointer to call vtable functions.
    /// </summary>
    public class VROverlayInterface
    {
        private readonly IntPtr _ptr;
        private readonly IntPtr _vtable;

        // Delegate types matching IVROverlay vtable
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate EVROverlayError _CreateOverlay(IntPtr self,
            [MarshalAs(UnmanagedType.LPStr)] string key,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            ref ulong handle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate EVROverlayError _DestroyOverlay(IntPtr self, ulong handle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate EVROverlayError _ShowOverlay(IntPtr self, ulong handle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate EVROverlayError _HideOverlay(IntPtr self, ulong handle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate EVROverlayError _SetOverlayWidthInMeters(IntPtr self, ulong handle, float width);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate EVROverlayError _SetOverlayAlpha(IntPtr self, ulong handle, float alpha);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate EVROverlayError _SetOverlayTransformAbsolute(IntPtr self, ulong handle,
            ETrackingUniverseOrigin origin, ref HmdMatrix34_t mat);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate EVROverlayError _SetOverlayTransformTrackedDeviceRelative(IntPtr self, ulong handle,
            uint deviceIndex, ref HmdMatrix34_t mat);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate EVROverlayError _SetOverlayRaw(IntPtr self, ulong handle,
            IntPtr buffer, uint width, uint height, uint bytesPerPixel);

        // Vtable slot indices — IVROverlay_027 (openvr_api.cs official Valve C# bindings)
        // Source: https://github.com/ValveSoftware/openvr/blob/master/headers/openvr_api.cs
        // IMPORTANT: slot 2 = CreateSubviewOverlay (added in newer SDK) shifts everything by 1
        private const int SLOT_FindOverlay = 0;
        private const int SLOT_CreateOverlay = 1;
        // slot 2 = CreateSubviewOverlay (not used, but shifts all subsequent slots)
        private const int SLOT_DestroyOverlay = 3;
        private const int SLOT_GetOverlayKey = 4;
        private const int SLOT_GetOverlayName = 5;
        private const int SLOT_SetOverlayName = 6;
        private const int SLOT_GetOverlayImageData = 7;
        private const int SLOT_GetOverlayErrorNameFromEnum = 8;
        private const int SLOT_SetOverlayRenderingPid = 9;
        private const int SLOT_GetOverlayRenderingPid = 10;
        private const int SLOT_SetOverlayFlag = 11;
        private const int SLOT_GetOverlayFlag = 12;
        private const int SLOT_GetOverlayFlags = 13;
        private const int SLOT_SetOverlayColor = 14;
        private const int SLOT_GetOverlayColor = 15;
        private const int SLOT_SetOverlayAlpha = 16;
        private const int SLOT_GetOverlayAlpha = 17;
        private const int SLOT_SetOverlayTexelAspect = 18;
        private const int SLOT_GetOverlayTexelAspect = 19;
        private const int SLOT_SetOverlaySortOrder = 20;
        private const int SLOT_GetOverlaySortOrder = 21;
        private const int SLOT_SetOverlayWidthInMeters = 22;
        private const int SLOT_GetOverlayWidthInMeters = 23;
        private const int SLOT_SetOverlayCurvature = 24;
        private const int SLOT_GetOverlayCurvature = 25;
        private const int SLOT_SetOverlayPreCurvePitch = 26;
        private const int SLOT_GetOverlayPreCurvePitch = 27;
        private const int SLOT_SetOverlayTextureColorSpace = 28;
        private const int SLOT_GetOverlayTextureColorSpace = 29;
        private const int SLOT_SetOverlayTextureBounds = 30;
        private const int SLOT_GetOverlayTextureBounds = 31;
        private const int SLOT_GetOverlayTransformType = 32;
        private const int SLOT_SetOverlayTransformAbsolute = 33;
        private const int SLOT_GetOverlayTransformAbsolute = 34;
        private const int SLOT_SetOverlayTransformTrackedDeviceRelative = 35;
        // slots 36-42 = other transform + projection functions
        private const int SLOT_ShowOverlay = 43;
        private const int SLOT_HideOverlay = 44;
        // slots 45-61 = input/event/cursor functions
        private const int SLOT_SetOverlayRaw = 62;

        public VROverlayInterface(IntPtr interfacePtr)
        {
            _ptr = interfacePtr;
            _vtable = Marshal.ReadIntPtr(_ptr);
        }

        private T GetFunc<T>(int slot) where T : Delegate
        {
            IntPtr funcPtr = Marshal.ReadIntPtr(_vtable, slot * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer<T>(funcPtr);
        }

        public EVROverlayError CreateOverlay(string key, string name, ref ulong handle)
            => GetFunc<_CreateOverlay>(SLOT_CreateOverlay)(_ptr, key, name, ref handle);

        public EVROverlayError DestroyOverlay(ulong handle)
            => GetFunc<_DestroyOverlay>(SLOT_DestroyOverlay)(_ptr, handle);

        public EVROverlayError ShowOverlay(ulong handle)
            => GetFunc<_ShowOverlay>(SLOT_ShowOverlay)(_ptr, handle);

        public EVROverlayError HideOverlay(ulong handle)
            => GetFunc<_HideOverlay>(SLOT_HideOverlay)(_ptr, handle);

        public EVROverlayError SetOverlayWidthInMeters(ulong handle, float width)
            => GetFunc<_SetOverlayWidthInMeters>(SLOT_SetOverlayWidthInMeters)(_ptr, handle, width);

        public EVROverlayError SetOverlayAlpha(ulong handle, float alpha)
            => GetFunc<_SetOverlayAlpha>(SLOT_SetOverlayAlpha)(_ptr, handle, alpha);

        public EVROverlayError SetOverlayTransformAbsolute(ulong handle, ETrackingUniverseOrigin origin, ref HmdMatrix34_t mat)
            => GetFunc<_SetOverlayTransformAbsolute>(SLOT_SetOverlayTransformAbsolute)(_ptr, handle, origin, ref mat);

        public EVROverlayError SetOverlayTransformTrackedDeviceRelative(ulong handle, uint deviceIndex, ref HmdMatrix34_t mat)
            => GetFunc<_SetOverlayTransformTrackedDeviceRelative>(SLOT_SetOverlayTransformTrackedDeviceRelative)(_ptr, handle, deviceIndex, ref mat);

        public EVROverlayError SetOverlayRaw(ulong handle, IntPtr buffer, uint width, uint height, uint bytesPerPixel)
            => GetFunc<_SetOverlayRaw>(SLOT_SetOverlayRaw)(_ptr, handle, buffer, width, height, bytesPerPixel);
    }

    // ================================================================
    // CORE OPENVR P/INVOKE
    // ================================================================

    public static class OpenVRInterop
    {
        private const string DLL = "openvr_api";
        private const string IVROverlay_Version = "IVROverlay_027";

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint VR_InitInternal(ref EVRInitError error, EVRApplicationType type);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void VR_ShutdownInternal();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool VR_IsHmdPresent();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool VR_IsRuntimeInstalled();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "VR_GetGenericInterface")]
        private static extern IntPtr VR_GetGenericInterface(
            [MarshalAs(UnmanagedType.LPStr)] string interfaceVersion,
            ref EVRInitError error);

        private static bool _initialized;

        /// <summary>
        /// Initialize OpenVR as an overlay application.
        /// Returns true if successful.
        /// </summary>
        public static bool Init(out string errorMessage)
        {
            errorMessage = "";

            try
            {
                if (!VR_IsRuntimeInstalled())
                {
                    errorMessage = "SteamVR n'est pas installé.";
                    return false;
                }

                var error = EVRInitError.None;
                VR_InitInternal(ref error, EVRApplicationType.VRApplication_Overlay);

                if (error != EVRInitError.None)
                {
                    errorMessage = $"Erreur OpenVR Init: {error}";
                    return false;
                }

                _initialized = true;
                return true;
            }
            catch (DllNotFoundException)
            {
                errorMessage = "openvr_api.dll introuvable. Copiez-la depuis le dossier SteamVR.";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Erreur VR: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Get the IVROverlay interface.
        /// </summary>
        public static VROverlayInterface? GetOverlayInterface(out string errorMessage)
        {
            errorMessage = "";
            if (!_initialized) { errorMessage = "OpenVR non initialisé."; return null; }

            try
            {
                var error = EVRInitError.None;
                IntPtr ptr = VR_GetGenericInterface(IVROverlay_Version, ref error);

                if (ptr == IntPtr.Zero || error != EVRInitError.None)
                {
                    errorMessage = $"Impossible d'obtenir IVROverlay: {error}";
                    return null;
                }

                return new VROverlayInterface(ptr);
            }
            catch (Exception ex)
            {
                errorMessage = $"Erreur IVROverlay: {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Shutdown OpenVR.
        /// </summary>
        public static void Shutdown()
        {
            if (_initialized)
            {
                VR_ShutdownInternal();
                _initialized = false;
            }
        }

        public static bool IsInitialized => _initialized;
        public static bool IsHmdPresent()
        {
            try { return VR_IsHmdPresent(); }
            catch { return false; }
        }
        public static bool IsRuntimeInstalled()
        {
            try { return VR_IsRuntimeInstalled(); }
            catch { return false; }
        }
    }
}
