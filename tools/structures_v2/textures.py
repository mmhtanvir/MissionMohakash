import numpy as np
from PIL import Image, ImageDraw, ImageFilter

def fnoise(n, beta=2.0, seed=0):
    rng = np.random.default_rng(seed)
    f = np.fft.fftfreq(n)[:, None] ** 2 + np.fft.fftfreq(n)[None, :] ** 2
    f[0, 0] = 1
    spec = np.fft.fft2(rng.normal(size=(n, n))) / (f ** (beta / 4))
    spec[0, 0] = 0
    a = np.real(np.fft.ifft2(spec)); a -= a.min(); a /= a.max()
    return a

def blur(a, r):
    n = a.shape[0]
    f = np.fft.fft2(a); fx = np.fft.fftfreq(n)
    g = np.exp(-(fx[:, None] ** 2 + fx[None, :] ** 2) * (r * 2 * np.pi) ** 2 / 2)
    return np.real(np.fft.ifft2(f * g))

def normal_from_h(h, strength):
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    gr = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    n = np.stack([-gx * strength, gr * strength, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n

def save_n(path, n):
    Image.fromarray(((n * 0.5 + 0.5) * 255).clip(0, 255).astype(np.uint8)).save(path, optimize=True)

def save_a(path, a):
    Image.fromarray((a * 255).clip(0, 255).astype(np.uint8)).save(path, quality=90, optimize=True)

N = 512

def grid_lines(n, cells, w):
    y, x = np.mgrid[0:n, 0:n]
    cs = n // cells
    dx = np.minimum(x % cs, cs - 1 - x % cs); dy = np.minimum(y % cs, cs - 1 - y % cs)
    return (np.minimum(dx, dy) < w).astype(float), cs

# ---------------- hull: white painted panels, 1 m panels, 2 m tile
def hull():
    rng = np.random.default_rng(3)
    seam, cs = grid_lines(N, 2, 2)
    y, x = np.mgrid[0:N, 0:N]
    pid = (y // cs) * 2 + (x // cs)
    base = 0.80 + rng.uniform(-0.035, 0.035, 4)[pid]
    # inner access-panel outline on two panels
    h = -seam * 1.0
    for (px, py) in ((0, 1), (1, 0)):
        x0, y0 = px * cs + 40, py * cs + 50; x1, y1 = x0 + 150, y0 + 110
        m = np.zeros((N, N)); m[y0:y0 + 2, x0:x1] = 1; m[y1:y1 + 2, x0:x1] = 1; m[y0:y1, x0:x0 + 2] = 1; m[y0:y1, x1:x1 + 2] = 1
        h -= m * 0.6
        # handle
        h[y0 + 50:y0 + 58, x0 + 60:x0 + 90] += 0.5
    # rivets
    riv = np.zeros((N, N))
    for k in range(0, N, 32):
        for off in (8, cs - 9):
            for (ry, rx) in ((off, k), (k, off)):
                for dy in range(-2, 3):
                    for dx in range(-2, 3):
                        if dx * dx + dy * dy <= 5: riv[(ry + dy) % N, (rx + dx) % N] = 1
    h += riv * 0.5
    fb = fnoise(N, 2.2, 1); fine = fnoise(N, 0.9, 2); blot = fnoise(N, 3.2, 3)
    a = base * (0.93 + 0.12 * fb) * (0.97 + 0.06 * fine) * (0.90 + 0.18 * blot)
    gro = blur(seam, 2.5)
    a *= 1 - 0.55 * np.clip(gro * 1.6, 0, 1)
    a *= 1 - 0.20 * blur(riv, 1.5) * 3
    # vertical grime streaks
    stk = fnoise(N, 2.0, 7); stk = blur(np.tile(stk[:1, :], (N, 1)) * 0 + stk, 0)  # keep tileable
    streak = np.tile(fnoise(N, 2.6, 9)[0:1, :], (N, 1))
    a *= 1 - 0.10 * np.clip((streak - 0.5) * 3, 0, 1)
    # scratches
    img = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)); d = ImageDraw.Draw(img)
    for _ in range(70):
        x0, y0 = rng.integers(0, N, 2); L = rng.integers(6, 40); ang = rng.uniform(0, 6.28)
        d.line([x0, y0, x0 + L * np.cos(ang), y0 + L * np.sin(ang)], fill=int(rng.choice([235, 70])), width=1)
    a = np.asarray(img).astype(float) / 255
    # warm dust tint near mid grey
    rgb = np.stack([a * 1.0, a * 0.985, a * 0.96], -1)
    h2 = h + (fnoise(N, 2.5, 5) - 0.5) * 0.25
    save_a('tex/hull_a.jpg', rgb); save_n('tex/hull_n.png', normal_from_h(blur(h2, 0.7), 3.0))

# ---------------- dark metal plates
def dark():
    rng = np.random.default_rng(5)
    seam, cs = grid_lines(N, 4, 1)
    y, x = np.mgrid[0:N, 0:N]
    pid = (y // cs) * 4 + (x // cs)
    base = 0.30 + rng.uniform(-0.04, 0.04, 16)[pid]
    brushed = fnoise(N, 1.2, 11)
    brushed = blur(np.tile(brushed[:, :1], (1, N)) * 0 + brushed, 0)
    a = base * (0.9 + 0.2 * fnoise(N, 2.4, 12)) * (0.95 + 0.1 * fnoise(N, 0.8, 13))
    a *= 1 - 0.5 * blur(seam, 1.5) * 2
    rgb = np.stack([a * 0.96, a * 0.98, a * 1.05], -1)
    save_a('tex/dark_a.jpg', rgb)
    save_n('tex/dark_n.png', normal_from_h(blur(-seam + fnoise(N, 1.5, 14) * 0.2, 0.7), 2.4))

# ---------------- gold foil (crinkled)
def gold():
    c = 0.55 * (1 - np.abs(fnoise(N, 2.0, 21) * 2 - 1)) + 0.45 * (1 - np.abs(fnoise(N, 1.4, 22) * 2 - 1))
    h = blur(c, 0.8)
    t = np.clip((c - 0.35) * 1.8, 0, 1)
    dark_c = np.array([0.62, 0.40, 0.08]); lite_c = np.array([0.95, 0.74, 0.28])
    rgb = dark_c[None, None] * (1 - t[..., None]) + lite_c[None, None] * t[..., None]
    rgb *= (0.92 + 0.12 * fnoise(N, 0.8, 23))[..., None]
    save_a('tex/gold_a.jpg', rgb); save_n('tex/gold_n.png', normal_from_h(h, 7.0))

# ---------------- solar cells (2 m tile, 16x16 cells)
def solar():
    rng = np.random.default_rng(31)
    cell = N // 16
    y, x = np.mgrid[0:N, 0:N]
    cx = x % cell; cy = y % cell
    gap = ((cx < 2) | (cy < 2)).astype(float)
    bus = ((np.abs(cx - cell // 3) < 1) | (np.abs(cx - 2 * cell // 3) < 1)).astype(float) * (1 - gap)
    pid = (y // cell) * 16 + (x // cell)
    tone = rng.uniform(0.85, 1.15, 256)[pid]
    grad = 0.7 + 0.5 * (cy / cell)
    base = np.stack([0.045 * np.ones_like(tone), 0.095 * np.ones_like(tone), 0.30 * np.ones_like(tone)], -1) * (tone * grad)[..., None]
    base *= (0.9 + 0.2 * fnoise(N, 0.8, 32))[..., None]
    base = base * (1 - gap[..., None]) + np.array([0.02, 0.025, 0.04]) * gap[..., None]
    base = base * (1 - bus[..., None]) + np.array([0.62, 0.66, 0.78]) * bus[..., None]
    save_a('tex/solar_a.jpg', base)
    save_n('tex/solar_n.png', normal_from_h(blur(-gap * 0.8 + bus * 0.15, 0.6), 2.2))

# ---------------- regolith dust (for ground plates / mounds)
def dust():
    a = 0.42 * (0.7 + 0.5 * fnoise(N, 2.4, 41)) * (0.92 + 0.16 * fnoise(N, 0.8, 42))
    rgb = np.stack([a * 1.0, a * 0.98, a * 0.95], -1)
    save_a('tex/dust_a.jpg', rgb)
    save_n('tex/dust_n.png', normal_from_h(blur(fnoise(N, 1.6, 43), 0.8), 3.0))

if __name__ == '__main__':
    hull(); dark(); gold(); solar(); dust()
    import os; print({f: os.path.getsize('tex/' + f) // 1024 for f in os.listdir('tex')})
