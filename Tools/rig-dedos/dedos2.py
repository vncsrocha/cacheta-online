import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dedos import Mao, graph, thumb_free, load, zbuf, smoothstep
from scipy.ndimage import gaussian_filter, gaussian_filter1d
from scipy.signal import find_peaks

def maps_excl(h, excl):
    tm = np.isin(h.d['I'], h.sel).all(1) & ~excl[h.d['I']].any(1); I = h.d['I'][tm]
    V2 = np.stack([h.w, h.a], 1)
    h.hi = zbuf(V2, h.n, I, h.x0, h.y0, h.res, h.Wd, h.Hd, 'max')
    h.lo = zbuf(V2, h.n, I, h.x0, h.y0, h.res, h.Wd, h.Hd, 'min')
    h.S = ~np.isnan(h.hi)
    h.Tz = gaussian_filter(np.where(h.S, h.hi - h.lo, 0.0), 0.8)

def row_minima(h, r, rel=0.12):
    row = h.Tz[r]; xs = np.where(h.S[r])[0]
    if len(xs) < 5: return []
    xa, xb = xs.min(), xs.max(); half = int(0.018 / h.res)
    out = []
    for x in range(xa + 2, xb - 1):
        if row[x] <= row[x - 1] and row[x] < row[x + 1] or (row[x] == 0 and row[x - 1] > 0):
            lm = row[max(xa, x - half):x + 1].max(); rm = row[x:min(xb, x + half) + 1].max()
            m = min(lm, rm)
            if m > 0 and (m - row[x]) > rel * m: out.append((x, (m - row[x]) / m))
    return out

def trace(h, x, r0, rel=0.10):
    """segue um vao a partir de (r0,x) para cima e para baixo"""
    half = int(0.018 / h.res)
    def sig(r, x):
        row = h.Tz[r]; lm = row[max(0, x - half):x + 1].max(); rm = row[x:min(h.Wd, x + half + 1)].max(); m = min(lm, rm)
        return m > 0 and (m - row[x]) > rel * m and h.S[r, max(0, x - half):x].any() and h.S[r, x + 1:x + half + 1].any()
    path = {}
    for dr in (1, -1):
        r, xx, miss = r0, x, 0
        while 1 < r < h.Hd - 2:
            lo_, hi_ = max(xx - 2, 0), min(xx + 2, h.Wd - 1)
            seg = h.Tz[r, lo_:hi_ + 1]
            xx = lo_ + int(np.argmin(seg))
            if sig(r, xx): miss = 0; path[r] = xx
            else:
                miss += 1
                if miss >= 3: break
            r += dr
    rs = sorted(path)
    return np.array([(r, path[r]) for r in rs])

def find_gaps(h, ngaps=3):
    rows = range(int(h.r_of(0.55 * h.L)), int(h.r_of(0.93 * h.L)))
    hist = np.zeros(h.Wd)
    for r in rows:
        for x, s in row_minima(h, r): hist[x] += s
    hs = gaussian_filter1d(hist, 1.5)
    pk, pr = find_peaks(hs, distance=int(0.012 / h.res))
    pk = sorted(pk, key=lambda p: -hs[p])[:ngaps]
    gaps = []
    for x in sorted(pk):
        # linha de partida: a de maior profundidade nesse x
        best, br = -1, None
        for r in rows:
            for xm, s in row_minima(h, r):
                if abs(xm - x) <= 2 and s > best: best, br, bx = s, r, xm
        p = trace(h, bx, br)
        gaps.append(p)
    h.gaps = gaps; h.ghist = hs
    return gaps

def bx(h, g, r):
    p = h.gaps[g]; rs = p[:, 0]
    if r <= rs.min(): return float(p[np.argmin(rs), 1])
    if r >= rs.max(): return float(p[np.argmax(rs), 1])
    i = np.argmin(np.abs(rs - r)); return float(p[i, 1])

