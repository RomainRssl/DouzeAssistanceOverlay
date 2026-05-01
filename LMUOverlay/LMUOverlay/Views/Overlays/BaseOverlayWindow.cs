using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    public abstract class BaseOverlayWindow : Window
    {
        protected readonly DataService DataService;
        public OverlaySettings Settings { get; }

        // Drag
        private bool _isDragging;
        private Point _dragStart;

        // Content
        private FrameworkElement? _originalContent;
        private Grid? _outerGrid;
        private Viewbox? _viewbox;
        private bool _wrapped;
        private bool _isCustomSize;
        private double _naturalWidth, _naturalHeight;
        private bool _suppressScaleResize; // évite la boucle Scale→resize→Scale

        // Resize
        private enum Edge { None, Right, Bottom, Corner }
        private Edge _activeEdge;
        private bool _isResizing;
        private Point _resizeScreenStart;
        private double _resizeStartW, _resizeStartH;

        // Edge handles
        private readonly Border _edgeRight, _edgeBottom;
        private readonly Polygon _grip;
        private bool _resizeDisabled;

        private const double EDGE_THICKNESS = 6;

        protected BaseOverlayWindow(DataService dataService, OverlaySettings settings)
        {
            DataService = dataService;
            Settings = settings;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            Left = settings.PosX;
            Top = settings.PosY;
            Opacity = settings.Opacity;

            bool unlocked = !settings.IsLocked;

            // Right edge: full height, thin strip on right
            _edgeRight = new Border
            {
                Width = EDGE_THICKNESS,
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeWE,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed
            };
            _edgeRight.MouseLeftButtonDown += (s, e) => StartResize(e, Edge.Right);
            _edgeRight.MouseMove += DoResize;
            _edgeRight.MouseLeftButtonUp += StopResize;

            // Bottom edge: full width, thin strip on bottom
            _edgeBottom = new Border
            {
                Height = EDGE_THICKNESS,
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeNS,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed
            };
            _edgeBottom.MouseLeftButtonDown += (s, e) => StartResize(e, Edge.Bottom);
            _edgeBottom.MouseMove += DoResize;
            _edgeBottom.MouseLeftButtonUp += StopResize;

            // Corner grip: triangle bottom-right (resize both)
            _grip = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(0, 16), new Point(16, 0), new Point(16, 16)
                },
                Fill = new SolidColorBrush(Color.FromArgb(100, 180, 210, 210)),
                Cursor = Cursors.SizeNWSE,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 0),
                Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed
            };
            _grip.MouseEnter += (_, _) =>
                _grip.Fill = BrushCache.Get(220, 0, 210, 190);
            _grip.MouseLeave += (_, _) =>
                _grip.Fill = BrushCache.Get(100, 180, 210, 210);
            _grip.MouseLeftButtonDown += (s, e) => StartResize(e, Edge.Corner);
            _grip.MouseMove += DoResize;
            _grip.MouseLeftButtonUp += StopResize;

            settings.PropertyChanged += OnSettingsChanged;
            MouseLeftButtonDown += OnMouseDown;
            MouseMove += OnMouseMoveHandler;
            MouseLeftButtonUp += OnMouseUp;
        }

        // ================================================================
        // WRAP CONTENT
        // ================================================================

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);

            if (!_wrapped && newContent is FrameworkElement fe && newContent != _outerGrid)
            {
                _wrapped = true;
                _originalContent = fe;
                Content = null;

                _outerGrid = new Grid();
                // Content at index 0
                _outerGrid.Children.Add(fe);
                // Resize handles on top (order matters for hit testing)
                _outerGrid.Children.Add(_edgeRight);
                _outerGrid.Children.Add(_edgeBottom);
                _outerGrid.Children.Add(_grip);

                Content = _outerGrid;
                SizeToContent = SizeToContent.WidthAndHeight;

                // Always capture natural size on first layout, then restore saved size / scale
                Loaded += OnFirstLoaded;
            }
        }

        private void OnFirstLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnFirstLoaded;
            _naturalWidth  = ActualWidth;
            _naturalHeight = ActualHeight;

            if (_resizeDisabled) return;

            if (Settings.OverlayWidth > 30 && Settings.OverlayHeight > 30)
                ActivateCustomSize(Settings.OverlayWidth, Settings.OverlayHeight);
            else if (Math.Abs(Settings.Scale - 1.0) > 0.01)
                ActivateCustomSize(_naturalWidth * Settings.Scale, _naturalHeight * Settings.Scale);
        }

        // ================================================================
        // CUSTOM SIZE (Viewbox wrapping)
        // ================================================================

        private void ActivateCustomSize(double w, double h)
        {
            if (_originalContent == null || _outerGrid == null) return;

            if (_viewbox == null)
            {
                _outerGrid.Children.Remove(_originalContent);
                _viewbox = new Viewbox
                {
                    Stretch = Stretch.Fill,
                    StretchDirection = StretchDirection.Both,
                    Child = _originalContent
                };
                _outerGrid.Children.Insert(0, _viewbox);
            }

            SizeToContent = SizeToContent.Manual;
            Width = w;
            Height = h;
            _isCustomSize = true;
        }

        private void DeactivateCustomSize()
        {
            if (_originalContent == null || _outerGrid == null) return;

            if (_viewbox != null)
            {
                _viewbox.Child = null;
                _outerGrid.Children.Remove(_viewbox);
                _outerGrid.Children.Insert(0, _originalContent);
                _viewbox = null;
            }

            Width = double.NaN;
            Height = double.NaN;
            SizeToContent = SizeToContent.WidthAndHeight;
            _isCustomSize = false;
        }

        // ================================================================
        // SETTINGS
        // ================================================================

        private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnSettingsChanged(sender, e));
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(OverlaySettings.Opacity):
                    Opacity = Settings.Opacity;
                    break;
                case nameof(OverlaySettings.IsEnabled):
                    if (Settings.IsEnabled) Show(); else Hide();
                    break;
                case nameof(OverlaySettings.PosX):
                    Left = Settings.PosX;
                    break;
                case nameof(OverlaySettings.PosY):
                    Top = Settings.PosY;
                    break;
                case nameof(OverlaySettings.IsLocked):
                    var vis = Settings.IsLocked ? Visibility.Collapsed : Visibility.Visible;
                    _grip.Visibility = vis;
                    _edgeRight.Visibility = vis;
                    _edgeBottom.Visibility = vis;
                    break;
                case nameof(OverlaySettings.Scale):
                    if (!_suppressScaleResize && !_resizeDisabled && _naturalWidth > 0)
                    {
                        double sc = Settings.Scale;
                        ActivateCustomSize(_naturalWidth * sc, _naturalHeight * sc);
                        Settings.OverlayWidth  = _naturalWidth  * sc;
                        Settings.OverlayHeight = _naturalHeight * sc;
                    }
                    break;
                case nameof(OverlaySettings.OverlayWidth):
                case nameof(OverlaySettings.OverlayHeight):
                    if (Settings.OverlayWidth <= 0 && Settings.OverlayHeight <= 0 && _isCustomSize)
                        DeactivateCustomSize();
                    break;
            }
        }

        /// <summary>
        /// Call from a subclass constructor to permanently hide resize handles
        /// and keep the window in auto-size mode (SizeToContent).
        /// </summary>
        protected void DisableManualResize()
        {
            _resizeDisabled = true;
            _grip.Visibility     = Visibility.Collapsed;
            _edgeRight.Visibility = Visibility.Collapsed;
            _edgeBottom.Visibility = Visibility.Collapsed;
            // Ensure no previously-saved custom size is applied
            Settings.OverlayWidth  = 0;
            Settings.OverlayHeight = 0;
        }

        public void UpdateLockState()
        {
            if (_resizeDisabled) return;
            var vis = Settings.IsLocked ? Visibility.Collapsed : Visibility.Visible;
            _grip.Visibility = vis;
            _edgeRight.Visibility = vis;
            _edgeBottom.Visibility = vis;
        }

        public abstract void UpdateData();

        // ================================================================
        // DRAG
        // ================================================================

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Settings.IsLocked || _isResizing) return;
            _isDragging = true;
            _dragStart = e.GetPosition(this);
            CaptureMouse();
        }

        private void OnMouseMoveHandler(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            var pos = e.GetPosition(this);
            Left += pos.X - _dragStart.X;
            Top += pos.Y - _dragStart.Y;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;
            ReleaseMouseCapture();
            Settings.PosX = Left;
            Settings.PosY = Top;
        }

        // ================================================================
        // RESIZE (right edge, bottom edge, corner grip)
        // ================================================================

        private void StartResize(MouseButtonEventArgs e, Edge edge)
        {
            if (Settings.IsLocked || _resizeDisabled) return;
            _isResizing = true;
            _activeEdge = edge;
            _resizeScreenStart = PointToScreen(e.GetPosition(this));

            // First resize: switch to custom size mode
            if (!_isCustomSize)
                ActivateCustomSize(ActualWidth, ActualHeight);

            _resizeStartW = Width > 0 && !double.IsNaN(Width) ? Width : ActualWidth;
            _resizeStartH = Height > 0 && !double.IsNaN(Height) ? Height : ActualHeight;

            ((UIElement)e.Source).CaptureMouse();
            e.Handled = true;
        }

        private void DoResize(object sender, MouseEventArgs e)
        {
            if (!_isResizing) return;

            var current = PointToScreen(e.GetPosition(this));
            double dx = current.X - _resizeScreenStart.X;
            double dy = current.Y - _resizeScreenStart.Y;

            switch (_activeEdge)
            {
                case Edge.Right:
                    Width = Math.Max(60, _resizeStartW + dx);
                    break;
                case Edge.Bottom:
                    Height = Math.Max(40, _resizeStartH + dy);
                    break;
                case Edge.Corner:
                    Width = Math.Max(60, _resizeStartW + dx);
                    Height = Math.Max(40, _resizeStartH + dy);
                    break;
            }

            e.Handled = true;
        }

        private void StopResize(object sender, MouseButtonEventArgs e)
        {
            if (!_isResizing) return;
            _isResizing = false;
            ((UIElement)e.Source).ReleaseMouseCapture();

            // Save dimensions and update Scale to keep the slider in sync
            if (!double.IsNaN(Width))  Settings.OverlayWidth  = Width;
            if (!double.IsNaN(Height)) Settings.OverlayHeight = Height;
            if (_naturalWidth > 0)
            {
                _suppressScaleResize = true;
                Settings.Scale = Width / _naturalWidth;
                _suppressScaleResize = false;
            }
            e.Handled = true;
        }

        // ================================================================

        protected override void OnClosed(EventArgs e)
        {
            Settings.PosX = Left;
            Settings.PosY = Top;
            Settings.PropertyChanged -= OnSettingsChanged;
            base.OnClosed(e);
        }
    }
}
