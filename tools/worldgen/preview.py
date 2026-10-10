"""Renders the whole world as one image (2 px per tile) and checks room edges.

python3 preview.py world.txt out.png
"""
import sys
from PIL import Image, ImageDraw

COL = {
    '#': (90, 80, 70), '=': (120, 120, 140), ':': (40, 34, 46), '-': (200, 150, 80), '.': (14, 14, 22),
    '^': (230, 60, 90), 'v': (230, 60, 90), '<': (230, 60, 90), '>': (230, 60, 90),
    'x': (150, 110, 70), 'b': (255, 210, 90), 'r': (150, 130, 255), 'u': (40, 70, 90),
    '|': (200, 200, 220), '!': (255, 80, 60), 'G': (255, 230, 120), 'M': (120, 200, 255), 'N': (220, 220, 255),
    'T': (180, 180, 200), 'H': (255, 90, 90), 'F': (255, 160, 60), '$': (255, 140, 40), 'A': (255, 255, 255),
    'L': (255, 255, 0), 'B': (255, 0, 0), 'K': (255, 240, 0), '@': (0, 255, 0),
    'c': (0, 200, 120), 'h': (0, 200, 120), 'w': (0, 200, 120), 'k': (0, 200, 120), 's': (0, 200, 120), 'd': (0, 200, 120),
}
REGION = {'village': (255, 200, 120), 'thornwood': (120, 220, 120), 'abbey': (200, 140, 255), 'frost': (140, 200, 255), 'summit': (255, 240, 160)}


def parse(path):
    rooms, cur = [], None
    for line in open(path):
        line = line.rstrip('\n')
        if line.startswith('@room'):
            parts = line.split()
            cur = {'id': parts[1], 'map': [], 'props': {}, 'inmap': False}
            for p in parts[2:]:
                k, v = p.split('=')
                cur[k] = v
            continue
        if cur is None:
            continue
        if line.startswith('@end'):
            rooms.append(cur); cur = None; continue
        if line.startswith('@map'):
            cur['inmap'] = True; continue
        if cur['inmap']:
            cur['map'].append(line)
        elif ':' in line:
            k, v = line.split(':', 1)
            cur['props'][k.strip()] = v.strip()
    for r in rooms:
        r['x'], r['y'] = map(int, r['at'].split(','))
        r['w'], r['h'] = map(int, r['size'].split(','))
    return rooms


def main(src, out):
    rooms = parse(src)
    cells = {}
    for r in rooms:
        for dy in range(r['h']):
            for dx in range(r['w']):
                cells[(r['x'] + dx, r['y'] + dy)] = r
    minx = min(x for x, _ in cells); maxx = max(x for x, _ in cells)
    miny = min(y for _, y in cells); maxy = max(y for _, y in cells)
    S = 2
    W, H = (maxx - minx + 1) * 40, (maxy - miny + 1) * 23
    img = Image.new('RGB', (W * S, H * S), (0, 0, 0))
    px = img.load()
    for r in rooms:
        ox, oy = (r['x'] - minx) * 40, (r['y'] - miny) * 23
        for y, row in enumerate(r['map']):
            for x, c in enumerate(row):
                col = COL.get(c, (255, 0, 255))
                for a in range(S):
                    for b in range(S):
                        px[(ox + x) * S + a, (oy + y) * S + b] = col
    d = ImageDraw.Draw(img)
    for r in rooms:
        ox, oy = (r['x'] - minx) * 40 * S, (r['y'] - miny) * 23 * S
        d.rectangle([ox, oy, ox + r['w'] * 40 * S - 1, oy + r['h'] * 23 * S - 1], outline=REGION[r['region']])
        d.text((ox + 3, oy + 2), r['id'], fill=REGION[r['region']])
    img = img.resize((img.width * 2 // 2, img.height * 2 // 2), Image.NEAREST)
    img.save(out)

    # edge check (same rule as Atlas.CheckEdges)
    def tile(wx, wy):
        r = cells.get((wx // 40, wy // 23))
        if r is None:
            return None
        return r['map'][wy - r['y'] * 23][wx - r['x'] * 40]
    passable = lambda c: c not in '#='
    problems = 0
    for r in rooms:
        bx, by = r['x'] * 40, r['y'] * 23
        w, h = r['w'] * 40, r['h'] * 23
        sky = 'sky' in r['props']
        edges = [(0, y, -1, 0) for y in range(h)] + [(w - 1, y, 1, 0) for y in range(h)] + [(x, h - 1, 0, 1) for x in range(w)]
        if not sky:
            edges += [(x, 0, 0, -1) for x in range(w)]
        for x, y, dx, dy in edges:
            if not passable(r['map'][y][x]):
                continue
            n = tile(bx + x + dx, by + y + dy)
            if n is None:
                print(f"{r['id']}: open edge {x},{y} leads nowhere"); problems += 1
            elif not passable(n):
                print(f"{r['id']}: open edge {x},{y} meets a wall"); problems += 1
    print(f'{len(rooms)} rooms, {problems} edge problems')


main(sys.argv[1], sys.argv[2])
