from PIL import Image

def analyze_crop(img_path, box, name):
    img = Image.open(img_path)
    cropped = img.crop(box)
    cropped.save(name)
    print(f"Saved crop {name} of size {cropped.size}")

# Let's save a crop of the top center region (where the step indicator is) for both images
# Image width is 1024. Top center: x from 300 to 724, y from 0 to 150
analyze_crop("C:/Users/Yunxi/.gemini/antigravity/brain/b44151d6-b436-42a5-97b4-c1c9d93166d6/media__1782108113806.png", (300, 0, 724, 150), "top_center_img1.png")
analyze_crop("C:/Users/Yunxi/.gemini/antigravity/brain/b44151d6-b436-42a5-97b4-c1c9d93166d6/media__1782108146807.png", (300, 0, 724, 150), "top_center_img2.png")

# Let's save a crop of the bottom region (where the buttons are) for both images
# Image height is 682. Bottom center: x from 300 to 724, y from 500 to 682
analyze_crop("C:/Users/Yunxi/.gemini/antigravity/brain/b44151d6-b436-42a5-97b4-c1c9d93166d6/media__1782108113806.png", (300, 500, 724, 682), "bottom_img1.png")
analyze_crop("C:/Users/Yunxi/.gemini/antigravity/brain/b44151d6-b436-42a5-97b4-c1c9d93166d6/media__1782108146807.png", (300, 500, 724, 682), "bottom_img2.png")
