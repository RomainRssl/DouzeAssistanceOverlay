<!DOCTYPE html>
<html lang="fr">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Douze Assistance — Overlay Le Mans Ultimate</title>
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

/* ── NAV ── */
nav{position:sticky;top:0;z-index:100;background:rgba(10,16,16,.95);
    border-bottom:1px solid var(--border2);backdrop-filter:blur(8px)}
.nav-inner{max-width:1100px;margin:0 auto;padding:0 24px;
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

/* ── HERO ── */
.hero{min-height:88vh;display:flex;flex-direction:column;align-items:center;
      justify-content:center;text-align:center;padding:60px 24px;position:relative;overflow:hidden}
.hero::before{content:'';position:absolute;inset:0;
  background:radial-gradient(ellipse 80% 60% at 50% 40%,rgba(0,204,170,.06) 0%,transparent 70%);
  pointer-events:none}
.hero-badge{display:inline-block;background:var(--bg4);border:1px solid var(--border2);
            color:var(--accent);font-size:10px;letter-spacing:2px;
            padding:4px 14px;border-radius:20px;margin-bottom:28px}
.hero h1{font-size:clamp(32px,6vw,64px);font-weight:bold;letter-spacing:4px;
         color:#fff;line-height:1.1;margin-bottom:16px}
.hero h1 span{color:var(--accent)}
.hero p{font-size:clamp(12px,1.5vw,15px);color:var(--text2);max-width:560px;
        margin:0 auto 36px;line-height:1.8}
.hero-ctas{display:flex;gap:12px;flex-wrap:wrap;justify-content:center}
.btn{display:inline-flex;align-items:center;gap:8px;padding:12px 28px;border-radius:4px;
     font-family:Consolas;font-size:12px;font-weight:bold;letter-spacing:1px;
     text-decoration:none;transition:.2s;cursor:pointer;border:none}
.btn-primary{background:var(--accent);color:#0a1010}
.btn-primary:hover{background:var(--accent2)}
.btn-outline{background:transparent;color:var(--text);border:1px solid var(--border2)}
.btn-outline:hover{background:var(--bg4);border-color:var(--accent);color:var(--accent)}
.hero-stats{display:flex;gap:32px;flex-wrap:wrap;justify-content:center;
            margin-top:56px;padding-top:40px;border-top:1px solid var(--border)}
.stat{text-align:center}
.stat-val{font-size:28px;font-weight:bold;color:var(--accent)}
.stat-lbl{font-size:10px;color:var(--text3);letter-spacing:1px;margin-top:2px}

/* ── SECTIONS ── */
section{padding:80px 24px}
.section-inner{max-width:1100px;margin:0 auto}
.section-header{text-align:center;margin-bottom:56px}
.section-header h2{font-size:clamp(18px,3vw,28px);font-weight:bold;color:#fff;
                   letter-spacing:3px;margin-bottom:10px}
.section-header p{color:var(--text2);font-size:12px;max-width:500px;margin:0 auto}
.tag{display:inline-block;background:var(--bg4);border:1px solid var(--border2);
     color:var(--accent);font-size:9px;letter-spacing:2px;padding:2px 10px;
     border-radius:2px;margin-bottom:14px}

/* ── FEATURE GRID ── */
.features-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:1px;
               background:var(--border);border:1px solid var(--border)}
.feat{background:var(--bg2);padding:24px;transition:.2s}
.feat:hover{background:var(--bg3)}
.feat-icon{font-size:24px;margin-bottom:12px}
.feat h3{font-size:12px;font-weight:bold;color:#fff;letter-spacing:1px;margin-bottom:6px}
.feat p{font-size:11px;color:var(--text3);line-height:1.7}
.feat-badge{display:inline-block;font-size:8px;letter-spacing:1px;padding:1px 6px;
            border-radius:2px;margin-top:8px}
.feat-badge.new{background:rgba(0,204,170,.15);color:var(--accent);border:1px solid rgba(0,204,170,.3)}
.feat-badge.vr{background:rgba(100,100,255,.15);color:#aaaaff;border:1px solid rgba(100,100,255,.3)}

/* ── CATEGORIES ── */
.cat-section{margin-bottom:56px}
.cat-title{font-size:10px;letter-spacing:2px;color:var(--accent);
           margin-bottom:16px;padding-bottom:8px;border-bottom:1px solid var(--border)}

/* ── HOW IT WORKS ── */
.steps{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:24px}
.step{background:var(--bg3);border:1px solid var(--border2);border-radius:4px;padding:24px}
.step-num{width:32px;height:32px;background:var(--accent);color:#0a1010;border-radius:50%;
          display:flex;align-items:center;justify-content:center;
          font-weight:bold;font-size:14px;margin-bottom:14px}
.step h3{font-size:12px;color:#fff;margin-bottom:8px;letter-spacing:1px}
.step p{font-size:11px;color:var(--text3);line-height:1.7}

/* ── CTA BAND ── */
.cta-band{background:var(--bg3);border-top:1px solid var(--border2);
          border-bottom:1px solid var(--border2)}
.cta-band .section-inner{display:flex;align-items:center;
                          justify-content:space-between;flex-wrap:wrap;gap:24px;
                          padding-top:48px;padding-bottom:48px}
.cta-band h2{font-size:clamp(16px,2.5vw,24px);font-weight:bold;color:#fff;letter-spacing:2px}
.cta-band p{color:var(--text2);font-size:11px;margin-top:6px}

/* ── FOOTER ── */
footer{background:var(--bg2);border-top:1px solid var(--border);
       padding:32px 24px;text-align:center}
footer p{font-size:10px;color:var(--text3);margin-bottom:6px}
footer a{color:var(--accent);text-decoration:none}
footer a:hover{text-decoration:underline}

/* ── RESPONSIVE ── */
@media(max-width:600px){
  .hero-stats{gap:20px}
  .cta-band .section-inner{flex-direction:column;text-align:center}
}
</style>
</head>
<body>

<!-- NAV -->
<nav>
  <div class="nav-inner">
    <a class="nav-logo" href="index.php">
      <div>
        <span>DOUZE ASSISTANCE</span>
        <small>Le Mans Ultimate Overlay</small>
      </div>
    </a>
    <div class="nav-links">
      <a href="index.php" class="active">ACCUEIL</a>
      <a href="classement.php">CLASSEMENT</a>
      <a href="https://github.com/GITHUB_URL" class="btn-dl" target="_blank" rel="noopener">
        ↓ TÉLÉCHARGER
      </a>
    </div>
  </div>
</nav>

<!-- HERO -->
<section class="hero">
  <div class="hero-badge">LE MANS ULTIMATE · OVERLAY</div>
  <h1>DOUZE<br><span>ASSISTANCE</span></h1>
  <p>L'overlay de référence pour Le Mans Ultimate. 19 modules en temps réel, support VR, télémétrie avancée et classement communautaire.</p>
  <div class="hero-ctas">
    <a class="btn btn-primary" href="https://github.com/GITHUB_URL/releases" target="_blank" rel="noopener">
      ↓ TÉLÉCHARGER GRATUITEMENT
    </a>
    <a class="btn btn-outline" href="classement.php">
      VOIR LE CLASSEMENT
    </a>
  </div>
  <div class="hero-stats">
    <div class="stat"><div class="stat-val">19</div><div class="stat-lbl">OVERLAYS</div></div>
    <div class="stat"><div class="stat-val">30<small style="font-size:16px">Hz</small></div><div class="stat-lbl">MISE À JOUR</div></div>
    <div class="stat"><div class="stat-val">VR</div><div class="stat-lbl">STEAMVR · OPENXR</div></div>
    <div class="stat"><div class="stat-val">100<small style="font-size:16px">%</small></div><div class="stat-lbl">GRATUIT</div></div>
  </div>
</section>

<!-- FEATURES -->
<section style="background:var(--bg2);border-top:1px solid var(--border)">
  <div class="section-inner">
    <div class="section-header">
      <div class="tag">FONCTIONNALITÉS</div>
      <h2>TOUT CE DONT TU AS BESOIN EN COURSE</h2>
      <p>Chaque overlay est positionnable librement, verrouillable et activable indépendamment.</p>
    </div>

    <!-- Informations de course -->
    <div class="cat-section">
      <div class="cat-title">INFORMATIONS DE COURSE</div>
      <div class="features-grid">
        <div class="feat">
          <div class="feat-icon">🏁</div>
          <h3>CLASSEMENTS GLOBAL</h3>
          <p>Tableau de tous les pilotes avec positions, écarts, meilleurs secteurs et statut DRS/pit.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">↕️</div>
          <h3>CLASSEMENT RELATIF</h3>
          <p>Vue centrée sur toi : pilotes devant et derrière avec leurs écarts en temps réel.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">⏱️</div>
          <h3>TIMER D'ÉCART</h3>
          <p>Écart dynamique avec la voiture immédiatement devant et derrière, mis à jour en continu.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">🚩</div>
          <h3>DRAPEAUX</h3>
          <p>Alertes visuelles pour tous les drapeaux : jaune, rouge, SC, VSC, drapeau à damier.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">🌤️</div>
          <h3>MÉTÉO</h3>
          <p>Conditions météo actuelles, température piste et air, prévisions de changement.</p>
        </div>
      </div>
    </div>

    <!-- Données voiture -->
    <div class="cat-section">
      <div class="cat-title">DONNÉES VOITURE</div>
      <div class="features-grid">
        <div class="feat">
          <div class="feat-icon">📊</div>
          <h3>TABLEAU DE BORD</h3>
          <p>Vitesse, rapport, RPM, DRS, temps intermédiaires et statut voiture en un coup d'œil.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">🔵</div>
          <h3>PNEUS</h3>
          <p>Températures cœur/surface/intérieur, pressions et niveau d'usure pour chaque pneu.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">⛽</div>
          <h3>STRATÉGIE CARBURANT</h3>
          <p>Consommation par tour, estimations de relais, alerte carburant et stratégie d'arrêt.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">Δ</div>
          <h3>DELTA TEMPS</h3>
          <p>Comparaison en temps réel avec ton meilleur tour, secteur par secteur.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">💥</div>
          <h3>DÉGÂTS</h3>
          <p>Visualisation graphique des dommages aérodynamiques et mécaniques sur la voiture.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">📈</div>
          <h3>GRAPHIQUE D'INPUTS</h3>
          <p>Courbes gaz, frein, embrayage et volant en temps réel. Idéal pour l'analyse de pilotage.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">⭕</div>
          <h3>G-FORCE</h3>
          <p>Indicateur visuel des forces G latérales et longitudinales en virage et freinage.</p>
        </div>
      </div>
    </div>

    <!-- Navigation & sécurité -->
    <div class="cat-section">
      <div class="cat-title">NAVIGATION &amp; SÉCURITÉ</div>
      <div class="features-grid">
        <div class="feat">
          <div class="feat-icon">🗺️</div>
          <h3>CARTE DU CIRCUIT</h3>
          <p>Tracé généré automatiquement en roulant, mémorisé sur disque. Positions de tous les pilotes en temps réel.</p>
          <span class="feat-badge new">NOUVEAU</span>
        </div>
        <div class="feat">
          <div class="feat-icon">📡</div>
          <h3>RADAR DE PROXIMITÉ</h3>
          <p>Vue radar 360° des voitures proches. Indispensable en bagarre ou dans la circulation.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">↔️</div>
          <h3>RELATIF AVANT/ARRIÈRE</h3>
          <p>Pilotes immédiatement devant et derrière avec leurs positions absolues et leurs écarts.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">👁️</div>
          <h3>ANGLE MORT</h3>
          <p>Alerte visuelle quand une voiture entre dans ton angle mort, côté gauche ou droit.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">🔄</div>
          <h3>REJOIN</h3>
          <p>Assistant de retour en piste après une sortie : indication du trafic et de la fenêtre sûre.</p>
        </div>
      </div>
    </div>

    <!-- Analyse -->
    <div class="cat-section">
      <div class="cat-title">ANALYSE &amp; TÉLÉMÉTRIE</div>
      <div class="features-grid">
        <div class="feat">
          <div class="feat-icon">📋</div>
          <h3>HISTORIQUE DES TOURS</h3>
          <p>Tableau de tous tes tours avec temps secteurs, carburant consommé et compound pneumatique.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">📉</div>
          <h3>GRAPHIQUE DE TOURS</h3>
          <p>Comparaison visuelle de tes tours sur le circuit. Identifie tes zones de progression.</p>
        </div>
        <div class="feat">
          <div class="feat-icon">🎯</div>
          <h3>TÉLÉMÉTRIE</h3>
          <p>Enregistrement complet de la télémétrie par tour (vitesse, gaz, frein, RPM, direction). Export Excel et comparaison inter-tours.</p>
          <span class="feat-badge new">AVANCÉ</span>
        </div>
        <div class="feat">
          <div class="feat-icon">🏆</div>
          <h3>CLASSEMENT EN LIGNE</h3>
          <p>Tes meilleurs temps envoyés automatiquement au classement communautaire après chaque tour valide.</p>
          <span class="feat-badge new">NOUVEAU</span>
        </div>
        <div class="feat">
          <div class="feat-icon">📝</div>
          <h3>NOTE</h3>
          <p>Bloc-notes accessible en cours de course pour noter une stratégie ou un réglage.</p>
        </div>
      </div>
    </div>

    <!-- VR -->
    <div class="cat-section">
      <div class="cat-title">RÉALITÉ VIRTUELLE</div>
      <div class="features-grid">
        <div class="feat">
          <div class="feat-icon">🥽</div>
          <h3>SUPPORT STEAMVR</h3>
          <p>Tous les overlays positionnables dans l'espace 3D via le système d'overlay SteamVR.</p>
          <span class="feat-badge vr">VR</span>
        </div>
        <div class="feat">
          <div class="feat-icon">🥽</div>
          <h3>SUPPORT OPENXR</h3>
          <p>Backend OpenXR natif pour les runtimes compatibles (Varjo, Pico, etc.).</p>
          <span class="feat-badge vr">VR</span>
        </div>
        <div class="feat">
          <div class="feat-icon">⚙️</div>
          <h3>MASQUAGE AUTO MENUS</h3>
          <p>Les overlays se masquent automatiquement dans les menus et pendant les pauses. Affichage uniquement en piste.</p>
        </div>
      </div>
    </div>
  </div>
</section>

<!-- HOW IT WORKS -->
<section>
  <div class="section-inner">
    <div class="section-header">
      <div class="tag">DÉMARRAGE RAPIDE</div>
      <h2>OPÉRATIONNEL EN 2 MINUTES</h2>
      <p>Pas de configuration complexe, pas d'abonnement. Télécharge, lance, joue.</p>
    </div>
    <div class="steps">
      <div class="step">
        <div class="step-num">1</div>
        <h3>TÉLÉCHARGER</h3>
        <p>Télécharge la dernière version depuis GitHub. Un simple exécutable, aucune installation requise.</p>
      </div>
      <div class="step">
        <div class="step-num">2</div>
        <h3>LANCER</h3>
        <p>Lance <strong>LMUOverlay.exe</strong> avant ou après Le Mans Ultimate. La connexion est automatique.</p>
      </div>
      <div class="step">
        <div class="step-num">3</div>
        <h3>ACTIVER</h3>
        <p>Active les overlays de ton choix depuis l'interface, positionne-les sur tes écrans.</p>
      </div>
      <div class="step">
        <div class="step-num">4</div>
        <h3>ROULER</h3>
        <p>Les overlays apparaissent dès que tu es en piste et se masquent automatiquement dans les menus.</p>
      </div>
    </div>
  </div>
</section>

<!-- CTA -->
<section class="cta-band">
  <div class="section-inner">
    <div>
      <h2>PRÊT À GAGNER DU TEMPS ?</h2>
      <p>Gratuit, open-source, sans inscription. Rejoins la communauté.</p>
    </div>
    <div style="display:flex;gap:12px;flex-wrap:wrap">
      <a class="btn btn-primary" href="https://github.com/GITHUB_URL/releases" target="_blank" rel="noopener">
        ↓ TÉLÉCHARGER
      </a>
      <a class="btn btn-outline" href="classement.php">
        VOIR LE CLASSEMENT
      </a>
    </div>
  </div>
</section>

<!-- FOOTER -->
<footer>
  <p>
    <a href="index.php">Accueil</a> ·
    <a href="classement.php">Classement</a> ·
    <a href="https://github.com/GITHUB_URL" target="_blank" rel="noopener">GitHub</a>
  </p>
  <p style="margin-top:10px">Douze Assistance — Overlay gratuit et open-source pour Le Mans Ultimate</p>
</footer>

</body>
</html>
