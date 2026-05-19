using Xunit;
using LMUOverlay.Models;
using LMUOverlay.Helpers;

namespace LMUOverlay.Tests.UI
{
    // TODO: implement VrProfileHelper and VrPosX/VrPosY/VrWidth/VrHeight fields in OverlaySettings in 02-04

    /// <summary>
    /// Tests for VR profile copy / separation logic (UI-04).
    /// These tests are RED until Plan 02-04 adds VrPosX/VrPosY/VrWidth/VrHeight to OverlaySettings
    /// and creates VrProfileHelper.
    /// </summary>
    [Trait("Category", "UI")]
    public class VrProfileTests
    {
        [Fact(DisplayName = "InitVrFromTwoD: null VR fields are copied from 2D fields")]
        public void ApplyVrProfile_WhenVrFieldsNull_CopiesFrom2DFields()
        {
            // Arrange: 2D fields set, VR fields null (not yet initialized)
            var settings = new OverlaySettings
            {
                PosX = 100,
                PosY = 200,
                OverlayWidth = 300,
                OverlayHeight = 150,
                VrPosX = null
            };

            // Act
            VrProfileHelper.InitVrFromTwoD(settings);

            // Assert: VR fields now match 2D fields
            Assert.Equal(100, settings.VrPosX);
            Assert.Equal(200, settings.VrPosY);
            Assert.Equal(300, settings.VrWidth);
            Assert.Equal(150, settings.VrHeight);
        }

        [Fact(DisplayName = "InitVrFromTwoD: existing VR fields are not overwritten")]
        public void ApplyVrProfile_WhenVrFieldsSet_DoesNotOverwrite()
        {
            // Arrange: VR fields already have values
            var settings = new OverlaySettings
            {
                PosX = 100,
                PosY = 200,
                OverlayWidth = 300,
                OverlayHeight = 150,
                VrPosX = 50
            };

            // Act
            VrProfileHelper.InitVrFromTwoD(settings);

            // Assert: VR field unchanged (only copies when null)
            Assert.Equal(50, settings.VrPosX);
        }

        [Fact(DisplayName = "SaveDragResult in VR mode: writes to VrPosX/VrPosY, not PosX/PosY")]
        public void SaveInVrMode_WritesToVrFields_Not2DFields()
        {
            // Arrange
            var settings = new OverlaySettings
            {
                PosX = 100,
                PosY = 200
            };

            // Act: drag result when VR is active
            VrProfileHelper.SaveDragResult(settings, newLeft: 400, newTop: 500, isVrActive: true);

            // Assert: VR fields updated, 2D fields unchanged
            Assert.Equal(400, settings.VrPosX);
            Assert.Equal(500, settings.VrPosY);
            Assert.Equal(100, settings.PosX);
        }

        [Fact(DisplayName = "SaveDragResult in 2D mode: writes to PosX/PosY, VR fields remain null")]
        public void SaveInTwoDMode_WritesToPosXY_NotVrFields()
        {
            // Arrange: VR fields not initialized
            var settings = new OverlaySettings
            {
                PosX = 100,
                PosY = 200,
                VrPosX = null
            };

            // Act: drag result when VR is NOT active
            VrProfileHelper.SaveDragResult(settings, newLeft: 400, newTop: 500, isVrActive: false);

            // Assert: 2D fields updated, VR fields remain null
            Assert.Equal(400, settings.PosX);
            Assert.Equal(500, settings.PosY);
            Assert.Null(settings.VrPosX);
        }

        [Fact(DisplayName = "SaveResizeResult in VR mode: writes to VrWidth/VrHeight, not OverlayWidth/Height")]
        public void SaveResizeResult_InVrMode_WritesToVrWidthHeight()
        {
            // Arrange
            var settings = new OverlaySettings
            {
                OverlayWidth = 300,
                OverlayHeight = 150,
                VrWidth = null
            };

            // Act: resize result when VR is active
            VrProfileHelper.SaveResizeResult(settings, newWidth: 500, newHeight: 250, isVrActive: true);

            // Assert: VR dimensions updated, 2D dimensions unchanged
            Assert.Equal(500, settings.VrWidth);
            Assert.Equal(250, settings.VrHeight);
            Assert.Equal(300, settings.OverlayWidth);
        }

        [Fact(DisplayName = "SaveResizeResult in 2D mode: writes to OverlayWidth/Height, VrWidth remains null")]
        public void SaveResizeResult_In2DMode_WritesToOverlayDimensions()
        {
            // Arrange
            var settings = new OverlaySettings
            {
                OverlayWidth = 300,
                OverlayHeight = 150,
                VrWidth = null
            };

            // Act: resize result when VR is NOT active
            VrProfileHelper.SaveResizeResult(settings, newWidth: 500, newHeight: 250, isVrActive: false);

            // Assert: 2D dimensions updated, VR dimensions remain null
            Assert.Equal(500, settings.OverlayWidth);
            Assert.Equal(250, settings.OverlayHeight);
            Assert.Null(settings.VrWidth);
        }
    }
}
