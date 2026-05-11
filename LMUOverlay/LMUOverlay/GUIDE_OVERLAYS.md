# 🛠 Guide de modification des Overlays — LMU Overlay

## Structure d'un overlay

Chaque overlay suit le même pattern :

```
Views/Overlays/
  MonOverlay.cs
    │
    ├─ Champs privés      → les éléments UI modifiables en live
    ├─ Constructeur()     → LE DESIGN (équivalent du XAML)
    └─ UpdateData()       → la logique temps réel (30 fps)
```

---

## Aide-mémoire WPF (C# code-only)

### Conteneurs (layout)

```csharp
// Empile verticalement
var stack = new StackPanel();

// Empile horizontalement
var stack = new StackPanel { Orientation = Orientation.Horizontal };

// Grille (le plus flexible)
var grid = new Grid();
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });   // fixe 100px
grid.ColumnDefinitions.Add(new ColumnDefinition());                                  // prend le reste
grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });             // auto
grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // remplit

// Placer un élément dans la grille
Grid.SetRow(monElement, 0);     // ligne 0
Grid.SetColumn(monElement, 1);  // colonne 1
Grid.SetColumnSpan(monElement, 2); // s'étend sur 2 colonnes
grid.Children.Add(monElement);
```

### Texte

```csharp
var texte = new TextBlock
{
    Text = "Mon texte",
    FontSize = 16,                                    // taille en pixels
    FontWeight = FontWeights.Bold,                    // gras
    FontFamily = new FontFamily("Consolas"),           // police
    Foreground = new SolidColorBrush(Colors.White),   // couleur texte
    HorizontalAlignment = HorizontalAlignment.Center, // centré
    VerticalAlignment = VerticalAlignment.Center,
    Margin = new Thickness(gauche, haut, droite, bas), // espacement externe
    TextTrimming = TextTrimming.CharacterEllipsis       // coupe avec "..."
};
```

### Bordure / Fond

```csharp
var cadre = new Border
{
    Background = new SolidColorBrush(Color.FromRgb(30, 60, 65)),  // fond
    CornerRadius = new CornerRadius(6),                             // coins arrondis
    Padding = new Thickness(10),                                    // espacement interne
    Margin = new Thickness(2),                                      // espacement externe
    BorderBrush = new SolidColorBrush(Colors.Gray),                // couleur bordure
    BorderThickness = new Thickness(1),                             // épaisseur bordure
    Width = 100, Height = 50,                                       // taille fixe (optionnel)
    ClipToBounds = true                                             // coupe le contenu qui dépasse
};
cadre.Child = monContenu;  // UN seul enfant !
```

### Couleurs

```csharp
// RGB
Color.FromRgb(255, 59, 48)           // rouge

// RGBA (avec transparence, 0=invisible, 255=opaque)
Color.FromArgb(128, 255, 255, 255)   // blanc 50% transparent

// Couleurs du thème (dans OverlayHelper)
OverlayHelper.BgDark        // fond principal (18, 32, 32)
OverlayHelper.BgCell         // fond cellule (24, 52, 55)
OverlayHelper.AccGreen       // vert accent (76, 217, 100)
OverlayHelper.AccRed         // rouge accent (255, 59, 48)
OverlayHelper.AccYellow      // jaune accent (255, 204, 0)
OverlayHelper.AccBlue        // bleu accent (88, 166, 255)
OverlayHelper.TextPrimary    // texte principal
OverlayHelper.TextSecondary  // texte secondaire
OverlayHelper.TextMuted      // texte discret

// Créer un brush
new SolidColorBrush(Color.FromRgb(76, 217, 100))
OverlayHelper.Br(76, 217, 100)    // raccourci
OverlayHelper.Br(OverlayHelper.AccGreen)  // depuis la palette
```

### Formes

```csharp
// Rectangle
var rect = new Rectangle
{
    Width = 20, Height = 10,
    RadiusX = 3, RadiusY = 3,  // coins arrondis
    Fill = new SolidColorBrush(Colors.Red)
};

// Cercle
var cercle = new Ellipse
{
    Width = 10, Height = 10,
    Fill = new SolidColorBrush(Colors.Blue),
    Stroke = new SolidColorBrush(Colors.White),
    StrokeThickness = 1
};

// Sur un Canvas (positionnement libre)
Canvas.SetLeft(cercle, 50);  // position X
Canvas.SetTop(cercle, 30);   // position Y
monCanvas.Children.Add(cercle);
```

### Barres de progression (DIY)

