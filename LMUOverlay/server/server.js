const express  = require('express');
const cors     = require('cors');
const Database = require('better-sqlite3');
const path     = require('path');
const fs       = require('fs');

const PORT     = process.env.PORT || 3000;
const DATA_DIR = path.join(__dirname, 'data');
const DB_PATH  = path.join(DATA_DIR, 'classement.db');

// ── Database ──────────────────────────────────────────────────────────────────

if (!fs.existsSync(DATA_DIR)) fs.mkdirSync(DATA_DIR);

const db = new Database(DB_PATH);

db.exec(`
  CREATE TABLE IF NOT EXISTS drivers (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    prenom  TEXT NOT NULL,
    nom     TEXT NOT NULL,
    discord TEXT,
    token   TEXT NOT NULL,
    UNIQUE(prenom, nom)
  );

  CREATE TABLE IF NOT EXISTS laps (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    driver_id   INTEGER NOT NULL REFERENCES drivers(id),
    circuit     TEXT    NOT NULL,
    car_class   TEXT    NOT NULL,
    car_name    TEXT,
    lap_time    REAL    NOT NULL,
    sector1     REAL,
    sector2     REAL,
    sector3     REAL,
    app_version TEXT,
    created_at  TEXT    DEFAULT (datetime('now'))
  );

  CREATE INDEX IF NOT EXISTS idx_laps_lookup
    ON laps(driver_id, circuit, car_class, lap_time);
`);

// ── Express ───────────────────────────────────────────────────────────────────

const app = express();
app.use(cors());
app.use(express.json());

// ── POST /submit  — reçoit un temps depuis l'app desktop ─────────────────────

app.post('/submit', (req, res) => {
  const { prenom, nom, discord, token, circuit, carClass,
          carName, lapTime, sector1, sector2, sector3, version } = req.body ?? {};

  for (const [k, v] of [['prenom', prenom], ['nom', nom], ['token', token],
                         ['circuit', circuit], ['carClass', carClass], ['lapTime', lapTime]]) {
    if (!v && v !== 0) return res.status(400).json({ error: `Missing: ${k}` });
  }

  const lt = parseFloat(lapTime);
  if (isNaN(lt) || lt <= 0 || lt > 86400)
    return res.status(400).json({ error: 'Invalid lap time' });

  const p   = String(prenom).trim().slice(0, 30);
  const n   = String(nom).trim().slice(0, 30);
  const dis = discord ? String(discord).trim().slice(0, 50) : null;
  const tok = String(token).trim().slice(0, 64);

  let driver = db.prepare('SELECT id, token FROM drivers WHERE prenom = ? AND nom = ?').get(p, n);

  if (!driver) {
    const info = db.prepare(
      'INSERT INTO drivers (prenom, nom, discord, token) VALUES (?, ?, ?, ?)'
    ).run(p, n, dis, tok);
    driver = { id: info.lastInsertRowid, token: tok };
  } else if (driver.token !== tok) {
    return res.status(403).json({ error: 'Invalid token' });
  } else if (dis) {
    // Met à jour le pseudo Discord si changé
    db.prepare('UPDATE drivers SET discord = ? WHERE id = ?').run(dis, driver.id);
  }

  db.prepare(`
    INSERT INTO laps (driver_id, circuit, car_class, car_name, lap_time,
                      sector1, sector2, sector3, app_version)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
  `).run(
    driver.id,
    String(circuit).trim().slice(0, 100),
    String(carClass).trim().slice(0, 30),
    carName  ? String(carName).trim().slice(0, 100) : null,
    lt,
    sector1 != null ? parseFloat(sector1) : null,
    sector2 != null ? parseFloat(sector2) : null,
    sector3 != null ? parseFloat(sector3) : null,
    version ? String(version).trim().slice(0, 20) : null
  );

  res.json({ success: true });
});

// ── GET /leaderboard  — meilleur temps par pilote × circuit × classe ──────────

app.get('/leaderboard', (_req, res) => {
  const rows = db.prepare(`
    SELECT l.circuit, l.car_class,
           d.prenom || ' ' || UPPER(d.nom) AS username,
           d.discord,
           l.car_name, l.lap_time, l.sector1, l.sector2, l.sector3
    FROM laps l
    JOIN drivers d ON l.driver_id = d.id
    JOIN (
      SELECT driver_id, circuit, car_class, MIN(lap_time) AS best
      FROM laps
      GROUP BY driver_id, circuit, car_class
    ) b ON  l.driver_id = b.driver_id
        AND l.circuit   = b.circuit
        AND l.car_class = b.car_class
        AND l.lap_time  = b.best
    ORDER BY l.circuit, l.car_class, l.lap_time ASC
  `).all();

  const data = {};
  for (const r of rows) {
    if (!data[r.circuit])               data[r.circuit] = {};
    if (!data[r.circuit][r.car_class])  data[r.circuit][r.car_class] = [];
    data[r.circuit][r.car_class].push({
      username: r.username,
      discord:  r.discord ?? '',
      carName:  r.car_name ?? '',
      lapTime:  r.lap_time,
      sector1:  r.sector1 ?? null,
      sector2:  r.sector2 ?? null,
      sector3:  r.sector3 ?? null,
    });
  }

  res.json(data);
});

// ── GET /health ───────────────────────────────────────────────────────────────

app.get('/health', (_req, res) => {
  const laps    = db.prepare('SELECT COUNT(*) AS n FROM laps').get();
  const drivers = db.prepare('SELECT COUNT(*) AS n FROM drivers').get();
  res.json({ status: 'ok', laps: laps.n, drivers: drivers.n });
});

// ── Démarrage ─────────────────────────────────────────────────────────────────

app.listen(PORT, () => {
  console.log(`Douze Assistance — serveur classement démarré sur le port ${PORT}`);
  console.log(`  POST /submit      → reçoit les temps de l'app`);
  console.log(`  GET  /leaderboard → sert le classement`);
  console.log(`  GET  /health      → état du serveur`);
});
