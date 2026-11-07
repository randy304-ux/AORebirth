// Match gpt-moves-20261107-r2 patrols to installed spawns; write patrolWaypoints; validate.
// STAGE ONLY — does not deploy. Never invents spawns.
'use strict';
const fs = require('fs');
const path = require('path');

const ROOT = 'I:/AO/src';
const GD = path.join(ROOT, 'AORebirth/GameData');
const IMPORT = 'I:/AO/imports/gpt-moves-20261107-r2';
const OUT = path.join(ROOT, 'tools-temp/patrol-import');
const MATCH_M = 15;
const MATCH_M2 = MATCH_M * MATCH_M;
const Y_SYNTH_MAX = 10;
const SPOT_N = 200;

function loadJson(p) {
  return JSON.parse(fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, ''));
}
function writeJson(p, obj) {
  fs.mkdirSync(path.dirname(p), { recursive: true });
  fs.writeFileSync(p, JSON.stringify(obj, null, 4) + '\n', 'utf8');
}

const zoneName = new Map();
for (const line of fs.readFileSync(path.join(IMPORT, 'zone-summary.csv'), 'utf8').split(/\r?\n/).slice(1)) {
  if (!line.trim()) continue;
  // Zone,PF,... — zone names may contain commas? from file they don't in first col with quotes rarely
  const m = line.match(/^([^,]+),(-?\d+),/);
  if (m) zoneName.set(Number(m[2]), m[1].trim());
}
function zname(pf) { return zoneName.get(pf) || ('PF ' + pf); }

// ---- templates: hash -> { names:Set, skipSynthetic:bool } ----
const nt = {};
for (const f of ['NpcTemplates.json', ...fs.readdirSync(GD).filter(f => /^NpcTemplates\..+\.json$/.test(f))]) {
  Object.assign(nt, loadJson(path.join(GD, f)));
}

function leaves(h, seen = new Set()) {
  if (!h || seen.has(h)) return [];
  seen.add(h);
  const t = nt[h];
  if (!t) return [];
  if (t.Children && t.Children.length) return t.Children.flatMap(c => leaves(c, seen));
  return t.Templates ? [[h, t]] : [];
}

const GUARD_RE = /\b(guard|trooper|sentinel|bodyguard|security|sentry|patrolman|watchman)\b/i;
const VENDOR_RE = /\b(vendor|shopkeeper|merchant|trader|arms dealer|clerk|broker)\b/i;
const CIVIC_RE = /\b(guide|instructor|clerk|attendant|receptionist|tourist|citizen|civilian)\b/i;
const DYNA_RE = /\bdyna\b|\bboss\b|the (voracious|fierce|deadly|mighty)/i;

function bandSkip(band) {
  if (!band) return { vendor: false, quest: false, guard: false, dyna: false, civic: false };
  const name = band.Name || '';
  const feats = band.Features || [];
  const shop = (band.ShopStock && band.ShopStock.length > 0);
  const knu = (band.KnuBotId | 0) > 0;
  const boss = (band.BossTriggers && band.BossTriggers.length > 0);
  const noCombat = feats.includes('NoCombat');
  return {
    vendor: shop || VENDOR_RE.test(name),
    quest: knu,
    guard: GUARD_RE.test(name),
    dyna: boss || DYNA_RE.test(name) || /\sDyna$/i.test(name),
    civic: noCombat || CIVIC_RE.test(name)
  };
}

function metaOf(hash) {
  const names = new Set();
  let vendor = false, quest = false, guard = false, dyna = false, civic = false;
  for (const [, t] of leaves(hash)) {
    for (const b of (t.Templates || [])) {
      if (b && b.Name) names.add(b.Name);
      const s = bandSkip(b);
      vendor = vendor || s.vendor;
      quest = quest || s.quest;
      guard = guard || s.guard;
      dyna = dyna || s.dyna;
      civic = civic || s.civic;
    }
  }
  return { names, vendor, quest, guard, dyna, civic, skipSynthetic: vendor || quest || guard || dyna || civic };
}

const hashMeta = new Map();
function getMeta(h) {
  if (!hashMeta.has(h)) hashMeta.set(h, metaOf(h));
  return hashMeta.get(h);
}

// ---- load spawns per playfield ----
function spawnFiles(pf) {
  const files = [];
  const main = path.join(GD, 'Playfields', String(pf), 'Spawns.json');
  if (fs.existsSync(main)) files.push(main);
  const d = path.join(GD, 'PlayfieldContent', String(pf));
  if (fs.existsSync(d)) {
    for (const f of fs.readdirSync(d).filter(f => /^SpawnsAdd.*\.json$/i.test(f)).sort())
      files.push(path.join(d, f));
  }
  return files;
}

/** @type {Map<number, {file:string, data:any, entries:{obj:any, x:number,y:number,z:number,hash:string,meta:any,used:boolean}[]}[]>} */
const pfIndex = new Map();

