import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from glb import *
from PIL import Image, ImageDraw
def load(path):
    g = GLB(path); J = g.json; sk = J['skins'][0]; joints = sk['joints']
    names = [J['nodes'][j]['name'] for j in joints]
    ibm = g.acc(sk['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
    pr = J['meshes'][0]['primitives'][0]
    P = g.acc(pr['attributes']['POSITION']).astype(float); Jt = g.acc(pr['attributes']['JOINTS_0']).astype(int); Wt = g.acc(pr['attributes']['WEIGHTS_0']).astype(float)
    I = g.acc(pr['indices']).reshape(-1, 3).astype(int)
    return dict(g=g, names=names, bind=np.linalg.inv(ibm), P=P, Jt=Jt, Wt=Wt, I=I)
def hand_frame(d, side):
    names = d['names']; h = names.index(side + 'Hand'); fa = names.index(side + 'ForeArm')
    hp = d['bind'][h][:3, 3]; fp = d['bind'][fa][:3, 3]
    m = ((d['Jt'] == h) * d['Wt']).sum(1); sel = np.where(m > 0.3)[0]
    ax = hp - fp; ax /= np.linalg.norm(ax)
    Q = d['P'][sel] - hp
    Qp = Q - np.outer(Q @ ax, ax)
    u, s, vt = np.linalg.svd(Qp - Qp.mean(0), full_matrices=False)
    wid = vt[0] - (vt[0] @ ax) * ax; wid /= np.linalg.norm(wid)
    nrm = np.cross(ax, wid)
    return hp, ax, wid, nrm, sel, m
def render(d, tri_mask, origin, R, scale, size, colors=None, out='o.png'):
    P = (d['P'] - origin) @ R.T  # R rows = screen x, screen y(up), depth(toward viewer)
    I = d['I'][tri_mask]
    img = Image.new('RGB', (size, size), (235, 235, 230)); dr = ImageDraw.Draw(img)
    v = P[I]; n = np.cross(v[:, 1] - v[:, 0], v[:, 2] - v[:, 0]); nl = np.linalg.norm(n, axis=1) + 1e-12; n /= nl[:, None]
    depth = v[:, :, 2].mean(1); order = np.argsort(depth)
    L = np.array([0.3, 0.5, 0.8]); L /= np.linalg.norm(L)
    for t in order:
        if n[t, 2] < 0: continue
        sh = 0.35 + 0.65 * max(0, n[t] @ L)
        base = colors[t] if colors is not None else (220, 180, 150)
        c = tuple(int(x * sh) for x in base)
        pts = [(size / 2 + p[0] * scale, size / 2 - p[1] * scale) for p in v[t]]
        dr.polygon(pts, fill=c)
    img.save(out); return img
