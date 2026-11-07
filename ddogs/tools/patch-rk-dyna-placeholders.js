#!/usr/bin/env node
// Suppress guide-placeholder D* Dyna spawns within 12m of SpawnsAdd.rk named bosses (all RK overlay pfs).
// Rename remaining 1:1 placeholders from scrape only (never invent names).
const fs = require('fs');
const path = require('path');

const gd = path.resolve(__dirname, '..', '..', 'AORebirth', 'GameData');
const scrapeCsv =
  process.env.RK_SCRAPE_CSV ||
  'I:\\AO\\imports\\scrape-20261106-ado\\raw\\scraped_npcs_20261106_Whozewhatzit.csv';
const OLD_DIRS = [
  'I:\\AO\\imports\\scrape-20261105\\extracted\\output',
  'I:\\AO\\imports\\scrape-20261105b\\extracted\\output',
];

function load(p) {
  return JSON.parse(fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, ''));
}
function save(p, obj) {
  fs.writeFileSync(p, JSON.stringify(obj, null, 4) + '\n', 'utf8');
}

function isNamedBoss(name) {
  if (!name) return false;
  if (/^(The Ring|Ring Ticket)/i.test(name) || /Ticket Salesman/i.test(name)) return false;
  return /\bthe\b/i.test(name);
}

function parseCsv(text) {
  const lines = text.replace(/^\uFEFF/, '').split(/\r?\n/).filter((l) => l.length);
  if (!lines.length) return [];
  function split(line) {
    const out = [];
    let cur = '';
    let q = false;
    for (let i = 0; i < line.length; i++) {
      const c = line[i];
      if (q) {
        if (c === '"') {
          if (line[i + 1] === '"') {
            cur += '"';
            i++;
          } else q = false;
        } else cur += c;
      } else if (c === '"') q = true;
      else if (c === ',') {
        out.push(cur);
        cur = '';
      } else cur += c;
    }
    out.push(cur);
    return out;
  }
  const header = split(lines[0]);
  return lines.slice(1).map((line) => {
    const parts = split(line);
    const o = {};
    for (let i = 0; i < header.length; i++) o[header[i]] = parts[i] || '';
    return o;
  });
}

const rk = load(path.join(gd, 'NpcTemplates.RK.json'));
const nt = load(path.join(gd, 'NpcTemplates.json'));

const pfRoot = path.join(gd, 'PlayfieldContent');
const pfList = fs
  .readdirSync(pfRoot, { withFileTypes: true })
  .filter((e) => e.isDirectory() && fs.existsSync(path.join(pfRoot, e.name, 'SpawnsAdd.rk.json')))
  .map((e) => +e.name)
  .filter((n) => Number.isFinite(n));

let removed = 0;
for (const pf of pfList) {
  const addPath = path.join(gd, 'PlayfieldContent', String(pf), 'SpawnsAdd.rk.json');
  const spPath = path.join(gd, 'Playfields', String(pf), 'Spawns.json');
  if (!fs.existsSync(spPath)) continue;
  const add = load(addPath);
  const sp = load(spPath);
  const bossAdds = [];
  for (const s of add.spawns || []) {
    const h = s.hashText;
    const tpl = rk[h];
    const nm = tpl && tpl.Templates && tpl.Templates[0] && tpl.Templates[0].Name;
    if (!isNamedBoss(nm)) continue;
    bossAdds.push({ x: s.position[0], z: s.position[2], h, nm });
  }
  if (!bossAdds.length) continue;
  const before = (sp.spawns || []).length;
  sp.spawns = (sp.spawns || []).filter((s) => {
    const h = s.hashText;
    const tpl = nt[h];
    const nm = tpl && tpl.Templates && tpl.Templates[0] && tpl.Templates[0].Name;
    const isPh = /^D[0-9A-F]{3}$/.test(h) && nm && /\bdyna\b/i.test(nm);
    if (!isPh) return true;
    const x = s.position[0],
      z = s.position[2];
    for (const b of bossAdds) {
      if (Math.hypot(b.x - x, b.z - z) <= 12) {
        removed++;
        return false;
      }
    }
    return true;
  });
  if (sp.spawns.length !== before) save(spPath, sp);
}

// Load scrape (today + older) for rename
const scrape = [];
function addScrape(file) {
  if (!fs.existsSync(file)) return;
  for (const r of parseCsv(fs.readFileSync(file, 'utf8'))) {
    if (r.IsNpc !== '1' && r.IsNpc !== 'True') continue;
    scrape.push(r);
  }
}
addScrape(scrapeCsv);
for (const dir of OLD_DIRS) {
  if (!fs.existsSync(dir)) continue;
  for (const f of fs.readdirSync(dir).filter((x) => /^scraped_npcs_.*\.csv$/.test(x))) {
    addScrape(path.join(dir, f));
  }
}

let renamed = 0;
let skippedMulti = 0;
const byPfScrape = new Map();
for (const r of scrape) {
  const pf = +r.PlayfieldID;
  if (!byPfScrape.has(pf)) byPfScrape.set(pf, []);
  byPfScrape.get(pf).push(r);
}

for (const pf of pfList) {
  const spPath = path.join(gd, 'Playfields', String(pf), 'Spawns.json');
  if (!fs.existsSync(spPath)) continue;
  const sp = load(spPath);
  const pool = byPfScrape.get(pf) || [];
  if (!pool.length) continue;
  const byHash = new Map();
  for (const s of sp.spawns || []) {
    const h = s.hashText;
    if (!/^D[0-9A-F]{3}$/.test(h)) continue;
    const tpl = nt[h];
    const nm = tpl && tpl.Templates && tpl.Templates[0] && tpl.Templates[0].Name;
    if (!nm || !/\bdyna\b/i.test(nm)) continue;
    if (!byHash.has(h)) byHash.set(h, []);
    byHash.get(h).push(s);
  }
  for (const [h, pts] of byHash) {
    const band = nt[h].Templates[0];
    const md = +(band.Stats && band.Stats['359']) || 0;
    const names = new Set();
    let best = null;
    for (const s of pts) {
      const x = s.position[0],
        z = s.position[2];
      let hit = null,
        bestD = 1e9;
      for (const r of pool) {
        if (md && +r.MonsterData !== md) continue;
        const d = Math.hypot(+r.X - x, +r.Z - z);
        if (d < bestD && d <= 40) {
          bestD = d;
          hit = r;
        }
      }
      if (!hit) {
        names.add('__unmatched__');
        continue;
      }
      names.add(hit.Name);
      best = hit;
    }
    if (names.size !== 1 || names.has('__unmatched__') || !best) {
      skippedMulti++;
      continue;
    }
    if (best.Name === band.Name) continue;
    const campKey = band.ContentProvenance && band.ContentProvenance.CampKey;
    band.Name = best.Name;
    if (+best.Level > 0) band.Level = +best.Level;
    const hp = +best.MaxHealth;
    if (hp > 0) {
      band.Stats['1'] = hp;
      band.Stats['27'] = hp;
    }
    band.ContentProvenance = {
      Guessed: false,
      Source: 'scrape-rk-20261106',
      Note: 'placeholder rename from live scrape; LiveScraped skips Big',
      CampKey: campKey || undefined,
    };
    nt[h].ContentProvenance = {
      Guessed: false,
      Source: 'scrape-rk-20261106',
      Note: 'placeholder renamed from live scrape',
    };
    renamed++;
  }
}
save(path.join(gd, 'NpcTemplates.json'), nt);

console.log(
  JSON.stringify({ pfs: pfList.length, removedDual: removed, renamed1to1: renamed, skippedMultiOrUnmatched: skippedMulti })
);
