# 🏎️ Douze Assistance — Le Mans Ultimate Racing HUD

Overlay temps réel pour **Le Mans Ultimate** basé sur la **shared memory rF2** (rF2SharedMemoryMapPlugin).

---

## 📋 16 Overlays

| Overlay | Description |
|---------|-------------|
| 📡 **Radar de Proximité** | Vue radar des véhicules autour du joueur |
| 🏆 **Classement Général** | Classification par catégorie (Hypercar, LMP2, GTE, GT3) |
| 📊 **Classement Relatif** | Véhicules proches en position avec écarts |
| 🗺 **Carte du Circuit** | Position de tous les concurrents sur la carte |
| 🎮 **Graphique Inputs** | Throttle/Brake/Clutch + volant rotatif + traces |
| ⏱ **Écarts Temps** | Gap avec véhicule devant/derrière + tendance |
| 🌦 **Météo** | Température air/piste, pluie, vent, humidité |
| 🏁 **Drapeaux** | Affichage drapeaux actifs (bleu, jaune, vert, noir, damier) |
| 🔧 **Pneus & Freins** | Températures (inner/mid/outer), usure, pression, freins |
| ⛽ **Essence** | Niveau, consommation/tour, arrêts nécessaires, déficit |
| ⏲ **Delta Temps** | Delta vs meilleur tour en temps réel + prédiction |
| 🔄 **Stratégie Pit** | Tour optimal de pit, carburant à ajouter, usure pneus |
| 💥 **Dommages** | 8 indicateurs de dégâts autour du véhicule + impacts |
| 📋 **Historique Tours** | Tableau de tous les tours avec S1/S2/S3, fuel, gomme |
| 🎯 **Force G** | Mètre G-Force avec dot plot, trail et valeurs |
| 🏎 **Dashboard** | Mini dashboard digital avec arc RPM, vitesse, position |

## 🆕 Fonctionnalités

| Fonctionnalité | Description |
|----------------|-------------|
| **Export CSV** | Export historique tours + snapshot télémétrie en CSV |
| **Profils par circuit** | Sauvegarde/chargement de positions d'overlay par circuit |
| **Support multi-écrans** | Sélecteur d'écran pour placer chaque overlay |
| **Mode Chroma Key** | Fond vert/bleu/magenta pour streaming |
| **Curseur taille** | Zoom 30% — 300% par overlay |
| **Curseur opacité** | Transparence 10% — 100% par overlay |
| **Verrouillage** | Empêche le déplacement accidentel |
| **Connexion auto** | Se connecte automatiquement au jeu |

---

## 🔧 Prérequis

- **Windows 10/11** (64-bit) + **.NET 8.0 SDK**
- **Le Mans Ultimate** avec le plugin **rF2SharedMemoryMapPlugin**

### Installation du plugin

1. Télécharger depuis [TheIronWolfModding/rF2SharedMemoryMapPlugin](https://github.com/TheIronWolfModding/rF2SharedMemoryMapPlugin)
2. Copier la DLL dans `Le Mans Ultimate\Bin64\Plugins\`

## 🚀 Compilation

```bash
dotnet build -c Release
dotnet run --project LMUOverlay -c Release
```

---

## 📁 Architecture (34 fichiers source)

```
LMUOverlay/
├── rF2SharedMemory/          # Bibliothèque shared memory (officielle)
│   ├── rF2Data.cs            # Structs officielles TheIronWolfModding
│   └── SharedMemoryReader.cs # Lecteur memory-mapped files
├── LMUOverlay/
│   ├── Models/OverlayConfig.cs       # Config + 10 data models
│   ├── Services/
│   │   ├── ConfigService.cs          # JSON persistence
│   │   ├── DataService.cs            # Traitement données → modèles
│   │   ├── OverlayManager.cs         # Cycle de vie 16 overlays
│   │   ├── CsvExportService.cs       # Export CSV
│   │   └── ProfileService.cs         # Profils par circuit
│   ├── Views/
│   │   ├── MainWindow.xaml/.cs       # Interface principale 14 onglets
│   │   └── Overlays/ (18 fichiers)   # Tous les overlays
│   ├── Themes/DarkTheme.xaml         # Thème sombre racing
│   └── Converters/Converters.cs
```

## 📄 Licence

MIT