def band_edges(h, k, r):
    r = int(np.clip(r, 0, h.Hd - 1)); row = h.S[r]; ng = len(h.gaps)
    L_ = bx(h, k - 1, r) if k > 0 else None
    R_ = bx(h, k, r) if k < ng else None
    if L_ is None:
        x = int(R_) - 1
        while x > 0 and row[x - 1]: x -= 1
        L_ = x
    if R_ is None:
        x = int(L_) + 1
        while x < h.Wd - 1 and row[x + 1]: x += 1
        R_ = x
    return L_, R_

def ncen(h, r, x, k=2):
    r = int(np.clip(round(r), 0, h.Hd - 1)); x = int(np.clip(round(x), 0, h.Wd - 1))
    for kk in range(k, k + 8):
        hi = h.hi[max(0, r - kk):r + kk + 1, max(0, x - kk):x + kk + 1]; lo = h.lo[max(0, r - kk):r + kk + 1, max(0, x - kk):x + kk + 1]
        if np.isfinite(hi).any(): return float((np.nanmean(hi) + np.nanmean(lo)) / 2)
    return 0.0

def P3(h, r, x, n): return h.hp + h.ax * h.a_of(r) + h.wid * h.w_of(x) + h.nrm * n

def fingers2(h, fr=(0.46, 0.75), mcpk=1.22):
    ng = len(h.gaps); F = []
    webs = [p[:, 0].min() for p in h.gaps]
    for k in range(ng + 1):
        # ponta: topo do contorno dentro da faixa
        rtop = int(h.r_of(0.97 * h.L))
        best = (-1, None)
        for r in range(h.Hd - 1, int(h.r_of(0.5 * h.L)), -1):
            L_, R_ = band_edges(h, k, r)
            xs = [x for x in range(int(np.ceil(L_)), int(R_) + 1) if h.S[r, x]]
            if xs and (k == 0 or xs[0] >= bx(h, k - 1, r) - 1) and (k == ng or xs[-1] <= bx(h, k, r) + 1):
                best = (r, float(np.mean(xs))); break
        rtip, xtip = best
        wk = [webs[g] for g in (k - 1, k) if 0 <= g < ng]
        web = min(wk)
        pts = []
        for r in range(int(min(wk)), int(rtip - 0.15 * (rtip - web))):
            L_, R_ = band_edges(h, k, r); pts.append((r, (L_ + R_) / 2, R_ - L_))
        pts = np.array(pts)
        c = np.polyfit(pts[:, 0], pts[:, 1], 1) if len(pts) > 3 else np.array([0, xtip])
        c[0] = np.clip(c[0], -0.25, 0.25)
        c[1] = np.mean(pts[:, 1] - c[0] * pts[:, 0]) if len(pts) > 3 else c[1]
        F.append(dict(k=k, rtip=rtip, xtip=xtip, web=web, c=c, wpx=np.median(pts[:, 2]) if len(pts) else 10))
    # MCP: abaixo da membrana; mantem a linha dos nos (mindinho mais baixo)
    for f in F:
        f['rmcp'] = f['rtip'] - mcpk * (f['rtip'] - f['web'])
    # indice e medio quase na mesma altura; anelar <= medio; mindinho <= anelar
    if len(F) == 4:
        F[1]['rmcp'] = min(F[1]['rmcp'], F[2]['rmcp'])
        F[0]['rmcp'] = min(F[0]['rmcp'], F[1]['rmcp'])
    for f in F:
        rows = [f['rmcp']] + [f['rmcp'] + t * (f['rtip'] - f['rmcp']) for t in fr]
        J = []
        for r in rows:
            x = np.polyval(f['c'], r); J.append(P3(h, r, x, ncen(h, r, x)))
        rt = f['rtip'] - 1; x = np.polyval(f['c'], rt); J.append(P3(h, rt, x, ncen(h, rt - 2, x)))
        f['J'] = np.array(J)   # MCP, PIP, DIP, ponta
        f['rad'] = 0.5 * f['wpx'] * h.res
    h.F = F
    return F

