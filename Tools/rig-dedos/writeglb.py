import sys, os, json, struct, hashlib, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from glb import GLB, world_mats
from dedos2 import *
from diag2 import analisa

def write_glb(J, BIN, path):
    js = json.dumps(J, separators=(',', ':'), ensure_ascii=False).encode('utf8')
    js += b' ' * ((4 - len(js) % 4) % 4)
    bn = bytes(BIN) + b'\0' * ((4 - len(BIN) % 4) % 4)
    total = 12 + 8 + len(js) + 8 + len(bn)
    out = struct.pack('<III', 0x46546C67, 2, total) + struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(bn), 0x004E4942) + bn
    open(path, 'wb').write(out); return out

def rig_model(name, src, dst, hands=None, log=print):
    d = load(src); g = d['g']; J = g.json; BIN = bytearray(g.bin)
    W, parent = world_mats(g)
    nodes = J['nodes']; skin = J['skins'][0]; joints = list(skin['joints'])
    pr = J['meshes'][0]['primitives'][0]
    Jt = d['Jt'].copy(); Wt = d['Wt'].copy()
    ibm_old = g.acc(skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
    new_ibm = list(ibm_old)
    info = {}
    for side in (hands or ['Left', 'Right']):
        h = analisa(d, side); rig_hand(h)
        hn = [i for i, n in enumerate(nodes) if n.get('name') == side + 'Hand'][0]
        Hj = joints.index(hn)
        name2node = {side + 'Hand': hn}; name2joint = {}
        for (bn, par, pos, isj) in h.bones:
            pw = W[name2node[par]]
            # mundo do osso novo: rotacao/escala da mao, posicao pos
            M = np.eye(4); M[:3, :3] = W[hn][:3, :3]; M[:3, 3] = pos
            local = np.linalg.inv(pw) @ M
            t = local[:3, 3]
            node = {'name': bn, 'translation': [float(x) for x in t]}
            ni = len(nodes); nodes.append(node); W[ni] = M
            nodes[name2node[par]].setdefault('children', []).append(ni)
            name2node[bn] = ni
            if isj:
                name2joint[bn] = len(joints); joints.append(ni); new_ibm.append(np.linalg.inv(M))
        # pesos: coluna -1 = mao
        V = h.hv; Wh = h.Wh
        cols = [name2joint.get(b[0]) for b in h.bones] + [Hj]
        for i, v in enumerate(V):
            infl = {}
            for k in range(4):
                if Wt[v, k] > 0: infl[int(Jt[v, k])] = infl.get(int(Jt[v, k]), 0) + float(Wt[v, k])
            wH = infl.pop(Hj, 0.0)
            if wH <= 0: continue
            for c, wv in enumerate(Wh[i]):
                if wv > 1e-4 and cols[c] is not None: infl[cols[c]] = infl.get(cols[c], 0) + wH * wv
            top = sorted(infl.items(), key=lambda kv: -kv[1])[:4]
            s = sum(w for _, w in top)
            Jt[v] = [j for j, _ in top] + [0] * (4 - len(top)); Wt[v] = [w / s for _, w in top] + [0.0] * (4 - len(top))
        info[side] = dict(F=[f['J'].tolist() for f in h.F], T=h.TJ.tolist())
        log(name, side, 'bones', len(h.bones), 'verts', len(V))
    assert max(joints.__len__(), 0) < 256
    # JOINTS_0 / WEIGHTS_0 no lugar
    for attr, arr, dt in [('JOINTS_0', Jt, np.uint8), ('WEIGHTS_0', Wt, np.float32)]:
        a = J['accessors'][pr['attributes'][attr]]; bv = J['bufferViews'][a['bufferView']]
        off = bv.get('byteOffset', 0) + a.get('byteOffset', 0); b = arr.astype(dt).tobytes()
        assert len(b) == bv['byteLength'], (attr, len(b), bv['byteLength'])
        BIN[off:off + len(b)] = b
    # IBM novo no fim do buffer
    while len(BIN) % 4: BIN.append(0)
    ib = np.array(new_ibm, np.float32).transpose(0, 2, 1).reshape(-1).tobytes()
    J['bufferViews'].append({'buffer': 0, 'byteOffset': len(BIN), 'byteLength': len(ib)})
    J['accessors'].append({'bufferView': len(J['bufferViews']) - 1, 'componentType': 5126, 'count': len(new_ibm), 'type': 'MAT4'})
    BIN += ib
    skin['joints'] = joints; skin['inverseBindMatrices'] = len(J['accessors']) - 1
    J['buffers'][0]['byteLength'] = len(BIN)
    J['asset']['extras'] = {'carteado': 'dedos v1'}
    out = write_glb(J, BIN, dst)
    return out, info

if __name__ == '__main__':
    os.makedirs(os.path.expanduser('~/cena/rig'), exist_ok=True)
    for nm in sys.argv[1:]:
        src = os.path.expanduser('~/cena/%s.glb' % nm); dst = os.path.expanduser('~/cena/rig/%s.glb' % nm)
        out, info = rig_model(nm, src, dst)
        json.dump(info, open(os.path.expanduser('~/cena/rig/%s.json' % nm), 'w'))
        print(nm, 'ok', len(out), hashlib.sha1(open(src, 'rb').read()).hexdigest()[:12], '->', hashlib.sha1(out).hexdigest()[:12])
