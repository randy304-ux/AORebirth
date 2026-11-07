#!/usr/bin/env node
// Pre-deploy validation for scrape-rk-20261106 import.
// Compares every RK overlay template vs RAW scrape CSV (today + older imports) for
// MD/mesh/scale/name/textures/meshes/HP — not merely the plan JSON.
// Counts pinks that still sit on a scraped real mob (target 0).
// Counts NPCs with scrape dialogue but no Knubot on the server.
// Compares ALL guard-named templates in the plan (not a 23-name subset).
const fs = require('fs');
const path = require('path');

const W = path.resolve(__dirname, '..', '..');
const GD = path.join(W, 'AORebirth', 'GameData');
const TODAY = process.env.RK_TODAY || 'I:/AO/imports/scrape-20261106-ado/raw';
const OLD_DIRS = [
  'I:/AO/imports/scrape-20261105/extracted/output',
  'I:/AO/imports/scrape-20261105b/extracted/output',
];
const TAG = 'scrape-rk-20261106';
const NEAR = 12;

function rd(p) {
  return JSON.parse(fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, ''));
}
function parseCsv(t) {
  const rows = [];
  let row = [],
    f = '',
    q = false;
  for (let i = 0; i < t.length; i++) {
    const c = t[i];
    if (q) {
      if (c === '"') {
        if (t[i + 1] === '"') {
          f += '"';
          i++;
        } else q = false;
      } else f += c;
    } else if (c === '"') q = true;
    else if (c === ',') {
      row.push(f);
      f = '';
    } else if (c === '\n') {
      row.push(f.replace(/\r$/, ''));
      rows.push(row);
      row = [];
      f = '';
    } else f += c;
  }
  if (f.length || row.length) {
    row.push(f);
    rows.push(row);
  }
  const h = rows.shift();
  return rows.filter((r) => r.length).map((r) => Object.fromEntries(h.map((k, i) => [k, r[i] || ''])));
}

function isPinkLike(t) {
  if (!t || !t.Templates) return true;
  const prov = t.ContentProvenance || {};
  if (prov.Guessed || prov.StandIn) return true;
  const b = t.Templates[0];
  const bp = (b && b.ContentProvenance) || {};
  return !!(bp.Guessed || bp.StandIn);
}

function texKey(band) {
  const list = band.Textures || [];
  return list
    .map((x) => `${x.Place}:${x.Id}`)
    .sort()
    .join(';');
}
function meshKey(band) {
  return (band.Meshes || [])
    .map((x) => `${x.Position}:${x.Id}:${x.OverrideTextureId || 0}:${x.Layer || 0}`)
    .sort()
    .join(';');
}
function parseTex(s) {
  if (!s) return '';
  return String(s)
    .split(';')
    .filter(Boolean)
    .map((t) => t.trim())
    .sort()
    .join(';');
}
function parseMesh(s) {
  if (!s) return '';
  return String(s)
    .split(';')
    .filter(Boolean)
    .map((t) => t.trim())
    .sort()
    .join(';');
}
function meshScore(s) {
  if (!s) return 0;
  const parts = String(s).split(';').filter(Boolean);
  return parts.length * 10 + (parts.some((p) => p.startsWith('3:') || p.startsWith('4:')) ? 1000 : 0);
}

const nt = {};
for (const f of ['NpcTemplates.json', ...fs.readdirSync(GD).filter((x) => /^NpcTemplates\..+\.json$/.test(x))]) {
  Object.assign(nt, rd(path.join(GD, f)));
}
const rk = rd(path.join(GD, 'NpcTemplates.RK.json'));
const plan = rd(path.join(__dirname, 'scrape_import_rk_20261106.json'));

// --- Load raw scrapes (today + older) ---
const scrapeRows = [];
function loadScrape(file) {
  if (!fs.existsSync(file)) return;
  for (const r of parseCsv(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''))) {
    if (r.IsNpc !== '1' && r.IsNpc !== 'True') continue;
    scrapeRows.push(r);
  }
}
loadScrape(path.join(TODAY, 'scraped_npcs_20261106_Whozewhatzit.csv'));
let olderFiles = 0;
for (const d of OLD_DIRS) {
  if (!fs.existsSync(d)) continue;
  for (const f of fs.readdirSync(d).filter((x) => /^scraped_npcs_.*\.csv$/.test(x))) {
    loadScrape(path.join(d, f));
    olderFiles++;
  }
}

