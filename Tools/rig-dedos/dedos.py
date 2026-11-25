"""Acha os dedos de uma mao (sem ossos) e calcula articulacoes + pesos."""
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from view import load
from zmap import zbuf
from scipy.ndimage import gaussian_filter, gaussian_filter1d
from scipy.signal import find_peaks
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import dijkstra

def weld(P, tol=1e-5):
    q = np.round(P / tol).astype(np.int64)
    _, inv = np.unique(q, axis=0, return_inverse=True)
    return inv.ravel()

def smoothstep(x):
    x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)

class Mao:
    def __init__(s, d, side, ov=None):
        s.d = d; s.side = side; s.ov = ov or {}
        names = d['names']; s.H = names.index(side + 'Hand'); fa = names.index(side + 'ForeArm')
        s.hp = d['bind'][s.H][:3, 3]; fp = d['bind'][fa][:3, 3]
        P = d['P']; s.mh = ((d['Jt'] == s.H) * d['Wt']).sum(1)
        s.sel = np.where(s.mh > 0.3)[0]; s.hv = np.where(s.mh > 0.001)[0]
        ax0 = s.hp - fp; ax0 /= np.linalg.norm(ax0)
        Q = P[s.sel] - s.hp; a0 = Q @ ax0
        far = Q[a0 > np.quantile(a0, 0.9)].mean(0); ax = far / np.linalg.norm(far)
        if 'ax' in s.ov: ax = np.array(s.ov['ax'], float); ax /= np.linalg.norm(ax)
        Qp = Q - np.outer(Q @ ax, ax)
        _, _, vt = np.linalg.svd(Qp - Qp.mean(0), full_matrices=False)
        wid = vt[0] - (vt[0] @ ax) * ax; wid /= np.linalg.norm(wid)
        s.ax, s.wid = ax, wid
        s.coords()
        s.L = s.a[s.sel].max()
        s.maps()
        ts = s.ov.get('thumb', s.thumb_side())
        if ts < 0:
            s.wid = -s.wid; s.coords(); s.maps()
        s.nrm = np.cross(s.ax, s.wid)
        s.palm = 1.0 if side == 'Left' else -1.0   # lado da palma em n
        if s.ov.get('flip_palm'): s.palm = -s.palm

    def coords(s):
        Q = s.d['P'] - s.hp
        s.a = Q @ s.ax; s.w = Q @ s.wid; s.n = Q @ np.cross(s.ax, s.wid)

    def maps(s):
        tm = np.isin(s.d['I'], s.sel).all(1); I = s.d['I'][tm]
        L = s.a[s.sel].max(); s.res = res = L / 200
        s.x0 = s.w[s.sel].min() - 3 * res; s.y0 = s.a[s.sel].min() - 3 * res
        s.Wd = int((s.w[s.sel].max() - s.x0) / res) + 4; s.Hd = int((s.a[s.sel].max() - s.y0) / res) + 4
        V2 = np.stack([s.w, s.a], 1)
        s.hi = zbuf(V2, s.n, I, s.x0, s.y0, res, s.Wd, s.Hd, 'max')
        s.lo = zbuf(V2, s.n, I, s.x0, s.y0, res, s.Wd, s.Hd, 'min')
        s.S = ~np.isnan(s.hi)
        s.Tz = gaussian_filter(np.where(s.S, s.hi - s.lo, 0.0), 0.8)

    def r_of(s, a): return (a - s.y0) / s.res
    def x_of(s, w): return (w - s.x0) / s.res
    def a_of(s, r): return s.y0 + r * s.res
    def w_of(s, x): return s.x0 + x * s.res

    def extents(s, r):
        xs = np.where(s.S[int(r)])[0]
        return (xs.min(), xs.max()) if len(xs) else (None, None)

    def thumb_side(s):
        mid = [s.extents(s.r_of(f * s.L)) for f in np.linspace(0.3, 0.6, 7)]
        top = [s.extents(s.r_of(f * s.L)) for f in np.linspace(0.8, 0.9, 4)]
        mid = [m for m in mid if m[0] is not None]; top = [t for t in top if t[0] is not None]
        ep = np.mean([m[1] for m in mid]) - np.mean([t[1] for t in top])
        em = np.mean([t[0] for t in top]) - np.mean([m[0] for m in mid])
        s.thumb_conf = (ep, em)
        return 1 if ep > em else -1

    # ---------- dedos ----------
    def tips(s):
        top = np.full(s.Wd, -1.0)
        for x in range(s.Wd):
            rs = np.where(s.S[:, x])[0]
            if len(rs): top[x] = rs.max()
        s.top = top
        ts = gaussian_filter1d(np.where(top < 0, 0, top), 1.0)
        pk, pr = find_peaks(ts, prominence=s.ov.get('prom', 3.0))
        pk = [p for p in pk if top[p] > s.r_of(0.6 * s.L)]
        if 'tips_x' in s.ov: pk = [int(s.x_of(v)) for v in s.ov['tips_x']]
        # mantem os 4 mais altos; se sobrar, tira o do lado do polegar (maior x) que for mais baixo
        while len(pk) > 4:
            hs = [ts[p] for p in pk]
            pk.pop(int(np.argmin(hs)))
        s.tip_x = sorted(pk)
        return s.tip_x

    def valley(s, xa, xb):
        x = xa + int(np.argmin(s.top[xa:xb + 1])); r = int(s.top[x]) - 1
        half = max(3, (xb - xa) // 2)
        path = []; miss = 0; web = r
        while r > 2:
            lo_, hi_ = max(x - 2, 0), min(x + 2, s.Wd - 1)
            row = s.Tz[r]
            x = lo_ + int(np.argmin(row[lo_:hi_ + 1]))
            lm = row[max(0, x - half):x + 1].max(); rm = row[x:min(s.Wd, x + half + 1)].max()
            dep = min(lm, rm) - row[x]
            sig = dep > 0.12 * min(lm, rm) and min(lm, rm) > 0
            if sig: miss = 0; web = r; path.append((r, x))
            else:
                miss += 1; path.append((r, x))
                if miss >= 4: break
            r -= 1
        path = [(rr, xx) for rr, xx in path if rr >= web]
        return np.array(path), web

    def fingers(s):
        tx = s.tips()
        s.gaps = []
        for i in range(len(tx) - 1):
            p, web = s.valley(tx[i], tx[i + 1]); s.gaps.append((p, web))
        F = []
        for k in range(len(tx)):
            webs = [s.gaps[g][1] for g in (k - 1, k) if 0 <= g < len(s.gaps)]
            web = max(webs) if k in (0, len(tx) - 1) else np.mean(webs)
            rtip = s.top[tx[k]]
            pts = []
            for r in range(int(max(webs)), int(rtip - 0.12 * (rtip - web))):
                L_ = s.bound(k - 1, r) if k > 0 else None
                R_ = s.bound(k, r) if k < len(tx) - 1 else None
                row = s.S[r]
                if L_ is None:
                    x = int(R_ if R_ is not None else tx[k]); 
                    while x > 0 and row[x - 1]: x -= 1
                    L_ = x
                if R_ is None:
                    x = int(L_) + 1
                    while x < s.Wd - 1 and row[x + 1]: x += 1
                    R_ = x
                pts.append((r, (L_ + R_) / 2, R_ - L_))
            pts = np.array(pts)
            c = np.polyfit(pts[:, 0], pts[:, 1], 1)
            F.append(dict(k=k, web=web, rtip=rtip, c=c, width=np.median(pts[:, 2]) * s.res, pts=pts))
        s.F = F
        return F

    def bound(s, g, r):
        p, web = s.gaps[g]
        rr = max(r, web)
        m = p[p[:, 0] == int(rr)]
        if len(m): return float(m[0, 1])
        return float(p[np.argmin(np.abs(p[:, 0] - rr)), 1])

    def ncenter(s, r, x):
        r = int(np.clip(round(r), 0, s.Hd - 1)); x = int(np.clip(round(x), 0, s.Wd - 1))
        for k in range(0, 6):
            win_hi = s.hi[max(0, r - k):r + k + 1, max(0, x - k):x + k + 1]
            win_lo = s.lo[max(0, r - k):r + k + 1, max(0, x - k):x + k + 1]
            if np.isfinite(win_hi).any(): return (np.nanmean(win_hi) + np.nanmean(win_lo)) / 2
        return 0.0

    def P3(s, r, x, n): return s.hp + s.ax * s.a_of(r) + s.wid * s.w_of(x) + s.nrm * n

    def finger_joints(s, fr=(0.0, 0.46, 0.75, 1.0), mcpk=1.22):
        J = []
        for f in s.F:
            rt, web = f['rtip'], f['web']
            rm = rt - mcpk * (rt - web)
            rows = [rm + t * (rt - rm) for t in fr]
            rows[-1] = rt - 0.5 * f['width'] / s.res * 0.6  # ponta: centro da polpa
            pts = []
            for r in rows:
                x = np.polyval(f['c'], r); pts.append(s.P3(r, x, s.ncenter(r, x)))
            pts.append(s.P3(rt, np.polyval(f['c'], rt), s.ncenter(rt - 1, np.polyval(f['c'], rt))))
            J.append(np.array(pts))  # MCP, PIP, DIP, (centro polpa), ponta
        s.FJ = J
        return J

def graph(d, verts):
    """grafo soldado (posicoes iguais viram um no) restrito a verts; devolve (G, mapa vert->no, posicoes dos nos)"""
    P = d['P']; wid_ = weld(P)
    keep = np.zeros(wid_.max() + 1, bool); keep[wid_[verts]] = True
    I = wid_[d['I']]
    e = np.concatenate([I[:, [0, 1]], I[:, [1, 2]], I[:, [2, 0]]])
    e = e[keep[e[:, 0]] & keep[e[:, 1]]]
    nodes = np.where(keep)[0]; idx = -np.ones(len(keep), int); idx[nodes] = np.arange(len(nodes))
    NP = np.zeros((len(nodes), 3)); NP[idx[wid_[verts]]] = P[verts]
    a, b = idx[e[:, 0]], idx[e[:, 1]]
    L = np.linalg.norm(NP[a] - NP[b], axis=1) + 1e-9
    G = coo_matrix((L, (a, b)), shape=(len(nodes), len(nodes))).tocsr()
    G = G.maximum(G.T)
    vmap = -np.ones(len(P), int); ok = keep[wid_]; vmap[ok] = idx[wid_[ok]]
    return G, vmap, NP

def mao_geo(h):
    from scipy.spatial import cKDTree
    G, vmap, NP = graph(h.d, h.hv)
    h.G, h.vmap, h.NP = G, vmap, NP
    Q = NP - h.hp; a = Q @ h.ax; w = Q @ h.wid; n = Q @ h.nrm
    h.na, h.nw, h.nn = a, w, n
    src = np.where(a < a.min() + 0.04 * h.L)[0]
    D = dijkstra(G, directed=False, indices=src, min_only=True)
    h.Dw = D
    tree = cKDTree(NP); nb = tree.query_ball_point(NP, 0.07 * h.L)
    ext = [i for i in range(len(NP)) if np.isfinite(D[i]) and D[i] >= max(D[j] for j in nb[i]) - 1e-9 and a[i] > 0.25 * h.L]
    h.ext = ext
    return ext

from scipy.sparse.csgraph import connected_components
def thumb_free(h, verbose=False):
    """parte livre do polegar: componente do lado do polegar que se separa quando cortamos a mao em a > c"""
    if not hasattr(h, 'G'):
        G, vmap, NP = graph(h.d, h.hv); h.G, h.vmap, h.NP = G, vmap, NP
        Q = NP - h.hp; h.na, h.nw, h.nn = Q @ h.ax, Q @ h.wid, Q @ h.nrm
    G = h.G.tocoo(); na = h.na
    best = None
    for f in np.arange(0.2, 0.8, 0.02):
        c = f * h.L
        keep = na > c
        m = keep[G.row] & keep[G.col]
        sub = coo_matrix((np.ones(m.sum()), (G.row[m], G.col[m])), shape=G.shape)
        nc, lab = connected_components(sub, directed=False)
        labs = np.unique(lab[keep])
        comps = []
        for l in labs:
            ids = np.where((lab == l) & keep)[0]
            if len(ids) < 8: continue
            comps.append((ids, na[ids].max(), h.nw[ids].mean()))
        if verbose: print('  cut %.2fL' % f, [(len(c_[0]), round(c_[1] / h.L, 2), round(c_[2], 3)) for c_ in comps])
        if len(comps) < 2: 
            if best is not None: break
            continue
        # o polegar: ponta mais baixa que os dedos e do lado +w
        cand = [c_ for c_ in comps if c_[1] < 0.85 * h.L and c_[2] > np.median(h.nw)]
        if cand:
            c_ = max(cand, key=lambda z: len(z[0]))
            if best is None: best = (f, c_)
            else:
                break
    return best
