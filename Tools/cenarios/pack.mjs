// node pack.mjs saida.glb pacote.json
// Junta as peças de um cenário num pacote só, uma peça por nó: <Nome> (sem giro nem escala) com um filho
// <Nome>_ajuste que deixa a peça em metros, centrada e apoiada (frente para +Z). Lê as peças preparadas de ../out.
// Cada peça no pacote.json: name, parts [{src, name?, pos?, rotY?, scale?, nodes?}], e opcionalmente
//   fit ["x"|"y"|"z", metros]  escala uniforme até a medida no eixo; scale (número ou [x,y,z]); mul [x,y,z]
//   anchor "base" (chão, padrão) | "top" (teto) | "wall" (centro na parede) | "wallbase" (base na parede)
//   glow {material: força}  brilha sozinho (vira "__glowN" no nome do material); cut [materiais] recorte;
//   hide [materiais] some com esses materiais.
import { Document, NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { mergeDocuments, getBounds, prune, dedup } from '@gltf-transform/functions';
import fs from 'fs';
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
const cfg = JSON.parse(fs.readFileSync(process.argv[3] || 'pack.json', 'utf8'));
const doc = new Document(); doc.createBuffer();
const scene = doc.createScene('Cenario');
async function load(src) { return io.read(`../out/${src}.glb`); }

async function addPart(parent, part, prefix, glow, hide, cut) {
  const src = await load(part.src);
  const map = mergeDocuments(doc, src);
  const sScene = src.getRoot().listScenes()[0];
  const tScene = map.get(sScene);
  const node = doc.createNode(part.name || part.src);
  if (part.pos) node.setTranslation(part.pos);
  if (part.rotY) { const a = part.rotY * Math.PI / 360; node.setRotation([0, Math.sin(a), 0, Math.cos(a)]); }
  if (part.scale != null) node.setScale(Array.isArray(part.scale) ? part.scale : [part.scale, part.scale, part.scale]);
  for (const c of tScene.listChildren()) {
    tScene.removeChild(c);
    if (part.nodes && !part.nodes.includes(c.getName())) { c.dispose(); continue; }
    node.addChild(c);
  }
  tScene.dispose();
  parent.addChild(node);
  // Materiais: nome único por peça + as opções que o Unity lê no nome (PropLibrary).
  const mats = new Set();
  node.traverse(n => { const m = n.getMesh(); if (!m) return; for (const p of m.listPrimitives()) {
    const mat = p.getMaterial(); const orig = mat ? (mat.getExtras().orig || mat.getName()) : '';
    if (hide && hide.includes(orig)) { p.dispose(); continue; }
    if (mat && !mats.has(mat)) { mats.add(mat); mat.setExtras({ orig });
      let nm = `${prefix}_${orig || 'mat'}`;
      if (glow && glow[orig] != null) nm += `__glow${glow[orig]}`;
      if (cut && cut.includes(orig)) nm += '__cut';
      mat.setName(nm);
      if (mat.getAlphaMode() === 'BLEND') mat.setAlphaMode(cut && cut.includes(orig) ? 'MASK' : 'OPAQUE');
    } } });
  return node;
}

for (const p of cfg.props) {
  const wrap = doc.createNode(p.name); scene.addChild(wrap);
  const inner = doc.createNode(p.name + '_ajuste'); wrap.addChild(inner);
  for (const part of p.parts) await addPart(inner, part, p.name, p.glow, p.hide, p.cut);
  let b = getBounds(inner); const dims = [0, 1, 2].map(k => b.max[k] - b.min[k]);
  let s = [1, 1, 1];
  if (p.fit) { const k = 'xyz'.indexOf(p.fit[0]); const u = p.fit[1] / dims[k]; s = [u, u, u]; }
  if (p.scale) s = Array.isArray(p.scale) ? p.scale : [p.scale, p.scale, p.scale];
  if (p.mul) s = s.map((v, k) => v * p.mul[k]);
  inner.setScale(s);
  b = getBounds(inner);
  const c = [0, 1, 2].map(k => (b.min[k] + b.max[k]) / 2);
  const a = p.anchor || 'base';
  const t = [-c[0], a === 'top' ? -b.max[1] : (a === 'wall' ? -c[1] : -b.min[1]), (a === 'wall' || a === 'wallbase') ? -b.min[2] : -c[2]];
  inner.setTranslation(t);
  b = getBounds(wrap);
  console.log(p.name.padEnd(12), 'size', [0, 1, 2].map(k => (b.max[k] - b.min[k]).toFixed(3)).join(' x '), 'min', b.min.map(v => v.toFixed(3)).join(','));
}
for (const s of doc.getRoot().listScenes()) if (s !== scene) s.dispose();
// Um buffer só (o .glb sai num arquivo).
for (const acc of doc.getRoot().listAccessors()) acc.setBuffer(doc.getRoot().listBuffers()[0]);
for (const bf of doc.getRoot().listBuffers().slice(1)) bf.dispose();
await doc.transform(prune(), dedup());
doc.getRoot().setDefaultScene(scene);
await io.write(process.argv[2], doc);
console.log('ok', (fs.statSync(process.argv[2]).size / 1048576).toFixed(2), 'MB', doc.getRoot().listTextures().length, 'texturas');
