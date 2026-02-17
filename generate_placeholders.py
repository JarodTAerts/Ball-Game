#!/usr/bin/env python3
"""
Generate simple placeholder PNG assets for player customization testing.
Run this once to create basic geometric shapes for eyes, mouths, noses, hair, and clothes.
"""

from PIL import Image, ImageDraw
import os

# Base paths
base_path = "Ball-Fight-Game/assets/textures/customization"

# Ensure directories exist
for subdir in ['eyes', 'mouths', 'noses', 'hair', 'clothes']:
    os.makedirs(os.path.join(base_path, subdir), exist_ok=True)

def create_eye(type_id, size_id):
    """Create eye placeholder images"""
    sizes = [32, 48, 64]
    size = sizes[size_id]

    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if type_id == 0:  # Dot eyes
        draw.ellipse([size//4, size//4, 3*size//4, 3*size//4], fill=(0, 0, 0, 255))
    elif type_id == 1:  # Oval eyes
        draw.ellipse([size//6, size//3, 5*size//6, 2*size//3], fill=(0, 0, 0, 255))
    elif type_id == 2:  # Angry slant
        draw.polygon([(size//6, 2*size//3), (5*size//6, size//3), (5*size//6, size//2), (size//6, 5*size//6)], fill=(0, 0, 0, 255))
    elif type_id == 3:  # Wide surprised
        draw.ellipse([size//8, size//4, 7*size//8, 3*size//4], fill=(0, 0, 0, 255))
        draw.ellipse([size//4, size//3, 3*size//4, 2*size//3], fill=(255, 255, 255, 255))
    elif type_id == 4:  # X eyes
        draw.line([size//4, size//4, 3*size//4, 3*size//4], fill=(0, 0, 0, 255), width=size//8)
        draw.line([3*size//4, size//4, size//4, 3*size//4], fill=(0, 0, 0, 255), width=size//8)

    filename = f"{base_path}/eyes/eye_{type_id}_{size_id}.png"
    img.save(filename)
    print(f"Created {filename}")

def create_mouth(type_id, size_id):
    """Create mouth placeholder images"""
    sizes = [(40, 20), (60, 30), (80, 40)]
    width, height = sizes[size_id]

    img = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if type_id == 0:  # Simple smile
        draw.arc([0, 0, width, height*2], 0, 180, fill=(0, 0, 0, 255), width=height//4)
    elif type_id == 1:  # Toothy grin
        draw.arc([0, 0, width, height*2], 0, 180, fill=(0, 0, 0, 255), width=height//3)
        # Add teeth
        for i in range(5):
            x = (i+1) * width // 6
            draw.line([x, height//2, x, height], fill=(255, 255, 255, 255), width=2)
    elif type_id == 2:  # Frown
        draw.arc([0, -height, width, height], 180, 360, fill=(0, 0, 0, 255), width=height//4)
    elif type_id == 3:  # O surprise
        draw.ellipse([width//4, 0, 3*width//4, height], fill=(0, 0, 0, 255))
    elif type_id == 4:  # Wavy
        points = [(0, height//2)]
        for i in range(1, 6):
            x = i * width // 5
            y = height//2 + (height//4 if i % 2 else -height//4)
            points.append((x, y))
        draw.line(points, fill=(0, 0, 0, 255), width=height//4)

    filename = f"{base_path}/mouths/mouth_{type_id}_{size_id}.png"
    img.save(filename)
    print(f"Created {filename}")

def create_nose(type_id, size_id):
    """Create nose placeholder images"""
    sizes = [20, 30, 40]
    size = sizes[size_id]

    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if type_id == 0:  # Dot nose
        draw.ellipse([size//3, size//3, 2*size//3, 2*size//3], fill=(0, 0, 0, 255))
    elif type_id == 1:  # Triangle nose
        draw.polygon([(size//2, size//4), (size//4, 3*size//4), (3*size//4, 3*size//4)], fill=(0, 0, 0, 255))
    elif type_id == 2:  # Round clown nose
        draw.ellipse([size//6, size//6, 5*size//6, 5*size//6], fill=(255, 0, 0, 255))
    elif type_id == 3:  # Pig snout
        draw.ellipse([size//6, size//4, size//2, 3*size//4], fill=(0, 0, 0, 255))
        draw.ellipse([size//2, size//4, 5*size//6, 3*size//4], fill=(0, 0, 0, 255))
    elif type_id == 4:  # Long nose
        draw.rectangle([2*size//5, size//4, 3*size//5, 3*size//4], fill=(0, 0, 0, 255))

    filename = f"{base_path}/noses/nose_{type_id}_{size_id}.png"
    img.save(filename)
    print(f"Created {filename}")

def create_hair(type_id):
    """Create hair placeholder images (grayscale for colorization)"""
    size = (256, 128)

    img = Image.new('RGBA', size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if type_id == 0:  # Bald (empty)
        pass
    elif type_id == 1:  # Spiky mohawk
        for i in range(5):
            x = size[0]//2 + (i - 2) * 30
            draw.polygon([(x-15, size[1]), (x, 20), (x+15, size[1])], fill=(128, 128, 128, 255))
    elif type_id == 2:  # Bowl cut
        draw.pieslice([size[0]//4, -size[1]//2, 3*size[0]//4, size[1]], 0, 180, fill=(128, 128, 128, 255))
    elif type_id == 3:  # Pigtails
        draw.ellipse([20, 40, 80, 100], fill=(128, 128, 128, 255))
        draw.ellipse([size[0]-80, 40, size[0]-20, 100], fill=(128, 128, 128, 255))
    elif type_id == 4:  # Afro
        draw.ellipse([size[0]//4, 0, 3*size[0]//4, size[1]], fill=(128, 128, 128, 255))

    filename = f"{base_path}/hair/hair_{type_id}.png"
    img.save(filename)
    print(f"Created {filename}")

def create_clothes(type_id):
    """Create clothes pattern placeholder images"""
    size = (512, 256)

    img = Image.new('RGBA', size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if type_id == 1:  # Horizontal stripes
        for i in range(0, size[1], 40):
            draw.rectangle([0, i, size[0], i+20], fill=(0, 0, 0, 128))
    elif type_id == 2:  # Polka dots
        for y in range(30, size[1], 60):
            for x in range(30, size[0], 60):
                draw.ellipse([x-15, y-15, x+15, y+15], fill=(0, 0, 0, 128))
    elif type_id == 3:  # Checkered
        square_size = 40
        for y in range(0, size[1], square_size):
            for x in range(0, size[0], square_size):
                if (x//square_size + y//square_size) % 2 == 0:
                    draw.rectangle([x, y, x+square_size, y+square_size], fill=(0, 0, 0, 128))
    elif type_id == 4:  # Star pattern
        for y in range(50, size[1], 80):
            for x in range(50, size[0], 80):
                # Simple 5-point star
                points = []
                for i in range(5):
                    angle = i * 144  # 360/5 * 2 for pentagram
                    outer_x = x + 20 * __import__('math').cos(__import__('math').radians(angle - 90))
                    outer_y = y + 20 * __import__('math').sin(__import__('math').radians(angle - 90))
                    points.append((outer_x, outer_y))
                    inner_angle = angle + 72
                    inner_x = x + 8 * __import__('math').cos(__import__('math').radians(inner_angle - 90))
                    inner_y = y + 8 * __import__('math').sin(__import__('math').radians(inner_angle - 90))
                    points.append((inner_x, inner_y))
                draw.polygon(points, fill=(0, 0, 0, 128))

    if type_id > 0:  # Don't create clothes_0 (plain)
        filename = f"{base_path}/clothes/clothes_{type_id}.png"
        img.save(filename)
        print(f"Created {filename}")

# Generate all assets
print("Generating placeholder customization assets...")

# Eyes: 5 types × 3 sizes = 15 files
for type_id in range(5):
    for size_id in range(3):
        create_eye(type_id, size_id)

# Mouths: 5 types × 3 sizes = 15 files
for type_id in range(5):
    for size_id in range(3):
        create_mouth(type_id, size_id)

# Noses: 5 types × 3 sizes = 15 files
for type_id in range(5):
    for size_id in range(3):
        create_nose(type_id, size_id)

# Hair: 5 types = 5 files
for type_id in range(5):
    create_hair(type_id)

# Clothes: 4 patterns = 4 files (no type 0/plain)
for type_id in range(1, 5):
    create_clothes(type_id)

print("\nDone! Created 49 placeholder PNG assets.")
print("These are simple geometric shapes for testing. Replace with better art later.")
