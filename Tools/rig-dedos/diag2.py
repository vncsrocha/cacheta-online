import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dedos2 import *
from view import render
from PIL import Image, ImageDraw
def analisa(d, side):
    h = Mao(d, side); b = thumb_free(h); f, (ids, amax, wm) = b
    tmask = np.isin(h.vmap, ids); maps_excl(h, tmask); find_gaps(h); fingers3(h); thumb_joints(h, ids)
    return h
def draw3d(h, R, c, scale, size):
    d = h.d; tm = np.isin(d['I'], h.sel).all(1)
    im = render(d, tm, c, R, scale, size, out='/tmp/x.png'); dr = ImageDraw.Draw(im)
    def pr(p):
        q = R @ (p - c); return (size / 2 + q[0] * scale, size / 2 - q[1] * scale)
    cols = [(255, 0, 0), (0, 160, 0), (0, 0, 255), (200, 0, 200), (255, 140, 0)]
    chains = [np.vstack([h.F[k]['J']]) for k in range(len(h.F))] + [h.TJ]
    for k, ch in enumerate(chains):
        pts = [pr(p) for p in ch]
        dr.line(pts, fill=cols[k % 5], width=2)
        for p in pts: dr.ellipse([p[0] - 3, p[1] - 3, p[0] + 3, p[1] + 3], fill=cols[k % 5])
    p = pr(h.hp); dr.ellipse([p[0] - 4, p[1] - 4, p[0] + 4, p[1] + 4], outline=(0, 0, 0))
    return im
if __name__ == '__main__':
    tiles = []
    for nm in sys.argv[1:]:
        d = load(os.path.expanduser('~/cena/%s.glb' % nm))
        for side in ['Left', 'Right']:
            h = analisa(d, side); c = h.hp + h.ax * 0.5 * h.L; sc = 260 / (1.05 * h.L)
            for R in [np.array([h.wid, h.ax, h.nrm]) * [[1], [1], [1]], np.array([-h.wid, h.ax, -h.nrm]), np.array([h.nrm, h.ax, -h.wid])]:
                im = draw3d(h, R, c, sc, 260); ImageDraw.Draw(im).text((3, 3), nm[:9] + ' ' + side[0], fill=(0, 0, 0)); tiles.append(im)
    cols = 6; W = Image.new('RGB', (260 * cols, 260 * ((len(tiles) + cols - 1) // cols)), (255, 255, 255))
    for i, t in enumerate(tiles): W.paste(t, ((i % cols) * 260, (i // cols) * 260))
    W.save(os.path.expanduser('~/rig/diag2.jpg'), quality=72); print(W.size)
