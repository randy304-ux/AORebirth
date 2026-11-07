// ddogs/tools/plan-adonis.js — full Adonis scrape import plan (2026-11-06 Whozewhatzit live AO)
// Source: I:\AO\imports\scrape-20261106-ado\ (raw + gpt-review). Invent nothing.
// Zones: 4872,4873,4213,4625,4626 (+4335 / gen dungeons if data). Same SpawnsAdd rules as Galway 685:
//   cluster 3 m, skip near existing spawn ≤12 m (any point = already present), reuse hash when name+md+levels cover,
//   chase displacement → calm/start pos, pets/world-objects/summon-chain held back, loot pairs counted once.
const fs = require('fs'), path = require('path');
const W = path.resolve(__dirname, '..', '..');
const GD = path.join(W, 'AORebirth', 'GameData');
const ROOT = process.env.ADO_SCRAPE || 'I:/AO/imports/scrape-20261106-ado';
const RAW = path.join(ROOT, 'raw');
const REV = path.join(ROOT, 'gpt-review');
const TAG = 'scrape-adonis-20261106';
const NEAR = 12, CLUSTER = 3;
const HOST_PFS = new Set([4872, 4873, 4213, 4625, 4626, 4335]);
const GEN_PFS = new Set([-1073474808, -1073343735, -1073278199]);
const ALL_PFS = new Set([...HOST_PFS, ...GEN_PFS]);
const SIDE = s => ({ Neutral: 0, Clan: 1, Omni: 2, OmniTek: 2, 'Omni-Tek': 2, Monster: 3 }[s] ?? 3);
const BREED = b => ({ Solitus: 1, Opifex: 2, Nanomage: 3, Atrox: 4, Monster: 0, HumanMonster: 0 }[b] ?? 0);
// Triggered Ancient-Container temple summons (guide) — never standing SpawnsAdd even if seen mid-fight.
const SUMMON_CHAIN = /^(Empath Joshua Kahn|Redeemed Gilthar|Omega Eva Pourais|Unredeemed Dalja|Gilthar|Joshua Kahn|Eva Pourais)\b/i;
const WORLDISH = /^(Guard Turret|Security Camera|Gilthar Statue|Gilthar's Sanctuary Statue|Ancient Control Panel|Portal|Exit |From |Treasure)\b/i;

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

const dropped = {
  sightingsIn: 0, pet: 0, worldObject: 0, summonChain: 0, worldish: 0,
  noMonsterData: 0, noY: 0, notNpc: 0, wrongPf: 0,
  instanceCollapsed: 0, clusterDedupe: 0, nearExistingSpawn: 0,
  nameMdCollision: 0, genDungeonNoHost: 0, lootDupPairs: 0, chaseCalmUsed: 0
};
const skipped = [];
const collisions = [];
const unresolved = [];
const notes = [];

// --- holdback indexes ---
const pets = parseCsv(fs.readFileSync(path.join(REV, 'excluded-pets.csv'), 'utf8'));
const petKeys = new Set(pets.map(r => `${r.PlayfieldID}|${r.Instance}|${r.Name}`));
const worldObjs = parseCsv(fs.readFileSync(path.join(REV, 'world-objects.csv'), 'utf8'));
const worldKeys = new Set(worldObjs.map(r => `${r.PlayfieldID}|${r.Instance}|${r.Name}`));
const moveReview = parseCsv(fs.readFileSync(path.join(REV, 'movement-review.csv'), 'utf8'));
const chase = new Map(); // pf|inst -> calm first pos (large chase)
for (const r of moveReview) {
  const span = +r.bbox_horizontal_span_m;
  if (!(span > 100)) continue; // large chase / pull; calm = first
  chase.set(`${r.PF}|${r.Instance}`, {
    x: +r.first_X, y: +r.first_Y, z: +r.first_Z, span,
    name: r.Name, pf: +r.PF
  });
}
const lootDup = parseCsv(fs.readFileSync(path.join(REV, 'loot-duplicate-review.csv'), 'utf8'));
// Prefer the named / high-slot copy of each pair; drop the blank-name low-slot twin.
const lootDupDrop = new Set();
{
  const groups = new Map();
  for (const r of lootDup) {
    const k = `${r.Timestamp}|${r.CorpseInstance}|${r.LowID}|${r.HighID}|${r.QL}|${r.Count}`;
    (groups.get(k) || groups.set(k, []).get(k)).push(r);
  }
  for (const [, rows] of groups) {
    if (rows.length < 2) continue;
    dropped.lootDupPairs++;
    rows.sort((a, b) => (+b.Slot) - (+a.Slot) || String(b.ItemName).length - String(a.ItemName).length);
    for (const r of rows.slice(1)) {
      lootDupDrop.add(`${r.source}`);
    }
  }
}

// --- load templates + server spawns ---
const nt = {};
for (const f of ['NpcTemplates.json', ...fs.readdirSync(GD).filter(f => /^NpcTemplates\..+\.json$/.test(f))]) {
  if (f === 'NpcTemplates.Adonis.json') continue; // ignore our own prior output when re-planning
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
      srv.push({
        src, hash: h, lo: s.minLevel, hi: s.maxLevel, position: s.position, ...p,
        names: namesOf(h), templated: !!(nt[h] && nt[h].Templates)
      });
    }
  }
  addSp(path.join(GD, 'Playfields', '' + pf, 'Spawns.json'), 'Spawns.json');
  const addDir = path.join(GD, 'PlayfieldContent', '' + pf);
  if (fs.existsSync(path.join(addDir, 'SpawnsAdd.json'))) addSp(path.join(addDir, 'SpawnsAdd.json'), 'SpawnsAdd.json');
  if (fs.existsSync(addDir)) {
    for (const f of fs.readdirSync(addDir).filter(f => /^SpawnsAdd\..+\.json$/.test(f))) {
      if (f === 'SpawnsAdd.adonis.json') continue; // ignore our own prior output when re-planning
      addSp(path.join(addDir, f), f);
    }
  }
  const np = path.join(addDir, 'Npcs.json');
  if (fs.existsSync(np)) {
    for (const n of rd(np).Npcs || []) {
      if (String(n.Key || '').startsWith('ddogs:scrapeado:')) continue;
      srv.push({
        src: 'Npcs.json', hash: n.Key, x: n.Position[0], y: n.Position[1], z: n.Position[2],
        names: [n.Name], templated: true
      });
    }
  }
  return srv;
}
const serverByPf = {};
for (const pf of HOST_PFS) serverByPf[pf] = loadServer(pf);

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
    for (const h of hs) {
      const cover = new Set(entries.filter(e => e.h === h).map(e => e.level));
      if (lvls.every(l => cover.has(l))) return h;
    }
    return null;
  }
  if (mds.every(m => m !== md)) {
    return { collision: true, existing: entries.map(e => `${e.h}:${e.md}`).filter((v, i, a) => a.indexOf(v) === i) };
  }
  return { collision: true, existing: entries.map(e => `${e.h}:${e.md}`).filter((v, i, a) => a.indexOf(v) === i) };
}