def thumb_joints(h, ids):
    vm = np.isin(h.vmap, ids)
    Pt = h.d['P'][vm]; C = Pt.mean(0)
    u, s, vt = np.linalg.svd(Pt - C, full_matrices=False); d = vt[0]
    # aponta para a ponta (lado de maior a)
    if (Pt @ d).argmax() != ((Pt - h.hp) @ h.ax).argmax() and d @ h.ax < 0: d = -d
    if d @ h.ax < 0 and d @ h.wid < 0: d = -d
    sp = (Pt - C) @ d; smax, smin = np.quantile(sp, 0.995), sp.min()
    rad = np.median(np.linalg.norm((Pt - C) - np.outer(sp, d), axis=1))
    lf = smax - smin
    tip = C + d * (smax - 0.6 * rad)
    mcp = C + d * (smin - 0.10 * lf)
    ip = C + d * (smax - 0.45 * (smax - (smin - 0.10 * lf)))
    # CMC: perto do punho, do lado do polegar
    ln = np.linalg.norm(tip - mcp)
    ext = mcp - d * 0.8 * ln
    halfw = np.nanmax(h.w[h.sel][(h.a[h.sel] > 0.25 * h.L) & (h.a[h.sel] < 0.35 * h.L)])
    A = h.hp + h.ax * 0.12 * h.L + h.wid * 0.45 * halfw
    cmc = 0.5 * ext + 0.5 * A
    # n do CMC: meio da palma ali
    Qc = cmc - h.hp; r, x = h.r_of(Qc @ h.ax), h.x_of(Qc @ h.wid)
    cmc = cmc - h.nrm * (Qc @ h.nrm) + h.nrm * ncen(h, r, x)
    h.TJ = np.array([cmc, mcp, ip, tip]); h.trad = rad; h.tmask = vm; h.tdir = d
    return h.TJ

