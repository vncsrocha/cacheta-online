import numpy as np
def zbuf(V2, Z, I, x0, y0, res, W, H, mode='max'):
    """V2: (N,2) coords, Z: (N,) depth; returns HxW map of max (or min) depth, nan where empty"""
    out = np.full((H, W), -np.inf if mode == 'max' else np.inf)
    px = (V2[:, 0] - x0) / res; py = (V2[:, 1] - y0) / res
    for t in I:
        xs = px[t]; ys = py[t]; zs = Z[t]
        xa, xb = int(np.floor(xs.min())), int(np.ceil(xs.max())); ya, yb = int(np.floor(ys.min())), int(np.ceil(ys.max()))
        xa = max(xa, 0); ya = max(ya, 0); xb = min(xb, W - 1); yb = min(yb, H - 1)
        if xa > xb or ya > yb: continue
        gx, gy = np.meshgrid(np.arange(xa, xb + 1) + 0.5, np.arange(ya, yb + 1) + 0.5)
        d = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
        if abs(d) < 1e-12: continue
        l0 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / d
        l1 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / d
        l2 = 1 - l0 - l1
        inside = (l0 >= -1e-6) & (l1 >= -1e-6) & (l2 >= -1e-6)
        if not inside.any(): continue
        z = l0 * zs[0] + l1 * zs[1] + l2 * zs[2]
        sub = out[ya:yb + 1, xa:xb + 1]
        if mode == 'max': np.maximum(sub, np.where(inside, z, -np.inf), out=sub)
        else: np.minimum(sub, np.where(inside, z, np.inf), out=sub)
    out[~np.isfinite(out)] = np.nan
    return out