const usedHashes = new Set([...Object.keys(nt), ...Object.values(serverByPf).flatMap(s => s.map(x => x.hash))]);
try {
  const itBase = rd(path.join(GD, 'ItemTemplates.json'));
  for (const h of Object.keys(itBase)) usedHashes.add(h);
} catch (e) { notes.push('ItemTemplates.json read soft-fail: ' + e.message); }
for (const f of fs.readdirSync(GD).filter(f => /^ItemTemplates\..+\.json$/.test(f))) {
  try {
    const ov = rd(path.join(GD, f));
    for (const h of Object.keys(ov)) usedHashes.add(h);
  } catch (_) { /* comment-bearing overlays skipped for hash scan */ }
}

const b36 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ';
let seq = 0;
function newHash(prefix) {
  for (;;) {
    seq++;
    const h = prefix + b36[Math.floor(seq / 1296) % 36] + b36[Math.floor(seq / 36) % 36] + b36[seq % 36];
    if (!usedHashes.has(h)) { usedHashes.add(h); return h; }
  }
}

// --- observed locations (gpt clustered) ---
const locs = parseCsv(fs.readFileSync(path.join(REV, 'observed-locations.csv'), 'utf8'));
dropped.sightingsIn = locs.length;
const kept = [];
for (const r of locs) {
  const pf = +r.PlayfieldID;
  if (!ALL_PFS.has(pf)) { dropped.wrongPf++; continue; }
  const key = `${r.PlayfieldID}|${r.Instance}|${r.Name}`;
  if (petKeys.has(key) || r.IsPet === '1' || r.IsPet === 'True') {
    dropped.pet++; skipped.push({ name: r.Name, pf, why: 'pet', x: +r.X, z: +r.Z }); continue;
  }
  if (worldKeys.has(key) || r.Kind === 'terminal' || r.Kind === 'object' || r.IdType === 'Terminal') {
    dropped.worldObject++; skipped.push({ name: r.Name, pf, why: 'world object', x: +r.X, z: +r.Z }); continue;
  }
  if (SUMMON_CHAIN.test(r.Name)) {
    dropped.summonChain++; skipped.push({ name: r.Name, pf, why: 'temple summon chain', x: +r.X, z: +r.Z }); continue;
  }
  if (WORLDISH.test(r.Name)) {
    dropped.worldish++; skipped.push({ name: r.Name, pf, why: 'worldish/non-spawn', x: +r.X, z: +r.Z }); continue;
  }
  if (r.IsNpc !== '1' && r.IsNpc !== 'True') {
    dropped.notNpc++; skipped.push({ name: r.Name, pf, why: 'not npc', x: +r.X, z: +r.Z }); continue;
  }
  if (!(+r.MonsterData > 0)) {
    dropped.noMonsterData++; skipped.push({ name: r.Name, pf, why: 'no MonsterData', x: +r.X, z: +r.Z }); continue;
  }
  if (!Number.isFinite(+r.Y)) {
    dropped.noY++; skipped.push({ name: r.Name, pf, why: 'no Y height', x: +r.X, z: +r.Z }); continue;
  }
  kept.push(r);
}