function ensurePf(pf) {
  if (pfIndex.has(pf)) return pfIndex.get(pf);
  const packs = [];
  for (const file of spawnFiles(pf)) {
    const data = loadJson(file);
    const spawns = data.spawns || data.Spawns || [];
    const entries = [];
    for (const s of spawns) {
      const pos = s.position || s.Position;
      if (!pos || pos.length < 3) continue;
      const hash = s.hashText || s.HashText || '';
      if (!hash) continue;
      entries.push({
        obj: s,
        x: +pos[0], y: +pos[1], z: +pos[2],
        hash,
        meta: getMeta(hash),
        used: false,
        file
      });
    }
    packs.push({ file, data, entries });
  }
  pfIndex.set(pf, packs);
  return packs;
}

function nameMatch(meta, want) {
  if (!want) return false;
  for (const n of meta.names) {
    if (n.localeCompare(want, undefined, { sensitivity: 'accent' }) === 0) return true;
  }
  // case-insensitive fallback
  const w = want.toLowerCase();
  for (const n of meta.names) if (n.toLowerCase() === w) return true;
  return false;
}

function xzDist2(ax, az, bx, bz) {
  const dx = ax - bx, dz = az - bz;
  return dx * dx + dz * dz;
}

const patrols = loadJson(path.join(IMPORT, 'patrols.json'));
const matched = [];
const unmatched = [];
const skipped = [];
const dirtyFiles = new Set();

for (let i = 0; i < patrols.length; i++) {
  const p = patrols[i];
  const pf = +p.playfield;
  const packs = ensurePf(pf);
  const isSynth = p.source === 'synthetic' || p.shape === 'camp-loop';
  let best = null, bestD = Infinity, bestPack = null, bestEntry = null;

  for (const pack of packs) {
    for (const e of pack.entries) {
      if (e.used) continue;
      if (!nameMatch(e.meta, p.name)) continue;
      if (isSynth && e.meta.skipSynthetic) continue;
      const d2 = xzDist2(e.x, e.z, +p.spawnX, +p.spawnZ);
      if (d2 <= MATCH_M2 && d2 < bestD) {
        bestD = d2;
        best = e;
        bestPack = pack;
        bestEntry = e;
      }
    }
  }

  if (!best) {
    // classify skip vs unmatched
    let skipReason = null;
    if (isSynth) {
      for (const pack of packs) {
        for (const e of pack.entries) {
          if (e.used) continue;
          if (!nameMatch(e.meta, p.name)) continue;
          const d2 = xzDist2(e.x, e.z, +p.spawnX, +p.spawnZ);
          if (d2 <= MATCH_M2 && e.meta.skipSynthetic) {
            skipReason = e.meta.vendor ? 'vendor'
              : e.meta.quest ? 'quest'
              : e.meta.guard ? 'guard'
              : e.meta.dyna ? 'dyna/boss'
              : 'civic';
            break;
          }
        }
        if (skipReason) break;
      }
    }
    if (skipReason) {
      skipped.push({ i, pf, zone: zname(pf), name: p.name, shape: p.shape, reason: skipReason });
    } else {
      unmatched.push({ i, pf, zone: zname(pf), name: p.name, shape: p.shape, source: p.source,
        spawnX: p.spawnX, spawnY: p.spawnY, spawnZ: p.spawnZ });
    }
    continue;
  }

  best.used = true;
  const wps = (p.waypoints || []).map(w => [+w[0], +w[1], +w[2]]);
  // write camelCase to match Spawns.json style
  best.obj.patrolWaypoints = wps;
  dirtyFiles.add(best.file);
  matched.push({
    i, pf, zone: zname(pf), name: p.name, shape: p.shape, source: p.source,
    hash: best.hash, dist: Math.sqrt(bestD),
    spawnX: best.x, spawnY: best.y, spawnZ: best.z,
    waypoints: wps.length, file: path.relative(GD, best.file).replace(/\\/g, '/')
  });
}

for (const file of dirtyFiles) {
  const packs = [...pfIndex.values()].flat();
  const pack = packs.find(p => p.file === file);
  if (!pack) continue;
  writeJson(file, pack.data);
}

// ---- validation ----
function parseCsv(t) {
  const rows = []; let row = [], f = '', q = false;
  for (let i = 0; i < t.length; i++) {
    const c = t[i];
    if (q) {
      if (c === '"') { if (t[i + 1] === '"') { f += '"'; i++; } else q = false; }
      else f += c;
    } else if (c === '"') q = true;
    else if (c === ',') { row.push(f); f = ''; }
    else if (c === '\n') { row.push(f.replace(/\r$/, '')); rows.push(row); row = []; f = ''; }
    else f += c;
  }
  if (f.length || row.length) { row.push(f); rows.push(row); }
  const h = rows.shift();
  return rows.filter(r => r.length >= h.length).map(r => Object.fromEntries(h.map((k, i) => [k, r[i] || ''])));
}

