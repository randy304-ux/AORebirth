// ddogs/tools/plan-galway685.js - Galway County (pf 685) outdoor scrape pilot plan
// Source: I:\AO\imports\scrape-20261106-review\685\missing.csv (reviewed; wrong.csv empty).
// Rules: invent nothing; cluster same-name within 3 m (3D when Y present); collapse multi-pos instances;
// drop pets/players/terminals/temp-city; skip name+md collisions; reuse existing hash when name+md+levels covered;
// positions/levels/mesh trusted; omit pre-v6.0 outfit/glove/texture/mesh-slot data.
const fs = require('fs'), path = require('path');
const W = path.resolve(__dirname, '..', '..');
const GD = path.join(W, 'AORebirth', 'GameData');
const REV = process.env.GALWAY_REVIEW || 'I:/AO/imports/scrape-20261106-review';
const PF = 685, TAG = 'scrape-galway685-20261106', NEAR = 12, CLUSTER = 3;
const TEMP_CITY = /^(Control Tower|SAM Battery|Guardian Conductor|Massive Cannon Turret)\b/i;
const SIDE = s => ({ Neutral: 0, Clan: 1, Omni: 2, OmniTek: 2, 'Omni-Tek': 2, Monster: 3 }[s] ?? 3);
const BREED = b => ({ Solitus: 1, Opifex: 2, Nanomage: 3, Atrox: 4, Monster: 0, HumanMonster: 0 }[b] ?? 0);

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
  return rows.filter(r => r.length).map(r => Object.fromEntries(h.map((k, i) => [k, r[i] || ''])));
}
const rd = p => JSON.parse(fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, ''));
const d2 = (a, b) => Math.hypot(a.x - b.x, a.z - b.z);
const d3 = (a, b) => {
  if (Number.isFinite(a.y) && Number.isFinite(b.y)) return Math.hypot(a.x - b.x, a.y - b.y, a.z - b.z);
  return d2(a, b);
};

const missing = parseCsv(fs.readFileSync(path.join(REV, '685', 'missing.csv'), 'utf8'));
const wrong = parseCsv(fs.readFileSync(path.join(REV, '685', 'wrong.csv'), 'utf8'));
const dropped = {
  sightingsIn: missing.length,
  wrongRows: wrong.length,
  tempCity: 0,
  noMonsterData: 0,
  noY: 0,
  notNpc: 0,
  pet: 0,
  instanceCollapsed: 0,
  clusterDedupe: 0,
  nearExistingSpawn: 0,
  nameMdCollision: 0
};
const skipped = [];
const collisions = [];

// --- filter raw missing sightings ---
const kept = [];
for (const r of missing) {
  if (r.IsPet === '1' || r.IsPet === 'True') { dropped.pet++; skipped.push({ name: r.Name, why: 'pet', x: +r.X, z: +r.Z }); continue; }
  if (r.IsNpc !== '1' && r.IsNpc !== 'True') { dropped.notNpc++; skipped.push({ name: r.Name, why: 'not npc', x: +r.X, z: +r.Z }); continue; }
  if (TEMP_CITY.test(r.Name)) { dropped.tempCity++; skipped.push({ name: r.Name, why: 'temp city', x: +r.X, z: +r.Z }); continue; }
  if (!(+r.MonsterData > 0)) { dropped.noMonsterData++; skipped.push({ name: r.Name, why: 'no MonsterData', x: +r.X, z: +r.Z }); continue; }
  if (!(+r.Y > 0) && +r.Y !== 0) { /* allow Y=0 ground */ }
  if (!Number.isFinite(+r.Y)) { dropped.noY++; skipped.push({ name: r.Name, why: 'no Y height', x: +r.X, z: +r.Z }); continue; }
  kept.push(r);
}

// --- collapse by Instance (roamers / multi-pos) -> one representative ---
const byInst = new Map();
for (const r of kept) {
  const id = String(r.Instance);
  const cur = byInst.get(id);
  if (!cur) { byInst.set(id, [r]); continue; }
  cur.push(r);
}
const instances = [];
for (const [, rs] of byInst) {
  rs.sort((a, b) => String(a.Timestamp).localeCompare(String(b.Timestamp)));
  const withY = rs.filter(r => Number.isFinite(+r.Y));
  const pool = withY.length ? withY : rs;
  const L = pool[pool.length - 1];
  if (rs.length > 1) dropped.instanceCollapsed += rs.length - 1;
  const lvls = [...new Set(rs.map(r => +r.Level))];
  const hpBy = {};
  for (const r of rs) hpBy[+r.Level] = +r.MaxHealth || 0;
  const roam = /multiple positions for instance/i.test(rs.map(r => r.review_flags || '').join(';')) ||
    rs.some(r => d2({ x: +r.X, z: +r.Z }, { x: +L.X, z: +L.Z }) > 5);
  instances.push({
    pf: PF, inst: L.Instance, name: L.Name, row: L, roam,
    x: +L.X, y: +L.Y, z: +L.Z,
    heading: Math.round(+L.HeadingDeg || 0),
    lvls, hpBy,
    md: +L.MonsterData, sc: +L.Scale || 100, mesh: +L.Mesh || 0,
    head: +L.HeadMesh || 0, tex: +L.MonsterTexture || 0, fam: +L.NPCFamily || 0,
    side: SIDE(L.Side), breed: BREED(L.Breed),
    sideName: L.Side, src: L.source
  });
}