// collapse by instance within pf
const byInst = new Map();
for (const r of kept) {
  const id = `${r.PlayfieldID}|${r.Instance}`;
  (byInst.get(id) || byInst.set(id, []).get(id)).push(r);
}
const instances = [];
for (const [, rs] of byInst) {
  rs.sort((a, b) => String(a.Timestamp).localeCompare(String(b.Timestamp)));
  const L = rs[rs.length - 1];
  const pf = +L.PlayfieldID;
  const ck = chase.get(`${pf}|${L.Instance}`);
  let x = +L.X, y = +L.Y, z = +L.Z, calm = false;
  if (ck && Number.isFinite(ck.x)) {
    x = ck.x; y = ck.y; z = ck.z; calm = true; dropped.chaseCalmUsed++;
  } else {
    // prefer earliest observation for multi-pos (closer to spawn) when roam/chase unclear
    const first = rs[0];
    if (rs.length > 1 && d2({ x: +first.X, z: +first.Z }, { x: +L.X, z: +L.Z }) > 5) {
      x = +first.X; y = +first.Y; z = +first.Z;
    }
  }
  if (rs.length > 1) dropped.instanceCollapsed += rs.length - 1;
  const lvls = [...new Set(rs.map(r => +r.Level))];
  const hpBy = {};
  for (const r of rs) hpBy[+r.Level] = +r.MaxHealth || 0;
  const roam = !calm && rs.some(r => d2({ x: +r.X, z: +r.Z }, { x, z }) > 5);
  // patrol waypoints from distinct positions (moderate roam only)
  let waypoints = [];
  if (roam) {
    const pts = [];
    for (const r of rs) {
      const p = { x: +r.X, y: +r.Y, z: +r.Z };
      if (!pts.some(q => d3(q, p) <= 3)) pts.push(p);
    }
    if (pts.length >= 2 && pts.length <= 12) {
      const span = Math.max(...pts.map(p => d2(p, pts[0])));
      if (span <= 80) waypoints = pts.slice(0, 8);
    }
  }
  instances.push({
    pf, inst: L.Instance, name: L.Name, row: L, roam, calm, waypoints,
    x, y, z,
    heading: Math.round(+L.HeadingDeg || 0),
    lvls, hpBy,
    md: +L.MonsterData, sc: +L.Scale || 100, mesh: +L.Mesh || 0,
    head: +L.HeadMesh || 0, tex: +L.MonsterTexture || 0, fam: +L.NPCFamily || 0,
    side: SIDE(L.Side), breed: BREED(L.Breed),
    sideName: L.Side, src: L.source
  });
}

