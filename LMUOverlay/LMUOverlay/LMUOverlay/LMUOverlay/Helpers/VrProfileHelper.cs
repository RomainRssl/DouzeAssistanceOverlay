using LMUOverlay.Models;

namespace LMUOverlay.Helpers
{
    /// <summary>
    /// Pure static helper for VR / 2D layout profile separation.
    /// Called from BaseOverlayWindow (drag/resize save) and OverlayManager (profile switch).
    /// </summary>
    public static class VrProfileHelper
    {
        /// <summary>
        /// If VR fields are not yet initialized, copies current 2D layout as starting point.
        /// Safe to call every time VR activates — only copies when VrPosX is null.
        /// </summary>
        public static void InitVrFromTwoD(OverlaySettings s)
        {
            if (s.VrPosX == null)
            {
                s.VrPosX   = s.PosX;
                s.VrPosY   = s.PosY;
                s.VrWidth  = s.OverlayWidth;
                s.VrHeight = s.OverlayHeight;
            }
        }

        /// <summary>
        /// Saves a drag result to the correct profile (VR or 2D).
        /// Call from BaseOverlayWindow.OnMouseUp passing IsVRModeActive.
        /// </summary>
        public static void SaveDragResult(OverlaySettings s, double newLeft, double newTop, bool isVrActive)
        {
            if (isVrActive)
            {
                s.VrPosX = newLeft;
                s.VrPosY = newTop;
            }
            else
            {
                s.PosX = newLeft;
                s.PosY = newTop;
            }
        }

        /// <summary>
        /// Saves a resize result to the correct profile (VR or 2D).
        /// Call from wherever OverlayWidth/OverlayHeight are saved on resize end.
        /// </summary>
        public static void SaveResizeResult(OverlaySettings s, double newWidth, double newHeight, bool isVrActive)
        {
            if (isVrActive)
            {
                s.VrWidth  = newWidth;
                s.VrHeight = newHeight;
            }
            else
            {
                s.OverlayWidth  = newWidth;
                s.OverlayHeight = newHeight;
            }
        }
    }
}
