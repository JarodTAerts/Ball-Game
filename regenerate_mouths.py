#!/usr/bin/env python3
"""Regenerate mouth placeholder images with better representations."""

from PIL import Image, ImageDraw

def create_mouth(mouth_type, size_index):
    """Create a mouth image based on type and size."""
    sizes = {
        0: (40, 20),   # small
        1: (60, 30),   # medium
        2: (80, 40),   # large
    }

    width, height = sizes[size_index]
    img = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if mouth_type == 0:  # Smile
        # Draw a smile curve (arc going upward)
        draw.arc([(0, height//2), (width, height + height//2)],
                 start=0, end=180, fill=(0, 0, 0, 255), width=max(2, size_index + 2))

    elif mouth_type == 1:  # Toothy Grin
        # Draw smile with teeth
        draw.arc([(0, height//2), (width, height + height//2)],
                 start=0, end=180, fill=(0, 0, 0, 255), width=max(2, size_index + 2))
        # Add teeth rectangles
        num_teeth = 4 + size_index
        tooth_width = width // (num_teeth * 2)
        for i in range(num_teeth):
            x = i * (width // num_teeth) + tooth_width//2
            y = height * 2 // 3
            draw.rectangle([x, y, x + tooth_width, y + height//4],
                          fill=(255, 255, 255, 255))

    elif mouth_type == 2:  # Frown
        # Draw a frown curve (arc going downward)
        draw.arc([(0, -height//2), (width, height//2)],
                 start=180, end=360, fill=(0, 0, 0, 255), width=max(2, size_index + 2))

    elif mouth_type == 3:  # O (surprised)
        # Draw an oval/circle
        oval_width = width // 2
        oval_height = height
        x_offset = (width - oval_width) // 2
        draw.ellipse([x_offset, 0, x_offset + oval_width, oval_height],
                    outline=(0, 0, 0, 255), width=max(2, size_index + 1))

    elif mouth_type == 4:  # Wavy
        # Draw a wavy line
        points = []
        segments = 3 + size_index
        for i in range(segments * 2):
            x = i * width // (segments * 2)
            y = height // 2 + (height // 4) * (1 if i % 2 == 0 else -1)
            points.append((x, y))
        if len(points) > 1:
            draw.line(points, fill=(0, 0, 0, 255), width=max(2, size_index + 1))

    return img

def main():
    """Generate all mouth variations."""
    import os

    output_dir = "Ball-Fight-Game/assets/textures/customization/mouths"
    os.makedirs(output_dir, exist_ok=True)

    mouth_names = ["smile", "grin", "frown", "o", "wavy"]

    for mouth_type in range(5):
        for size_index in range(3):
            img = create_mouth(mouth_type, size_index)
            filename = f"mouth_{mouth_type}_{size_index}.png"
            filepath = os.path.join(output_dir, filename)
            img.save(filepath)
            print(f"Created {filename}")

    print(f"\nGenerated 15 mouth images in {output_dir}")

if __name__ == "__main__":
    main()
