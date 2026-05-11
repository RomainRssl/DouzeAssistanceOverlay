<!DOCTYPE html>
<html lang="fr">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Classement — Douze Assistance</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
:root{
  --bg:#0a1010;--bg2:#0d1616;--bg3:#121e1e;--bg4:#1a2c2c;
  --accent:#00ccaa;--accent2:#00ffcc;
  --border:#1a3030;--border2:#244444;
  --text:#e0f0f0;--text2:#90b0b0;--text3:#506e6e;
  --green:#2e5a2e;
}
html{scroll-behavior:smooth}
body{background:var(--bg);color:var(--text);font-family:Consolas,monospace;font-size:13px;line-height:1.6}

/* NAV */
nav{position:sticky;top:0;z-index:100;background:rgba(10,16,16,.95);
    border-bottom:1px solid var(--border2);backdrop-filter:blur(8px)}
.nav-inner{max-width:1200px;margin:0 auto;padding:0 24px;
           display:flex;align-items:center;justify-content:space-between;height:52px}
.nav-logo{display:flex;align-items:center;gap:10px;text-decoration:none}
.nav-logo span{font-size:14px;font-weight:bold;color:var(--accent);letter-spacing:2px}
.nav-logo small{font-size:9px;color:var(--text3);display:block;line-height:1}
.nav-links{display:flex;align-items:center;gap:4px}
.nav-links a{color:var(--text2);text-decoration:none;padding:6px 14px;border-radius:3px;
             font-size:11px;letter-spacing:1px;transition:.15s}