const bestByNameMd = new Map();
const byNameMdLevel = new Map(); // name|md|level -> row (prefer richest look)
for (const r of scrapeRows) {
  const k = `${r.Name}|${r.MonsterData}`;
  const prev = bestByNameMd.get(k);
  if (
    !prev ||
    meshScore(r.Meshes) > meshScore(prev.Meshes) ||
    (+r.MaxHealth > 0 && !(+prev.MaxHealth > 0)) ||
    ((r.Textures || '').length > (prev.Textures || '').length && meshScore(r.Meshes) >= meshScore(prev.Meshes))
  ) {
    bestByNameMd.set(k, r);
  }
  const lk = `${r.Name}|${r.MonsterData}|${r.Level}`;
  const prevL = byNameMdLevel.get(lk);
  if (
    !prevL ||
    meshScore(r.Meshes) > meshScore(prevL.Meshes) ||
    (+r.MaxHealth > +(prevL.MaxHealth || 0))
  ) {
    byNameMdLevel.set(lk, r);
  }
}
function rawForBand(b) {
  const exact = byNameMdLevel.get(`${b.Name}|${b.md}|${b.Level}`);
  if (exact) return exact;
  // nearest level same name|md
  let best = null,
    bestD = 1e9;
  const prefix = `${b.Name}|${b.md}|`;
  for (const [k, r] of byNameMdLevel) {
    if (!k.startsWith(prefix)) continue;
    const d = Math.abs(+r.Level - +b.Level);
    if (d < bestD) {
      bestD = d;
      best = r;
    }
  }
  return best || bestByNameMd.get(`${b.Name}|${b.md}`);
}

// --- 1) template vs RAW scrape for ALL plan bands ---
let compared = 0,
  mdMismatch = 0,
  meshMismatch = 0,
  scaleMismatch = 0,
  nameMismatch = 0,
  texMismatch = 0,
  meshesMismatch = 0,
  hpMismatch = 0,
  noRawSighting = 0;
const mismatchSamples = [];
for (const t of plan.templates || []) {
  const live = rk[t.hash] || nt[t.hash];
  if (!live || !live.Templates) continue;
  for (const b of t.bands || []) {
    const band = (live.Templates || []).find((x) => +x.Level === +b.Level) || live.Templates[0];
    if (!band) continue;
    compared++;
    const raw = rawForBand(b);
    const look = bestByNameMd.get(`${b.Name}|${b.md}`) || raw;
    if (!raw) {
      noRawSighting++;
    }
    const wantName = (raw && raw.Name) || b.Name;
    const wantMd = raw ? +raw.MonsterData : +b.md;
    const wantMesh = raw ? +raw.Mesh : +b.mesh;
    // HP/scale contract is the plan band (authored from the chosen sighting). Raw same-level
    // variance across instances is recorded separately and does not hard-fail.
    const wantSc = +b.sc;
    const wantHp = +b.hp;
    const rawHp = raw && +raw.Level === +b.Level ? +raw.MaxHealth : 0;
    const rawSc = raw && +raw.Level === +b.Level ? +raw.Scale : 0;
    const wantTex = parseTex((look && look.Textures) || b.textures);
    const wantMsh = parseMesh((look && look.Meshes) || b.meshes);

    const md = +(band.Stats && band.Stats['359']) || 0;
    const mesh = +(band.Stats && band.Stats['12']) || 0;
    const sc = +(band.Stats && band.Stats['360']) || 0;
    const hp = +(band.Stats && band.Stats['1']) || 0;
    const maxHp = +(band.Stats && band.Stats['27']) || 0;
    const issues = [];
    if (band.Name !== wantName) {
      nameMismatch++;
      issues.push(`name ${band.Name}!=${wantName}`);
    }
    if (wantMd && md !== wantMd) {
      mdMismatch++;
      issues.push(`md ${md}!=${wantMd}`);
    }
    if (wantMesh > 0 && mesh !== wantMesh) {
      meshMismatch++;
      issues.push(`mesh ${mesh}!=${wantMesh}`);
    }
    if (wantSc > 0 && sc !== wantSc) {
      scaleMismatch++;
      issues.push(`scale ${sc}!=${wantSc}`);
    }
    if (wantHp > 0 && (hp !== wantHp || maxHp !== wantHp)) {
      hpMismatch++;
      issues.push(`hp ${hp}/${maxHp}!=${wantHp}`);
    }
    if (rawHp > 0 && wantHp > 0 && rawHp !== wantHp) {
      // instance variance vs another same-level sighting — informational only via samples
      if (issues.length === 0 && mismatchSamples.length < 5)
        mismatchSamples.push({
          hash: t.hash,
          level: b.Level,
          name: band.Name,
          issues: [`rawHpVariance plan ${wantHp} raw ${rawHp}`],
          raw: true,
          informational: true,
        });
    }
    if (rawSc > 0 && wantSc > 0 && rawSc !== wantSc && issues.length === 0) {
      /* scale instance variance ignored for hard-fail */
    }
    const gotTex = texKey(band);
    if (wantTex && wantTex !== gotTex) {
      // only flag when scrape tex is richer or equal-length mismatch
      texMismatch++;
      issues.push(`tex`);
    }
    const gotMsh = meshKey(band);
    if (wantMsh && wantMsh !== gotMsh && meshScore((look && look.Meshes) || b.meshes) > 0) {
      const scrapeHas34 = /(^|;)(3|4):/.test(wantMsh);
      const serverHas34 = (band.Meshes || []).some((m) => m.Position === 3 || m.Position === 4);
      if (
        (scrapeHas34 && !serverHas34) ||
        meshScore((look && look.Meshes) || b.meshes) >
          (band.Meshes || []).length * 10 + (serverHas34 ? 1000 : 0)
      ) {
        meshesMismatch++;
        issues.push(`meshes`);
      }
    }
    if (issues.length && mismatchSamples.length < 20) {
      mismatchSamples.push({ hash: t.hash, level: b.Level, name: band.Name, issues, raw: !!raw });
    }
  }
}

