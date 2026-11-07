#!/usr/bin/env node
// Node port of Build-ScrapeImportRK.ps1 — same rules, fast JSON write.
const fs = require('fs');
const path = require('path');

const W = path.resolve(__dirname, '..', '..');
const GD = process.argv[2]
  ? path.resolve(process.argv[2])
  : path.join(W, 'AORebirth', 'GameData');
const TAG = 'scrape-rk-20261106';
const u = { encoding: 'utf8' };

function rd(p) {
  return JSON.parse(fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, ''));
}
function wr(p, obj) {
  fs.writeFileSync(p, JSON.stringify(obj, null, 4) + '\n', u);
}

const plan = rd(path.join(__dirname, 'scrape_import_rk_20261106.json'));
if (plan.source !== TAG) throw new Error('plan source ' + plan.source);

const nt = rd(path.join(GD, 'NpcTemplates.json'));
for (const f of fs.readdirSync(GD).filter((x) => /^NpcTemplates\..+\.json$/.test(x))) {
  if (f === 'NpcTemplates.RK.json') continue;
  Object.assign(nt, rd(path.join(GD, f)));
}

const pool = [];
for (const t of Object.values(nt)) {
  if (t.ContentProvenance && t.ContentProvenance.Guessed) continue;
  for (const b of t.Templates || []) {
    if (b && b.Stats && b.Stats['359']) pool.push(b);
  }
}
if (!pool.length) throw new Error('no donor pool');

function parseTextures(s) {
  if (!s) return [];
  const out = [];
  for (const tok of String(s).split(';')) {
    if (!tok) continue;
    const p = tok.split(':');
    if (p.length < 2) continue;
    const place = +p[0],
      id = +p[1];
    if (!(id > 0)) continue;
    out.push({ Id: id, Place: place });
  }
  return out;
}
function parseMeshes(s) {
  if (!s) return [];
  const out = [];
  for (const tok of String(s).split(';')) {
    if (!tok) continue;
    const p = tok.split(':');
    if (p.length < 4) continue;
    const pos = +p[0],
      id = +p[1],
      ovr = +p[2],
      layer = +p[3];
    if (!(id > 0)) continue;
    out.push({ Id: id, Position: pos, OverrideTextureId: ovr, Layer: layer });
  }
  return out;
}
function pinkLike(tp) {
  if (!tp || !tp.Templates) return true;
  const prov = tp.ContentProvenance || {};
  if (prov.Guessed || prov.StandIn) return true;
  const b = tp.Templates[0];
  const bp = (b && b.ContentProvenance) || {};
  return !!(bp.Guessed || bp.StandIn);
}
function bandHash(L) {
  if (L <= 15) return 'DYB1';
  if (L <= 35) return 'DYB2';
  if (L <= 55) return 'DYB3';
  if (L <= 85) return 'DYB4';
  if (L <= 120) return 'DYB5';
  if (L <= 160) return 'DYB6';
  if (L <= 200) return 'DYB7';
  return 'DYB8';
}
function deepClone(o) {
  return JSON.parse(JSON.stringify(o));
}

const itemHash = new Map();
const it = {};
const b36 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ';
let seq = 0;
const used = new Set(Object.keys(nt));
function newItemHash() {
  for (;;) {
    seq++;
    const h =
      'L' +
      b36[Math.floor(seq / 1296) % 36] +
      b36[Math.floor(seq / 36) % 36] +
      b36[seq % 36];
    if (!used.has(h) && !it[h]) {
      used.add(h);
      return h;
    }
  }
}
function itemRow(e) {
  const k = `${e.low}/${e.high}`;
  if (!itemHash.has(k)) {
    const h = newItemHash();
    itemHash.set(k, h);
    it[h] = { Templates: [+e.low, +e.high], Source: TAG };
  }
  return { Hash: itemHash.get(k), Repeats: 1, Chance: +e.chance, LevelMod: 0 };
}

const lootByName = {};
for (const [name, rows] of Object.entries(plan.loot || {})) {
  lootByName[name] = (rows || []).map(itemRow);
}

const ov = {};
let made = 0,
  lootRows = 0,
  wanderT = 0,
  pinkOverwrites = 0;

