// node prep.mjs entrada.gltf|glb saida.glb [maxTriangulos] [tamanhoTextura]
// Deixa uma peça leve para o jogo: tira normal map / rugosidade (o shader do jogo é chapado), simplifica a malha até
// maxTriangulos e recomprime as texturas em JPEG do tamanho pedido. Mostra o tamanho final da peça em metros.
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { weld, simplify, prune, dedup, textureCompress, getBounds } from '@gltf-transform/functions';
import { MeshoptSimplifier } from 'meshoptimizer';
import sharp from 'sharp';
const [,, inp, out, maxTrisS, texS] = process.argv;
const maxTris = +(maxTrisS || 12000), tex = +(texS || 512);
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc = await io.read(inp);
const root = doc.getRoot();
for (const m of root.listMaterials()) {
  m.setNormalTexture(null); m.setOcclusionTexture(null); m.setMetallicRoughnessTexture(null);
  m.setMetallicFactor(0); m.setRoughnessFactor(1);
  for (const e of m.listExtensions()) m.setExtension(e.extensionName, null);
}
const tris = () => root.listMeshes().reduce((s, me) => s + me.listPrimitives().reduce((a, p) => a + (p.getIndices() ? p.getIndices().getCount() : p.getAttribute('POSITION').getCount()) / 3, 0), 0);
const before = tris();
await doc.transform(prune(), dedup(), weld());
await MeshoptSimplifier.ready;
const t1 = tris();
if (t1 > maxTris) await doc.transform(simplify({ simplifier: MeshoptSimplifier, ratio: maxTris / t1, error: 0.002, lockBorder: false }));
await doc.transform(prune(), textureCompress({ encoder: sharp, targetFormat: 'jpeg', resize: [tex, tex], quality: 85 }));
const b = getBounds(root.listScenes()[0]);
await io.write(out, doc);
const size = b.max.map((v, i) => +(v - b.min[i]).toFixed(3));
console.log(JSON.stringify({ out, before, after: Math.round(tris()), size, min: b.min.map(v => +v.toFixed(3)),
  nodes: root.listNodes().map(n => n.getName()).slice(0, 12), mats: root.listMaterials().map(m => m.getName() + ':' + m.getAlphaMode()) }));
