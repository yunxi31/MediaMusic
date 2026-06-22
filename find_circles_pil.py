from PIL import Image, ImageFilter

def find_white_blobs(image_path):
    img = Image.open(image_path).convert("RGB")
    width, height = img.size
    
    # Create binary image of near-white pixels
    # The user's white circle is probably very white. Let's find pixels where R, G, B > 250
    pixels = img.load()
    binary_img = Image.new("1", (width, height), 0)
    binary_pixels = binary_img.load()
    
    for y in range(height):
        for x in range(width):
            r, g, b = pixels[x, y]
            if r > 250 and g > 250 and b > 250:
                binary_pixels[x, y] = 1
                
    # Now find connected components (bounding boxes of white pixels)
    # Since we don't have cv2, we can implement a simple Union-Find or DFS, 
    # or just use ImageFilter to find edges and get bbox.
    # Actually, we can do a simple flood fill or connected components in Python.
    visited = set()
    blobs = []
    
    # We only scan every 2nd pixel for speed
    for y in range(0, height, 2):
        for x in range(0, width, 2):
            if binary_pixels[x, y] == 1 and (x, y) not in visited:
                # Flood fill to find bbox
                queue = [(x, y)]
                visited.add((x, y))
                min_x, max_x = x, x
                min_y, max_y = y, y
                
                count = 0
                while queue:
                    curr_x, curr_y = queue.pop(0)
                    count += 1
                    
                    if curr_x < min_x: min_x = curr_x
                    if curr_x > max_x: max_x = curr_x
                    if curr_y < min_y: min_y = curr_y
                    if curr_y > max_y: max_y = curr_y
                    
                    # check neighbors
                    for dx, dy in [(-2, 0), (2, 0), (0, -2), (0, 2)]:
                        nx, ny = curr_x + dx, curr_y + dy
                        if 0 <= nx < width and 0 <= ny < height:
                            if binary_pixels[nx, ny] == 1 and (nx, ny) not in visited:
                                visited.add((nx, ny))
                                queue.append((nx, ny))
                                if len(queue) > 5000: # limit size to avoid infinite loops
                                    break
                
                # Check if it's a medium-sized blob (not tiny text noise, not background)
                w_b = max_x - min_x
                h_b = max_y - min_y
                if 20 < w_b < 400 and 20 < h_b < 400 and count > 50:
                    blobs.append((min_x, min_y, max_x, max_y, w_b, h_b, count))
                    
    print(f"File: {image_path}")
    print(f"Found {len(blobs)} white blobs:")
    for idx, blob in enumerate(blobs):
        min_x, min_y, max_x, max_y, w_b, h_b, count = blob
        print(f"Blob {idx}: x={min_x}..{max_x} (w={w_b}), y={min_y}..{max_y} (h={h_b}), count={count}")
        # Crop and save
        crop = img.crop((max(0, min_x - 20), max(0, min_y - 20), min(width, max_x + 20), min(height, max_y + 20)))
        crop.save(f"crop_{image_path.split('_')[-1].split('.')[0]}_{idx}.png")

print("Analyzing media__1782108113806.png...")
find_white_blobs("C:/Users/Yunxi/.gemini/antigravity/brain/b44151d6-b436-42a5-97b4-c1c9d93166d6/media__1782108113806.png")

print("\nAnalyzing media__1782108146807.png...")
find_white_blobs("C:/Users/Yunxi/.gemini/antigravity/brain/b44151d6-b436-42a5-97b4-c1c9d93166d6/media__1782108146807.png")
