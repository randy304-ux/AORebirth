#!/usr/bin/env node
// After SpawnsAdd.rk is built: remove Spawns.json pink/Guessed points within 12 m of a
// SpawnsAdd.rk spawn. Contested pink fills fall through to a new hash; the old pink must go.
const fs = require('fs');
const path = require('path');

const GD = path.resolve(__dirname, '..', '..', 'AORebirth', 'GameData');
const NEAR = 12;

function rd(p) {
  return JSON.parse(fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, ''));
}
function wr(p, o) {
  fs.writeFileSync(p, JSON.stringify(o, null, 4) + '\n');
}

const nt = {};
for (const f of ['NpcTemplates.json', ...fs.readdirSync(GD).filter((x) => /^NpcTemplates\..+\.json$/.test(x))]) {
  Object.assign(nt, rd(path.join(GD, f)));
}

function isPinkLike(hash) {
  const t = nt[hash];
  if (!t || !t.Templates) return true;
  const prov = t.ContentProvenance || {};
  if (prov.Guessed || prov.StandIn) return true;
  const b = t.Templates[0];
  const bp = (b && b.ContentProvenance) || {};
  return !!(bp.Guessed || bp.StandIn);
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

const TODAY = process.env.RK_TODAY || 'I:/AO/imports/scrape-20261106-ado/raw';
const scrapeByPf = new Map();
const scrapeCsv = path.join(TODAY, 'scraped_npcs_20261106_Whozewhatzit.csv');
if (fs.existsSync(scrapeCsv)) {
  for (const r of parseCsv(fs.readFileSync(scrapeCsv, 'utf8').replace(/^\uFEFF/, ''))) {
    if (r.IsNpc !== '1' && r.IsNpc !== 'True') continue;
    const pf = +r.PlayfieldID;
    if (!scrapeByPf.has(pf)) scrapeByPf.set(pf, []);
    scrapeByPf.get(pf).push({ x: +r.X, z: +r.Z, name: r.Name });
  }
}

let removed = 0,
  pfs = 0;
const samples = [];
const pfRoot = path.join(GD, 'PlayfieldContent');
for (const ent of fs.readdirSync(pfRoot, { withFileTypes: true })) {
  if (!ent.isDirectory()) continue;
  const addPath = path.join(pfRoot, ent.name, 'SpawnsAdd.rk.json');
  const spPath = path.join(GD, 'Playfields', ent.name, 'Spawns.json');
  if (!fs.existsSync(spPath)) continue;
  const sp = rd(spPath);
  const adds = fs.existsSync(addPath)
    ? (rd(addPath).spawns || []).map((s) => ({ x: s.position[0], z: s.position[2], h: s.hashText }))
    : [];
  const scrapes = scrapeByPf.get(+ent.name) || [];
  if (!adds.length && !scrapes.length) continue;
  const before = (sp.spawns || []).length;
  sp.spawns = (sp.spawns || []).filter((s) => {
    if (!isPinkLike(s.hashText)) return true;
    const x = s.position[0],
      z = s.position[2];
    for (const a of adds) {
      if (Math.hypot(a.x - x, a.z - z) <= NEAR) {
        removed++;
        if (samples.length < 20)
          samples.push({ pf: +ent.name, pink: s.hashText, via: 'add:' + a.h, d: Math.hypot(a.x - x, a.z - z) });
        return false;
      }
    }
    // Also drop pinks sitting on a scraped real sighting (even if import reused another hash).
    for (const r of scrapes) {
      if (Math.hypot(r.x - x, r.z - z) <= NEAR) {
        removed++;
        if (samples.length < 20)
          samples.push({ pf: +ent.name, pink: s.hashText, via: 'scrape:' + r.name, d: Math.hypot(r.x - x, r.z - z) });
        return false;
      }
    }
    return true;
  });
  if (sp.spawns.length !== before) {
    wr(spPath, sp);
    pfs++;
  }
}
console.log(JSON.stringify({ removed, pfsTouched: pfs, samples }, null, 2));
