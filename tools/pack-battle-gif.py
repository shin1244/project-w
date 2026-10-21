"""Pack frames captured by previews/BattleStage.tscn into the README GIFs.

Every other frame (about 7.5fps), 512x320, one 127-colour octree palette per GIF without dithering,
and index 127 for pixels unchanged since the previous frame. Packs every scene-N folder the capture
has (`--scenes=` can limit a capture). Needs Pillow and NumPy.

    python tools/pack-battle-gif.py <capture folder> docs/images/store
"""

import os
import sys

import numpy as np
from PIL import Image

SIZE = (512, 320)
COLOURS = 127  # index 127 stays free for "unchanged"
NAMES = {1: "battle-1", 2: "battle-2", 3: "battle-3", 4: "teamwork", 5: "magic-stone", 6: "hq-siege"}


def pack(frames_dir, out_path):
    names = sorted(name for name in os.listdir(frames_dir) if name.endswith(".jpg"))[::2]
    frames = [Image.open(os.path.join(frames_dir, name)).convert("RGB").resize(SIZE, Image.Resampling.LANCZOS)
              for name in names]
    sheet = Image.new("RGB", (SIZE[0], SIZE[1] * len(frames)))
    for i, frame in enumerate(frames):
        sheet.paste(frame, (0, SIZE[1] * i))
    # Median cut washes the team colours out; the octree keeps them and packs smaller.
    palette = sheet.quantize(colors=COLOURS, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE)
    colours = palette.getpalette()[:COLOURS * 3]
    palette.putpalette(colours)  # only these colours, so no pixel maps to the "unchanged" index
    colours += [0, 0, 0]
    indices = [np.asarray(frame.quantize(palette=palette, dither=Image.Dither.NONE)) for frame in frames]
    # Pillow folds a frame identical to the one before into it, so fold it here too and keep the check exact.
    kept, durations = [], []
    for index in indices:
        if kept and np.array_equal(index, kept[-1]):
            durations[-1] += 130
            continue
        kept.append(index)
        durations.append(130)
    indices = kept
    packed = []
    for i, index in enumerate(indices):
        delta = index.copy()
        if i:
            delta[index == indices[i - 1]] = COLOURS
        image = Image.fromarray(delta.astype(np.uint8), "P")
        image.putpalette(colours)
        packed.append(image)
    packed[0].save(out_path, save_all=True, append_images=packed[1:], duration=durations, loop=0,
                   disposal=1, transparency=COLOURS, optimize=False)

    # Decode the file and compare every frame with what was meant.
    check = Image.open(out_path)
    rgb = np.array(colours, dtype=np.uint8).reshape(-1, 3)
    for i in range(check.n_frames):
        check.seek(i)
        if not np.array_equal(np.asarray(check.convert("RGB")), rgb[indices[i]]):
            raise SystemExit(f"{out_path}: frame {i} does not decode as packed")
    print(f"{out_path}: {len(frames)} frames, {os.path.getsize(out_path) / 1e6:.2f} MB")


def main():
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    capture, out_dir = sys.argv[1], sys.argv[2]
    for scene, name in NAMES.items():
        if os.path.isdir(os.path.join(capture, f"scene-{scene}")):
            pack(os.path.join(capture, f"scene-{scene}"), os.path.join(out_dir, f"{name}.gif"))


if __name__ == "__main__":
    main()