const moveKey = (x, y, z) => `${(+x).toFixed(3)}|${(+y).toFixed(3)}|${(+z).toFixed(3)}`;
const movePts = new Set();
const srcDir = path.join(IMPORT, 'srcdata');
for (const f of fs.readdirSync(srcDir).filter(f => /^npc_moves_.*\.csv$/i.test(f))) {
  const rows = parseCsv(fs.readFileSync(path.join(srcDir, f), 'utf8'));
  for (const r of rows) {
    if (r.X === undefined) continue;
    movePts.add(moveKey(r.X, r.Y, r.Z));
  }
}

const scrapedWps = [];
for (const p of patrols) {
  if (p.source !== 'scraped') continue;
  for (const w of (p.waypoints || [])) scrapedWps.push({ pf: p.playfield, name: p.name, x: +w[0], y: +w[1], z: +w[2] });
}

// deterministic sample of 200
function mulberry32(a) {
  return function () {
    let t = a += 0x6D2B79F5;
    t = Math.imul(t ^ t >>> 15, t | 1);
    t ^= t + Math.imul(t ^ t >>> 7, t | 61);
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}
const rnd = mulberry32(20261107);
const sample = [];
const usedIdx = new Set();
const nSpot = Math.min(SPOT_N, scrapedWps.length);
while (sample.length < nSpot) {
  const j = Math.floor(rnd() * scrapedWps.length);
  if (usedIdx.has(j)) continue;
  usedIdx.add(j);
  sample.push(scrapedWps[j]);
}

let spotOk = 0, spotFail = [];
for (const s of sample) {
  if (movePts.has(moveKey(s.x, s.y, s.z))) spotOk++;
  else spotFail.push(s);
}

let synthYOk = 0, synthYFail = 0, synthYFailSamples = [];
for (const p of patrols) {
  if (p.source !== 'synthetic') continue;
  const ay = +p.spawnY;
  for (const w of (p.waypoints || [])) {
    const dy = Math.abs(+w[1] - ay);
    if (dy <= Y_SYNTH_MAX) synthYOk++;
    else {
      synthYFail++;
      if (synthYFailSamples.length < 20)
        synthYFailSamples.push({ name: p.name, pf: p.playfield, ay, wy: +w[1], dy });
    }
  }
}

// per-zone tallies
const byZone = new Map();
function bump(zone, key) {
  if (!byZone.has(zone)) byZone.set(zone, { zone, matched: 0, unmatched: 0, skipped: 0 });
  byZone.get(zone)[key]++;
}
for (const m of matched) bump(m.zone, 'matched');
for (const u of unmatched) bump(u.zone, 'unmatched');
for (const s of skipped) bump(s.zone, 'skipped');

const zoneRows = [...byZone.values()].sort((a, b) => a.zone.localeCompare(b.zone));
const validationPass = spotFail.length === 0 && synthYFail === 0;

fs.mkdirSync(OUT, { recursive: true });
writeJson(path.join(OUT, 'matched.json'), matched);
writeJson(path.join(OUT, 'unmatched.json'), unmatched);
writeJson(path.join(OUT, 'skipped-synthetic.json'), skipped);
writeJson(path.join(OUT, 'by-zone.json'), zoneRows);
writeJson(path.join(OUT, 'validation.json'), {
  pass: validationPass,
  scrapedWaypointUniverse: scrapedWps.length,
  movePtsLoaded: movePts.size,
  spotSample: sample.length,
  spotOk,
  spotFail: spotFail.length,
  spotFailSamples: spotFail.slice(0, 10),
  synthYOk,
  synthYFail,
  synthYFailSamples,
  matched: matched.length,
  unmatched: unmatched.length,
  skippedSynthetic: skipped.length,
  dirtyFiles: [...dirtyFiles].map(f => path.relative(GD, f).replace(/\\/g, '/'))
});

const md = [];
md.push('# Patrol import match report');
md.push('');
md.push(`Total patrols: ${patrols.length}`);
md.push(`Matched: ${matched.length}`);
md.push(`Unmatched: ${unmatched.length}`);
md.push(`Skipped synthetic (vendor/quest/guard/dyna/civic): ${skipped.length}`);
md.push(`Validation: ${validationPass ? 'PASS' : 'FAIL'} (spot ${spotOk}/${sample.length}, synthY fail ${synthYFail})`);
md.push('');
md.push('| Zone | Matched | Unmatched | Skipped |');
md.push('|---|---:|---:|---:|');
for (const r of zoneRows)
  md.push(`| ${r.zone} | ${r.matched} | ${r.unmatched} | ${r.skipped} |`);
fs.writeFileSync(path.join(OUT, 'REPORT.md'), md.join('\n') + '\n', 'utf8');

console.log(JSON.stringify({
  matched: matched.length,
  unmatched: unmatched.length,
  skipped: skipped.length,
  dirtyFiles: dirtyFiles.size,
  validationPass,
  spotOk,
  spotFail: spotFail.length,
  synthYFail
}, null, 2));
process.exit(validationPass ? 0 : 2);
