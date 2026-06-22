import cv2
import numpy as np

def find_white_circles(image_path):
    img = cv2.imread(image_path)
    if img is None:
        print(f"Failed to load {image_path}")
        return
    
    # Convert to HSV or threshold for white
    # Since the user drew a white circle (probably pure white or very bright), 
    # let's look for white pixels.
    # In BGR, white is (255, 255, 255). Let's threshold for near white.
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    _, thresh = cv2.threshold(gray, 240, 255, cv2.THRESH_BINARY)
    
    # Find contours
    contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    
    print(f"Image: {image_path}, shape: {img.shape}")
    count = 0
    for i, c in enumerate(contours):
        x, y, w, h = cv2.boundingRect(c)
        # We are looking for something circular-like or of medium size (not tiny text, not the whole screen)
        area = cv2.contourArea(c)
        if 100 < area < 50000:
            # Check aspect ratio
            aspect_ratio = float(w)/h
            if 0.5 < aspect_ratio < 2.0:
                print(f"Possible Circle {count}: bounding box [x={x}, y={y}, w={w}, h={h}], area={area}")
                # Save crop
                crop = img[max(0, y-10):min(img.shape[0], y+h+10), max(0, x-10):min(img.shape[1], x+w+10)]
                cv2.imwrite(f"crop_{image_path.split('_')[-1].split('.')[0]}_{count}.png", crop)
                count += 1

print("Analyzing media__1782108113806.png...")
find_white_circles("C:/Users/Yunxi/.gemini/antigravity/brain/b44151d6-b436-42a5-97b4-c1c9d93166d6/media__1782108113806.png")

print("\nAnalyzing media__1782108146807.png...")
find_white_circles("C:/Users/Yunxi/.gemini/antigravity/brain/b44151d6-b436-42a5-97b4-c1c9d93166d6/media__1782108146807.png")
