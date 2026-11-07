// ddogs/tools/plan-rk-remaining.js — Phase 2: remaining Rubi-Ka scrape import
// Sources: scrape-20261106-review missing.csv; today raw RK rows; older outdoor OK.
// Skip already-live: 685 Galway, 585 Aegean, 655 ICC. Skip Adonis/SL. No invent.
// Loot: only loot_*_v61.csv (+ confirmed Reck facts). Gloves blank outdoors; city outfits only when scraped.
const fs = require('fs'), path = require('path');
const W = path.resolve(__dirname, '..', '..');
const GD = path.join(W, 'AORebirth', 'GameData');
const REV = process.env.RK_REVIEW || 'I:/AO/imports/scrape-20261106-review';
const TODAY = process.env.RK_TODAY || 'I:/AO/imports/scrape-20261106-ado/raw';
const TAG = 'scrape-rk-20261106';
const NEAR = 12, CLUSTER = 3;
const SKIP_LIVE = new Set([685, 585, 655]); // already imported / reconciliation hold
const SKIP_ADONIS = new Set([4872, 4873, 4213, 4625, 4626]);
const CITY_PF = new Set([540, 545, 566, 567, 700, 705, 710, 730, 735, 740, 800, 950, 954, 1902, 4376, 7001]);
const SIDE = s => ({ Neutral: 0, Clan: 1, Omni: 2, OmniTek: 2, 'Omni-Tek': 2, Monster: 3 }[s] ?? 3);
const BREED = b => ({ Solitus: 1, Opifex: 2, Nanomage: 3, Atrox: 4, Monster: 0, HumanMonster: 0 }[b] ?? 0);
const GUARD_PULL = /^(Male Captain|Female Captain|Patrol mechdog|Waste collector|Simple Missile Turret|SAM Battery|Control Tower|Guardian Conductor|Massive Cannon|Guard\b|Trooper\b)/i;
const TEMP_CITY = /^(Control Tower|SAM Battery|Guardian Conductor|Massive Cannon Turret)\b/i;

// Confirmed Reck loot (user + itemnames.sql IDs — do not guess)
const RECK_LOOT = {
  'Abandoned Hope': [{ low: 303071, high: 303071, chance: 100, name: "Death's Door" }],
  'Warchief Skawt': [
    { low: 303058, high: 303058, chance: 100, name: "Skawt's Plasma Emitter" },
    { low: 303059, high: 303059, chance: 100, name: "Skawt's Compact Plasma Emitter" }
  ],
  'Prime Evolution Huzzum': [
    { low: 303056, high: 303056, chance: 100, name: "The Wizdom of Huzzum" },
    { low: 303055, high: 303055, chance: 100, name: "Huzzum's Iron Fist" }
  ],
  'Huzzum': [
    { low: 303056, high: 303056, chance: 100, name: "The Wizdom of Huzzum" },
    { low: 303055, high: 303055, chance: 100, name: "Huzzum's Iron Fist" }
  ],
  'Prototype 73-Omega Ninjadroid': [
    { low: 142695, high: 142695, chance: 100, name: 'Augmented Biomech Body Armor', ql: 185 },
    { low: 147687, high: 147687, chance: 100, name: 'Instruction Disc (Cascade of the Storm)' },
    { low: 162347, high: 162347, chance: 100, name: 'Instruction Disc (Frenzy of Fur)' }
  ]
};
const SKAWT_TRASH = new Set(['Baron Skullfaccia', 'EVO Assassin', 'EVO Berserker']);

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
  return rows.filter(r => r.length >= h.length - 2).map(r => Object.fromEntries(h.map((k, i) => [k, r[i] || ''])));
}
const rd = p => JSON.parse(fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, ''));
const d2 = (a, b) => Math.hypot(a.x - b.x, a.z - b.z);
const d3 = (a, b) => (Number.isFinite(a.y) && Number.isFinite(b.y))
  ? Math.hypot(a.x - b.x, a.y - b.y, a.z - b.z) : d2(a, b);

function isSlPf(pf) {
  if (SKIP_ADONIS.has(pf)) return true;
  if (pf >= 4000 && pf <= 4999) return true; // SL / Jobe / gardens / Adonis / Scheol
  return false;
}

const counts = parseCsv(fs.readFileSync(path.join(REV, 'counts.csv'), 'utf8').replace(/^\uFEFF/, ''));
const pfName = {};
for (const r of counts) pfName[+r.PF] = (r.Name || '').trim() || ('PF ' + r.PF);
pfName[125] = pfName[125] || "Smuggler's Rearranged";
pfName[750] = pfName[750] || 'The Reck';

