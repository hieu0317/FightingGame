from pathlib import Path
import argparse
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import import_aerial_sheets as art

ROOT = Path(art.ROOT)
SOURCE = ROOT / "ArtSource/PixelPack"
TICKS = {"light_1": [2,2,2,3,3,3], "light_1_end": [3,3,4],
         "light_2": [2,2,2,3,3,3], "light_2_end": [3,3,4],
         "light_3": [2,2,3,3,4,4,3,3], "heavy": [5,5,4,4,4,4,5,4,4]}


def atlas_rows(path, fighter, count, reference="idle_fight"):
    source = Image.open(path).convert("RGBA")
    solid = np.asarray(source)[:, :, 3] >= 128
    ys = art.split_points(solid.sum(1), count, source.height)
    rows = []
    for y0, y1 in zip(ys, ys[1:]):
        xs = art.split_points(solid[y0:y1].sum(0), 4, source.width)
        row = []
        for x0, x1 in zip(xs, xs[1:]):
            cell = source.crop((x0,y0,x1,y1))
            # Erode isolated translucent pixels before measuring the silhouette.
            alpha = cell.getchannel("A").point(lambda x: 255 if x >= 128 else 0)
            mask = alpha.filter(ImageFilter.MedianFilter(3))
            if not mask.getbbox():
                raise ValueError(f"Empty cell in {path}, row {len(rows)}, column {len(row)}")
            row.append(cell.crop(mask.getbbox()))
        rows.append(row)
    idle = art.strip_frames(Path(art.PACK)/"BasicActions"/fighter/(reference+".png"))[0]
    height = art.opaque_height(idle)
    scale = height / np.median([row[0].height for row in rows])
    pal = art.palette_of(fighter)
    out = []
    for row in rows:
        images = []
        for cell in row:
            small = art.downscale(np.asarray(cell).astype(np.int32), scale, pal)
            # Align back edge for extended weapons, centre neutral stance on pivot.
            anchor = min(small.shape[1]//2, round(row[0].width*scale/2))
            images.append(art.place(small,anchor))
        out.append(images)
    return out


def shift(frame, dx, height_delta=0):
    x,y,r,b = frame.getbbox()
    part = frame.crop((x,y,r,b)).resize((r-x,b-y+height_delta), Image.Resampling.NEAREST)
    out = Image.new("RGBA",frame.size)
    out.paste(part,(x+dx,b-part.height))
    return out


def combo(path, fighter):
    rows = atlas_rows(path, fighter, 4)
    folder = Path(art.PACK)/"BasicActions"/fighter
    idle = art.strip_frames(folder/"idle_fight.png")[0]
    previous = idle
    for i, (ready, windup, impact, follow) in enumerate(rows):
        # Give contact priority even for moves whose weapon starts extended.
        reach = max(f.getbbox()[2] for f in [previous,ready,windup,follow,idle])
        impact = shift(impact,max(3,reach+5-impact.getbbox()[2]))
        if i < 2:
            joint = shift(follow,2,-2)
            name = f"light_{i+1}"
            frames = [previous,windup,impact,shift(impact,-1,-1),shift(follow,3),joint]
            recovery = [joint,ready,idle]
            art.save_strip(str(folder),name+"_end",recovery,range(3),TICKS[name+"_end"])
            previous = joint
        elif i == 2:
            name = "light_3"
            # Two tapered trails behind the striking limb, inside its measured reach.
            solid = np.asarray(impact)[:,:,3] > 0
            x = impact.getbbox()[2]-3
            y = int(np.median(np.flatnonzero(solid[:,x])))
            trail = Image.new("RGBA", impact.size)
            draw = ImageDraw.Draw(trail)
            for dx,dy in ((0,-5),(-4,4)):
                draw.polygon([(x-28+dx,y+dy+4),(x-12+dx,y+dy),(x-4+dx,y+dy),
                              (x-15+dx,y+dy+2)], fill=(243,213,173,255))
            impact = Image.alpha_composite(trail,impact)
            frames = [previous,ready,windup,impact,shift(impact,-1,-1),follow,shift(ready,-1),idle]
        else:
            name = "heavy"
            frames = [idle,ready,shift(windup,-3,-2),windup,impact,shift(impact,-1,-1),follow,ready,idle]
        art.save_strip(str(folder),name,frames,range(len(frames)),TICKS[name])


def main():
    p = argparse.ArgumentParser()
    p.add_argument("kind", choices=["combo","air","cast"])
    p.add_argument("fighter", choices=art.PEOPLE)
    p.add_argument("source",type=Path)
    a = p.parse_args()
    if a.kind == "combo":
        combo(a.source,a.fighter)
        timings = SOURCE/"basic-timings.json"
        data = json.loads(timings.read_text(encoding="utf8"))
        for name,ticks in TICKS.items():
            data[name] = {"ticks":ticks,"loop":False}
        timings.write_text(json.dumps(data,indent=2,ensure_ascii=False)+"\n",encoding="utf8")
        return
    rows = atlas_rows(a.source,a.fighter,6 if a.kind=="air" else 5,
                      "jump_air" if a.kind=="cast" else "idle_fight")
    if a.kind == "air":
        folder = Path(art.PACK)/"BasicActions"/a.fighter
        jump = art.strip_frames(folder/"jump_air.png")[0]
        for row in rows[:5]:
            row[0] = jump
        rows[-1][-1] = art.strip_frames(folder/"idle_fight.png")[0]
    sheet = Image.new("RGBA",(4*art.FW,len(rows)*art.FH))
    for y,row in enumerate(rows):
        for x,frame in enumerate(row):
            sheet.paste(frame,(x*art.FW,y*art.FH))
    name = f"{a.kind}_{a.fighter}" + ("_air" if a.kind=="cast" and a.fighter=="bao" else "")
    sheet.save(SOURCE/"AerialSheets"/(name+".png"))


if __name__ == "__main__":
    main()
