from PIL import Image
import numpy as np

def find_purple_regions(img_path):
    img = Image.open(img_path).convert("RGB")
    width, height = img.size
    pixels = img.load()
    
    # We look for colors close to --accent in dark theme: rgb(208, 188, 255)
    # Let's count where R > 150 and G > 130 and B > 200
    accent_pixels = []
    for y in range(height):
        for x in range(width):
            r, g, b = pixels[x, y]
            if r > 150 and g > 130 and b > 200:
                # Exclude pure white (user drawings)
                if not (r > 250 and g > 250 and b > 250):
                    accent_pixels.append((x, y))
                    
    print(f"\nImage: {img_path}")
    print(f"Total purple/accent pixels: {len(accent_pixels)}")
    if accent_pixels:
        xs = [p[0] for p in accent_pixels]
        ys = [p[1] for p in accent_pixels]
        print(f"Accent bbox: x={min(xs)}..{max(xs)} (w={max(xs)-min(xs)}), y={min(ys)}..{max(ys)} (h={max(ys)-min(ys)})")
        # Find local clusters of accent pixels to identify different UI components
        # Let's group them by y coordinates (e.g. top vs middle vs bottom)
        clusters = {}
        for x, y in accent_pixels:
            y_group = y // 40
            clusters.setdefault(y_group, []).append((x, y))
        for yg, pts in sorted(clusters.items()):
            pt_xs = [p[0] for p in pts]
            pt_ys = [p[1] for p in pts]
            print(f"  Cluster at y_group {yg} (y={min(pt_ys)}..{max(pt_ys)}): count={len(pts)}, x={min(pt_xs)}..{max(pt_xs)}")

find_purple_regions("C:/Users/Yunxi/.gemini/antigravity/brain/b44151d6-b436-42a5-97b4-c1c9d93166d6/media__1782108113806.png")
find_purple_regions("C:/Users/Yunxi/.gemini/antigravity/brain/b44151d6-b436-42a5-97b4-c1c9d93166d6/media__1782108146807.png")
