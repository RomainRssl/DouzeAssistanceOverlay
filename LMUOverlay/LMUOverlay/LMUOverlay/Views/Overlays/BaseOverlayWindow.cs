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
        private Border? _bgBorder;          // background-only layer (sits behind content)
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
        private double _aspectRatio = 1.0;

        // Edge handles
        private readonly Border _edgeRight, _edgeBottom;
        private readonly Polygon _grip;
        private bool _resizeDisabled;

        /// <summary>
        /// Quand true : pas de Viewbox, la largeur est fixée manuellement et la hauteur
        /// s'auto-ajuste au contenu (SizeToContent.Height). Idéal pour les overlays à hauteur variable.
        /// </summary>
        protected bool UseWidthOnlyResize { get; set; }

        /// <summary>
        /// Quand true : pas de Viewbox, largeur ET hauteur redimensionnables librement.
        /// Le contenu ne se met PAS à l'échelle — il remplit l'espace disponible.
        /// Idéal pour les overlays scrollables (chat, listes).
        /// Taille par défaut : 300×350 px.
        /// </summary>
        protected bool UseRawResize { get; set; }

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

            Helpers.ThemeManager.ThemeChanged += OnThemeChangedInternal;

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

                // Extract background from root Border into a separate layer so that
                // BackgroundOpacity can control the background independently of content.
                _bgBorder = null;
                if (fe is Border rb && rb.Background is { } bg)
                {
                    _bgBorder = new Border
                    {
                        Background          = bg,
                        CornerRadius        = rb.CornerRadius,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment   = VerticalAlignment.Stretch,
                        Opacity             = Settings.BackgroundOpacity
                    };
                    rb.Background = null;
                    _outerGrid.Children.Add(_bgBorder);  // index 0: background layer
                }

                // Content (index 0 if no _bgBorder, otherwise index 1)
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

            if (UseRawResize)
            {
                // Mode scroll : largeur ET hauteur libres, pas de Viewbox, taille fixe
                // Tous les handles de resize sont actifs
                double savedW = Settings.OverlayWidth;
                double savedH = Settings.OverlayHeight;
                if (savedW < 150) savedW = 300;
                if (savedH < 100) savedH = 350;
                SizeToContent = SizeToContent.Manual;
                Width  = savedW;
                Height = savedH;
                Settings.OverlayWidth  = Width;
                Settings.OverlayHeight = Height;
                return;
            }

            if (UseWidthOnlyResize)
            {
                // Mode graphique : largeur fixe, hauteur auto — pas de Viewbox
                _edgeBottom.Visibility = Visibility.Collapsed;
                _grip.Visibility       = Visibility.Collapsed;

                // OverlayHeight > 0 = valeur issue de l'ancien mode Viewbox (config corrompue)
                // OverlayWidth < 200 = overlay trop étroit → reset
                double savedW = Settings.OverlayWidth;
                if (Settings.OverlayHeight > 0 || savedW < 200)
                    savedW = 300;

                Settings.OverlayHeight = 0; // hauteur non sauvegardée dans ce mode

                SizeToContent = SizeToContent.Manual;
                Width  = savedW;
                ClearValue(HeightProperty);
                SizeToContent = SizeToContent.Height;

                Settings.OverlayWidth = Width;
                return;
            }

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
                // Insert after _bgBorder (if present) so background stays behind content
                int insertIdx = _bgBorder != null ? 1 : 0;
                _outerGrid.Children.Insert(insertIdx, _viewbox);
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
                // Re-insert after _bgBorder (if present) so background stays behind content
                int insertIdx = _bgBorder != null ? 1 : 0;
                _outerGrid.Children.Insert(insertIdx, _originalContent);
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
                case nameof(OverlaySettings.BackgroundOpacity):
                    if (_bgBorder != null)
                        _bgBorder.Opacity = Settings.BackgroundOpacity;
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
                    _edgeRight.Visibility = vis;
                    if (!UseWidthOnlyResize)
                    {
                        _grip.Visibility    = vis;
                        _edgeBottom.Visibility = vis;
                    }
                    break;
                case nameof(OverlaySettings.Scale):
                    // UseRawResize : pas de Viewbox, le Scale n'a pas de sens dans ce mode
                    if (!_suppressScaleResize && !_resizeDisabled && !UseRawResize && _naturalWidth > 0)
                    {
                        double sc = Settings.Scale;
                        ActivateCustomSize(_naturalWidth * sc, _naturalHeight * sc);
                        Settings.OverlayWidth  = _naturalWidth  * sc;
                        Settings.OverlayHeight = _naturalHeight * sc;
                    }
                    break;
                case nameof(OverlaySettings.OverlayWidth):
                case nameof(OverlaySettings.OverlayHeight):
                    if (UseRawResize)
                    {
                        // Reset taille depuis le panel → revenir aux valeurs par défaut
                        if (Settings.OverlayWidth <= 0)
                        {
                            Width = 300;
                            Settings.OverlayWidth = 300;
                        }
                        if (Settings.OverlayHeight <= 0)
                        {
                            Height = 350;
                            Settings.OverlayHeight = 350;
                        }
                    }
                    else if (UseWidthOnlyResize)
                    {
                        // Reset Size depuis le panel → revenir à 300px
                        if (Settings.OverlayWidth == 0)
                        {
                            Width = 300;
                            Settings.OverlayWidth = 300;
                        }
                        Settings.OverlayHeight = 0;
                    }
                    else if (Settings.OverlayWidth <= 0 && Settings.OverlayHeight <= 0 && _isCustomSize)
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
            _edgeRight.Visibility = vis;
            if (!UseWidthOnlyResize)
            {
                _grip.Visibility       = vis;
                _edgeBottom.Visibility = vis;
            }
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

            if (UseRawResize)
            {
                // Mode raw : largeur ET hauteur manipulées directement, pas de Viewbox
                _resizeStartW = !double.IsNaN(Width)  ? Width  : ActualWidth;
                _resizeStartH = !double.IsNaN(Height) ? Height : ActualHeight;
                ((UIElement)e.Source).CaptureMouse();
                e.Handled = true;
                return;
            }

            if (UseWidthOnlyResize)
            {
                // Pas de Viewbox : on manipule directement Width
                _resizeStartW = !double.IsNaN(Width) ? Width : ActualWidth;
                ((UIElement)e.Source).CaptureMouse();
                e.Handled = true;
                return;
            }

            // First resize: switch to custom size mode
            if (!_isCustomSize)
                ActivateCustomSize(ActualWidth, ActualHeight);

            _resizeStartW = Width > 0 && !double.IsNaN(Width) ? Width : ActualWidth;
            _resizeStartH = Height > 0 && !double.IsNaN(Height) ? Height : ActualHeight;
            _aspectRatio  = _resizeStartH > 0 ? _resizeStartW / _resizeStartH : 1.0;

            ((UIElement)e.Source).CaptureMouse();
            e.Handled = true;
        }

        private void DoResize(object sender, MouseEventArgs e)
        {
            if (!_isResizing) return;

            var current = PointToScreen(e.GetPosition(this));
            double dx = current.X - _resizeScreenStart.X;
            double dy = current.Y - _resizeScreenStart.Y;

            if (UseRawResize)
            {
                // Resize libre : chaque handle ne contrôle que son axe
                switch (_activeEdge)
                {
                    case Edge.Right:
                        Width  = Math.Max(150, _resizeStartW + dx);
                        break;
                    case Edge.Bottom:
                        Height = Math.Max(100, _resizeStartH + dy);
                        break;
                    case Edge.Corner:
                        Width  = Math.Max(150, _resizeStartW + dx);
                        Height = Math.Max(100, _resizeStartH + dy);
                        break;
                }
                e.Handled = true;
                return;
            }

            if (UseWidthOnlyResize)
            {
                Width = Math.Max(150, _resizeStartW + dx);
                e.Handled = true;
                return;
            }

            switch (_activeEdge)
            {
                case Edge.Right:
                    Width  = Math.Max(60, _resizeStartW + dx);
                    if (_aspectRatio > 0) Height = Width / _aspectRatio;
                    break;
                case Edge.Bottom:
                    Height = Math.Max(40, _resizeStartH + dy);
                    if (_aspectRatio > 0) Width  = Height * _aspectRatio;
                    break;
                case Edge.Corner:
                    Width  = Math.Max(60, _resizeStartW + dx);
                    if (_aspectRatio > 0) Height = Width / _aspectRatio;
                    break;
            }

            e.Handled = true;
        }

        private void StopResize(object sender, MouseButtonEventArgs e)
        {
            if (!_isResizing) return;
            _isResizing = false;
            ((UIElement)e.Source).ReleaseMouseCapture();

            if (UseRawResize)
            {
                // Persiste largeur ET hauteur
                if (!double.IsNaN(Width))  Settings.OverlayWidth  = Width;
                if (!double.IsNaN(Height)) Settings.OverlayHeight = Height;
                e.Handled = true;
                return;
            }

            if (UseWidthOnlyResize)
            {
                // Persiste uniquement la largeur (la hauteur est auto)
                if (!double.IsNaN(Width)) Settings.OverlayWidth = Width;
                e.Handled = true;
                return;
            }

            // Save dimensions and update Scale to keep the slider in sync
            // Height is always derived from Width via aspect ratio, so save both
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
            Helpers.ThemeManager.ThemeChanged -= OnThemeChangedInternal;
            base.OnClosed(e);
        }

        // ================================================================
        // THÈME
        // ================================================================

        private void OnThemeChangedInternal()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(OnThemeChangedInternal);
                return;
            }
            OnThemeChanged();
        }

        /// <summary>
        /// Appelé sur le thread UI quand ThemeManager.ThemeChanged est déclenché.
        /// Surcharger dans les overlays pour reconstruire l'interface.
        /// </summary>
        protected virtual void OnThemeChanged() { }
    }
}
