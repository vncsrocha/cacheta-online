"""Troca a mao fundida de um personagem pela mao (com dedos e pesos) de outro, ligando no punho."""
import sys, os, json, struct, hashlib, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from glb import GLB, world_mats
from dedos2 import *
from dedos import weld
from writeglb import write_glb
from PIL import Image
import io

def frame_local(h, P):
    Q = P - h.hp; return np.stack([Q @ h.ax, Q @ h.wid, Q @ h.nrm], -1)

def welded_edges(I, wid_):
    Iw = wid_[I]
    e = np.sort(np.concatenate([Iw[:, [0, 1]], Iw[:, [1, 2]], Iw[:, [2, 0]]]), 1)
    u, c = np.unique(e, axis=0, return_counts=True)
    return u[c == 1]

def chain_loops(edges):
    adj = {}
    for a, b in edges: adj.setdefault(a, []).append(b); adj.setdefault(b, []).append(a)
    seen = set(); loops = []
    for s in adj:
        if s in seen: continue
        loop = [s]; seen.add(s); prev = None; cur = s
        while True:
            nx = [x for x in adj[cur] if x != prev and x not in seen]
            if not nx: break
            prev, cur = cur, nx[0]; loop.append(cur); seen.add(cur)
        loops.append(loop)
    return loops

def ring(d, keep_tris, h, a_cut, region):
    """laco de borda externo perto do corte (meias-arestas orientadas), vertices representantes"""
    wid_ = weld(d['P']); I = d['I'][keep_tris]; Iw = wid_[I]
    he = np.concatenate([Iw[:, [0, 1]], Iw[:, [1, 2]], Iw[:, [2, 0]]])
    key = {}
    for a, b in he: key[(a, b)] = key.get((a, b), 0) + 1
    bnd = [(a, b) for (a, b) in key if (b, a) not in key and a != b]
    rep = {}
    for v in I.ravel(): rep.setdefault(wid_[v], v)
    loc = frame_local(h, d['P'])
    ok = lambda nid: abs(loc[rep[nid], 0] - a_cut) < region and np.hypot(loc[rep[nid], 1], loc[rep[nid], 2]) < 0.07
    bnd = [(a, b) for a, b in bnd if ok(a) and ok(b)]
    nxt = {}
    for a, b in bnd: nxt.setdefault(a, []).append(b)
    used = set(); loops = []
    for a0, b0 in bnd:
        if (a0, b0) in used: continue
        loop = [a0]; used.add((a0, b0)); cur = b0
        while cur != a0 and cur in nxt:
            cand = [b for b in nxt[cur] if (cur, b) not in used]
            if not cand: break
            used.add((cur, cand[0])); loop.append(cur); cur = cand[0]
        if cur == a0 and len(loop) >= 5: loops.append(loop)
    def mrad(l):
        L = loc[[rep[n] for n in l]]; return np.hypot(L[:, 1] - L[:, 1].mean(), L[:, 2] - L[:, 2].mean()).mean()
    loops.sort(key=mrad, reverse=True)
    return [rep[n] for n in loops[0]], len(loops), [(len(l), round(mrad(l), 4)) for l in loops[:4]]

def skin_uv(A, h, ringv=None):
    g = A['g']; J = g.json
    img = J['images'][0]; bv = J['bufferViews'][img['bufferView']]
    im = Image.open(io.BytesIO(bytes(g.bin[bv.get('byteOffset', 0):bv.get('byteOffset', 0) + bv['byteLength']]))).convert('RGB')
    arr = np.asarray(im).astype(float); Hh, Ww = arr.shape[:2]
    pr = J['meshes'][0]['primitives'][0]; UV = g.acc(pr['attributes']['TEXCOORD_0']).astype(float)
    V = np.array(ringv) if ringv is not None else np.where(h.mh > 0.8)[0]
    px = np.clip((UV[V, 0] * Ww).astype(int), 0, Ww - 1); py = np.clip((UV[V, 1] * Hh).astype(int), 0, Hh - 1)
    cols = arr[py, px]; med = np.median(cols, 0)
    # vizinhanca uniforme
    best, bu = 1e9, None
    for i in np.argsort(np.linalg.norm(cols - med, axis=1))[:60]:
        x, y = px[i], py[i]; patch = arr[max(0, y - 6):y + 7, max(0, x - 6):x + 7].reshape(-1, 3)
        sc = np.linalg.norm(patch - med, axis=1).mean()
        if sc < best: best, bu = sc, UV[V[i]]
    return bu, med, best