// cluster same-name within 3 m (per pf)
const pts = [];
for (const i of instances) {
  const hit = pts.find(p => p.pf === i.pf && p.name === i.name && d3(p, i) <= CLUSTER);
  if (!hit) { pts.push({ ...i, members: 1 }); continue; }
  dropped.clusterDedupe++;
  skipped.push({ name: i.name, pf: i.pf, why: 'dedupe within 3 m', x: i.x, z: i.z, kept: `${hit.x.toFixed(1)},${hit.z.toFixed(1)}` });
  if (String(i.row.Timestamp) > String(hit.row.Timestamp) && !hit.calm) {
    const members = hit.members + 1;
    const lvls = [...new Set([...hit.lvls, ...i.lvls])];
    const hpBy = { ...hit.hpBy, ...i.hpBy };
    const waypoints = hit.waypoints.length >= i.waypoints.length ? hit.waypoints : i.waypoints;
    Object.assign(hit, i, { members, lvls, hpBy, waypoints, roam: hit.roam || i.roam, calm: hit.calm || i.calm });
  } else {
    hit.members++;
    hit.roam = hit.roam || i.roam;
    hit.calm = hit.calm || i.calm;
    for (const lv of i.lvls) if (!hit.lvls.includes(lv)) hit.lvls.push(lv);
    Object.assign(hit.hpBy, i.hpBy);
    if (i.waypoints.length > hit.waypoints.length) hit.waypoints = i.waypoints;
  }
}

const templates = {}; // key -> {hash,wander,friendly,src,bands:{},fillPink}
const spawnsByPf = {};
const pinkFills = []; // {hash, name, pf, ...} templates bound to existing pink hashText
const pinkClaimed = new Map(); // hashText -> {name, md} first claim wins
const genTemplatesOnly = [];
const flaggedNames = new Set();

function ensureTemplate(a, hashOpt) {
  const key = [a.name, a.md, a.mesh, a.fam].join('|');
  if (hashOpt) {
    // pink fill / forced hash
    if (!templates[key + '|' + hashOpt]) {
      templates[key + '|' + hashOpt] = {
        hash: hashOpt, wander: !!a.roam, friendly: a.side !== 3,
        src: `observed ${a.src} inst ${a.inst}`, bands: {}, fillPink: true, pf: a.pf
      };
      usedHashes.add(hashOpt);
    }
    const t = templates[key + '|' + hashOpt];
    t.wander = t.wander || !!a.roam;
    for (const lv of a.lvls) {
      if (t.bands[lv]) continue;
      t.bands[lv] = {
        Name: a.name, Level: lv, hp: a.hpBy[lv] || 0, side: a.side, breed: a.breed,
        md: a.md, sc: a.sc, mesh: a.mesh, head: a.head, tex: a.tex, fam: a.fam,
        garden: false, guard: a.side !== 3
      };
    }
    return t.hash;
  }
  if (!templates[key]) {
    templates[key] = {
      hash: newHash('D'), wander: !!a.roam, friendly: a.side !== 3,
      src: `observed ${a.src} inst ${a.inst}`, bands: {}, fillPink: false, pf: a.pf
    };
  }
  const t = templates[key];
  t.wander = t.wander || !!a.roam;
  for (const lv of a.lvls) {
    if (t.bands[lv]) continue;
    t.bands[lv] = {
      Name: a.name, Level: lv, hp: a.hpBy[lv] || 0, side: a.side, breed: a.breed,
      md: a.md, sc: a.sc, mesh: a.mesh, head: a.head, tex: a.tex, fam: a.fam,
      garden: false, guard: a.side !== 3
    };
  }
  return t.hash;
}