FN = ['Pinky', 'Ring', 'Middle', 'Index']
def rig_hand(h, zones=(0.09, 0.05, 0.045)):
    """devolve lista de ossos (nome, pai, posicao) e matriz de pesos por vertice (so os da mao)"""
    side = h.side; bones = []
    pref = side + 'Hand'
    for k, f in enumerate(h.F):
        nm = pref + FN[k]
        for j in range(4): bones.append((nm + str(j + 1), (pref if j == 0 else nm + str(j)), f['J'][j], j < 3))
    for j in range(4): bones.append((pref + 'Thumb' + str(j + 1), (pref if j == 0 else pref + 'Thumb' + str(j)), h.TJ[j], j < 3))
    bidx = {b[0]: i for i, b in enumerate(bones)}
    P = h.d['P']; V = h.hv
    Wn = {}  # vert -> {bone or 'H': w}
    pdir = h.palm * h.nrm
    # polegar: parte livre por cadeia, eminencia tenar por proximidade
    cmc, mcp, ip, tip = h.TJ
    def chain_w(p, J, z):
        """pesos ao longo de uma cadeia J=[j0,j1,j2,tip]: osso antes de j0 = 'pai'"""
        u = J[-1] - J[0]; Ln = np.linalg.norm(u); u /= Ln
        t = (p - J[0]) @ u / Ln
        tj = [0.0] + [((J[i] - J[0]) @ u) / Ln for i in (1, 2)]
        s = [smoothstep((t - (tj[i] - z[i])) / (2 * z[i])) for i in range(3)]
        return np.stack([1 - s[0], s[0] - s[1], s[1] - s[2], s[2]], -1), t
    out = {}
    tm = h.tmask[V]
    # polegar (cadeia CMC->MCP->IP->ponta): pai = mao
    Jt = np.array([cmc, mcp, ip, tip])
    wt, tt = chain_w(P[V], Jt, (0.08, 0.07, 0.06))
    # distancia ao metacarpo do polegar
    seg = mcp - cmc; sl = np.linalg.norm(seg); su = seg / sl
    q = P[V] - cmc; ts = np.clip(q @ su / sl, 0, 1)
    dist = np.linalg.norm(q - np.outer(ts * sl, su), axis=1)
    near = 1 - smoothstep((dist - 0.9 * h.trad) / (1.3 * h.trad))
    sT = np.where(tm, 1.0, near * smoothstep(ts / 0.6))
    # so do lado do polegar a partir do centro do indicador
    fi = h.F[-1]; xc = np.polyval(fi['c'], h.r_of(h.a[V])); wc = h.w_of(xc)
    sT = np.where(tm, 1.0, sT * np.clip((h.w[V] - wc) / max(fi['rad'], 1e-3), 0, 1))
    # dedos: faixa no mapa 2D
    k_of = k_of_verts(h, V)
    W = np.zeros((len(V), len(bones) + 1))  # ultima coluna = mao
    H = len(bones)
    # polegar
    for j, col in enumerate([H, bidx[pref + 'Thumb1'], bidx[pref + 'Thumb2'], bidx[pref + 'Thumb3']]):
        if j == 0:
            # parte livre: pesos da cadeia; tenar: sT vai para Thumb1
            W[:, H] += np.where(tm, wt[:, 0], 0)
        else:
            W[:, col] += np.where(tm, wt[:, j], 0)
    W[:, bidx[pref + 'Thumb1']] += np.where(tm, 0, sT)
    rest = np.where(tm, 0, 1 - sT)
    for k, f in enumerate(h.F):
        m = (k_of == k) & ~tm
        if not m.any(): continue
        wf, tf = chain_w(P[V][m], f['J'], zones)
        cols = [H] + [bidx[pref + FN[k] + str(j)] for j in (1, 2, 3)]
        for j, col in enumerate(cols): W[m, col] += rest[m] * wf[:, j]
    h.bones = bones; h.Wh = W; h.k_of = k_of; h.sT = sT
    return bones, W

def runs(row):
    xs = np.where(row)[0]
    if not len(xs): return []
    br = np.where(np.diff(xs) > 1)[0]
    st = np.r_[xs[0], xs[br + 1]]; en = np.r_[xs[br], xs[-1]]
    return list(zip(st, en))

def label_map(h):
    ng = len(h.gaps); nf = ng + 1
    lab = -np.ones((h.Hd, h.Wd), int)
    webs = [p[:, 0].min() for p in h.gaps]; tops = [p[:, 0].max() for p in h.gaps]
    r_lo = int(min(webs) - 0.25 * (min(tops) - min(webs)))
    r_st = int(min(tops))
    def gap_at(g, r):
        p = h.gaps[g]; m = p[p[:, 0] == r]
        return float(m[0, 1]) if len(m) else None
    for r in range(max(0, r_lo), r_st + 1):
        cuts = [bx(h, g, r) for g in range(ng)]
        for x in np.where(h.S[r])[0]: lab[r, x] = int(sum(x > c for c in cuts))
    prev = {k: (np.where(lab[r_st] == k)[0].min(), np.where(lab[r_st] == k)[0].max()) for k in range(nf) if (lab[r_st] == k).any()}
    alive = dict(prev); h.tipinfo = {}
    for r in range(r_st + 1, h.Hd):
        segs = []
        for s0, s1 in runs(h.S[r]):
            cs = [s0]
            for g in range(ng):
                gx = gap_at(g, r)
                if gx is not None and s0 < gx < s1: cs.append(int(round(gx)))
            cs.append(s1 + 1); cs = sorted(set(cs))
            for i in range(len(cs) - 1): segs.append((cs[i], cs[i + 1] - 1))
        claim = {}
        for si, (a0, a1) in enumerate(segs):
            best, bk = 0, None
            for k, (b0, b1) in alive.items():
                ov = min(a1, b1) - max(a0, b0) + 1
                if ov > best: best, bk = ov, k
            if bk is not None: claim.setdefault(bk, []).append(si)
        new = {}
        for k in list(alive):
            if k in claim:
                a0 = min(segs[i][0] for i in claim[k]); a1 = max(segs[i][1] for i in claim[k])
                new[k] = (a0, a1); lab[r, a0:a1 + 1] = np.where(h.S[r, a0:a1 + 1], k, -1)
            else:
                b0, b1 = alive[k]; h.tipinfo[k] = (r - 1, (b0 + b1) / 2)
        alive = new
        if not alive: break
    for k, (b0, b1) in alive.items(): h.tipinfo[k] = (h.Hd - 1, (b0 + b1) / 2)
    h.lab = lab; h.r_lo = r_lo
    return lab