const dropped = {
  sightingsIn: 0, skipLive: 0, skipSl: 0, skipAlreadyImportTag: 0,
  pet: 0, tempCity: 0, guardPull: 0, clondykeWest: 0,
  noMonsterData: 0, noY: 0, notNpc: 0,
  instanceCollapsed: 0, clusterDedupe: 0, nearExistingSpawn: 0,
  nameMdCollision: 0, chaseCalmUsed: 0, oldLootHeld: 0
};
const skipped = [];
const collisions = [];
const heldBack = [];
const alreadyPresent = []; // per-zone tallies of skipped-as-present
const notes = [];

// --- templates + server ---
const nt = {};
for (const f of ['NpcTemplates.json', ...fs.readdirSync(GD).filter(f => /^NpcTemplates\..+\.json$/.test(f))]) {
  if (f === 'NpcTemplates.RK.json') continue;
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

/** Pink / guessed stand-ins must NEVER count as "already present" for the 12 m skip. */
function isPinkLike(hash, templated) {
  if (!templated) return true; // missing template -> AAAA pink fallback
  const t = nt[hash];
  if (!t || !t.Templates) return true;
  const prov = t.ContentProvenance || {};
  if (prov.Guessed || prov.StandIn) return true;
  const band = t.Templates[0];
  const bp = (band && band.ContentProvenance) || {};
  if (bp.Guessed || bp.StandIn) return true;
  return false;
}

function loadServer(pf) {
  const srv = [];
  function addSp(file, src) {
    if (!fs.existsSync(file)) return;
    const doc = rd(file);
    // ignore our own prior RK output when re-planning
    if (src.includes('.rk.') || doc.source === TAG) return;
    for (const s of doc.spawns || []) {
      const h = s.hashText || '';
      if (hidden.has(h)) continue;
      const templated = !!(nt[h] && nt[h].Templates);
      srv.push({
        src, hash: h, x: s.position[0], y: s.position[1], z: s.position[2],
        names: namesOf(h), templated, pinkLike: isPinkLike(h, templated)
      });
    }
  }
  addSp(path.join(GD, 'Playfields', '' + pf, 'Spawns.json'), 'Spawns.json');
  const addDir = path.join(GD, 'PlayfieldContent', '' + pf);
  if (fs.existsSync(path.join(addDir, 'SpawnsAdd.json'))) addSp(path.join(addDir, 'SpawnsAdd.json'), 'SpawnsAdd.json');
  if (fs.existsSync(addDir)) {
    for (const f of fs.readdirSync(addDir).filter(f => /^SpawnsAdd\..+\.json$/.test(f))) {
      if (f === 'SpawnsAdd.rk.json') continue;
      addSp(path.join(addDir, f), f);
    }
  }
  const np = path.join(addDir, 'Npcs.json');
  if (fs.existsSync(np)) {
    for (const n of rd(np).Npcs || []) {
      if (String(n.Key || '').includes(':scrapeado:') || String(n.Key || '').includes(':scraperk:')) continue;
      srv.push({ src: 'Npcs.json', hash: n.Key, x: n.Position[0], y: n.Position[1], z: n.Position[2], names: [n.Name], templated: true });
    }
  }
  return srv;
}

const byName = {};
for (const [h, t] of Object.entries(nt)) {
  if (!t.Templates) continue;
  for (const b of t.Templates) {
    if (!b || !b.Name || !b.Stats) continue;
    (byName[b.Name] = byName[b.Name] || []).push({ h, md: +b.Stats['359'] || 0, level: +b.Level });
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
  if (mds.every(m => m !== md) || mds.length > 1) {
    return { collision: true, existing: entries.map(e => `${e.h}:${e.md}`).filter((v, i, a) => a.indexOf(v) === i) };
  }
  return null;
}

const usedHashes = new Set(Object.keys(nt));
try {
  for (const h of Object.keys(rd(path.join(GD, 'ItemTemplates.json')))) usedHashes.add(h);
} catch (_) {}
for (const f of fs.readdirSync(GD).filter(f => /^ItemTemplates\..+\.json$/.test(f))) {
  try { for (const h of Object.keys(rd(path.join(GD, f)))) usedHashes.add(h); } catch (_) {}
}
const b36 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ';
let seq = 0;
function newHash() {
  for (;;) {
    seq++;
    const h = 'K' + b36[Math.floor(seq / 1296) % 36] + b36[Math.floor(seq / 36) % 36] + b36[seq % 36];
    if (!usedHashes.has(h)) { usedHashes.add(h); return h; }
  }
}

// --- gather sightings: review missing + today RK ---
const raw = [];
function pushSight(r, src, pfForced) {
  const pf = pfForced != null ? pfForced : +r.PlayfieldID;
  if (!Number.isFinite(pf)) return;
  if (SKIP_LIVE.has(pf)) { dropped.skipLive++; return; }
  if (isSlPf(pf)) { dropped.skipSl++; return; }
  if (r.IsPet === '1' || r.IsPet === 'True') { dropped.pet++; return; }
  if (r.IsNpc !== '1' && r.IsNpc !== 'True' && r.IsNpc !== undefined) {
    // review missing always NPC; today may omit
    if (src.startsWith('today') && r.IsNpc !== '1' && r.IsNpc !== 'True') { dropped.notNpc++; return; }
  }
  if (TEMP_CITY.test(r.Name || '')) { dropped.tempCity++; return; }
  if (pf === 687 && GUARD_PULL.test(r.Name || '')) {
    dropped.guardPull++;
    heldBack.push({ name: r.Name, pf, why: 'Galway Shire zone-line guard/city pull cluster', x: +r.X, z: +r.Z });
    return;
  }
  if (pf === 670 && +r.X < 963) {
    dropped.clondykeWest++;
    heldBack.push({ name: r.Name, pf, why: 'Clondyke west of X=963 (review-only border)', x: +r.X, z: +r.Z });
    return;
  }
  if (!(+r.MonsterData > 0)) { dropped.noMonsterData++; return; }
  if (!Number.isFinite(+r.Y)) { dropped.noY++; return; }
  raw.push({
    pf, inst: String(r.Instance), name: r.Name, src,
    ts: r.Timestamp || '', x: +r.X, y: +r.Y, z: +r.Z,
    heading: Math.round(+r.HeadingDeg || 0),
    level: +r.Level || 1, hp: +r.MaxHealth || 0,
    md: +r.MonsterData, sc: +r.Scale || 100, mesh: +r.Mesh || 0,
    head: +r.HeadMesh || 0, tex: +r.MonsterTexture || 0, fam: +r.NPCFamily || 0,
    side: SIDE(r.Side), breed: BREED(r.Breed),
    textures: r.Textures || '', meshes: r.Meshes || '',
    review_flags: r.review_flags || ''
  });
}

// review missing per PF folder
for (const r of counts) {
  const pf = +r.PF;
  if (SKIP_LIVE.has(pf)) { dropped.skipLive += +r.missing || 0; continue; }
  if (isSlPf(pf)) { dropped.skipSl += +r.missing || 0; continue; }
  if (+r.missing < 1) continue;
  const f = path.join(REV, String(pf), 'missing.csv');
  if (!fs.existsSync(f)) continue;
  const rows = parseCsv(fs.readFileSync(f, 'utf8').replace(/^\uFEFF/, ''));
  for (const row of rows) pushSight(row, 'review-missing', pf);
}

// today full-day RK NPCs (Reck, Smuggler's, Shire, cities, etc.)
const todayNpc = path.join(TODAY, 'scraped_npcs_20261106_Whozewhatzit.csv');
if (fs.existsSync(todayNpc)) {
  const rows = parseCsv(fs.readFileSync(todayNpc, 'utf8').replace(/^\uFEFF/, ''));
  for (const row of rows) {
    const pf = +row.PlayfieldID;
    if (SKIP_LIVE.has(pf) || isSlPf(pf)) continue;
    // only RK-ish: outdoor <1000, Reck 750, Smuggler 125, training, barracks, backyards, offices
    if (pf > 1000 && ![125, 152, 1833, 1902, 4376, 7001, 3089, 3067, 3052].includes(pf) && !(pf >= 3000 && pf < 3100)) {
      continue;
    }
    // exclude signed ACG instance IDs (gen dungeons already handled in Adonis pass)
    if (pf < 0) continue;
    pushSight(row, 'today', pf);
  }
}

dropped.sightingsIn = raw.length;
notes.push('Older loot_* (non-v61) held back per brief; Reck confirmed facts applied separately');
dropped.oldLootHeld = 95; // Reck older-file rows not imported as drop table evidence

// collapse by pf|instance
const byInst = new Map();
for (const r of raw) {
  const id = r.pf + '|' + r.inst;
  (byInst.get(id) || byInst.set(id, []).get(id)).push(r);
}
const instances = [];
for (const [, rs] of byInst) {
  rs.sort((a, b) => String(a.ts).localeCompare(String(b.ts)));
  const first = rs[0], last = rs[rs.length - 1];
  let x = last.x, y = last.y, z = last.z, calm = false;
  // chase / multi-pos: prefer earliest (calm/start)
  if (rs.length > 1 && d2(first, last) > 30) {
    x = first.x; y = first.y; z = first.z; calm = true; dropped.chaseCalmUsed++;
  } else if (rs.length > 1 && d2(first, last) > 5) {
    x = first.x; y = first.y; z = first.z;
  }
  if (rs.length > 1) dropped.instanceCollapsed += rs.length - 1;
  const lvls = [...new Set(rs.map(r => r.level))];
  const hpBy = {};
  for (const r of rs) hpBy[r.level] = r.hp || 0;
  const roam = !calm && rs.some(r => d2(r, { x, z }) > 5);
  const L = last;
  instances.push({
    pf: L.pf, inst: L.inst, name: L.name, src: L.src, roam, calm,
    x, y, z, heading: L.heading, lvls, hpBy,
    md: L.md, sc: L.sc, mesh: L.mesh, head: L.head, tex: L.tex, fam: L.fam,
    side: L.side, breed: L.breed, textures: L.textures, meshes: L.meshes
  });
}

// cluster same-name within 3m per pf
const pts = [];
for (const i of instances) {
  const hit = pts.find(p => p.pf === i.pf && p.name === i.name && d3(p, i) <= CLUSTER);
  if (!hit) { pts.push({ ...i, members: 1 }); continue; }
  dropped.clusterDedupe++;
  hit.members++;
  hit.roam = hit.roam || i.roam;
  hit.calm = hit.calm || i.calm;
  for (const lv of i.lvls) if (!hit.lvls.includes(lv)) hit.lvls.push(lv);
  Object.assign(hit.hpBy, i.hpBy);
}

const serverCache = {};
const templates = {};
const spawnsByPf = {};
const pinkFills = [];
const pinkClaimed = new Map();
const flaggedNames = new Set();

function ensureTemplate(a, hashOpt) {
  const key = hashOpt
    ? [a.name, a.md, a.mesh, a.fam, hashOpt].join('|')
    : [a.name, a.md, a.mesh, a.fam].join('|');
  if (!templates[key]) {
    templates[key] = {
      hash: hashOpt || newHash(), wander: !!a.roam, friendly: a.side !== 3,
      src: `${a.src} inst ${a.inst}`, fillPink: !!hashOpt, pf: a.pf, bands: {}
    };
    usedHashes.add(templates[key].hash);
  }
  const t = templates[key];
  t.wander = t.wander || !!a.roam;
  for (const lv of a.lvls) {
    if (t.bands[lv]) continue;
    t.bands[lv] = {
      Name: a.name, Level: lv, hp: a.hpBy[lv] || 0, side: a.side, breed: a.breed,
      md: a.md, sc: a.sc, mesh: a.mesh, head: a.head, tex: a.tex, fam: a.fam,
      textures: a.textures || '', meshes: a.meshes || '',
      guard: a.side !== 3, city: CITY_PF.has(a.pf)
    };
  }
  return t.hash;
}

for (const a of pts.sort((x, y) => x.pf - y.pf || x.name.localeCompare(y.name) || x.x - y.x)) {
  if (!serverCache[a.pf]) serverCache[a.pf] = loadServer(a.pf);
  const srv = serverCache[a.pf];
  // Pinks never count as present. If a pink sits on the scrape, fill/overwrite it even when
  // another real spawn is also within 12 m (otherwise the pink remains forever).
  const nearReal = srv.find(s => d2(s, a) <= NEAR && !s.pinkLike);
  const nearPink = srv.find(s => d2(s, a) <= NEAR && s.pinkLike);
  if (nearPink && nearPink.hash && nearPink.hash.length === 4) {
    const claimed = pinkClaimed.get(nearPink.hash);
    if (claimed && (claimed.name !== a.name || claimed.md !== a.md)) {
      // Contested pink hash: fall through to SpawnsAdd with a new/reused hash.
    } else {
      const ru = reuseHash(a.name, a.md, a.lvls);
      // Name/md collision on a pink still fills the pink with scrape stats (overwrite Guessed).
      if (ru && ru.collision) {
        if (!flaggedNames.has(a.name)) {
          flaggedNames.add(a.name);
          collisions.push({ kind: 'name-md', name: a.name, scrapeMd: a.md, existing: ru.existing.join(',') });
        }
      }
      if (!claimed) pinkClaimed.set(nearPink.hash, { name: a.name, md: a.md });
      ensureTemplate(a, nearPink.hash);
      pinkFills.push({ pf: a.pf, hash: nearPink.hash, name: a.name });
      continue;
    }
  }
  if (nearReal) {
    dropped.nearExistingSpawn++;
    alreadyPresent.push({ pf: a.pf, name: a.name, why: `templated ${nearReal.hash}`, x: a.x, z: a.z });
    continue;
  }

  const ru = reuseHash(a.name, a.md, a.lvls);
  let hash = null, reuse = false;
  if (ru && ru.collision) {
    if (!flaggedNames.has(a.name)) {
      flaggedNames.add(a.name);
      collisions.push({ kind: 'name-md', name: a.name, scrapeMd: a.md, existing: ru.existing.join(',') });
    }
    // Last chance: overwrite the nearest pink even on name/md collision (Guessed wrong MD).
    const pinkHit = srv.filter(s => s.pinkLike && d2(s, a) <= NEAR).sort((x, y) => d2(x, a) - d2(y, a))[0];
    if (pinkHit && pinkHit.hash && pinkHit.hash.length === 4) {
      const claimed = pinkClaimed.get(pinkHit.hash);
      if (!claimed || (claimed.name === a.name && claimed.md === a.md)) {
        if (!claimed) pinkClaimed.set(pinkHit.hash, { name: a.name, md: a.md });
        ensureTemplate(a, pinkHit.hash);
        pinkFills.push({ pf: a.pf, hash: pinkHit.hash, name: a.name });
        continue;
      }
    }
    dropped.nameMdCollision++;
    skipped.push({ name: a.name, pf: a.pf, why: 'name/md collision', x: a.x, z: a.z });
    continue;
  }
  if (typeof ru === 'string') { hash = ru; reuse = true; }
  else hash = ensureTemplate(a);

  (spawnsByPf[a.pf] = spawnsByPf[a.pf] || []).push({
    pf: a.pf, hash, name: a.name, reuse,
    lo: Math.min(...a.lvls), hi: Math.max(...a.lvls),
    x: +a.x.toFixed(3), y: +a.y.toFixed(3), z: +a.z.toFixed(3),
    angle: a.heading, wander: !!a.roam,
    posFrom: `${a.src}${a.calm ? ' calm/start' : ''}`
  });
}

// --- loot: v61 only (RK rows if any) + Reck confirmed ---
const lootByName = {};
const v61 = path.join(TODAY, 'loot_20261106_Whozewhatzit_v61.csv');
if (fs.existsSync(v61)) {
  const rows = parseCsv(fs.readFileSync(v61, 'utf8').replace(/^\uFEFF/, ''));
  const seen = new Set();
  for (const r of rows) {
    const pf = +r.PlayfieldID;
    // v61 in this pack is Adonis-only; keep RK corpses only (pf 1..999 + known RK interiors)
    if (!(pf > 0 && pf < 1000) && ![125, 1833, 1902].includes(pf)) continue;
    if (SKIP_LIVE.has(pf) || isSlPf(pf)) continue;
    if (r.SourceType && r.SourceType !== 'Corpse') continue;
    if (!r.ItemName && +r.Slot < 100) {
      const twin = rows.find(x =>
        x.Timestamp === r.Timestamp && x.CorpseInstance === r.CorpseInstance &&
        x.LowID === r.LowID && x.HighID === r.HighID && x.ItemName);
      if (twin) continue;
    }
    const uniq = `${r.CorpseInstance}|${r.LowID}|${r.HighID}|${r.QL}|${r.MobName}`;
    if (seen.has(uniq)) continue;
    seen.add(uniq);
    const name = (r.MobName || '').trim();
    const low = +r.LowID;
    if (!name || !(low > 0)) continue;
    const list = lootByName[name] || (lootByName[name] = []);
    if (!list.some(e => e.low === low && e.high === +r.HighID)) {
      list.push({ low, high: +r.HighID || low, chance: 100, ql: +r.QL || 0, name: r.ItemName || '' });
    }
  }
}
for (const [name, rows] of Object.entries(RECK_LOOT)) {
  const list = lootByName[name] || (lootByName[name] = []);
  for (const e of rows) {
    if (!list.some(x => x.low === e.low)) list.push(e);
  }
}
for (const name of SKAWT_TRASH) {
  const list = lootByName[name] || (lootByName[name] = []);
  if (!list.some(e => e.low === 303059)) {
    list.push({ low: 303059, high: 303059, chance: 100, name: "Skawt's Compact Plasma Emitter" });
  }
}

// ensure Reck boss templates exist even if only pink-filled / spawn-skipped
function ensureNamedBoss(name, pf) {
  const has = Object.values(templates).some(t => Object.values(t.bands).some(b => b.Name === name));
  if (has) return;
  const sample = instances.find(i => i.name === name && i.pf === pf) || pts.find(p => p.name === name && p.pf === pf);
  if (!sample) {
    heldBack.push({ name, pf, why: 'confirmed loot but no spawn sample to template' });
    return;
  }
  ensureTemplate(sample);
}
for (const n of ['Abandoned Hope', 'Warchief Skawt', 'Prime Evolution Huzzum', 'Prototype 73-Omega Ninjadroid', ...SKAWT_TRASH]) {
  ensureNamedBoss(n, 750);
}

const existingLoot = {};
for (const [name, rows] of Object.entries(lootByName)) {
  const entries = byName[name] || [];
  if (!entries.length) continue;
  const made = Object.values(templates).some(t => Object.values(t.bands).some(b => b.Name === name));
  if (made) continue;
  existingLoot[name] = { hashes: [...new Set(entries.map(e => e.h))], rows };
}

// per-zone already-present summary
const alreadyByPf = {};
for (const a of alreadyPresent) {
  alreadyByPf[a.pf] = (alreadyByPf[a.pf] || 0) + 1;
}

const plan = {
  source: TAG,
  generated: new Date().toISOString(),
  review: REV,
  today: TODAY,
  skipLive: [...SKIP_LIVE],
  templates: Object.values(templates).map(t => ({
    hash: t.hash, wander: t.wander, friendly: t.friendly, src: t.src,
    fillPink: !!t.fillPink, pf: t.pf,
    bands: Object.values(t.bands).sort((a, b) => a.Level - b.Level)
  })),
  spawnsByPf,
  pinkFills,
  loot: lootByName,
  existingLoot,
  collisions,
  skipped,
  heldBack,
  alreadyPresentSample: alreadyPresent.slice(0, 200),
  alreadyByPf,
  notes,
  dropped,
  pfName,
  counts: {
    sightingsIn: dropped.sightingsIn,
    instances: instances.length,
    afterCluster: pts.length,
    spawnPointsAdded: Object.values(spawnsByPf).reduce((n, a) => n + a.length, 0),
    spawnReuse: Object.values(spawnsByPf).flat().filter(s => s.reuse).length,
    newTemplates: Object.keys(templates).length,
    pinkFills: pinkFills.length,
    lootMobs: Object.keys(lootByName).length,
    collisions: collisions.length,
    heldBack: heldBack.length,
    alreadyPresent: alreadyPresent.length
  },
  perZone: Object.fromEntries(
    [...new Set([...Object.keys(spawnsByPf).map(Number), ...Object.keys(alreadyByPf).map(Number), ...pinkFills.map(p => p.pf)])]
      .sort((a, b) => a - b)
      .map(pf => [pf, {
        name: pfName[pf] || ('PF ' + pf),
        spawns: (spawnsByPf[pf] || []).length,
        pinkFills: pinkFills.filter(p => p.pf === pf).length,
        templates: Object.values(templates).filter(t => t.pf === pf).length,
        alreadyPresent: alreadyByPf[pf] || 0
      }])
  )
};

const out = path.join(__dirname, 'scrape_import_rk_20261106.json');
fs.writeFileSync(out, JSON.stringify(plan, null, 1) + '\n');
console.log(JSON.stringify(plan.counts, null, 2));
console.log('dropped', JSON.stringify(dropped, null, 2));
console.log('zones with spawns', Object.keys(spawnsByPf).length);
console.log('top zones', Object.entries(plan.perZone).sort((a, b) => b[1].spawns - a[1].spawns).slice(0, 15));
console.log('wrote', out);