// --- cluster same-name within 3 m ---
const pts = [];
for (const i of instances) {
  const hit = pts.find(p => p.name === i.name && d3(p, i) <= CLUSTER);
  if (!hit) { pts.push({ ...i, members: 1 }); continue; }
  dropped.clusterDedupe++;
  skipped.push({ name: i.name, why: 'dedupe within 3 m', x: i.x, z: i.z, kept: `${hit.x.toFixed(1)},${hit.z.toFixed(1)}` });
  // prefer later timestamp / keep roam flag
  if (String(i.row.Timestamp) > String(hit.row.Timestamp)) {
    const members = hit.members + 1;
    const lvls = [...new Set([...hit.lvls, ...i.lvls])];
    const hpBy = { ...hit.hpBy, ...i.hpBy };
    Object.assign(hit, i, { members, lvls, hpBy, roam: hit.roam || i.roam });
  } else {
    hit.members++;
    hit.roam = hit.roam || i.roam;
    for (const lv of i.lvls) if (!hit.lvls.includes(lv)) hit.lvls.push(lv);
    Object.assign(hit.hpBy, i.hpBy);
  }
}

// --- load templates + server spawns ---
const nt = {};
for (const f of ['NpcTemplates.json', ...fs.readdirSync(GD).filter(f => /^NpcTemplates\..+\.json$/.test(f))]) {
  Object.assign(nt, rd(path.join(GD, f)));
}
function leaves(h, seen = new Set()) {
  if (!h || seen.has(h)) return [];
  seen.add(h);
  const t = nt[h];
  if (!t) return [];
  if (t.Children && t.Children.length) return t.Children.flatMap(c => leaves(c, seen));
  return t.Templates ? [[h, t]] : [];
}
const namesOf = h => leaves(h).flatMap(([, t]) => (t.Templates || []).filter(x => x).map(x => x.Name));
const skips = fs.existsSync(path.join(GD, 'SpawnSkips.json')) ? rd(path.join(GD, 'SpawnSkips.json')) : { hiddenHashes: [] };
const hidden = new Set((skips.hiddenHashes || []).map(h => typeof h === 'string' ? h : h.hash));

function loadServer(pf) {
  const srv = [];
  function addSp(file, src) {
    if (!fs.existsSync(file)) return;
    for (const s of rd(file).spawns || []) {
      const h = s.hashText || '';
      if (hidden.has(h)) continue;
      const p = { x: s.position[0], y: s.position[1], z: s.position[2] };
      srv.push({ src, hash: h, lo: s.minLevel, hi: s.maxLevel, position: s.position, ...p, names: namesOf(h), templated: !!(nt[h] && nt[h].Templates) });
    }
  }
  addSp(path.join(GD, 'Playfields', '' + pf, 'Spawns.json'), 'Spawns.json');
  const addDir = path.join(GD, 'PlayfieldContent', '' + pf);
  if (fs.existsSync(path.join(addDir, 'SpawnsAdd.json'))) addSp(path.join(addDir, 'SpawnsAdd.json'), 'SpawnsAdd.json');
  if (fs.existsSync(addDir)) {
    for (const f of fs.readdirSync(addDir).filter(f => /^SpawnsAdd\..+\.json$/.test(f))) addSp(path.join(addDir, f), f);
  }
  const np = path.join(addDir, 'Npcs.json');
  if (fs.existsSync(np)) {
    for (const n of rd(np).Npcs || []) {
      srv.push({ src: 'Npcs.json', hash: n.Key, x: n.Position[0], y: n.Position[1], z: n.Position[2], names: [n.Name], templated: true });
    }
  }
  return srv;
}
const srv = loadServer(PF);

// existing name -> [{h, md, levels}]
const byName = {};
for (const [h, t] of Object.entries(nt)) {
  if (!t.Templates) continue;
  for (const b of t.Templates) {
    if (!b || !b.Name || !b.Stats) continue;
    const md = +b.Stats['359'] || 0;
    (byName[b.Name] = byName[b.Name] || []).push({ h, md, level: +b.Level, n: t.Templates.length });
  }
}
function reuseHash(name, md, lvls) {
  const entries = byName[name] || [];
  if (!entries.length) return null;
  const mds = [...new Set(entries.map(e => e.md))];
  if (mds.length === 1 && mds[0] === md) {
    const hs = [...new Set(entries.map(e => e.h))];
    // prefer a hash whose bands cover all scrape levels
    for (const h of hs) {
      const cover = new Set(entries.filter(e => e.h === h).map(e => e.level));
      if (lvls.every(l => cover.has(l))) return h;
    }
    return null; // same md but levels not covered -> make new Galway template
  }
  if (mds.every(m => m !== md)) {
    return { collision: true, existing: entries.map(e => `${e.h}:${e.md}`).filter((v, i, a) => a.indexOf(v) === i) };
  }
  // mixed md on same name — treat as collision
  return { collision: true, existing: entries.map(e => `${e.h}:${e.md}`).filter((v, i, a) => a.indexOf(v) === i) };
}