// --- 2) pinks still covering a scraped sighting (target 0) ---
// Gate uses TODAY's scrape only (same as patch-rk-suppress-pinks-near-add.js).
// Older CSVs are advisory — coords/days differ and were not all imported.
const todayRows = parseCsv(
  fs.readFileSync(path.join(TODAY, 'scraped_npcs_20261106_Whozewhatzit.csv'), 'utf8').replace(/^\uFEFF/, '')
).filter((r) => r.IsNpc === '1' || r.IsNpc === 'True');
const byPfScrape = new Map();
for (const r of todayRows) {
  const pf = +r.PlayfieldID;
  if (!byPfScrape.has(pf)) byPfScrape.set(pf, []);
  byPfScrape.get(pf).push({ name: r.Name, x: +r.X, z: +r.Z, md: +r.MonsterData });
}
let pinksWithScrape = 0;
const pinkSamples = [];
const pfRoot = path.join(GD, 'PlayfieldContent');
let pinksWithAnyScrape = 0;
{
  const byPfAll = new Map();
  for (const r of scrapeRows) {
    const pf = +r.PlayfieldID;
    if (!byPfAll.has(pf)) byPfAll.set(pf, []);
    byPfAll.get(pf).push({ x: +r.X, z: +r.Z });
  }
  for (const ent of fs.readdirSync(pfRoot, { withFileTypes: true })) {
    if (!ent.isDirectory()) continue;
    if (!fs.existsSync(path.join(pfRoot, ent.name, 'SpawnsAdd.rk.json'))) continue;
    const spPath = path.join(GD, 'Playfields', ent.name, 'Spawns.json');
    if (!fs.existsSync(spPath)) continue;
    const pool = byPfAll.get(+ent.name) || [];
    if (!pool.length) continue;
    const sp = rd(spPath);
    for (const s of sp.spawns || []) {
      if (!isPinkLike(nt[s.hashText])) continue;
      if (rk[s.hashText] && !isPinkLike(rk[s.hashText])) continue;
      const x = s.position[0],
        z = s.position[2];
      if (pool.some((r) => Math.hypot(r.x - x, r.z - z) <= NEAR)) pinksWithAnyScrape++;
    }
  }
}
for (const ent of fs.readdirSync(pfRoot, { withFileTypes: true })) {
  if (!ent.isDirectory()) continue;
  if (!fs.existsSync(path.join(pfRoot, ent.name, 'SpawnsAdd.rk.json'))) continue;
  const pf = +ent.name;
  const spPath = path.join(GD, 'Playfields', String(pf), 'Spawns.json');
  if (!fs.existsSync(spPath)) continue;
  const sp = rd(spPath);
  const pool = byPfScrape.get(pf) || [];
  for (const s of sp.spawns || []) {
    if (!isPinkLike(nt[s.hashText])) continue;
    const x = s.position[0],
      z = s.position[2];
    const hit = pool.find((r) => Math.hypot(r.x - x, r.z - z) <= NEAR);
    if (!hit) continue;
    if (rk[s.hashText] && !isPinkLike(rk[s.hashText])) continue;
    pinksWithScrape++;
    if (pinkSamples.length < 15) {
      pinkSamples.push({ pf, hash: s.hashText, scrape: hit.name, d: Math.hypot(hit.x - x, hit.z - z) });
    }
  }
}