for (const a of pts.sort((x, y) => x.pf - y.pf || x.name.localeCompare(y.name) || x.x - y.x || x.z - y.z)) {
  // Generated SL instance IDs cannot be hosted as permanent SpawnsAdd playfields.
  if (GEN_PFS.has(a.pf)) {
    dropped.genDungeonNoHost++;
    const ru = reuseHash(a.name, a.md, a.lvls);
    let hash = null, reuse = false;
    if (ru && ru.collision) {
      if (!flaggedNames.has(a.name)) {
        flaggedNames.add(a.name);
        collisions.push({ kind: 'name-exists-different-md', name: a.name, scrapeMd: a.md, existing: ru.existing.join(', ') });
      }
      skipped.push({ name: a.name, pf: a.pf, why: 'gen dungeon + name/md collision', x: a.x, z: a.z });
      continue;
    }
    if (typeof ru === 'string') { hash = ru; reuse = true; }
    else hash = ensureTemplate(a);
    genTemplatesOnly.push({
      pf: a.pf, hash, name: a.name, reuse, lo: Math.min(...a.lvls), hi: Math.max(...a.lvls),
      x: +a.x.toFixed(3), y: +a.y.toFixed(3), z: +a.z.toFixed(3),
      angle: a.heading, wander: !!a.roam, waypoints: a.waypoints,
      note: 'template+loot only; engine cannot host signed ACG instance as permanent SpawnsAdd'
    });
    continue;
  }

  if (!HOST_PFS.has(a.pf)) continue;
  if (a.pf === 4335) {
    // Dark Ruins Solo — pack has no observed-location rows (omitted extract).
    continue;
  }

  const srv = serverByPf[a.pf] || [];
  const near = srv.find(s => d2(s, a) <= NEAR);
  if (near) {
    if (near.templated) {
      dropped.nearExistingSpawn++;
      skipped.push({ name: a.name, pf: a.pf, why: `near existing templated spawn ${near.hash} ≤${NEAR}m`, x: a.x, z: a.z });
      continue;
    }
    // Pink point already present: fill template onto pink hash once (first name+md wins).
    // If another name already claimed this pink, fall through to SpawnsAdd so we do not overwrite.
    if (near.hash && near.hash.length === 4 && !nt[near.hash]) {
      const claimed = pinkClaimed.get(near.hash);
      if (claimed && (claimed.name !== a.name || claimed.md !== a.md)) {
        // different mob near same pink slot — keep pink claim, place this one via SpawnsAdd below
        skipped.push({
          name: a.name, pf: a.pf,
          why: `pink ${near.hash} already claimed by ${claimed.name}; SpawnsAdd instead`,
          x: a.x, z: a.z
        });
        // do not continue — fall through to SpawnsAdd path
      } else {
        dropped.nearExistingSpawn++;
        const ru = reuseHash(a.name, a.md, a.lvls);
        if (ru && ru.collision) {
          if (!flaggedNames.has(a.name)) {
            flaggedNames.add(a.name);
            collisions.push({ kind: 'name-exists-different-md', name: a.name, scrapeMd: a.md, existing: ru.existing.join(', ') });
          }
          dropped.nameMdCollision++;
          skipped.push({ name: a.name, pf: a.pf, why: 'pink fill blocked: name/md collision', x: a.x, z: a.z });
          continue;
        }
        if (typeof ru === 'string') {
          skipped.push({ name: a.name, pf: a.pf, why: `near pink ${near.hash}; reuse existing hash ${ru} (no fill)`, x: a.x, z: a.z });
          continue;
        }
        if (!claimed) pinkClaimed.set(near.hash, { name: a.name, md: a.md });
        ensureTemplate(a, near.hash);
        pinkFills.push({ pf: a.pf, hash: near.hash, name: a.name, x: a.x, z: a.z });
        skipped.push({ name: a.name, pf: a.pf, why: `pink fill ${near.hash} (spawn already present)`, x: a.x, z: a.z });
        continue;
      }
    } else {
      dropped.nearExistingSpawn++;
      skipped.push({ name: a.name, pf: a.pf, why: `near existing spawn ${near.hash || '?'} ≤${NEAR}m`, x: a.x, z: a.z });
      continue;
    }
  }

  const ru = reuseHash(a.name, a.md, a.lvls);
  let hash = null, reuse = false;
  if (ru && ru.collision) {
    if (!flaggedNames.has(a.name)) {
      flaggedNames.add(a.name);
      collisions.push({ kind: 'name-exists-different-md', name: a.name, scrapeMd: a.md, scrapeMesh: a.mesh, existing: ru.existing.join(', ') });
    }
    dropped.nameMdCollision++;
    skipped.push({ name: a.name, pf: a.pf, why: 'name exists with different MonsterData — not guessed', x: a.x, z: a.z, scrapeMd: a.md, existing: ru.existing.join(', ') });
    continue;
  }
  if (typeof ru === 'string') { hash = ru; reuse = true; }
  else hash = ensureTemplate(a);

  (spawnsByPf[a.pf] = spawnsByPf[a.pf] || []).push({
    pf: a.pf, hash, name: a.name, reuse, lo: Math.min(...a.lvls), hi: Math.max(...a.lvls),
    x: +a.x.toFixed(3), y: +a.y.toFixed(3), z: +a.z.toFixed(3),
    angle: a.heading, wander: !!a.roam,
    additionalPoints: (a.waypoints || []).filter(p => d3(p, a) > 0.5).map(p => [+p.x.toFixed(3), +p.y.toFixed(3), +p.z.toFixed(3)]),
    posFrom: `observed ${a.src} inst ${a.inst}${a.calm ? ' (calm/start)' : ''}`
  });
}

