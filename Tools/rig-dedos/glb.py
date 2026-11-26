import json, struct, numpy as np
CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
class GLB:
    def __init__(s, path):
        b = open(path, 'rb').read()
        magic, ver, length = struct.unpack('<III', b[:12])
        off = 12; s.json = None; s.bin = None
        while off < length:
            cl, ctype = struct.unpack('<II', b[off:off+8]); data = b[off+8:off+8+cl]
            if ctype == 0x4E4F534A: s.json = json.loads(data)
            elif ctype == 0x004E4942: s.bin = bytearray(data)
            off += 8 + cl
    def acc(s, i):
        a = s.json['accessors'][i]; bv = s.json['bufferViews'][a['bufferView']]
        dt = np.dtype(CT[a['componentType']]); n = NC[a['type']]
        start = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
        stride = bv.get('byteStride', 0)
        if stride and stride != dt.itemsize * n:
            raw = np.frombuffer(bytes(s.bin), dtype=np.uint8)
            out = np.empty((a['count'], n), dt)
            for k in range(a['count']):
                out[k] = np.frombuffer(bytes(s.bin[start+k*stride:start+k*stride+dt.itemsize*n]), dt)
            return out
        arr = np.frombuffer(bytes(s.bin), dtype=dt, count=a['count']*n, offset=start).reshape(a['count'], n)
        if a.get('normalized') and dt.kind in 'ui':
            arr = arr.astype(np.float32) / np.iinfo(dt).max
        return arr
def local_mat(n):
    if 'matrix' in n: return np.array(n['matrix'], float).reshape(4, 4).T
    t = np.array(n.get('translation', [0, 0, 0]), float); r = n.get('rotation', [0, 0, 0, 1]); sc = np.array(n.get('scale', [1, 1, 1]), float)
    x, y, z, w = r
    R = np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)], [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)], [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])
    M = np.eye(4); M[:3, :3] = R * sc; M[:3, 3] = t; return M
def world_mats(g):
    nodes = g.json['nodes']; parent = {}
    for i, n in enumerate(nodes):
        for c in n.get('children', []): parent[c] = i
    W = {}
    def get(i):
        if i in W: return W[i]
        M = local_mat(nodes[i]); 
        if i in parent: M = get(parent[i]) @ M
        W[i] = M; return M
    for i in range(len(nodes)): get(i)
    return W, parent