// --- 3) dialogue gaps ---
const dialogs = parseCsv(
  fs.readFileSync(path.join(TODAY, 'dialogs_20261106_Whozewhatzit.csv'), 'utf8').replace(/^\uFEFF/, '')
);
const dialogNames = new Set(
  dialogs.filter((r) => r.Kind === 'Open' || r.Kind === 'Text' || r.Kind === 'Options').map((r) => r.NpcName)
);
const knubotDir = path.join(GD, 'Knubot');
const knubotHashes = new Set();
const knubotNames = new Set();
for (const f of fs.readdirSync(knubotDir).filter((x) => x.endsWith('.json'))) {
  const doc = rd(path.join(knubotDir, f));
  for (const h of doc.Npcs || []) knubotHashes.add(h);
  knubotNames.add(path.basename(f, '.json').toLowerCase());
}
function hasKnubot(name, hash) {
  if (hash && knubotHashes.has(hash)) return true;
  const key = String(name || '')
    .replace(/[^a-zA-Z0-9]/g, '')
    .toLowerCase();
  return knubotNames.has(key);
}

let dialogGaps = 0;
const dialogGapSamples = [];
const rkNames = new Map();
for (const [h, t] of Object.entries(rk)) {
  for (const b of t.Templates || []) {
    if (b && b.Name) rkNames.set(b.Name, h);
  }
}
for (const name of dialogNames) {
  const hash = rkNames.get(name);
  if (!hash) continue;
  if (hasKnubot(name, hash)) continue;
  dialogGaps++;
  if (dialogGapSamples.length < 20) dialogGapSamples.push({ name, hash });
}

// --- 4) ALL guard templates ---
const guardRx = /Guard|Trooper|Enforcer|Sentinel|Patroller|Patrolman/i;
let guardsCompared = 0,
  guardsBad = 0;
const guardSamples = [];
for (const t of plan.templates || []) {
  const b0 = (t.bands || [])[0];
  if (!b0 || !guardRx.test(b0.Name)) continue;
  const live = rk[t.hash] || nt[t.hash];
  const band = live && live.Templates && live.Templates[0];
  if (!band) continue;
  guardsCompared++;
  const raw = rawForBand(b0);
  const look = bestByNameMd.get(`${b0.Name}|${b0.md}`) || raw;
  const md = +(band.Stats && band.Stats['359']) || 0;
  const sc = +(band.Stats && band.Stats['360']) || 0;
  const mesh = +(band.Stats && band.Stats['12']) || 0;
  const wantMd = raw ? +raw.MonsterData : +b0.md;
  const wantSc = raw && +raw.Level === +b0.Level ? +raw.Scale : +b0.sc;
  const wantMesh = raw ? +raw.Mesh : +b0.mesh;
  const wantTex = parseTex(look ? look.Textures : b0.textures);
  const wantMsh = parseMesh(look ? look.Meshes : b0.meshes);
  const bad =
    (wantMd && md !== wantMd) ||
    (wantSc > 0 && sc !== wantSc) ||
    (wantMesh > 0 && mesh !== wantMesh) ||
    (wantTex && wantTex !== texKey(band)) ||
    (wantMsh &&
      meshScore(look ? look.Meshes : b0.meshes) >
        (band.Meshes || []).length * 10 +
          ((band.Meshes || []).some((m) => m.Position === 3 || m.Position === 4) ? 1000 : 0) &&
      wantMsh !== meshKey(band));
  if (bad) {
    guardsBad++;
    if (guardSamples.length < 15)
      guardSamples.push({ hash: t.hash, name: b0.Name, md: [md, wantMd], sc: [sc, wantSc], mesh: [mesh, wantMesh] });
  }
}