// --- loot (v61), count duplicate pairs once ---
const lootRows = parseCsv(fs.readFileSync(path.join(RAW, 'loot_20261106_Whozewhatzit_v61.csv'), 'utf8'));
const lootByName = {};
const lootSeen = new Set(); // corpse|low|high|ql|count once
for (const r of lootRows) {
  const pf = +r.PlayfieldID;
  if (!ALL_PFS.has(pf)) continue;
  if (r.SourceType && r.SourceType !== 'Corpse') continue; // containers separate
  const srcRef = `ado_loot_20261106_Whozewhatzit_v61.csv#record=?`;
  // drop blank twin of duplicate pairs when ItemName empty and a named sibling exists in review
  const dupKey = `${r.Timestamp}|${r.CorpseInstance}|${r.LowID}|${r.HighID}|${r.QL}|${r.Count}`;
  // Prefer rows with ItemName when both exist; skip blank-name if named sibling in lootDupDrop set via source — use ItemName empty + low slot heuristic
  if (!r.ItemName && +r.Slot < 100) {
    // likely the unnamed twin; skip if a named copy exists in same corpse/item
    const namedTwin = lootRows.find(x =>
      x.Timestamp === r.Timestamp && x.CorpseInstance === r.CorpseInstance &&
      x.LowID === r.LowID && x.HighID === r.HighID && x.QL === r.QL && x.Count === r.Count && x.ItemName);
    if (namedTwin) continue;
  }
  const uniq = `${r.CorpseInstance}|${r.LowID}|${r.HighID}|${r.QL}|${r.Count}|${r.MobName}`;
  if (lootSeen.has(uniq)) continue;
  lootSeen.add(uniq);
  const name = (r.MobName || '').trim();
  if (!name) continue;
  const low = +r.LowID, high = +r.HighID;
  if (!(low > 0)) continue;
  const list = lootByName[name] || (lootByName[name] = []);
  if (!list.some(e => e.low === low && e.high === high)) {
    list.push({ low, high, chance: 100, ql: +r.QL || 0, count: +r.Count || 1, name: r.ItemName || '' });
  }
}
// Eradicus must include Joker's Glyph 227905 (already in loot if logged; ensure)
if (!lootByName['Eradicus']) lootByName['Eradicus'] = [];
if (!lootByName['Eradicus'].some(e => e.low === 227905)) {
  lootByName['Eradicus'].push({ low: 227905, high: 227905, chance: 100, ql: 160, count: 1, name: "Joker's Glyph" });
  notes.push("Eradicus loot: ensured Joker's Glyph 227905/227905 QL160 (logged 19:25:07); LootStackOverrides stack-of-6 already present");
}