def fingers3(h, fr=(0.46, 0.75), mcpk=1.22):
    label_map(h)
    ng = len(h.gaps); webs = [p[:, 0].min() for p in h.gaps]; F = []
    for k in range(ng + 1):
        rtip, xtip = h.tipinfo[k]
        wk = [webs[g] for g in (k - 1, k) if 0 <= g < ng]; web = min(wk)
        pts = []
        for r in range(int(min(wk)), int(rtip - 0.15 * (rtip - web))):
            xs = np.where(h.lab[r] == k)[0]
            if len(xs) > 1: pts.append((r, (xs.min() + xs.max()) / 2, xs.max() - xs.min()))
        pts = np.array(pts)
        if len(pts) > 3:
            c = np.polyfit(pts[:, 0], pts[:, 1], 1); c[0] = np.clip(c[0], -0.3, 0.3); c[1] = np.mean(pts[:, 1] - c[0] * pts[:, 0])
        else: c = np.array([0.0, xtip])
        F.append(dict(k=k, rtip=rtip, xtip=xtip, web=web, c=c, wpx=np.median(pts[:, 2]) if len(pts) else 10))
    for f in F: f['rmcp'] = f['rtip'] - mcpk * (f['rtip'] - f['web'])
    if len(F) == 4:
        F[3]['rmcp'] = min(F[3]['rmcp'], F[2]['rmcp'] + 0.1 * (F[2]['rtip'] - F[2]['rmcp']))
        F[1]['rmcp'] = min(F[1]['rmcp'], F[2]['rmcp']); F[0]['rmcp'] = min(F[0]['rmcp'], F[1]['rmcp'])
    for f in F:
        rows = [f['rmcp']] + [f['rmcp'] + t * (f['rtip'] - f['rmcp']) for t in fr]
        J = []
        for r in rows:
            x = np.polyval(f['c'], r); J.append(P3(h, r, x, ncen(h, r, x)))
        rt = f['rtip'] - 1; x = np.polyval(f['c'], rt); J.append(P3(h, rt, x, ncen(h, rt - 2, x)))
        f['J'] = np.array(J); f['rad'] = 0.5 * f['wpx'] * h.res
    h.F = F
    return F

def k_of_verts(h, V):
    rr = np.clip(np.round(h.r_of(h.a[V])).astype(int), 0, h.Hd - 1); xx = np.clip(np.round(h.x_of(h.w[V])).astype(int), 0, h.Wd - 1)
    k = np.zeros(len(V), int)
    for g in range(len(h.gaps)):
        bxs = np.array([bx(h, g, r) for r in rr]); k += (xx > bxs).astype(int)
    for i in range(len(V)):
        r, x = rr[i], xx[i]
        if r < h.r_lo: continue
        row = h.lab[r]; xs = np.where(row >= 0)[0]
        if len(xs) == 0: continue
        j = xs[np.argmin(np.abs(xs - x))]
        if abs(j - x) <= 6: k[i] = row[j]
    return k
