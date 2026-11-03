import sys
from PIL import Image, ImageDraw

OUT = sys.argv[1]
N = 1024  # dibujar grande y reducir: bordes suaves en todos los tamaños

def gradient_squircle(n, top, bottom, radius):
    grad = Image.new("RGBA", (n, n))
    d = ImageDraw.Draw(grad)
    for y in range(n):
        t = y / (n - 1)
        d.line([(0, y), (n, y)], fill=tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,))
    mask = Image.new("L", (n, n), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, n - 1, n - 1], radius=radius, fill=255)
    out = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    out.paste(grad, (0, 0), mask)
    return out

CORAL_TOP, CORAL_BOTTOM = (245, 150, 125), (208, 82, 58)
TILES = [(232, 111, 85), (74, 159, 234), (245, 197, 24), (76, 195, 138), (155, 109, 230), (40, 182, 196)]

def detailed():
    img = gradient_squircle(N, CORAL_TOP, CORAL_BOTTOM, 230)
    d = ImageDraw.Draw(img)
    # Sombra suave del widget
    d.rounded_rectangle([200, 262, 824, 826], radius=78, fill=(120, 40, 25, 70))
    # Widget blanco
    d.rounded_rectangle([196, 236, 828, 800], radius=78, fill=(255, 255, 255, 245))
    # Pestaña activa (píldora) y una segunda pestaña
    d.rounded_rectangle([256, 290, 470, 346], radius=28, fill=(232, 111, 85, 255))
    d.rounded_rectangle([500, 300, 640, 336], radius=18, fill=(225, 214, 210, 255))
    # Cuadrícula 3x2 de íconos
    size, gap, x0, y0 = 150, 46, 264, 400
    for i, c in enumerate(TILES):
        x = x0 + (i % 3) * (size + gap)
        y = y0 + (i // 3) * (size + gap)
        d.rounded_rectangle([x, y, x + size, y + size], radius=38, fill=c + (255,))
    return img

def simple():
    # 16-24 px: menos detalle para que no se vuelva una mancha
    img = gradient_squircle(N, CORAL_TOP, CORAL_BOTTOM, 230)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([170, 200, 854, 860], radius=90, fill=(255, 255, 255, 250))
    size, gap, x0, y0 = 250, 64, 222, 260
    for i, c in enumerate([TILES[0], TILES[1], TILES[2], TILES[3]]):
        x = x0 + (i % 2) * (size + gap)
        y = y0 + (i // 2) * (size + gap)
        d.rounded_rectangle([x, y, x + size, y + size], radius=60, fill=c + (255,))
    return img

big, small = detailed(), simple()
sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
frames = [(small if s <= 24 else big).resize((s, s), Image.LANCZOS) for s in sizes]
frames[-1].save(OUT, format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[:-1])
big.resize((256, 256), Image.LANCZOS).save(OUT.replace(".ico", "-preview.png"))
small.resize((64, 64), Image.LANCZOS).save(OUT.replace(".ico", "-preview-small.png"))
print("ok", OUT)