const usedHashes = new Set([...Object.keys(nt), ...srv.map(s => s.hash)]);
const b36 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ';
let seq = 0;
function newHash() {
  for (;;) {
    seq++;
    const h = 'P' + b36[Math.floor(seq / 1296) % 36] + b36[Math.floor(seq / 36) % 36] + b36[seq % 36];
    // start after existing P0xx: skip until free; P000-P029 taken, also avoid anything in usedHashes
    if (!usedHashes.has(h)) { usedHashes.add(h); return h; }
  }
}

const templates = {}; // key -> {hash,wander,friendly,src,bands:{}}
const spawns = [];
const flaggedNames = new Set();

for (const a of pts.sort((x, y) => x.name.localeCompare(y.name) || x.x - y.x || x.z - y.z)) {
  // drop if near any TEMPLATED server spawn (pink unresolved left alone for SpawnsAdd)
  const near = srv.find(s => s.templated && d2(s, a) <= NEAR);
  if (near) {
    dropped.nearExistingSpawn++;
    skipped.push({ name: a.name, why: `near existing templated spawn ${near.hash} ≤${NEAR}m`, x: a.x, z: a.z });
    continue;
  }

  const ru = reuseHash(a.name, a.md, a.lvls);
  let hash = null, reuse = false;
  if (ru && ru.collision) {
    if (!flaggedNames.has(a.name)) {
      flaggedNames.add(a.name);
      collisions.push({ kind: 'name-exists-different-md', name: a.name, scrapeMd: a.md, scrapeMesh: a.mesh, existing: ru.existing.join(', ') });
    }
    dropped.nameMdCollision++;
    skipped.push({ name: a.name, why: 'name exists with different MonsterData — not guessed', x: a.x, z: a.z, scrapeMd: a.md, existing: ru.existing.join(', ') });
    continue;
  }
  if (typeof ru === 'string') { hash = ru; reuse = true; }
  else {
    const key = [a.name, a.md, a.mesh, a.fam].join('|');
    if (!templates[key]) {
      templates[key] = {
        hash: newHash(), wander: false, friendly: a.side !== 3,
        src: `review missing ${a.src} inst ${a.inst}`,
        bands: {}
      };
    }
    const t = templates[key];
    t.wander = t.wander || !!a.roam;
    hash = t.hash;
    for (const lv of a.lvls) {
      if (t.bands[lv]) continue;
      // trusted fields only — no Textures/Meshes/EquipmentGloves from pre-v6.0 scrapes
      t.bands[lv] = {
        Name: a.name, Level: lv, hp: a.hpBy[lv] || 0, side: a.side, breed: a.breed,
        md: a.md, sc: a.sc, mesh: a.mesh, head: a.head, tex: a.tex, fam: a.fam,
        garden: false, guard: false
      };
    }
  }

  spawns.push({
    pf: PF, hash, name: a.name, reuse, lo: Math.min(...a.lvls), hi: Math.max(...a.lvls),
    x: +a.x.toFixed(3), y: +a.y.toFixed(3), z: +a.z.toFixed(3),
    angle: a.heading, wander: !!a.roam,
    posFrom: `review missing ${a.src} inst ${a.inst}`
  });
}

const plan = {
  source: TAG,
  generated: new Date().toISOString(),
  playfield: PF,
  playfieldName: 'Galway County',
  review: REV,
  wrongFixed: 0,
  wrongNote: '685/wrong.csv header-only (0 rows); nothing to fix',
  templates: Object.values(templates).map(t => ({
    hash: t.hash, wander: t.wander, friendly: t.friendly, src: t.src,
    bands: Object.values(t.bands).sort((a, b) => a.Level - b.Level)
  })),
  spawnsByPf: { [PF]: spawns },
  collisions,
  skipped,
  dropped,
  counts: {
    sightingsIn: dropped.sightingsIn,
    afterFilter: kept.length,
    instances: instances.length,
    afterCluster: pts.length,
    spawnPointsAdded: spawns.length,
    spawnReuse: spawns.filter(s => s.reuse).length,
    spawnNewTemplate: spawns.filter(s => !s.reuse).length,
    newTemplates: Object.keys(templates).length,
    wrongFixed: 0,
    collisions: collisions.length,
    skipped: skipped.length
  }
};

const out = path.join(__dirname, 'scrape_import_galway685_20261106.json');
fs.writeFileSync(out, JSON.stringify(plan, null, 1) + '\n');
console.log(JSON.stringify(plan.counts, null, 2));
console.log('dropped', JSON.stringify(dropped, null, 2));
console.log('collisions', collisions.length, collisions.slice(0, 8));
console.log('newTemplates sample', plan.templates.slice(0, 3).map(t => t.hash + ':' + t.bands[0].Name));
console.log('spawns', spawns.length, 'reuse', plan.counts.spawnReuse);
console.log('wrote', out);
