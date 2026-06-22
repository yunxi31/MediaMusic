from PIL import Image
import os

def crop_and_compare():
    img1 = Image.open("C:/Users/Yunxi/.gemini/antigravity/brain/b44151d6-b436-42a5-97b4-c1c9d93166d6/media__1782108113806.png").convert("RGB")
    img2 = Image.open("C:/Users/Yunxi/.gemini/antigravity/brain/b44151d6-b436-42a5-97b4-c1c9d93166d6/media__1782108146807.png").convert("RGB")
    
    # Region 1 (Top area)
    box1 = (434, 0, 598, 48)
    crop1_img1 = img1.crop(box1)
    crop1_img2 = img2.crop(box1)
    
    crop1_img1.save("r1_img1.png")
    crop1_img2.save("r1_img2.png")
    
    # Region 2 (Bottom area)
    box2 = (446, 646, 602, 680)
    crop2_img1 = img1.crop(box2)
    crop2_img2 = img2.crop(box2)
    
    crop2_img1.save("r2_img1.png")
    crop2_img2.save("r2_img2.png")
    
    print("Saved r1_img1, r1_img2, r2_img1, r2_img2.")

crop_and_compare()