for (const t of plan.templates || []) {
  const existing = nt[t.hash];
  if (existing && existing.Templates && !t.fillPink) continue;
  if (t.fillPink && existing && existing.Templates && !pinkLike(existing)) continue;
  if (t.fillPink && existing && existing.Templates && pinkLike(existing)) pinkOverwrites++;

  const bands = [];
  for (const b of t.bands || []) {
    const L = +b.Level;
    let donor = pool[0];
    let best = Infinity;
    for (const d of pool) {
      const dist = Math.abs(+d.Level - L);
      if (dist < best) {
        best = dist;
        donor = d;
      }
    }
    const c = deepClone(donor);
    c.Name = b.Name;
    c.Level = L;
    c.Stats = c.Stats || {};
    const set = {
      54: L,
      33: +b.side,
      359: +b.md,
      360: +b.sc,
      455: +b.fam,
    };
    if (+b.hp > 0) {
      set[1] = +b.hp;
      set[27] = +b.hp;
    }
    if (+b.breed > 0) set[4] = +b.breed;
    if (+b.head > 0) set[64] = +b.head;
    if (+b.mesh > 0) set[12] = +b.mesh;
    if (+b.tex > 0) set[344] = +b.tex;
    const isMonster = +b.side === 3;
    set[0] = b.guard && !isMonster ? 277352961 : 268964353;
    Object.assign(c.Stats, Object.fromEntries(Object.entries(set).map(([k, v]) => [String(k), v])));

    let loot = [...(lootByName[b.Name] || [])];
    if (!loot.length && isMonster) {
      const nm = String(b.Name || '');
      const isNamedDyna =
        /\bthe\b/i.test(nm) &&
        !/^(The Ring|Ring Ticket)|Ticket Salesman/i.test(nm) &&
        L >= 1 &&
        L <= 275;
      if (isNamedDyna) {
        loot = [
          {
            Hash: bandHash(L),
            Repeats: 1,
            Chance: 100,
            LevelMod: 25,
            CountMin: 5,
            CountMax: 7,
          },
        ];
      } else {
        loot = [
          { Hash: 'CMTR', Repeats: 1, Chance: 50, LevelMod: 20 },
          { Hash: 'CMHK', Repeats: 1, Chance: 12, LevelMod: 20 },
          { Hash: 'OFAB', Repeats: 1, Chance: 1, LevelMod: 0 },
        ];
      }
    }
    lootRows += loot.length;
    c.LootTable = loot;
    delete c.Equipment;
    delete c.ContentProvenance;
    delete c.Textures;
    delete c.Meshes;
    const tex = parseTextures(b.textures);
    const msh = parseMeshes(b.meshes);
    if (tex.length) c.Textures = tex;
    if (msh.length) c.Meshes = msh;
    c.HasHeadMesh = +b.head > 0;
    let feat = (c.Features || []).filter(
      (x) => x && x !== 'NoCombat' && x !== 'NoMove' && x !== 'Wander'
    );
    if (!isMonster) feat = [...feat, 'NoCombat'];
    if (t.wander) feat = [...feat, 'Wander'];
    else if (!isMonster) feat = [...feat, 'NoMove'];
    c.Features = [...new Set(feat)];
    bands.push(c);
  }
  if (t.wander) wanderT++;
  let note = `live RK scrape (${t.src})`;
  if (t.fillPink) note += '; pink hash fill';
  if (t.wander) note += '; Wander';
  note += '; gloves omitted unless city v6.0 capture supplies them (none in this pack)';
  ov[t.hash] = {
    Templates: bands,
    ContentProvenance: { Guessed: false, Source: TAG, Note: note },
  };
  made++;
}

// existing loot appends
let exRows = 0;
for (const [name, spec] of Object.entries(plan.existingLoot || {})) {
  const rows = (spec.rows || []).map(itemRow);
  for (const h of spec.hashes || []) {
    const tp = nt[h];
    if (!tp) continue;
    let changed = false;
    const newBands = (tp.Templates || []).map((b) => {
      if (b.Name !== name) return b;
      const have = new Set((b.LootTable || []).map((e) => e.Hash));
      const add = rows.filter((r) => !have.has(r.Hash));
      if (!add.length) return b;
      changed = true;
      exRows += add.length;
      return { ...b, LootTable: [...(b.LootTable || []), ...add] };
    });
    if (changed) {
      ov[h] = {
        Templates: newBands,
        ContentProvenance: tp.ContentProvenance || {
          Guessed: false,
          Source: TAG,
          Note: 'loot append ' + TAG,
        },
      };
    }
  }
}

wr(path.join(GD, 'NpcTemplates.RK.json'), ov);
wr(path.join(GD, 'ItemTemplates.RK.json'), it);

let nsp = 0,
  reuse = 0;
for (const [pfName, pts] of Object.entries(plan.spawnsByPf || {})) {
  const pf = +pfName;
  const dir = path.join(GD, 'PlayfieldContent', String(pf));
  fs.mkdirSync(dir, { recursive: true });
  const spawns = (pts || []).map((s) => {
    if (s.reuse) reuse++;
    return {
      districtIndex: 0,
      hash: 0,
      hashText: s.hash,
      manifestHash: 0,
      minLevel: +s.lo,
      maxLevel: +s.hi,
      respawnChance: 100,
      flags: 1,
      respawnTime: 600,
      version7Extra: 0,
      hasOptionalFlagBlock: true,
      nativeFlags: 0,
      moreFlags: 0,
      assistanceRadius: 0,
      unknownOptionalU8: 0,
      angle: +s.angle,
      angleW: 1,
      position: [+s.x, +s.y, +s.z],
      radius: 1,
      additionalPoints: [],
      extensions: null,
    };
  });
  wr(path.join(dir, 'SpawnsAdd.rk.json'), {
    schemaVersion: 1,
    recordType: 1000014,
    playfieldId: pf,
    source: TAG,
    spawns,
  });
  nsp += spawns.length;
}

const summary = {
  source: TAG,
  templates: made,
  spawns: nsp,
  reuse,
  pinkFills: (plan.pinkFills || []).length,
  pinkOverwrites,
  lootItems: itemHash.size,
  lootRowsNew: lootRows,
  lootRowsExisting: exRows,
  wander: wanderT,
};
wr(path.join(__dirname, 'scrape_import_rk_20261106.build.json'), summary);
console.log(
  `templates=${made} spawns=${nsp} reuse=${reuse} pinkFills=${summary.pinkFills} pinkOverwrites=${pinkOverwrites} lootItems=${itemHash.size} lootRowsNew=${lootRows} lootRowsExisting=${exRows} wander=${wanderT}`
);