def transplant(dst_name, donor_name, out, cut_f=0.04):
    A = load(os.path.expanduser('~/cena/%s.glb' % dst_name))
    Dorig = load(os.path.expanduser('~/cena/%s.glb' % donor_name))
    Dr = load(os.path.expanduser('~/cena/rig/%s.glb' % donor_name))
    dinfo = json.load(open(os.path.expanduser('~/cena/rig/%s.json' % donor_name)))
    g = A['g']; J = g.json; nodes = J['nodes']; skin = J['skins'][0]; joints = list(skin['joints'])
    Wn, parent = world_mats(g)
    pr = J['meshes'][0]['primitives'][0]
    attrs = {k: g.acc(v).copy() for k, v in pr['attributes'].items()}
    I = A['I'].copy(); keep = np.ones(len(I), bool)
    newP, newN, newUV, newJ, newW, newT, newI = [], [], [], [], [], [], []
    nV = len(attrs['POSITION'])
    ibms = list(g.acc(skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1))
    dJ = Dr['g'].json; dnames = [dJ['nodes'][j]['name'] for j in dJ['skins'][0]['joints']]
    report = {}
    for side in ['Left', 'Right']:
        ha = Mao(A, side); hd = Mao(Dorig, side)
        La, Ld = ha.L, hd.L
        a_cut_a, a_cut_d = cut_f * La, cut_f * Ld
        # 1) tira a mao do destino
        cen = frame_local(ha, A['P'][I].mean(1))
        allh = (ha.mh[I] > 0.01).all(1)
        rem = allh & (cen[:, 0] > a_cut_a)
        keep &= ~rem
        # 2) triangulos do doador
        dI = Dr['I']; dcen = frame_local(hd, Dorig['P'][dI].mean(1))
        fai = Dorig['names'].index(side + 'ForeArm'); hdi = Dorig['names'].index(side + 'Hand')
        armw = (((Dorig['Jt'] == fai) | (Dorig['Jt'] == hdi)) * Dorig['Wt']).sum(1)
        OV = 0.10
        dkeep = (armw[dI] > 0.6).all(1) & (dcen[:, 0] > a_cut_d - OV * Ld) & ((dcen[:, 0] > a_cut_d) | (np.hypot(dcen[:, 1], dcen[:, 2]) < 0.06))
        ringA, nla, la = ring(A, keep, ha, a_cut_a, 0.02)
        dvk = np.unique(dI[dkeep]); dl_ = frame_local(hd, Dorig['P'][dvk])
        sl = dvk[np.abs(dl_[:, 0] - a_cut_d) < 0.004]
        ringD, nld, ld = list(sl), 1, [len(sl)]
        # 3) mapeamento doador -> destino (no referencial da mao)
        locA = frame_local(ha, A['P'][ringA]); locD = frame_local(hd, Dorig['P'][ringD])
        cA, cD = locA.mean(0), locD.mean(0); cA[0] = a_cut_a; cD[0] = a_cut_d
        sA = locA.std(0); sD = locD.std(0)
        fa_a = np.linalg.norm(ha.hp - A['bind'][A['names'].index(side + 'ForeArm')][:3, 3])
        fa_d = np.linalg.norm(hd.hp - Dorig['bind'][Dorig['names'].index(side + 'ForeArm')][:3, 3])
        su = fa_a / fa_d
        sw, sn = np.clip(sA[1] / sD[1], 0.8 * su, 1.25 * su), np.clip(sA[2] / sD[2], 0.8 * su, 1.25 * su)
        def mapP(Pd):
            l = frame_local(hd, Pd)
            t = smoothstep((l[:, 0] - a_cut_d) / (0.3 * Ld))
            kw = sw + (su - sw) * t; kn = sn + (su - sn) * t
            a = cA[0] + (l[:, 0] - cD[0]) * su
            w = cA[1] + (l[:, 1] - cD[1]) * kw; n = cA[2] + (l[:, 2] - cD[2]) * kn
            return ha.hp + np.outer(a, ha.ax) + np.outer(w, ha.wid) + np.outer(n, ha.nrm)
        def mapN(Nd):
            l = np.stack([Nd @ hd.ax, Nd @ hd.wid, Nd @ hd.nrm], -1)
            v = np.outer(l[:, 0], ha.ax) + np.outer(l[:, 1], ha.wid) + np.outer(l[:, 2], ha.nrm)
            return v / np.linalg.norm(v, axis=1, keepdims=True)
        def radprof(locs, c, a0, a1, nb=24, step=0.002):
            th = np.arctan2(locs[:, 2] - c[2], locs[:, 1] - c[1]); r = np.hypot(locs[:, 2] - c[2], locs[:, 1] - c[1])
            levels = np.arange(a0, a1 + 1e-9, step); G = np.full((len(levels), nb), np.nan)
            for i, al in enumerate(levels):
                k = np.abs(locs[:, 0] - al) < step * 1.5
                b = ((th[k] + np.pi) / (2 * np.pi) * nb).astype(int) % nb
                for j in range(nb):
                    rr = r[k][b == j]
                    if len(rr): G[i, j] = rr.max()
            # preenche buracos por interpolacao circular, depois entre niveis
            for i in range(len(levels)):
                row = G[i]; ok = ~np.isnan(row)
                if ok.sum() >= 3:
                    xs = np.where(ok)[0]; G[i] = np.interp(np.arange(nb), np.r_[xs - nb, xs, xs + nb], np.r_[row[ok], row[ok], row[ok]])
            for j in range(nb):
                col = G[:, j]; ok = ~np.isnan(col)
                if ok.any(): G[:, j] = np.interp(np.arange(len(levels)), np.where(ok)[0], col[ok])
            def f(a, t):
                ia = np.clip((a - a0) / step, 0, len(levels) - 1); jt = (t + np.pi) / (2 * np.pi) * nb - 0.5
                i0 = np.floor(ia).astype(int); i1 = np.minimum(i0 + 1, len(levels) - 1); fa = ia - i0
                j0 = np.floor(jt).astype(int) % nb; j1 = (j0 + 1) % nb; fj = jt - np.floor(jt)
                g0 = G[i0, j0] * (1 - fj) + G[i0, j1] * fj; g1 = G[i1, j0] * (1 - fj) + G[i1, j1] * fj
                return g0 * (1 - fa) + g1 * fa
            return f
        # perfis dos lacos por angulo (no referencial do destino)
        def prof(locs, c):
            th = np.arctan2(locs[:, 2] - c[2], locs[:, 1] - c[1]); r = np.hypot(locs[:, 2] - c[2], locs[:, 1] - c[1])
            o = np.argsort(th); th, r, aa = th[o], r[o], locs[o, 0]
            th = np.r_[th - 2 * np.pi, th, th + 2 * np.pi]; r = np.r_[r, r, r]; aa = np.r_[aa, aa, aa]
            return lambda t: (np.interp(t, th, r), np.interp(t, th, aa))
        keepA_v = np.unique(I[keep]); armA = frame_local(ha, A['P'][keepA_v])
        armA = armA[(armA[:, 0] > a_cut_a - 0.2 * La) & (armA[:, 0] < a_cut_a + 0.01) & (np.hypot(armA[:, 1] - cA[1], armA[:, 2] - cA[2]) < 0.05)]
        rA = radprof(armA, cA, a_cut_a - OV * La - 0.01, a_cut_a)
        def snap(Pm, ld_a):
            l = frame_local(ha, Pm)
            # perfil do doador ja mapeado
            return l
        def snap_all(Pm):
            l = frame_local(ha, Pm)
            rD = radprof(l, cA, a_cut_a - OV * La - 0.01, a_cut_a + 0.3 * La)
            t = np.arctan2(l[:, 2] - cA[2], l[:, 1] - cA[1]); rr = np.hypot(l[:, 2] - cA[2], l[:, 1] - cA[1])
            a = l[:, 0]; a0 = a_cut_a - OV * La
            u = np.clip((a - a0) / (a_cut_a - a0), 0, 1)
            eps = -0.18 + 0.30 * u          # dentro do antebraco no fim, por fora na borda do corte
            tgt = rA(np.minimum(a, a_cut_a), t) * (1 + eps)
            k = np.clip(tgt / np.maximum(rD(a, t), 1e-4), 0.5, 2.0)
            f = 1 - smoothstep((a - a_cut_a) / (0.22 * La))   # depois do corte volta ao formato do doador
            sc = 1 + f * (k - 1)
            l2 = l.copy(); l2[:, 1] = cA[1] + (l[:, 1] - cA[1]) * sc; l2[:, 2] = cA[2] + (l[:, 2] - cA[2]) * sc
            return ha.hp + np.outer(l2[:, 0], ha.ax) + np.outer(l2[:, 1], ha.wid) + np.outer(l2[:, 2], ha.nrm)
        dv = np.unique(dI[dkeep]); remap = -np.ones(len(Dorig['P']), int); remap[dv] = nV + np.arange(len(dv))
        nV += len(dv)
        Pm = snap_all(mapP(Dorig['P'][dv])); newP.append(Pm)
        dN = Dr['g'].acc(dJ['meshes'][0]['primitives'][0]['attributes']['NORMAL']).astype(float)[dv]
        Nm = mapN(dN); newN.append(Nm)
        Tm = np.cross(Nm, ha.ax); Tm /= np.linalg.norm(Tm, axis=1, keepdims=True) + 1e-9; newT.append(np.c_[Tm, np.ones(len(dv))])
        # 4) ossos dos dedos (posicoes do doador mapeadas)
        hn = [i for i, n in enumerate(nodes) if n.get('name') == side + 'Hand'][0]
        name2node = {side + 'Hand': hn}; name2joint = {}
        FNs = ['Pinky', 'Ring', 'Middle', 'Index']
        chains = [(side + 'Hand' + FNs[k], np.array(dinfo[side]['F'][k])) for k in range(4)] + [(side + 'HandThumb', np.array(dinfo[side]['T']))]
        for base, pts in chains:
            mp = mapP(pts)
            for j in range(4):
                bn = base + str(j + 1); par = side + 'Hand' if j == 0 else base + str(j)
                M = np.eye(4); M[:3, :3] = Wn[hn][:3, :3]; M[:3, 3] = mp[j]
                local = np.linalg.inv(Wn[name2node[par]]) @ M
                ni = len(nodes); nodes.append({'name': bn, 'translation': [float(x) for x in local[:3, 3]]}); Wn[ni] = M
                nodes[name2node[par]].setdefault('children', []).append(ni); name2node[bn] = ni
                if j < 3: name2joint[bn] = len(joints); joints.append(ni); ibms.append(np.linalg.inv(M))
        # 5) pesos do doador -> juntas do destino pelo nome
        aJnames = [nodes[j]['name'] for j in joints]
        dJt = Dr['Jt'][dv]; dWt = Dr['Wt'][dv]
        Jm = np.zeros_like(dJt); Wm = dWt.copy()
        for k in range(4):
            Jm[:, k] = [aJnames.index(dnames[j]) for j in dJt[:, k]]
        newJ.append(Jm); newW.append(Wm)
        # 6) UV de pele
        if side == 'Left' or 'uv0' not in report: report['uv0'] = skin_uv(A, ha, ringA)
        uv, col, sc = report['uv0']; newUV.append(np.tile(uv, (len(dv), 1)))
        # 7) triangulos do doador + ponte
        tris = remap[dI[dkeep]]
        newI.append(tris)
        report[side] = dict(removed=int(rem.sum()), donor_tris=int(dkeep.sum()), donor_verts=len(dv), ringA=len(ringA), loopsA=la, loopsD=ld, sw=round(sw, 2), sn=round(sn, 2), skin=np.round(col).tolist(), skin_sc=round(sc, 1))
    # monta atributos
    attrs['POSITION'] = np.vstack([attrs['POSITION']] + newP).astype(np.float32)
    attrs['NORMAL'] = np.vstack([attrs['NORMAL']] + newN).astype(np.float32)
    attrs['TANGENT'] = np.vstack([attrs['TANGENT']] + newT).astype(np.float32)
    attrs['TEXCOORD_0'] = np.vstack([attrs['TEXCOORD_0']] + newUV).astype(np.float32)
    attrs['JOINTS_0'] = np.vstack([attrs['JOINTS_0']] + newJ).astype(np.uint8)
    attrs['WEIGHTS_0'] = np.vstack([attrs['WEIGHTS_0']] + newW).astype(np.float32)
    Iall = np.vstack([I[keep]] + newI).astype(np.uint16 if nV < 65536 else np.uint32)
    # reempacota o BIN
    order = {}
    for k, v in pr['attributes'].items(): order[J['accessors'][v]['bufferView']] = ('attr', k, v)
    order[J['accessors'][pr['indices']]['bufferView']] = ('idx', None, pr['indices'])
    newbin = bytearray()
    for bi, bv in enumerate(J['bufferViews']):
        while len(newbin) % 4: newbin.append(0)
        if bi in order:
            kind, k, ai = order[bi]
            data = (attrs[k] if kind == 'attr' else Iall.reshape(-1)).tobytes()
            acc = J['accessors'][ai]; acc['count'] = int(len(attrs[k]) if kind == 'attr' else Iall.size)
            if kind == 'idx': acc['componentType'] = 5123 if Iall.dtype == np.uint16 else 5125
            if k == 'POSITION': acc['min'] = attrs[k].min(0).tolist(); acc['max'] = attrs[k].max(0).tolist()
            acc.pop('byteOffset', None)
        else:
            o = bv.get('byteOffset', 0); data = bytes(g.bin[o:o + bv['byteLength']])
        bv['byteOffset'] = len(newbin); bv['byteLength'] = len(data); newbin += data
    while len(newbin) % 4: newbin.append(0)
    ib = np.array(ibms, np.float32).transpose(0, 2, 1).reshape(-1).tobytes()
    J['bufferViews'].append({'buffer': 0, 'byteOffset': len(newbin), 'byteLength': len(ib)})
    J['accessors'].append({'bufferView': len(J['bufferViews']) - 1, 'componentType': 5126, 'count': len(ibms), 'type': 'MAT4'})
    newbin += ib
    skin['joints'] = joints; skin['inverseBindMatrices'] = len(J['accessors']) - 1
    J['buffers'][0]['byteLength'] = len(newbin)
    J['asset']['extras'] = {'carteado': 'dedos v1, maos de %s' % donor_name}
    o = write_glb(J, newbin, out)
    report.pop('uv0', None)
    return o, report

if __name__ == '__main__':
    o, rep = transplant(sys.argv[1], sys.argv[2], os.path.expanduser('~/cena/rig/%s.glb' % sys.argv[1]))
    print(len(o), hashlib.sha1(o).hexdigest()[:12]); print(json.dumps(rep, indent=0))
