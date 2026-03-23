# Track Images — Douze Assistance

## How to add track images

1. Place PNG or JPG images of circuit layouts in this folder
2. Name them to match the track name from the game

### Naming

The overlay tries to match the filename to the game's track name.
It searches in order:
- Exact match: `Le Mans 24h.png`
- Sanitized match: `Le_Mans_24h.png`
- Fuzzy/partial match: `lemans.png` matches "Le Mans 24h"

### Image guidelines

- **Transparent or black background** preferred
- **Top-down view** of the circuit outline
- White, light gray, or colored track outline works best
- Any resolution — the overlay scales it to fit
- PNG with transparency gives the cleanest look

### Runtime folder

You can also place images in a `Tracks/` folder next to the exe:
```
DouzeAssistance.exe
Tracks/
  Le Mans 24h.png
  Spa-Francorchamps.png
  Monza.png
```

This way you can add new tracks without rebuilding the project.

### If no image is found

The overlay falls back to drawing the track from vehicle positions
(the old behavior — collects points from all cars on track).
