from PIL import Image
import numpy as np

def describe_image(img_path):
    img = Image.open(img_path)
    arr = np.array(img)
    unique, counts = np.unique(arr.reshape(-1, 3), axis=0, return_counts=True)
    print(f"\nImage: {img_path}")
    print(f"Size: {img.size}")
    # Print the top 5 most common colors
    sorted_indices = np.argsort(-counts)
    print("Top colors (RGB and count):")
    for i in range(min(5, len(sorted_indices))):
        idx = sorted_indices[i]
        print(f"Color: {unique[idx]}, Count: {counts[idx]}")

describe_image("r1_img1.png")
describe_image("r1_img2.png")
describe_image("r2_img1.png")
describe_image("r2_img2.png")
