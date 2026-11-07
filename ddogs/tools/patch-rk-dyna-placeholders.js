#!/usr/bin/env node
// Suppress guide-placeholder D* Dyna spawns within 12m of SpawnsAdd.rk named bosses.
// Rename remaining 1:1 Newland placeholders from scrape only (never invent names).
const fs = require('fs');
const path = require('path');

const gd = path.resolve(__dirname, '..', '..', 'AORebirth', 'GameData');
const scrapeCsv =
  process.env.RK_SCRAPE_CSV ||
  'I:\\AO\\imports\\scrape-20261106-ado\\raw\\scraped_npcs_20261106_Whozewhatzit.csv';

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
  // Proper-ish CSV for quoted fields
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

let removed = 0;
// Scoped to Newland Desert (565) for this staging pass — expand after Mike accepts.
const pfList = [565];
for (const pf of pfList) {
  const addPath = path.join(gd, 'PlayfieldContent', String(pf), 'SpawnsAdd.rk.json');
  const spPath = path.join(gd, 'Playfields', String(pf), 'Spawns.json');
  if (!fs.existsSync(addPath) || !fs.existsSync(spPath)) continue;
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

let renamed = 0;
let skippedMulti = 0;
if (fs.existsSync(scrapeCsv)) {
  const scrape = parseCsv(fs.readFileSync(scrapeCsv, 'utf8')).filter(
    (r) => (r.IsNpc === '1' || r.IsNpc === 'True') && +r.PlayfieldID === 565
  );
  const sp565Path = path.join(gd, 'Playfields', '565', 'Spawns.json');
  if (fs.existsSync(sp565Path)) {
    const sp = load(sp565Path);
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
        for (const r of scrape) {
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
    save(path.join(gd, 'NpcTemplates.json'), nt);
  }
}

console.log(
  JSON.stringify({ removedDual: removed, renamed1to1: renamed, skippedMultiOrUnmatched: skippedMulti })
);
