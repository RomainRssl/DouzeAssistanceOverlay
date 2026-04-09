# 🏎️ Douze Assistance — Le Mans Ultimate Racing HUD

![Version](https://img.shields.io/badge/version-1.5.6-blue) ![.NET](https://img.shields.io/badge/.NET-8.0-purple) ![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4) ![Licence](https://img.shields.io/badge/licence-MIT-green)

Overlay HUD temps réel pour **Le Mans Ultimate**, basé sur la shared memory rF2. Affiche 20 overlays configurables superposés à la fenêtre du jeu : classements, télémétrie, stratégie, météo, drapeaux et bien plus.

> 📸 *Screenshots à venir — placez une image `docs/screenshot.png` pour l'afficher ici.*

---

## 📑 Table des matières

1. [Prérequis](#-prérequis)
2. [Installation](#-installation)
   - [Étape 1 — Plugin rF2SharedMemoryMapPlugin](#étape-1--plugin-rf2sharedmemorymapplugin-obligatoire)
   - [Étape 2 — Installer Douze Assistance](#étape-2--installer-douze-assistance)
   - [Étape 3 — Premier lancement](#étape-3--premier-lancement)
3. [Les 20 Overlays](#-les-20-overlays)
4. [Paramètres communs](#-paramètres-communs-à-tous-les-overlays)
5. [Paramètres globaux](#-paramètres-globaux)
6. [Fonctionnalités avancées](#-fonctionnalités-avancées)
7. [Architecture technique](#-architecture-technique)
8. [Licence](#-licence)

---

## 📋 Prérequis

| Composant | Version minimale |
|---|---|
| Windows | 10 ou 11 (64-bit) |
| [.NET 8.0 Runtime](https://dotnet.microsoft.com/fr-fr/download/dotnet/8.0) | 8.0.x |
| Le Mans Ultimate | Toute version récente (Steam) |
| rF2SharedMemoryMapPlugin | Dernière version |

---

## 🚀 Installation

### Étape 1 — Plugin rF2SharedMemoryMapPlugin (obligatoire)

Ce plugin permet à LMU d'exposer ses données en mémoire partagée, ce que Douze Assistance lit en temps réel.

1. Télécharger la dernière version depuis le dépôt officiel :
   **[TheIronWolfModding/rF2SharedMemoryMapPlugin](https://github.com/TheIronWolfModding/rF2SharedMemoryMapPlugin)**

2. Copier le fichier `rFactor2SharedMemoryMapPlugin64.dll` dans le dossier Plugins de LMU :
   ```
   <Steam>\steamapps\common\Le Mans Ultimate\Bin64\Plugins\
   ```
   Le chemin Steam par défaut est généralement :
   ```
   C:\Program Files (x86)\Steam\steamapps\common\Le Mans Ultimate\Bin64\Plugins\
   ```

3. Lancer Le Mans Ultimate, aller dans **Paramètres → Plugins** et cocher le plugin dans la liste.

4. **Relancer LMU** pour que le plugin soit pris en compte.

> ⚠️ Sans ce plugin, Douze Assistance ne peut pas récupérer les données du jeu. L'indicateur de connexion dans l'interface restera rouge.

---

### Étape 2 — Installer Douze Assistance

#### Option A — Via l'installateur *(recommandé)*

1. Aller dans l'onglet **[Releases](../../releases)** de ce dépôt GitHub.
2. Télécharger le fichier `DouzeAssistance_Setup_vX.X.X.exe` (dernière version disponible).
3. Exécuter l'installateur et suivre l'assistant d'installation.
4. L'application se lance automatiquement à la fin de l'installation.

#### Option B — Compilation depuis les sources

Nécessite le [SDK .NET 8.0](https://dotnet.microsoft.com/fr-fr/download/dotnet/8.0).

```bash
git clone https://github.com/<votre-repo>/douze-assistance.git
cd douze-assistance/LMUOverlay
dotnet build -c Release
dotnet run --project LMUOverlay -c Release
```

---

### Étape 3 — Premier lancement

1. **Lancer LMU en premier** — rejoindre une session de jeu ou rester dans le menu principal.
2. **Lancer Douze Assistance** — l'icône apparaît dans la barre des tâches.
3. Cliquer sur **Connecter** dans l'interface principale. Le voyant passe au vert si le plugin est bien installé.
4. Dans chaque onglet de l'interface, **activer les overlays** souhaités avec le bouton on/off.
5. **Déplacer** les overlays par glisser-déposer directement sur l'écran.
6. **Redimensionner** via la poignée triangulaire en bas à droite de chaque overlay.
7. **Verrouiller** les overlays une fois positionnés pour éviter les déplacements accidentels en course.

> 💡 Les positions et tailles sont sauvegardées automatiquement par circuit (profils). Chaque piste peut avoir sa propre disposition d'overlays.

---

## 🖥️ Les 20 Overlays

### Vue d'ensemble

| # | Overlay | Description courte | Rafraîchissement |
|---|---|---|---|
| 1 | 📡 Radar de Proximité | Véhicules détectés dans un rayon de 30m | 30 Hz |
| 2 | 🏆 Classement Général | Classification par catégorie, 24 colonnes | 10 Hz |
| 3 | 📊 Classement Relatif | Toutes classes mélangées, gap en temps réel | 10 Hz |
| 4 | 🗺️ Carte du Circuit | Positions de tous les concurrents sur le tracé | 10 Hz |
| 5 | 🎮 Graphique Inputs | Throttle/brake/clutch, volant rotatif, traces | 30 Hz |
| 6 | ⏱️ Écarts Temps | Gap avec le véhicule devant et derrière | 30 Hz |
| 7 | 🌦️ Météo | Températures, pluie, vent, nuages | 10 Hz |
| 8 | 🏁 Drapeaux | Drapeau actif + direction et distance de l'incident | 30 Hz |
| 9 | 🔧 Pneus & Freins | Températures, usure, pression, freins par roue | 30 Hz |
| 10 | ⛽ Stratégie Carburant | Niveau, conso, fenêtre de pit, énergie hybride | 10 Hz |
| 11 | ⏲️ Delta Temps | Delta vs meilleur tour en continu + prédiction | 30 Hz |
| 12 | 📋 Historique des Tours | Tableau de tous les tours complétés | 10 Hz |
| 13 | 📈 Graphique des Tours | Courbe des chronos sur les 9 derniers tours | 10 Hz |
| 14 | 💥 Dommages | 8 zones de dégâts + pourcentage total | 10 Hz |
| 15 | 🎯 Mètre G-Force | Plot 2D avec trail et cercles de référence | 30 Hz |
| 16 | 🏎️ Dashboard | Vitesse, RPM, rapport, position, carburant | 30 Hz |
| 17 | 👁️ Angle Mort | Alertes visuelle gauche/droite | 30 Hz |
| 18 | ↕️ Relatif Devant/Derrière | Affichage compact : voiture devant, joueur, derrière | 30 Hz |
| 19 | ⚠️ Alerte Rejoin | Sécurité rentrée sur piste (actif si ≤ 15 m/s) | 30 Hz |
| 20 | 📝 Notes | Bloc-notes persistant | — |

---

### Fiches détaillées

---

#### 1. 📡 Radar de Proximité

Affiche un radar circulaire centré sur le joueur avec les véhicules proches dans un rayon de 30 mètres.

- **Code couleur par distance :** rouge (0–5m), jaune (5–15m), vert (15–30m)
- Deux anneaux de référence concentriques
- Opacité des points diminue avec la distance
- La taille du point indique l'angle relatif (devant/derrière)

| Paramètre | Description |
|---|---|
| Rayon de détection | Fixe : 30m |

---

#### 2. 🏆 Classement Général

Classification complète organisée par catégorie : Hypercar, LMP2, LMP3, LMGTE, GT3, GT4. Chaque groupe affiche un en-tête de classe avec le meilleur temps de la catégorie et le nombre de voitures. Si le joueur est hors des N premières lignes, sa position s'affiche quand même avec ses voisins immédiats.

<details>
<summary><strong>24 colonnes disponibles (cliquer pour développer)</strong></summary>

| Colonne | Description |
|---|---|
| Position | Rang absolu dans la course |
| Barre de classe | Bande colorée par catégorie |
| Pilote | Nom du pilote (format Prénom N.) |
| Nom voiture | Badge marque du constructeur |
| Numéro | Numéro de course (badge coloré) |
| Progression tour | Fraction du tour actuel |
| Meilleur tour | Meilleur chrono absolu |
| Dernier tour | Chrono du dernier tour complété |
| Gap suivant | Écart sur le véhicule devant |
| Gap leader | Écart sur le leader de classe |
| Delta | Delta vs meilleur tour de classe |
| Secteur 1 | Temps secteur 1 en cours |
| Secteur 2 | Temps secteur 2 en cours |
| Secteur 3 | Temps secteur 3 en cours |
| Statut secteurs | Indicateurs S1/S2/S3 (violet/vert/jaune) |
| Compound pneus | 4 points colorés par compound (2×2) |
| Pit stops | Nombre d'arrêts effectués |
| Vitesse | Vitesse instantanée (km/h) |
| Pénalités | Pénalités de temps |
| Indicateur | Drapeau / statut courant (point coloré) |
| Dommages | Niveau de dégâts |
| Tours de relais | Tours effectués sur le relais actuel |
| Temps de relais | Durée du relais actuel |
| Tours totaux | Nombre de tours total effectués |

</details>

| Paramètre | Défaut | Description |
|---|---|---|
| Entrées max (classe joueur) | 10 | Lignes affichées pour la classe du joueur |
| Entrées max (autres classes) | 3 | Lignes affichées pour les autres classes |
| Barre de session | Activé | Affiche météo et infos session en en-tête |
| Colonnes actives | Voir ci-dessus | Chaque colonne s'active/désactive indépendamment |

---

#### 3. 📊 Classement Relatif

Affiche toutes les catégories mélangées, triées par gap réel sur la piste par rapport au joueur. Les voitures en avance ont un gap négatif (affiché en vert), celles en retard un gap positif (affiché en rouge).

| Paramètre | Défaut | Description |
|---|---|---|
| Voitures devant | 5 | Nombre de véhicules à afficher devant |
| Voitures derrière | 5 | Nombre de véhicules à afficher derrière |

---

#### 4. 🗺️ Carte du Circuit

Dessine le tracé du circuit avec la position de tous les concurrents. Le joueur est mis en évidence par un halo. Si une image du circuit (`<NomCircuit>.png`) est disponible dans le dossier `Tracks/`, elle est utilisée comme fond. Sinon, le tracé est généré automatiquement à partir des positions GPS des véhicules et sauvegardé pour les prochaines sessions.

**Nommage des images de circuit :** correspondance exacte, nom avec underscores, ou correspondance approximative (ex. `lemans.png` pour `Le Mans 24h`).

---

#### 5. 🎮 Graphique Inputs

Affiche les entrées de pilotage en temps réel.

- **Barres verticales** : throttle (vert), brake (rouge), clutch (bleu)
- **Trace historique** : courbe des N derniers frames pour chaque axe
- **Volant rotatif** : indicateur graphique ±450° selon l'angle réel
- **Données numériques** : rapport, vitesse (km/h), régime (RPM)
- **Alerte trail brake** : clignotement si throttle et brake actifs simultanément
- **Ghost inputs** : superposition des entrées du meilleur tour à la position piste courante

| Paramètre | Défaut | Description |
|---|---|---|
| Afficher throttle | Oui | Barre et trace verte |
| Afficher brake | Oui | Barre et trace rouge |
| Afficher clutch | Non | Barre et trace bleue |
| Afficher volant | Oui | Indicateur rotatif |
| Afficher rapport | Oui | — |
| Afficher vitesse | Oui | — |
| Afficher RPM | Oui | — |
| Afficher graphique | Oui | Courbes historiques |
| Alerte trail brake | Oui | Flash rouge si double appui |
| Ghost inputs | Non | Superposition meilleur tour |
| Épaisseur traits | 1.5 | Épaisseur des courbes (px) |

---

#### 6. ⏱️ Écarts Temps

Affiche le gap en secondes avec le véhicule immédiatement devant (▲ fond vert) et celui immédiatement derrière (▼ fond rouge), avec les noms des pilotes.

---

#### 7. 🌦️ Météo

Affiche les conditions météo de la session en cours.

- Température ambiante et température de piste (°C)
- Niveau de pluie (0–100%)
- Couverture nuageuse (0–100%)
- Vitesse du vent (km/h) et direction
- Taux d'humidité de la piste
- Icône météo colorée (ensoleillé / nuageux / pluvieux)
- Flèches de tendance pluie et nuages (en hausse / stable / en baisse)
- Texte descriptif de l'état météo

---

#### 8. 🏁 Drapeaux

Affiche le drapeau de course actuellement actif avec le nom en grand.

**Drapeaux supportés :** Vert · Jaune · Bleu · Rouge · Blanc · Damier · Noir

En cas de drapeau **jaune :**
- Direction de l'incident (gauche / centre / droite)
- Distance en mètres jusqu'à l'incident
- Nom du pilote à l'origine du drapeau

---

#### 9. 🔧 Pneus & Freins

Quatre panneaux disposés en croix (FL, FR, RL, RR) avec le schéma du véhicule au centre.

Pour chaque roue :
- **Températures** : intérieure / milieu / extérieure (code couleur bleu→vert→jaune→rouge)
- **Usure** (%) avec indicateur visuel
- **Pression** (PSI ou kPa)
- **Température de frein** (°C)
- **Compound** du pneu
- Indicateur de crevaison / pneu à plat

---

#### 10. ⛽ Stratégie Carburant

Informations de stratégie carburant et arrêt au stand.

- **Jauge carburant** : niveau actuel avec pourcentage et litres
- **Consommation** : litres/tour calculée sur les derniers tours
- **Autonomie** : nombre de tours restants avant panne
- **Jauge énergie** (hybride/Hypercar) : niveau énergie virtuelle et consommation/tour
- **Fenêtre de pit** : tour optimal pour l'arrêt, carburant à ajouter
- **Usure pneus** restante et tours possibles sur les pneus actuels
- **Facteur limitant** : carburant ou énergie (selon lequel est le plus contraignant)
- **Nombre d'arrêts** encore nécessaires pour finir la course

---

#### 11. ⏲️ Delta Temps

Delta en temps réel par rapport au meilleur tour personnel, calculé à la position courante sur le circuit.

- **Affichage numérique** : delta en secondes (+ en retard, − en avance)
- **Barre visuelle** : proportionnelle au delta
- **Code couleur** : vert (en avance), blanc (neutre), rouge (en retard)
- **Temps affichés** : meilleur tour · dernier tour · tour en cours · prédiction

---

#### 12. 📋 Historique des Tours

Tableau récapitulatif de tous les tours complétés depuis le début de la session.

- Numéro de tour, chrono complet (M:SS.mmm)
- Delta vs tour précédent
- Température de piste au moment du tour
- Meilleur tour mis en évidence (barre violette)
- Défilement automatique vers le dernier tour

---

#### 13. 📈 Graphique des Tours

Courbe graphique des chronos sur les **9 derniers tours** avec mise à l'échelle automatique.

- **Couleur des points :** violet (meilleur tour session) · vert (amélioration) · rouge (régression) · croix (tour actuel)
- Étiquettes de temps sur l'axe droit
- Lignes de connexion avec dégradé entre les tours consécutifs

---

#### 14. 💥 Dommages

Schéma du véhicule avec 8 zones d'impact.

**Zones :** Avant · Arrière · Gauche · Droite · Avant-gauche · Avant-droite · Arrière-gauche · Arrière-droite

Pour chaque zone :
- **Couleur** : gris (aucun) · orange (léger) · rouge (sévère)
- Pourcentage de dégâts total de la voiture
- Estimation du temps de réparation si en stand

---

#### 15. 🎯 Mètre G-Force

Représentation 2D des forces G avec trace temporelle.

- **Axe X** : force latérale (gauche ↔ droite)
- **Axe Y** : force longitudinale (accélération ↕ freinage)
- **Trail** : 30 derniers samples avec fondu
- **Cercles de référence** : 1G, 2G, 3G
- **Code couleur** : bleu (<2G) · jaune (2–3G) · rouge (>3G)
- Valeurs numériques : G latéral, G longitudinal, G combiné

---

#### 16. 🏎️ Dashboard

Mini tableau de bord digital complet.

- **Arc de vitesse** : barre 0–350 km/h avec remplissage coloré
- **Arc RPM** : barre avec flash de shift light à l'approche du régime maxi
- **Rapport engagé** : grand affichage central (change de couleur en zone rouge)
- **Grille d'informations** :

| Donnée | Description |
|---|---|
| Position | Rang dans la course |
| Tour | Tour actuel / total |
| Carburant | Niveau en litres |
| Énergie | Niveau énergie hybride (si disponible) |
| Conso/tour | Consommation carburant au tour |
| Conso énergie/tour | Consommation énergie au tour |
| Tours restants | Estimation selon conso actuelle |
| Temps restant | Durée restante de la session |
| ABS | Indicateur ABS actif |
| TC | Indicateur contrôle de traction actif |
| Temp eau | Température liquide de refroidissement |
| Temp huile | Température huile moteur |

- **Indicateur limiteur de vitesse** : clignotement "PIT" quand le limiteur est actif

| Paramètre | Défaut | Description |
|---|---|---|
| Afficher vitesse | Oui | Arc de vitesse |
| Afficher RPM | Oui | Arc régime + shift light |
| Afficher rapport | Oui | — |
| Afficher position | Oui | — |
| Afficher tour | Oui | — |
| Afficher carburant | Oui | — |
| Afficher énergie | Non | Pour véhicules hybrides |
| Afficher conso/tour | Non | — |
| Afficher tours restants | Oui | — |
| Afficher temps restant | Oui | — |
| Afficher ABS | Oui | — |
| Afficher temp eau | Non | — |
| Afficher temp huile | Non | — |

---

#### 17. 👁️ Angle Mort (Blind Spot)

Deux panneaux HUD (GAUCHE / DROITE) qui s'allument en couleur quand un véhicule se trouve dans l'angle mort du joueur.

- Détection de présence dans la zone latérale (à hauteur du cockpit)
- État inactif : fond sombre quasi invisible
- État actif : fond coloré avec effet de halo
- Taille et espacement ajustables

---

#### 18. ↕️ Relatif Devant/Derrière

Affichage ultra-compact en 3 lignes :

```
▲  M. Dupont   +1.234s
●  JOUEUR      ─────
▼  K. Smith    -0.891s
```

Nom du pilote, point de couleur de classe, gap en secondes.

---

#### 19. ⚠️ Alerte Rejoin

S'active automatiquement quand le joueur est lent ou immobile (≤ 15 m/s) — typiquement après une sortie de piste ou un accident.

- **✓ PISTE LIBRE** (fond vert) : aucune voiture proche derrière
- **⚠ ATTENTION** (fond orange) : véhicule en approche
- **✕ ATTENDRE** (fond rouge clignotant) : danger immédiat, ne pas rentrer

Affiche la distance et la vitesse du véhicule le plus proche derrière.

---

#### 20. 📝 Notes

Bloc-notes libre intégré dans l'overlay.

- Zone de texte multilignes (redimensionnable de 180 à 400 px de hauteur)
- Sauvegarde automatique dans la configuration
- Toujours visible quand activé (non masqué en menu)
- Utile pour les notes de setup, stratégie, stint planning

---

## ⚙️ Paramètres communs à tous les overlays

Ces paramètres sont disponibles pour chaque overlay individuellement dans l'interface.

| Paramètre | Valeur par défaut | Description |
|---|---|---|
| Activé | Non | Active ou désactive l'overlay |
| Position X | Variable | Coordonnée horizontale en pixels |
| Position Y | Variable | Coordonnée verticale en pixels |
| Échelle | 1.0× | Zoom de 0.3× (30%) à 3.0× (300%) |
| Opacité | 100% | Transparence de 10% à 100% |
| Verrouillé | Non | Empêche le déplacement accidentel |
| Largeur fixe | Auto (0) | Force une largeur en pixels (0 = automatique) |
| Hauteur fixe | Auto (0) | Force une hauteur en pixels (0 = automatique) |

---

## 🌐 Paramètres globaux

Accessibles depuis l'onglet **Paramètres** de l'interface principale.

### Général

| Paramètre | Défaut | Description |
|---|---|---|
| Taux de rafraîchissement | 30 Hz | Fréquence de mise à jour des overlays (10–60 Hz) |
| Toujours au premier plan | Oui | Les overlays restent au-dessus du jeu |
| Masquer dans les menus | Oui | Overlays masqués quand le jeu est en pause/ESC |
| Connexion automatique | Oui | Tente de se connecter au jeu au démarrage |
| Démarrer minimisé | Non | Démarre sans afficher l'interface principale |

### Alertes vocales

| Paramètre | Défaut | Description |
|---|---|---|
| Alertes vocales activées | Non | Active les annonces audio |
| Volume | 80% | Volume des alertes (0–100%) |
| Vitesse de parole | Normal (0) | Vitesse TTS de −10 à +10 |
| Voix | Système | Moteur de synthèse vocale Windows |
| Pack vocal | default | Dossier de fichiers WAV personnalisés |

### Alertes individuelles

| Alerte | Défaut | Déclencheur |
|---|---|---|
| Carburant critique | Activé | Niveau carburant faible |
| Drapeaux | Activé | Drapeau jaune ou bleu actif |
| Écarts | Activé | Gap ≤ seuil (défaut 1.0s) |
| Tours | Activé | Passage de tour |
| Position | Activé | Changement de position |
| Spotter | Activé | Véhicule dans l'angle mort |

### Streaming & VR

| Paramètre | Défaut | Description |
|---|---|---|
| Mode Chroma Key | Non | Remplace le fond des overlays par une couleur unie |
| Couleur Chroma Key | #00FF00 (vert) | Couleur de fond pour la suppression d'arrière-plan |
| VR activé | Non | Active le rendu en réalité virtuelle |
| Échelle VR globale | 1.0 | Taille de tous les overlays en VR |

---

## 🔬 Fonctionnalités avancées

### Export de données

- **Export CSV** — Export de l'historique complet des tours avec tous les secteurs, la télémétrie et les conditions météo (disponible depuis l'onglet *Données*).
- **Export Excel (.xlsx)** — Export formaté avec feuilles séparées par session.

### Profils par circuit

Les positions, tailles et états de tous les overlays sont sauvegardés séparément pour chaque circuit. Douze Assistance détecte automatiquement le circuit chargé et applique le profil correspondant. Vous pouvez créer, nommer et supprimer des profils manuellement.

### Packs vocaux personnalisés

Pour remplacer les voix TTS par des enregistrements audio :
1. Créer un sous-dossier dans `voice/` : `voice/mon-pack/`
2. Y placer des fichiers WAV nommés selon les événements (ex. `blue_flag.wav`, `fuel_low.wav`)
3. Sélectionner le pack dans Paramètres → Pack vocal

Les fichiers WAV manquants retombent automatiquement sur la synthèse vocale système.

### Support multi-écrans

Chaque overlay peut être assigné à un écran différent via le sélecteur d'écran dans ses paramètres. Utile pour les configurations avec plusieurs moniteurs ou un écran dédié HUD.

### Réalité Virtuelle (VR)

Douze Assistance supporte deux backends VR :

- **OpenXR** (natif) — aucune dépendance supplémentaire, compatible avec toutes les casques OpenXR (HP Reverb, Meta, Pico, Pimax, Varjo…)
- **SteamVR** (via OpenVR) — placer `openvr_api.dll` dans le dossier de l'application pour activer ce backend

L'échelle globale VR permet d'ajuster la taille apparente de tous les overlays dans le casque simultanément.

### Leaderboard en ligne

Douze Assistance peut envoyer automatiquement vos meilleurs chronos vers un classement en ligne :
- Configurer le nom de pilote et l'URL d'API dans l'onglet *Profil*
- Un token d'authentification unique est généré automatiquement
- L'envoi s'effectue après chaque tour si l'option est activée

---

## 📁 Architecture technique

```
LMUOverlay/
├── rF2SharedMemory/
│   ├── rF2Data.cs                # Structs officielles TheIronWolfModding
│   └── SharedMemoryReader.cs     # Lecteur memory-mapped files (dirty-check)
│
└── LMUOverlay/
    ├── Models/
    │   └── OverlayConfig.cs      # 20+ modèles de données + configs overlay
    │
    ├── Services/
    │   ├── DataService.cs        # Traduction shared memory → modèles métier
    │   ├── ConfigService.cs      # Sérialisation JSON des paramètres
    │   ├── OverlayManager.cs     # Cycle de vie des 20 overlays
    │   ├── VoiceService.cs       # TTS + lecture WAV
    │   ├── CsvExportService.cs   # Export CSV
    │   ├── ExcelExportService.cs # Export .xlsx
    │   ├── LeaderboardService.cs # Envoi chronos API
    │   └── ProfileService.cs     # Profils par circuit
    │
    ├── Views/
    │   ├── MainWindow.xaml/.cs   # Interface principale (14 onglets)
    │   └── Overlays/             # 20 classes overlay + BaseOverlayWindow
    │       ├── BaseOverlayWindow.cs
    │       ├── StandingsOverlay.cs
    │       ├── RelativeOverlay.cs
    │       ├── TrackMapOverlay.cs
    │       ├── InputGraphOverlay.cs
    │       ├── GapOverlay.cs
    │       ├── WeatherOverlay.cs
    │       ├── FlagOverlay.cs
    │       ├── TireInfoOverlay.cs
    │       ├── FuelStrategyOverlay.cs
    │       ├── DeltaOverlay.cs
    │       ├── LapHistoryOverlay.cs
    │       ├── LapGraphOverlay.cs
    │       ├── DamageOverlay.cs
    │       ├── GForceOverlay.cs
    │       ├── DashboardOverlay.cs
    │       ├── BlindSpotOverlay.cs
    │       ├── RelativeAheadBehindOverlay.cs
    │       ├── RejoinOverlay.cs
    │       ├── NoteOverlay.cs
    │       └── ProximityRadarOverlay.cs
    │
    ├── Helpers/
    │   ├── BrushCache.cs         # Cache de SolidColorBrush (performances)
    │   ├── OverlayHelper.cs      # Helpers UI communs + couleurs thème
    │   └── ScreenHelper.cs       # Gestion multi-écrans
    │
    ├── Themes/
    │   └── DarkTheme.xaml        # Thème sombre racing
    │
    └── Resources/
        ├── Tracks/               # Images de circuits (.png/.jpg)
        ├── Damage/               # Assets overlay dommages
        └── Manufacturers/        # Logos constructeurs (.png)

voice/
├── default/                      # Pack vocal par défaut (WAV)
└── <custom>/                     # Packs vocaux personnalisés
```

**Dépendances NuGet :**

| Package | Version | Usage |
|---|---|---|
| AutoUpdater.NET.Official | 1.9.2 | Mises à jour automatiques |
| ClosedXML | 0.103+ | Export Excel |
| CommunityToolkit.Mvvm | 8.2.2 | Patterns MVVM |
| Newtonsoft.Json | 13.0.3 | Sérialisation config JSON |
| System.Speech | 8.0.0 | Synthèse vocale TTS |
| Silk.NET.OpenXR | 2.22.0 | Support VR OpenXR |

---

## 📄 Licence

Distribué sous licence **MIT**. Voir le fichier `LICENSE` pour les détails.

Ce projet utilise la bibliothèque [rF2SharedMemoryMapPlugin](https://github.com/TheIronWolfModding/rF2SharedMemoryMapPlugin) de TheIronWolfModding.