.nav-links a:hover{color:var(--text);background:var(--bg4)}
.nav-links a.active{color:var(--accent)}
.nav-links .btn-dl{background:var(--green);color:#fff;border:1px solid #3a7a3a;margin-left:8px}
.nav-links .btn-dl:hover{background:#3a7a3a}

/* PAGE HEADER */
.page-header{padding:32px 24px 24px;border-bottom:1px solid var(--border);
             background:var(--bg2)}
.page-header-inner{max-width:1200px;margin:0 auto;
                   display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:12px}
.page-header h1{font-size:20px;font-weight:bold;color:#fff;letter-spacing:3px}
.page-header p{font-size:10px;color:var(--text3);margin-top:4px}

/* FILTERS */
.filter-bar{background:var(--bg3);border-bottom:1px solid var(--border);padding:10px 24px;
            display:flex;gap:8px;flex-wrap:wrap;align-items:center}
.filter-bar-inner{max-width:1200px;margin:0 auto;width:100%;display:flex;gap:8px;flex-wrap:wrap;align-items:center}
.filter-label{font-size:10px;color:var(--text3);letter-spacing:1px;margin-right:4px}
.filter-btn{background:var(--bg4);border:1px solid var(--border2);color:var(--text2);
            padding:4px 14px;border-radius:3px;cursor:pointer;
            font-family:Consolas;font-size:10px;letter-spacing:1px;transition:.15s}
.filter-btn:hover{color:var(--text)}
.filter-btn.active{color:var(--text);border-color:currentColor}

/* TABLE */
.table-wrap{overflow-x:auto;padding:24px}
.table-inner{max-width:1200px;margin:0 auto}
table{width:100%;border-collapse:collapse}
th{background:var(--bg4);border:1px solid var(--border2);padding:10px 16px;
   font-size:10px;letter-spacing:1px;white-space:nowrap}
th.th-circuit{text-align:left;color:var(--text3)}
th.th-class{text-align:center;font-weight:bold}
td{border:1px solid var(--border);padding:10px 14px;vertical-align:top;min-width:170px}
td:first-child{min-width:220px;background:var(--bg2)}
.td-circuit{font-size:11px;color:var(--text);font-weight:bold;white-space:nowrap}
.entry{padding:4px 0;border-bottom:1px solid var(--border)}
.entry:last-child{border-bottom:none}
.entry-rank{font-size:10px;color:var(--text3);min-width:18px;display:inline-block}
.entry-name{color:var(--accent);font-size:11px}
.entry-time{color:#fff;font-size:12px;font-weight:bold;margin-left:6px}
.entry-gap{color:var(--text3);font-size:10px;margin-left:4px}
.entry-sectors{font-size:9px;color:var(--text3);margin-top:2px;margin-left:18px}
.entry-car{font-size:9px;color:var(--text3);margin-top:1px;margin-left:18px;
           white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:140px}
td.td-empty{text-align:center;color:var(--border2);font-size:18px;vertical-align:middle;padding:20px}
#loading{text-align:center;padding:80px;color:var(--text3);font-size:12px}
#error{text-align:center;padding:80px;color:#ff4466;font-size:12px}
</style>
</head>
<body>

<nav>
  <div class="nav-inner">
    <a class="nav-logo" href="index.php">
      <div>
        <span>DOUZE ASSISTANCE</span>
        <small>Le Mans Ultimate Overlay</small>
      </div>
    </a>
    <div class="nav-links">
      <a href="index.php">ACCUEIL</a>
      <a href="classement.php" class="active">CLASSEMENT</a>
      <a href="https://github.com/GITHUB_URL" class="btn-dl" target="_blank" rel="noopener">
        ↓ TÉLÉCHARGER
      </a>
    </div>
  </div>
</nav>

<div class="page-header">
  <div class="page-header-inner">
    <div>
      <h1>CLASSEMENT COMMUNAUTAIRE</h1>
      <p>Meilleurs temps par circuit et par catégorie · Mis à jour en temps réel</p>
    </div>
    <div id="stats-bar" style="font-size:10px;color:var(--text3)"></div>
  </div>
</div>

<div class="filter-bar">
  <div class="filter-bar-inner">
    <span class="filter-label">CATÉGORIE :</span>
    <div id="filters"></div>
  </div>
</div>

<div class="table-wrap">
  <div class="table-inner">
    <div id="loading">Chargement du classement...</div>
    <div id="error" style="display:none"></div>
    <table id="board" style="display:none">
      <thead id="thead"></thead>
      <tbody id="tbody"></tbody>
    </table>
  </div>
</div>

<footer style="background:var(--bg2);border-top:1px solid var(--border);padding:24px;text-align:center">
  <p style="font-size:10px;color:var(--text3)">
    <a href="index.php" style="color:var(--accent);text-decoration:none">Accueil</a> ·
    <a href="classement.php" style="color:var(--accent);text-decoration:none">Classement</a> ·
    <a href="https://github.com/GITHUB_URL" target="_blank" rel="noopener" style="color:var(--accent);text-decoration:none">GitHub</a>
  </p>
</footer>

<script>
const API = 'api/leaderboard.php';
const CLASS_ORDER = ['GT3','HYPERCAR','LMP2','LMP3','LMP2_ELMS','GTE'];
const CLASS_COLORS = {
  GT3:'#00cc88', HYPERCAR:'#ff4466', LMP2:'#44aaff',
  LMP3:'#cc66ff', LMP2_ELMS:'#2288ff', GTE:'#ffaa00'
};

let rawData = {};
let presentClasses = new Set();
let activeClasses = new Set();

function fmt(s) {
  if (!s || s <= 0) return '—';
  const m = Math.floor(s / 60);
  const sec = (s % 60).toFixed(3).padStart(6, '0');
  return m > 0 ? `${m}:${sec}` : sec;
}

function buildFilters() {
  const container = document.getElementById('filters');
  container.innerHTML = '';
  CLASS_ORDER.filter(c => presentClasses.has(c)).forEach(cls => {
    const btn = document.createElement('button');
    btn.className = 'filter-btn active';
    btn.textContent = cls;
    btn.style.color = CLASS_COLORS[cls] || '#fff';
    btn.style.borderColor = CLASS_COLORS[cls] || '#244444';
    btn.onclick = () => {
      if (activeClasses.has(cls)) { activeClasses.delete(cls); btn.classList.remove('active'); btn.style.opacity='.4'; }
      else                        { activeClasses.add(cls);    btn.classList.add('active');    btn.style.opacity='1'; }
      render();
    };
    container.appendChild(btn);
  });
}

function render() {
  const visClasses = CLASS_ORDER.filter(c => activeClasses.has(c) && presentClasses.has(c));
  const circuits   = Object.keys(rawData).sort();

  // Header
  const thead = document.getElementById('thead');
  thead.innerHTML = '';
  const hrow = document.createElement('tr');
  hrow.innerHTML = '<th class="th-circuit">CIRCUIT</th>';
  visClasses.forEach(c => {
    hrow.innerHTML += `<th class="th-class" style="color:${CLASS_COLORS[c]||'#fff'}">${c}</th>`;
  });
  thead.appendChild(hrow);

  // Rows
  const tbody = document.getElementById('tbody');
  tbody.innerHTML = '';
  circuits.forEach(circuit => {
    const tr = document.createElement('tr');
    let html = `<td><div class="td-circuit">${circuit}</div></td>`;
    visClasses.forEach(cls => {
      const entries = (rawData[circuit] || {})[cls] || [];
      if (!entries.length) { html += '<td class="td-empty">—</td>'; return; }
      const best = entries[0].lapTime;
      html += '<td>';
      entries.slice(0, 5).forEach((e, i) => {
        const gap  = i > 0 ? `<span class="entry-gap">+${(e.lapTime - best).toFixed(3)}</span>` : '';
        const secs = (e.sector1 && e.sector2 && e.sector3)
          ? `<div class="entry-sectors">${fmt(e.sector1)} · ${fmt(e.sector2)} · ${fmt(e.sector3)}</div>` : '';
        const car  = e.carName ? `<div class="entry-car">${e.carName}</div>` : '';
        html += `<div class="entry">
          <span class="entry-rank">${i+1}.</span>
          <span class="entry-name">${e.username}</span>
          <span class="entry-time">${fmt(e.lapTime)}</span>${gap}
          ${car}${secs}
        </div>`;
      });
      html += '</td>';
    });
    tr.innerHTML = html;
    tbody.appendChild(tr);
  });

  document.getElementById('board').style.display = circuits.length ? 'table' : 'none';
  document.getElementById('loading').style.display = 'none';
}

fetch(API)
  .then(r => { if (!r.ok) throw new Error(r.status); return r.json(); })
  .then(data => {
    rawData = data;
    let totalLaps = 0, totalDrivers = new Set();
    Object.values(data).forEach(byClass =>
      Object.entries(byClass).forEach(([,entries]) => {
        entries.forEach(e => { totalDrivers.add(e.username); totalLaps++; });
        entries.forEach(e => presentClasses.add(e.constructor?.name || Object.keys(byClass).find(k => byClass[k] === entries)));
      })
    );
    // Rebuild presentClasses correctly
    presentClasses = new Set();
    Object.values(data).forEach(byClass => Object.keys(byClass).forEach(c => presentClasses.add(c)));
    activeClasses = new Set(presentClasses);

    const circuitCount = Object.keys(data).length;
    document.getElementById('stats-bar').textContent =
      `${circuitCount} circuits · ${totalDrivers.size} pilotes · ${totalLaps} temps enregistrés`;

    buildFilters();
    render();
  })
  .catch(err => {
    document.getElementById('loading').style.display = 'none';
    const el = document.getElementById('error');
    el.style.display = 'block';
    el.textContent = 'Impossible de charger le classement. Réessaye plus tard.';
  });
</script>
</body>
</html>