// Loot-only dryads: no spawn, attach loot to template if one exists / create template-only
for (const dry of ['Mooching Dryad', 'Skulking Dryad']) {
  if (!lootByName[dry]) continue;
  const existing = byName[dry];
  if (existing && existing.length) {
    notes.push(`${dry}: loot-only (no spawn row); loot will append to existing template hashes ${[...new Set(existing.map(e => e.h))].join(',')}`);
  } else {
    // create template-only if we somehow have md from loot? loot has MobMonsterData
    const sample = lootRows.find(r => r.MobName === dry && +r.MobMonsterData > 0);
    if (sample) {
      const a = {
        name: dry, md: +sample.MobMonsterData, mesh: +sample.MobMesh || 0, fam: 0,
        sc: 100, head: 0, tex: 0, side: 3, breed: 0, lvls: [+sample.MobLevel || 1],
        hpBy: { [+sample.MobLevel || 1]: 0 }, roam: false, src: 'loot-only', inst: sample.CorpseInstance, pf: +sample.PlayfieldID
      };
      ensureTemplate(a);
      notes.push(`${dry}: loot-only; created template-only (no SpawnsAdd)`);
    } else {
      unresolved.push({ kind: 'loot-only-no-template', name: dry });
    }
  }
}

// existingLoot append for names that already have templates and weren't newly created
const existingLoot = {};
for (const [name, rows] of Object.entries(lootByName)) {
  const entries = byName[name] || [];
  if (!entries.length) continue;
  // only if we didn't create a new template for this name in this plan
  const made = Object.values(templates).some(t => Object.values(t.bands).some(b => b.Name === name));
  if (made) continue;
  const hashes = [...new Set(entries.map(e => e.h))];
  existingLoot[name] = { hashes, rows };
}

// --- vendors ---
const vendorsCsv = parseCsv(fs.readFileSync(path.join(REV, 'vendors.csv'), 'utf8'));
const shopItems = parseCsv(fs.readFileSync(path.join(RAW, 'shop_items_20261106_Whozewhatzit.csv'), 'utf8'));
const npcsRaw = parseCsv(fs.readFileSync(path.join(RAW, 'scraped_npcs_20261106_Whozewhatzit.csv'), 'utf8'));
const yuttosPos = {};
for (const r of npcsRaw) {
  if (+r.PlayfieldID !== 4872) continue;
  if (!/^Yuttos /i.test(r.Name)) continue;
  if (!yuttosPos[r.Name]) {
    yuttosPos[r.Name] = {
      name: r.Name, x: +r.X, y: +r.Y, z: +r.Z,
      rot: [+r.RotX || 0, +r.RotY || 0, +r.RotZ || 0, +r.RotW || 1],
      mesh: +r.Mesh || 0, md: +r.MonsterData || 0, inst: r.Instance
    };
  }
}
const vendors = [];
for (const v of vendorsCsv) {
  const inst = v.VendorInstance;
  const items = shopItems.filter(s => String(s.VendorInstance) === String(inst) && +s.PlayfieldID === 4872);
  const stock = [];
  const seen = new Set();
  for (const it of items) {
    const k = `${it.LowID}|${it.HighID}|${it.QL}`;
    if (seen.has(k)) continue;
    seen.add(k);
    stock.push([+it.LowID, +it.HighID, +it.QL || 1]);
  }
  const provisional = (v.nearby_dialog_names_unverified || '').trim();
  const pos = provisional && yuttosPos[provisional] ? yuttosPos[provisional] : null;
  unresolved.push({
    kind: 'vendor-unresolved',
    vendorInstance: inst,
    stockRows: stock.length,
    provisionalName: provisional || '',
    placedAtYuttosPos: !!pos,
    reason: pos
      ? 'stock placed at matching Yuttos NPC position using nearby-dialogue provisional name; typed VendorInstance join unproven'
      : 'no clear position join; stock retained in plan only'
  });
  vendors.push({
    pf: 4872,
    vendorInstance: inst,
    name: provisional || `Adonis Vending ${inst}`,
    provisional: true,
    unresolved: true,
    nameStatus: v.name_status,
    pos: pos ? [pos.x, pos.y, pos.z] : null,
    rot: pos ? pos.rot : [0, 0, 0, 1],
    mesh: pos ? pos.mesh : 0,
    items: stock
  });
}

