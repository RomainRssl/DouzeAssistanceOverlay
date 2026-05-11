using System.IO;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace LMUOverlay.Helpers
{
    /// <summary>
    /// Singleton centralisant toutes les valeurs visuelles de l'application.
    /// Charge theme.json au démarrage, supporte le rechargement à chaud.
    /// Tous les overlays lisent leurs couleurs/polices/tailles ici.
    /// </summary>
    public class ThemeManager
    {
        private static ThemeManager _current = CreateDefault();
        public static ThemeManager Current => _current;

        /// <summary>Déclenché sur le thread UI après chaque changement de thème.</summary>
        public static event Action? ThemeChanged;

        // ----------------------------------------------------------------
        // Métadonnées
        // ----------------------------------------------------------------
        public string Name { get; internal set; } = "Endurance Noir";

        // ----------------------------------------------------------------
        // Couleurs
        // ----------------------------------------------------------------
        public Color Background      { get; internal set; }
        public Color PanelBackground { get; internal set; }
        public byte  PanelAlpha      { get; internal set; }
        public Color AccentPrimary   { get; internal set; }
        public Color AccentSecondary { get; internal set; }
        public Color TextPrimary     { get; internal set; }
        public Color TextSecondary   { get; internal set; }
        public Color TextMuted       { get; internal set; }
        public Color StateGood       { get; internal set; }
        public Color StateWarn       { get; internal set; }
        public Color StateDanger     { get; internal set; }
        public Color StateBestLap    { get; internal set; }
        public Color Border          { get; internal set; }
        public Color ClassHypercar   { get; internal set; }
        public Color ClassLmp2       { get; internal set; }
        public Color ClassLmgt       { get; internal set; }
        public Color ClassGt3        { get; internal set; }

        // ----------------------------------------------------------------
        // Typographie
        // ----------------------------------------------------------------
        public FontFamily DisplayFont { get; internal set; } = null!;
        public FontFamily MonoFont    { get; internal set; } = null!;
        public double SizeLabel { get; internal set; }
        public double SizeBase  { get; internal set; }
        public double SizeLarge { get; internal set; }
        public double SizeHuge  { get; internal set; }

        // ----------------------------------------------------------------
        // Géométrie
        // ----------------------------------------------------------------
        public CornerRadius CornerRadius    { get; internal set; }
        public Thickness    BorderThickness { get; internal set; }
        public Thickness    PanelPadding    { get; internal set; }
        public double HeaderHeight { get; internal set; }
        public double RowHeight    { get; internal set; }
        public double CellPaddingH { get; internal set; }
        public double CellPaddingV { get; internal set; }

        // ----------------------------------------------------------------
        // Effets
        // ----------------------------------------------------------------
        public bool BarGradient { get; internal set; }
        public bool AccentLine  { get; internal set; }
        public bool AlertGlow   { get; internal set; }
        public bool RoundedBars { get; internal set; }

        // ================================================================
        // API publique
        // ================================================================

        /// <summary>
        /// Charge un thème depuis le dossier themes/ par son nom (sans extension).
        /// Si le fichier n'existe pas, conserve le thème actuel.
        /// </summary>
        public static void Load(string themeName)
        {
            try
            {
                string path = Path.Combine(ThemesDirectory, themeName + ".json");
                if (!File.Exists(path))
                    path = Path.Combine(ThemesDirectory, themeName);
                if (!File.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"[ThemeManager] Thème introuvable : {themeName}");
                    return;
                }
                LoadFromFile(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Erreur Load : {ex.Message}");
            }
        }

        /// <summary>Charge un thème depuis un chemin complet.</summary>
        public static void LoadFromFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var data = JObject.Parse(json);
                var tm = new ThemeManager();
                tm.ApplyDefaults();
                tm.ApplyJson(data);
                _current = tm;
                FireThemeChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Erreur LoadFromFile : {ex.Message}");
            }
        }

        /// <summary>Sauvegarde le thème courant en JSON vers le fichier indiqué.</summary>
        public static void SaveCurrentTo(string filePath)
        {
            var tm = _current;
            var obj = new JObject
            {
                ["name"] = tm.Name,
                ["colors"] = new JObject
                {
                    ["background"]      = ColorToHex(tm.Background),
                    ["panelBackground"] = ColorToHex(tm.PanelBackground),
                    ["panelAlpha"]      = (int)tm.PanelAlpha,
                    ["accentPrimary"]   = ColorToHex(tm.AccentPrimary),
                    ["accentSecondary"] = ColorToHex(tm.AccentSecondary),
                    ["textPrimary"]     = ColorToHex(tm.TextPrimary),
                    ["textSecondary"]   = ColorToHex(tm.TextSecondary),
                    ["textMuted"]       = ColorToHex(tm.TextMuted),
                    ["stateGood"]       = ColorToHex(tm.StateGood),
                    ["stateWarn"]       = ColorToHex(tm.StateWarn),
                    ["stateDanger"]     = ColorToHex(tm.StateDanger),
                    ["stateBestLap"]    = ColorToHex(tm.StateBestLap),
                    ["border"]          = ColorToHex(tm.Border),
                    ["hypercar"]        = ColorToHex(tm.ClassHypercar),
                    ["lmp2"]            = ColorToHex(tm.ClassLmp2),
                    ["lmgt"]            = ColorToHex(tm.ClassLmgt),
                    ["gt3"]             = ColorToHex(tm.ClassGt3),
                },
                ["typography"] = new JObject
                {
                    ["displayFamily"]         = tm.DisplayFont.Source,
                    ["displayFamilyFallback"] = "Calibri",
                    ["monoFamily"]            = tm.MonoFont.Source,
                    ["monoFamilyFallback"]    = "Consolas",
                    ["sizeLabel"]  = tm.SizeLabel,
                    ["sizeBase"]   = tm.SizeBase,
                    ["sizeLarge"]  = tm.SizeLarge,
                    ["sizeHuge"]   = tm.SizeHuge,
                },
                ["geometry"] = new JObject
                {
                    ["cornerRadius"]    = tm.CornerRadius.TopLeft,
                    ["borderThickness"] = tm.BorderThickness.Left,
                    ["panelPadding"]    = tm.PanelPadding.Left,
                    ["headerHeight"]    = tm.HeaderHeight,
                    ["rowHeight"]       = tm.RowHeight,
                    ["cellPaddingH"]    = tm.CellPaddingH,
                    ["cellPaddingV"]    = tm.CellPaddingV,
                },
                ["effects"] = new JObject
                {
                    ["barGradient"] = tm.BarGradient,
                    ["accentLine"]  = tm.AccentLine,
                    ["alertGlow"]   = tm.AlertGlow,
                    ["roundedBars"] = tm.RoundedBars,
                }
            };
            File.WriteAllText(filePath, obj.ToString(Newtonsoft.Json.Formatting.Indented), System.Text.Encoding.UTF8);
        }

        /// <summary>Recharge le thème actuel depuis son fichier (rechargement à chaud).</summary>
        public static void Reload() => FireThemeChanged();

        /// <summary>Applique des modifications en mémoire et notifie les overlays.</summary>
        public static void ApplyLive(Action<ThemeManager> modifier)
        {
            modifier(_current);
            FireThemeChanged();
        }

        // ----------------------------------------------------------------
        // Dossier thèmes
        // ----------------------------------------------------------------

        public static string ThemesDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DouzeAssistance", "themes");

        public static string[] GetAvailableThemes()
        {
            if (!Directory.Exists(ThemesDirectory)) return [];
            return Directory.GetFiles(ThemesDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .ToArray();
        }

        /// <summary>
        /// S'assure que le dossier themes/ et le thème par défaut existent.
        /// À appeler au démarrage avant Load().
        /// </summary>
        public static void EnsureDefaultThemeExists()
        {
            Directory.CreateDirectory(ThemesDirectory);
            string defaultPath = Path.Combine(ThemesDirectory, "endurance-noir.json");
            if (!File.Exists(defaultPath))
                WriteDefaultThemeToDisk(defaultPath);
        }

        // ================================================================
        // Internes
        // ================================================================

        private static ThemeManager CreateDefault()
        {
            var tm = new ThemeManager();
            tm.ApplyDefaults();
            return tm;
        }

        private void ApplyDefaults()
        {
            Name             = "Endurance Noir";
            Background       = ParseColor("#0A0B0D");
            PanelBackground  = ParseColor("#0C0E12");
            PanelAlpha       = 235;
            AccentPrimary    = ParseColor("#C41E28");
            AccentSecondary  = ParseColor("#B48C32");
            TextPrimary      = ParseColor("#E8E8E8");
            TextSecondary    = ParseColor("#888888");
            TextMuted        = ParseColor("#444444");
            StateGood        = ParseColor("#1A9E5C");
            StateWarn        = ParseColor("#D4A017");
            StateDanger      = ParseColor("#C41E28");
            StateBestLap     = ParseColor("#A855F7");
            Border           = ParseColor("#1E2028");
            ClassHypercar    = ParseColor("#C41E28");
            ClassLmp2        = ParseColor("#0090FF");
            ClassLmgt        = ParseColor("#FF8C00");
            ClassGt3         = ParseColor("#00C850");

            DisplayFont = ResolveFont("Barlow Condensed", "Calibri");
            MonoFont    = ResolveFont("Share Tech Mono", "Consolas");
            SizeLabel   = 9;
            SizeBase    = 11;
            SizeLarge   = 14;
            SizeHuge    = 22;

            CornerRadius    = new CornerRadius(3);
            BorderThickness = new Thickness(1);
            PanelPadding    = new Thickness(8);
            HeaderHeight    = 20;
            RowHeight       = 22;
            CellPaddingH    = 4;
            CellPaddingV    = 2;

            BarGradient = true;
            AccentLine  = true;
            AlertGlow   = false;
            RoundedBars = false;
        }

        private void ApplyJson(JObject data)
        {
            Name = data["name"]?.Value<string>() ?? Name;

            if (data["colors"] is JObject colors)
            {
                Background      = Try(colors["background"],      Background);
                PanelBackground = Try(colors["panelBackground"],  PanelBackground);
                PanelAlpha      = (byte)(colors["panelAlpha"]?.Value<int>() ?? PanelAlpha);
                AccentPrimary   = Try(colors["accentPrimary"],    AccentPrimary);
                AccentSecondary = Try(colors["accentSecondary"],  AccentSecondary);
                TextPrimary     = Try(colors["textPrimary"],      TextPrimary);
                TextSecondary   = Try(colors["textSecondary"],    TextSecondary);
                TextMuted       = Try(colors["textMuted"],        TextMuted);
                StateGood       = Try(colors["stateGood"],        StateGood);
                StateWarn       = Try(colors["stateWarn"],        StateWarn);
                StateDanger     = Try(colors["stateDanger"],      StateDanger);
                StateBestLap    = Try(colors["stateBestLap"],     StateBestLap);
                Border          = Try(colors["border"],           Border);
                ClassHypercar   = Try(colors["hypercar"],         ClassHypercar);
                ClassLmp2       = Try(colors["lmp2"],             ClassLmp2);
                ClassLmgt       = Try(colors["lmgt"],             ClassLmgt);
                ClassGt3        = Try(colors["gt3"],              ClassGt3);
            }

            if (data["typography"] is JObject typo)
            {
                string? dp = typo["displayFamily"]?.Value<string>();
                string  df = typo["displayFamilyFallback"]?.Value<string>() ?? "Calibri";
                string? mp = typo["monoFamily"]?.Value<string>();
                string  mf = typo["monoFamilyFallback"]?.Value<string>() ?? "Consolas";
                if (dp != null) DisplayFont = ResolveFont(dp, df);
                if (mp != null) MonoFont    = ResolveFont(mp, mf);
                SizeLabel = typo["sizeLabel"]?.Value<double>() ?? SizeLabel;
                SizeBase  = typo["sizeBase"]?.Value<double>()  ?? SizeBase;
                SizeLarge = typo["sizeLarge"]?.Value<double>() ?? SizeLarge;
                SizeHuge  = typo["sizeHuge"]?.Value<double>()  ?? SizeHuge;
            }

            if (data["geometry"] is JObject geom)
            {
                CornerRadius    = new CornerRadius(geom["cornerRadius"]?.Value<double>()    ?? CornerRadius.TopLeft);
                BorderThickness = new Thickness(geom["borderThickness"]?.Value<double>()   ?? BorderThickness.Left);
                PanelPadding    = new Thickness(geom["panelPadding"]?.Value<double>()      ?? PanelPadding.Left);
                HeaderHeight    = geom["headerHeight"]?.Value<double>() ?? HeaderHeight;
                RowHeight       = geom["rowHeight"]?.Value<double>()    ?? RowHeight;
                CellPaddingH    = geom["cellPaddingH"]?.Value<double>() ?? CellPaddingH;
                CellPaddingV    = geom["cellPaddingV"]?.Value<double>() ?? CellPaddingV;
            }

            if (data["effects"] is JObject fx)
            {
                BarGradient = fx["barGradient"]?.Value<bool>() ?? BarGradient;
                AccentLine  = fx["accentLine"]?.Value<bool>()  ?? AccentLine;
                AlertGlow   = fx["alertGlow"]?.Value<bool>()   ?? AlertGlow;
                RoundedBars = fx["roundedBars"]?.Value<bool>() ?? RoundedBars;
            }
        }

        private static void FireThemeChanged()
        {
            var app = Application.Current;
            if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
                app.Dispatcher.Invoke(() => ThemeChanged?.Invoke());
            else
                ThemeChanged?.Invoke();
        }

        private static Color Try(JToken? token, Color fallback)
        {
            if (token == null) return fallback;
            try { return ParseColor(token.Value<string>()!); }
            catch { return fallback; }
        }

        public static Color ParseColor(string hex)
        {
            hex = hex.TrimStart('#');
            return hex.Length switch
            {
                6 => Color.FromRgb(
                        Convert.ToByte(hex[0..2], 16),
                        Convert.ToByte(hex[2..4], 16),
                        Convert.ToByte(hex[4..6], 16)),
                8 => Color.FromArgb(
                        Convert.ToByte(hex[0..2], 16),
                        Convert.ToByte(hex[2..4], 16),
                        Convert.ToByte(hex[4..6], 16),
                        Convert.ToByte(hex[6..8], 16)),
                _ => Colors.Magenta // couleur d'erreur évidente
            };
        }

        public static string ColorToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private static FontFamily ResolveFont(string primary, string fallback)
        {
            bool found = Fonts.SystemFontFamilies.Any(f =>
                f.Source.Equals(primary, StringComparison.OrdinalIgnoreCase));
            return new FontFamily(found ? primary : fallback);
        }

        private static void WriteDefaultThemeToDisk(string path)
        {
            SaveCurrentTo(path); // _current already has defaults applied
        }
    }
}
