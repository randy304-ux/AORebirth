#!/usr/bin/env node
// Pre-deploy validation for scrape-rk-20261106 import.
// Compares every RK overlay template vs the scrape for MD/mesh/scale/name/textures.
// Counts pinks that still sit on a scraped real mob (target 0).
// Counts NPCs with scrape dialogue but no Knubot on the server.
const fs = require('fs');
const path = require('path');

const W = path.resolve(__dirname, '..', '..');
const GD = path.join(W, 'AORebirth', 'GameData');
const TODAY = process.env.RK_TODAY || 'I:/AO/imports/scrape-20261106-ado/raw';
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
function parseTex(s) {
  if (!s) return '';
  return String(s)
    .split(';')
    .filter(Boolean)
    .map((t) => t.trim())
    .sort()
    .join(';');
}

const nt = {};
for (const f of ['NpcTemplates.json', ...fs.readdirSync(GD).filter((x) => /^NpcTemplates\..+\.json$/.test(x))]) {
  Object.assign(nt, rd(path.join(GD, f)));
}
const rk = rd(path.join(GD, 'NpcTemplates.RK.json'));
const plan = rd(path.join(__dirname, 'scrape_import_rk_20261106.json'));

// --- 1) template vs scrape (plan bands are scrape-sourced) ---
let compared = 0,
  mdMismatch = 0,
  meshMismatch = 0,
  scaleMismatch = 0,
  nameMismatch = 0,
  texMismatch = 0;
const mismatchSamples = [];
for (const t of plan.templates || []) {
  const live = rk[t.hash] || nt[t.hash];
  if (!live || !live.Templates) continue;
  for (const b of t.bands || []) {
    const band = (live.Templates || []).find((x) => +x.Level === +b.Level) || live.Templates[0];
    if (!band) continue;
    compared++;
    const md = +(band.Stats && band.Stats['359']) || 0;
    const mesh = +(band.Stats && band.Stats['12']) || 0;
    const sc = +(band.Stats && band.Stats['360']) || 0;
    const issues = [];
    if (band.Name !== b.Name) {
      nameMismatch++;
      issues.push(`name ${band.Name}!=${b.Name}`);
    }
    if (md !== +b.md) {
      mdMismatch++;
      issues.push(`md ${md}!=${b.md}`);
    }
    if (+b.mesh > 0 && mesh !== +b.mesh) {
      meshMismatch++;
      issues.push(`mesh ${mesh}!=${b.mesh}`);
    }
    if (+b.sc > 0 && sc !== +b.sc) {
      scaleMismatch++;
      issues.push(`scale ${sc}!=${b.sc}`);
    }
    const wantTex = parseTex(b.textures);
    const gotTex = texKey(band);
    if (wantTex && wantTex !== gotTex) {
      texMismatch++;
      issues.push(`tex`);
    }
    if (issues.length && mismatchSamples.length < 12) {
      mismatchSamples.push({ hash: t.hash, level: b.Level, issues });
    }
  }
}

// --- 2) pinks still covering a scraped sighting (target 0) ---
const scrape = parseCsv(
  fs.readFileSync(path.join(TODAY, 'scraped_npcs_20261106_Whozewhatzit.csv'), 'utf8').replace(/^\uFEFF/, '')
).filter((r) => r.IsNpc === '1' || r.IsNpc === 'True');

const byPfScrape = new Map();
for (const r of scrape) {
  const pf = +r.PlayfieldID;
  if (!byPfScrape.has(pf)) byPfScrape.set(pf, []);
  byPfScrape.get(pf).push({ name: r.Name, x: +r.X, z: +r.Z, md: +r.MonsterData });
}

let pinksWithScrape = 0;
const pinkSamples = [];
const pfRoot = path.join(GD, 'PlayfieldContent');
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
    // Ignore if RK overlay already replaced this hash with a real scrape template
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
  if (!hash) continue; // not in this RK import
  if (hasKnubot(name, hash)) continue;
  dialogGaps++;
  if (dialogGapSamples.length < 20) dialogGapSamples.push({ name, hash });
}

// --- 4) guard appearance spot check ---
const guardRx = /^(Omni-Tek |Clan |Neutral )?(Guard|Trooper|Enforcer|Patrol|Sentinel)\b|Guard$/i;
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
  const md = +(band.Stats && band.Stats['359']) || 0;
  const sc = +(band.Stats && band.Stats['360']) || 0;
  const mesh = +(band.Stats && band.Stats['12']) || 0;
  const bad =
    md !== +b0.md ||
    (+b0.sc > 0 && sc !== +b0.sc) ||
    (+b0.mesh > 0 && mesh !== +b0.mesh) ||
    (parseTex(b0.textures) && parseTex(b0.textures) !== texKey(band));
  if (bad) {
    guardsBad++;
    if (guardSamples.length < 10) guardSamples.push({ hash: t.hash, name: b0.Name, md: [md, b0.md], sc: [sc, b0.sc] });
  }
}

// --- 5) named spot-checks for Randy ---
const spotNames = [
  'M.A.G.S.',
  'Elliot Fairlane',
  'Sharpclaw the Bearded Grila Monster',
  'Scurrytail the Troubled',
  'Warchief Skawt',
  'Abandoned Hope',
  'Prime Evolution Huzzum',
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
  spots.push({
    name,
    hash: tpl.hash,
    fillPink: !!tpl.fillPink,
    pf: tpl.pf,
    scrape: { md: want.md, mesh: want.mesh, sc: want.sc, tex: want.textures || '' },
    server: band
      ? {
          md: band.Stats && band.Stats['359'],
          mesh: band.Stats && band.Stats['12'],
          sc: band.Stats && band.Stats['360'],
          tex: texKey(band),
          pinkLike: isPinkLike(live),
        }
      : null,
  });
}

const report = {
  tag: TAG,
  comparedBands: compared,
  mismatches: { nameMismatch, mdMismatch, meshMismatch, scaleMismatch, texMismatch },
  mismatchSamples,
  pinksWithScrapeNearby: pinksWithScrape,
  pinkSamples,
  dialogNamesInScrape: dialogNames.size,
  dialogGapsOnRkTemplates: dialogGaps,
  dialogGapSamples,
  guardsCompared,
  guardsBad,
  guardSamples,
  spotChecks: spots,
  planCounts: plan.counts,
  perZoneReck: plan.perZone && plan.perZone['750'],
};

const out = path.join(__dirname, 'rk-scrape-validate-20261106.json');
fs.writeFileSync(out, JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report, null, 2));
console.log('wrote', out);
process.exit(pinksWithScrape === 0 && mdMismatch === 0 && scaleMismatch === 0 ? 0 : 2);