```csharp
// Fond
var fond = new Border { Height = 8, Background = Br(40, 40, 40), CornerRadius = new CornerRadius(4) };

// Remplissage (mettre à jour .Width dans UpdateData)
var remplissage = new Border
{
    Height = 8,
    CornerRadius = new CornerRadius(4),
    Background = new SolidColorBrush(Colors.Green),
    HorizontalAlignment = HorizontalAlignment.Left,
    Width = 0  // sera mis à jour dynamiquement
};
fond.Child = remplissage;

// Dans UpdateData():
remplissage.Width = pourcentage * largeurMax;
```

### Visibility (montrer/cacher)

```csharp
monElement.Visibility = Visibility.Visible;    // visible
monElement.Visibility = Visibility.Collapsed;  // caché (ne prend plus de place)
monElement.Visibility = Visibility.Hidden;     // invisible mais prend toujours sa place
```

---

## Exemple complet : créer un overlay simple

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DouzeAssistance.Models;
using DouzeAssistance.Services;

namespace DouzeAssistance.Views.Overlays
{
    public class MonOverlay : BaseOverlayWindow
    {
        private readonly TextBlock _valeur1;
        private readonly Border _barre;

        public MonOverlay(DataService ds, OverlaySettings s) : base(ds, s)
        {
            // --- DESIGN ---
            var border = OverlayHelper.MakeBorder();

            var sp = new StackPanel();
            sp.Children.Add(OverlayHelper.MakeTitle("MON OVERLAY"));

            // Une cellule avec label + valeur
            var cell = OverlayHelper.MakeCell();
            var cellContent = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            cellContent.Children.Add(OverlayHelper.MakeLabel("MA DONNÉE"));
            _valeur1 = OverlayHelper.MakeValue(20);
            cellContent.Children.Add(_valeur1);
            cell.Child = cellContent;
            sp.Children.Add(cell);

            // Une barre
            var barreFond = new Border
            {
                Height = 8,
                Background = OverlayHelper.Br(40, 40, 40),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 4, 0, 0)
            };
            _barre = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = OverlayHelper.Br(OverlayHelper.AccGreen),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            barreFond.Child = _barre;
            sp.Children.Add(barreFond);

            border.Child = sp;
            Content = border;
        }

        public override void UpdateData()
        {
            // --- LOGIQUE (appelé 30x/sec) ---
            var input = DataService.GetInputData();

            _valeur1.Text = $"{input.Speed:F0} km/h";
            _barre.Width = Math.Max(0, input.Throttle * 200);

            // Changer la couleur selon une condition
            _valeur1.Foreground = new SolidColorBrush(
                input.Speed > 200 ? OverlayHelper.AccRed : OverlayHelper.TextPrimary);
        }
    }
}
```

---

## Données disponibles dans DataService

| Méthode                  | Retourne        | Contenu |
|--------------------------|-----------------|---------|
| GetInputData()           | InputData       | throttle, brake, steering, clutch, gear, RPM, speed |
| GetAllVehicles()         | List<VehicleData> | position, nom, classe, temps, gaps, secteurs... |
| GetTireData()            | TireData[4]     | températures 3 zones, usure, pression, freins |
| GetFuelData()            | FuelData        | fuel, conso/tour, tours restants |
| GetDashboardData()       | DashboardData   | tout ci-dessus + énergie, TC, ABS |
| GetGForceData()          | GForceData      | G latéral, longitudinal, combiné |
| GetDeltaData()           | DeltaData       | delta vs meilleur tour |
| GetDamageData()          | DamageData      | dégâts 8 zones, impacts |
| GetWeatherData()         | WeatherData     | temp air/piste, pluie, vent |
| GetPitStrategyData()     | PitStrategyData | pit optimal, fuel à ajouter |
| GetTrackLimitsData()     | TrackLimitsData | pénalités, hors-piste |
| GetLapHistory()          | List<LapRecord> | historique de tous les tours |
| GetCurrentFlag()         | byte            | drapeau actuel |
| GetGamePhase()           | byte            | phase de jeu (0-9) |

## Pour enregistrer un nouvel overlay

1. Ajouter `public OverlaySettings MonOverlay { get; set; } = new("Mon Overlay", false);` dans `AppConfig`
2. Ajouter `RegisterOverlay("MonOverlay", new MonOverlay(_dataService, _config.MonOverlay));` dans `OverlayManager.Initialize()`
3. Ajouter un onglet dans `MainWindow.xaml`
4. Ajouter `BuildSettingsPanel(MonPanel, _config.MonOverlay, "MonOverlay");` dans `MainWindow.xaml.cs`