// --- doors (evidence only; no DoorsAdd loader) ---
const doors = parseCsv(fs.readFileSync(path.join(REV, 'door-evidence.csv'), 'utf8'))
  .filter(r => r.Name && r.Name !== '(no doors)' && ALL_PFS.has(+r.PlayfieldID))
  .map(r => ({
    pf: +r.PlayfieldID, name: r.Name, x: +r.X, y: +r.Y, z: +r.Z,
    heading: +r.HeadingDeg || 0, instance: r.Instance, identity: r.Identity,
    roomLink1: r.RoomLink1, roomLink2: r.RoomLink2
  }));
notes.push(`doors: ${doors.length} actual door rows recorded as evidence; no DoorsAdd GameData loader — maze 4213 has no Doors.dat in Playfields (report)`);

// Dark Ruins / gen C missing
notes.push('Dark Ruins Solo (4335): visited but omitted from detailed extract — no observed-location rows; nothing imported');
notes.push('Generated dungeon -1073278199: ~25s visit, omitted from extract — no data');
notes.push('Generated dungeons -1073474808 / -1073343735: templates+loot only (ACG instance IDs not hostable as permanent SpawnsAdd). Eradicus interior pos retained on template-only record; not placed outdoors (would invent).');

const plan = {
  source: TAG,
  generated: new Date().toISOString(),
  playfields: [...HOST_PFS],
  generatedPlayfields: [...GEN_PFS],
  review: REV,
  raw: RAW,
  templates: Object.values(templates).map(t => ({
    hash: t.hash, wander: t.wander, friendly: t.friendly, src: t.src,
    fillPink: !!t.fillPink, pf: t.pf,
    bands: Object.values(t.bands).sort((a, b) => a.Level - b.Level)
  })),
  spawnsByPf,
  genTemplatesOnly,
  pinkFills,
  loot: lootByName,
  existingLoot,
  vendors,
  doors,
  collisions,
  skipped,
  unresolved,
  notes,
  dropped,
  counts: {
    sightingsIn: dropped.sightingsIn,
    afterFilter: kept.length,
    instances: instances.length,
    afterCluster: pts.length,
    spawnPointsAdded: Object.values(spawnsByPf).reduce((n, a) => n + a.length, 0),
    spawnReuse: Object.values(spawnsByPf).flat().filter(s => s.reuse).length,
    spawnNewTemplate: Object.values(spawnsByPf).flat().filter(s => !s.reuse).length,
    newTemplates: Object.keys(templates).length,
    pinkFills: pinkFills.length,
    genTemplateOnly: genTemplatesOnly.length,
    lootMobs: Object.keys(lootByName).length,
    lootItems: Object.values(lootByName).reduce((n, a) => n + a.length, 0),
    vendors: vendors.length,
    vendorUnresolved: unresolved.filter(u => u.kind === 'vendor-unresolved').length,
    doors: doors.length,
    collisions: collisions.length,
    skipped: skipped.length
  }
};

const out = path.join(__dirname, 'scrape_import_adonis_20261106.json');
fs.writeFileSync(out, JSON.stringify(plan, null, 1) + '\n');
console.log(JSON.stringify(plan.counts, null, 2));
console.log('dropped', JSON.stringify(dropped, null, 2));
console.log('unresolved', unresolved.length, unresolved.slice(0, 12));
console.log('notes', notes.length);
console.log('per-pf spawns', Object.fromEntries(Object.entries(spawnsByPf).map(([k, v]) => [k, v.length])));
console.log('wrote', out);
