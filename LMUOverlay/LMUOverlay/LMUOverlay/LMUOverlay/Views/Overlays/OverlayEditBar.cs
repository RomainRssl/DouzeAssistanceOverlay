using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Helpers;
using LMUOverlay.Models;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Floating per-overlay contextual bar shown in edit mode.
    /// Separate Window (not embedded) to avoid WPF airspace restrictions.
    /// Reused across attaches — never destroyed, just hidden via Detach().
    /// </summary>
    public class OverlayEditBar : Window
    {
        private BaseOverlayWindow? _target;
        private Slider _opacitySlider = null!;
        private Slider _bgOpacitySlider = null!;
        private TextBox _colorBgInput = null!;
        private TextBox _colorTextInput = null!;
        private TextBox _colorAccentInput = null!;
        private Label _titleLabel = null!;

        public OverlayEditBar()
        {
            WindowStyle      = WindowStyle.None;
            AllowsTransparency = false;
            Topmost          = true;
            ShowInTaskbar    = false;
            ResizeMode       = ResizeMode.NoResize;
            Width            = 280;
            Height           = 160;
            Background       = new SolidColorBrush(Color.FromRgb(30, 30, 40));
            Foreground       = Brushes.White;

            BuildUI();

            // Hide on close instead of destroying — reuse instance
            Closing += (s, e) => { e.Cancel = true; Visibility = Visibility.Collapsed; };
        }

        private void BuildUI()
        {
            var root = new StackPanel { Margin = new Thickness(8), Orientation = Orientation.Vertical };

            _titleLabel = new Label
            {
                FontSize   = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 200)),
                Padding    = new Thickness(0, 0, 0, 4)
            };
            root.Children.Add(_titleLabel);

            // Overlay opacity
            root.Children.Add(new Label { Content = "Opacity", FontSize = 10, Padding = new Thickness(0), Foreground = Brushes.LightGray });
            _opacitySlider = new Slider { Minimum = 0.1, Maximum = 1.0, SmallChange = 0.05, Width = 260, Margin = new Thickness(0, 0, 0, 2) };
            _opacitySlider.ValueChanged += (s, e) =>
            {
                if (_target != null) _target.Settings.Opacity = _opacitySlider.Value;
            };
            root.Children.Add(_opacitySlider);

            // Background opacity
            root.Children.Add(new Label { Content = "Bg Opacity", FontSize = 10, Padding = new Thickness(0), Foreground = Brushes.LightGray });
            _bgOpacitySlider = new Slider { Minimum = 0.0, Maximum = 1.0, SmallChange = 0.05, Width = 260, Margin = new Thickness(0, 0, 0, 4) };
            _bgOpacitySlider.ValueChanged += (s, e) =>
            {
                if (_target != null) _target.Settings.BackgroundOpacity = _bgOpacitySlider.Value;
            };
            root.Children.Add(_bgOpacitySlider);

            // Color overrides (hex inputs)
            var colorRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            colorRow.Children.Add(BuildColorInput("Bg",   ref _colorBgInput!,     "ColorBg"));
            colorRow.Children.Add(BuildColorInput("Text", ref _colorTextInput!,   "ColorText"));
            colorRow.Children.Add(BuildColorInput("Acc",  ref _colorAccentInput!, "ColorAccent"));
            root.Children.Add(colorRow);

            Content = root;
        }

        private StackPanel BuildColorInput(string label, ref TextBox field, string key)
        {
            var sp = new StackPanel { Margin = new Thickness(4, 0, 4, 0) };
            sp.Children.Add(new Label { Content = label, FontSize = 9, Padding = new Thickness(0), Foreground = Brushes.LightGray });
            field = new TextBox { Width = 72, MaxLength = 9, FontSize = 9 };
            var capturedKey = key;
            var capturedField = field;
            field.LostFocus += (s, e) =>
            {
                if (_target == null) return;
                var hex = capturedField.Text.Trim();
                if (string.IsNullOrEmpty(hex))
                    _target.Settings.CustomOptions.Remove(capturedKey);
                else
                    ColorOverrideHelper.Set(_target.Settings, capturedKey, hex);
                _target.RefreshColorOverrides();
            };
            sp.Children.Add(field);
            return sp;
        }

        /// <summary>
        /// Attach the edit bar to a specific overlay and show it just below that window.
        /// </summary>
        public void AttachTo(BaseOverlayWindow overlay)
        {
            _target = overlay;
            _titleLabel.Content    = overlay.Title ?? overlay.GetType().Name;
            _opacitySlider.Value   = overlay.Settings.Opacity;
            _bgOpacitySlider.Value = overlay.Settings.BackgroundOpacity;
            _colorBgInput.Text     = ColorOverrideHelper.Get(overlay.Settings, "ColorBg")     ?? "";
            _colorTextInput.Text   = ColorOverrideHelper.Get(overlay.Settings, "ColorText")   ?? "";
            _colorAccentInput.Text = ColorOverrideHelper.Get(overlay.Settings, "ColorAccent") ?? "";

            // Position: just below the overlay, same left edge
            Left = overlay.Left;
            Top  = overlay.Top + overlay.ActualHeight + 4;
            Visibility = Visibility.Visible;
        }

        /// <summary>Hide without destroying — reuse on next AttachTo call.</summary>
        public void Detach()
        {
            _target = null;
            Visibility = Visibility.Collapsed;
        }
    }
}
