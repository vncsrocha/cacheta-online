# Gera as texturas do Bar do Tio Beto em tex/ (depois vão para UnityProject/Assets/Resources/Texturas/Bar).
# Precisa de numpy e Pillow. As artes feitas com IA ficam em ia/ (6_ladrilho.png, 7_precos.png, ...);
# azulejo, parede e forro são desenhados aqui mesmo.
import os
import numpy as np
from PIL import Image, ImageOps
rng = np.random.default_rng(7)
os.makedirs('tex', exist_ok=True)
def save(im, name, q=86):
    im.convert('RGB').save(f'tex/{name}.jpg', quality=q, optimize=True)

# 1. Ladrilho hidráulico: o desenho de uma placa, espelhado 2x2 (a roseta fecha no meio); 1 placa = 20 cm.
m = Image.open('ia/6_ladrilho.png').convert('RGB').resize((512, 512), Image.LANCZOS)
t = Image.new('RGB', (1024, 1024))
t.paste(m, (0, 0)); t.paste(ImageOps.mirror(m), (512, 0)); t.paste(ImageOps.flip(m), (0, 512)); t.paste(m.rotate(180), (512, 512))
a = np.asarray(t).astype(np.float32)
for k in (0, 511, 512, 1023):  # rejunte fino entre as placas
    a[:, k] *= 0.72; a[k, :] *= 0.72
save(Image.fromarray(a.clip(0, 255).astype(np.uint8)), 'ladrilho')

def smooth_noise(n, scale, seed):
    """Ruído suave e periódico (a textura repete sem emenda)."""
    r = np.random.default_rng(seed).standard_normal((n, n))
    f = np.fft.fft2(r); ky = np.fft.fftfreq(n)[:, None]; kx = np.fft.fftfreq(n)[None, :]
    k = np.sqrt(kx**2 + ky**2); f *= np.exp(-(k * scale)**2)
    out = np.real(np.fft.ifft2(f)); out -= out.min(); out /= out.max(); return out

# 2. Azulejo 15x15 verde-água, 4x4 placas por textura (60 cm), cada placa num tom um pouco diferente.
N = 512; tile = N // 4; g = 5
base = np.array([0.50, 0.77, 0.62]) * 255
img = np.zeros((N, N, 3), np.float32)
nz = smooth_noise(N, 40, 3)
for ty in range(4):
    for tx in range(4):
        v = rng.uniform(-0.05, 0.05); c = base * (1 + v) + rng.uniform(-6, 6, 3)
        y0, x0 = ty * tile, tx * tile
        yy, xx = np.mgrid[0:tile, 0:tile]
        edge = np.minimum.reduce([xx, yy, tile - 1 - xx, tile - 1 - yy]).astype(np.float32)
        bevel = np.clip(edge / 8.0, 0, 1)
        shade = 0.9 + 0.1 * bevel + 0.06 * (1 - (xx + yy) / (2 * tile))
        img[y0:y0 + tile, x0:x0 + tile] = c * shade[..., None]
img *= (0.96 + 0.08 * nz)[..., None]
grout = np.array([228, 226, 214], np.float32)
for k in range(4):
    s = k * tile
    img[:, max(0, s - g // 2):s + g - g // 2] = grout; img[max(0, s - g // 2):s + g - g // 2, :] = grout
img[:, -2:] = grout; img[-2:, :] = grout
save(Image.fromarray(img.clip(0, 255).astype(np.uint8)), 'azulejo')

# 3. Parede pintada de creme, com manchas suaves; 1 textura = 2 m.
N = 512
n1 = smooth_noise(N, 12, 11); n2 = smooth_noise(N, 60, 12); n3 = smooth_noise(N, 160, 13)
c = np.array([246, 226, 170], np.float32)
v = 1 - 0.05 * n1 - 0.05 * n2 + 0.025 * n3 + rng.normal(0, 0.012, (N, N))
img = c[None, None, :] * v[..., None]
img[..., 2] -= 10 * n1
save(Image.fromarray(img.clip(0, 255).astype(np.uint8)), 'parede')

# 4. Forro de PVC: réguas de 10 cm; 1 textura = 1 m.
N = 256; img = np.full((N, N, 3), 238, np.float32); img[..., 2] = 230
for k in range(10):
    s = int(k * N / 10); img[:, s:s + 2] = [198, 196, 188]; img[:, s + 2:s + 3] = [250, 250, 246]
img *= (0.98 + 0.03 * smooth_noise(N, 30, 5))[..., None]
save(Image.fromarray(img.clip(0, 255).astype(np.uint8)), 'forro')

# 5. Quadros, tela da TV e lousa do cavalete (artes feitas com IA, recortadas no formato de cada uma).
def fit(src, name, w, h, q=86):
    im = Image.open(src).convert('RGB'); im = ImageOps.fit(im, (w, h), Image.LANCZOS); save(im, name, q)
fit('ia/7_precos.png', 'precos', 768, 512)
fit('ia/8_fiado.png', 'fiado', 768, 432)
fit('ia/9_cartaz.png', 'cartaz', 512, 768)
fit('ia/10_foto.png', 'foto-time', 640, 480)
fit('ia/11_tv.png', 'tv-jogo', 512, 384)
fit('ia/13_cavalete.png', 'cavalete', 400, 600)