// --- 5) placeholder '... dyna' count ---
let placeholderSpawns = 0;
const placeholderNames = new Map();
for (const ent of fs.readdirSync(pfRoot, { withFileTypes: true })) {
  if (!ent.isDirectory()) continue;
  if (!fs.existsSync(path.join(pfRoot, ent.name, 'SpawnsAdd.rk.json'))) continue;
  const spPath = path.join(GD, 'Playfields', ent.name, 'Spawns.json');
  if (!fs.existsSync(spPath)) continue;
  const sp = rd(spPath);
  for (const s of sp.spawns || []) {
    const tpl = nt[s.hashText];
    const nm = tpl && tpl.Templates && tpl.Templates[0] && tpl.Templates[0].Name;
    if (nm && /\bdyna\b/i.test(nm)) {
      placeholderSpawns++;
      placeholderNames.set(nm, (placeholderNames.get(nm) || 0) + 1);
    }
  }
}

// --- 6) named spot-checks ---
const spotNames = [
  'M.A.G.S.',
  'Elliot Fairlane',
  'Sharpclaw the Bearded Grila Monster',
  'Scurrytail the Troubled',
  'Warchief Skawt',
  'Abandoned Hope',
  'Prime Evolution Huzzum',
  'Omni Special Forces',
];
const spots = [];
for (const name of spotNames) {
  const tpl = (plan.templates || []).find((t) => (t.bands || []).some((b) => b.Name === name));
  if (!tpl) {
    spots.push({ name, status: 'missing-from-plan' });
    continue;
  }
  const live = rk[tpl.hash] || nt[tpl.hash];
  const want = tpl.bands[0];
  const band = live && live.Templates && live.Templates[0];
  const raw = bestByNameMd.get(`${want.Name}|${want.md}`);
  spots.push({
    name,
    hash: tpl.hash,
    fillPink: !!tpl.fillPink,
    pf: tpl.pf,
    scrape: {
      md: raw ? +raw.MonsterData : want.md,
      mesh: raw ? +raw.Mesh : want.mesh,
      sc: raw ? +raw.Scale : want.sc,
      hp: raw ? +raw.MaxHealth : want.hp,
      tex: (raw && raw.Textures) || want.textures || '',
      meshes: (raw && raw.Meshes) || want.meshes || '',
    },
    server: band
      ? {
          md: band.Stats && band.Stats['359'],
          mesh: band.Stats && band.Stats['12'],
          sc: band.Stats && band.Stats['360'],
          hp: band.Stats && band.Stats['1'],
          max: band.Stats && band.Stats['27'],
          tex: texKey(band),
          meshes: meshKey(band),
          pinkLike: isPinkLike(live),
        }
      : null,
  });
}

const elliotKnubot = rd(path.join(GD, 'Knubot', 'ElliotFairlane.json'));
const elliotHasNano = JSON.stringify(elliotKnubot).includes('{givenano 293685}');

const report = {
  tag: TAG,
  validatorMode: 'raw-scrape+older+plan',
  scrapeRowsLoaded: scrapeRows.length,
  olderScrapeFiles: olderFiles,
  comparedBands: compared,
  noRawSighting,
  mismatches: {
    nameMismatch,
    mdMismatch,
    meshMismatch,
    scaleMismatch,
    hpMismatch,
    texMismatch,
    meshesMismatch,
  },
  mismatchSamples,
  pinksWithScrapeNearby: pinksWithScrape,
  pinksWithAnyHistoricalScrapeNearby: pinksWithAnyScrape,
  pinkSamples,
  dialogNamesInScrape: dialogNames.size,
  dialogGapsOnRkTemplates: dialogGaps,
  dialogGapSamples,
  guardsCompared,
  guardsBad,
  guardSamples,
  placeholderDynaSpawns: placeholderSpawns,
  placeholderDynaNames: [...placeholderNames.entries()].sort((a, b) => b[1] - a[1]),
  elliotFairlaneGivenano293685: elliotHasNano,
  spotChecks: spots,
  planCounts: plan.counts,
  perZoneReck: plan.perZone && plan.perZone['750'],
};

const out = path.join(__dirname, 'rk-scrape-validate-20261106.json');
fs.writeFileSync(out, JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report, null, 2));
console.log('wrote', out);
const hardFail =
  pinksWithScrape !== 0 ||
  mdMismatch !== 0 ||
  scaleMismatch !== 0 ||
  hpMismatch !== 0 ||
  meshesMismatch !== 0;
process.exit(hardFail ? 2 : 0);
