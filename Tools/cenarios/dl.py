# Baixa modelos grátis (CC0) do Poly Haven em glTF 1k: python3 dl.py <id> [<id> ...]  ->  ph/<id>/<id>_1k.gltf
import json, os, sys, subprocess
ids = sys.argv[1:]
for i in ids:
    d = f'ph/{i}'
    os.makedirs(d, exist_ok=True)
    j = json.loads(subprocess.check_output(['curl', '-s', f'https://api.polyhaven.com/files/{i}']))
    g = j['gltf']['1k']['gltf']
    files = [(os.path.basename(g['url']), g['url'])] + [(p, v['url']) for p, v in g.get('include', {}).items()]
    for p, u in files:
        out = os.path.join(d, p)
        os.makedirs(os.path.dirname(out), exist_ok=True)
        if not os.path.exists(out):
            subprocess.run(['curl', '-s', '-o', out, u])
    print(i, len(files), sum(os.path.getsize(os.path.join(d, p)) for p, _ in files) // 1024, 'KB')
