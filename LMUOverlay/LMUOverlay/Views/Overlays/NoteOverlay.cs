using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LMUOverlay.Helpers;
using LMUOverlay.Models;
using LMUOverlay.Services;

namespace LMUOverlay.Views.Overlays
{
    /// <summary>
    /// Bloc-notes persistant : toujours visible quand activé,
    /// indépendant de HideInMenus et de l'état de connexion.
    /// </summary>
    public class NoteOverlay : BaseOverlayWindow
    {
        private readonly TextBox _textBox;
        private bool _loading;

        private const string KEY_TEXT = "NoteText";

        public NoteOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            var outer = OverlayHelper.MakeBorder();
            var main = new StackPanel();
            main.Children.Add(OverlayHelper.MakeTitle("NOTE"));

            _textBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab    = true,
                TextWrapping  = TextWrapping.Wrap,
                MinWidth      = 180,
                MinHeight     = 80,
                MaxHeight     = 400,
                Background    = BrushCache.Get(18, 28, 28),
                Foreground    = BrushCache.Get(220, 235, 235),
                BorderBrush   = BrushCache.Get(36, 68, 68),
                BorderThickness = new Thickness(1),
                CaretBrush    = BrushCache.Get(0, 210, 190),
                FontFamily    = new FontFamily("Consolas"),
                FontSize      = 11,
                Padding       = new Thickness(6),
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin        = new Thickness(0, 2, 0, 0)
            };

            // Load saved text
            _loading = true;
            if (s.CustomOptions.TryGetValue(KEY_TEXT, out var saved))
                _textBox.Text = saved?.ToString() ?? "";
            _loading = false;

            _textBox.TextChanged += OnTextChanged;

            main.Children.Add(_textBox);
            outer.Child = main;
            Content = outer;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            Settings.CustomOptions[KEY_TEXT] = _textBox.Text;
        }

        /// <summary>
        /// NoteOverlay has no real-time data — UpdateData is a no-op.
        /// </summary>
        public override void UpdateData() { }
    }
}
