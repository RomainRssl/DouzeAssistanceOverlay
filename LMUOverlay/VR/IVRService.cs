using LMUOverlay.Models;
using LMUOverlay.Views.Overlays;

namespace LMUOverlay.VR
{
    /// <summary>
    /// Common interface for VR overlay backends (SteamVR / OpenXR).
    /// Allows OverlayManager to switch backends transparently.
    /// </summary>
    public interface IVRService : IDisposable
    {
        bool   IsVRActive { get; }
        string LastError  { get; }

        event EventHandler<bool>? VRStatusChanged;

        bool Initialize();
        void Shutdown();

        void RegisterOverlay(string key, BaseOverlayWindow window, OverlaySettings settings);
        void UnregisterOverlay(string key);
        void UpdateAll();
        void SetOverlayPosition(string key, float x, float y, float z, float widthMeters = -1);
    }
}
