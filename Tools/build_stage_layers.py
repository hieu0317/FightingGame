from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw
import import_aerial_sheets as art

PACK = Path(art.PACK)
STAGES = {
    "chodem": [
        ("awning_cart", [(151,127),(309,128),(341,164),(171,165)], [171,322,159]),
        ("awning_stall", [(641,107),(767,108),(767,155),(664,155)], [665,767,151])],
    "congtruong": [
        ("scaffold_low", [(0,171),(162,170),(182,180),(371,179),(371,195),(181,198),(162,191),(0,194)], [1,161,171]),
        ("scaffold_upper", [(73,31),(249,31),(273,55),(272,70),(249,53),(73,53)], [74,248,32])],
    "santhuong": [
        ("tank_platform", [(75,49),(81,42),(107,35),(117,29),(147,28),(153,34),(178,39),(185,47),(185,116),(178,121),(189,135),(181,136),(172,123),(92,124),(85,135),(77,135),(82,119),(76,113)], [94,166,38]),
        ("roof_ledge", [(0,133),(174,134),(210,145),(207,161),(170,151),(0,150)], [0,173,134]),
        ("parapet_platform", [(312,249),(555,248),(555,260),(313,261)], [313,554,249])],
}


def save(path, image, names=None, pivot=(.5,0)):
    art.ensure_folder(str(path.parent))
    image.save(path)
    names = names or [path.stem]
    art.write_texture_meta(str(path),names,image.width//len(names),image.height,pivot)


def main():
    bounds = {}
    for stage,parts in STAGES.items():
        background = Image.open(PACK/"Stages"/(stage+".png")).convert("RGBA")
        entries = []
        for name,polygon,surface in parts:
            alpha = Image.new("L",background.size)
            ImageDraw.Draw(alpha).polygon(polygon,fill=255)
            box = alpha.getbbox()
            layer = background.crop(box)
            layer.putalpha(alpha.crop(box))
            save(PACK/"StageModules"/stage/(name+".png"),layer)
            entries.append({"sprite":name,"rect_xywh":[box[0],box[1],layer.width,layer.height],
                            "top_x1_x2_y":surface})
        bounds[stage] = entries
    target = Path(art.ROOT)/"ArtSource/PixelPack/stage-platforms.json"
    target.write_text(json.dumps({"origin":"background top left, pixels", "stages":bounds},indent=2)+"\n")
    shadow = Image.new("RGBA",(42,10))
    d = ImageDraw.Draw(shadow)
    for inset,opacity in ((0,45),(2,65),(5,85)):
        d.ellipse((inset,inset//2,41-inset,9-inset//2),fill=(36,24,38,opacity))
    save(PACK/"StageModules/congtruong/drop_shadow.png",shadow,pivot=(.5,.5))
    hazard = Image.open(PACK/"Hazards/chodem/hazard.png").convert("RGBA")
    warning = hazard.crop((166,54,210,96))
    flash = np.array(warning)
    mask = (flash[:,:,3]>0) & (flash[:,:,:3].max(2)>100)
    flash[:,:,:3][mask] = np.minimum(255,flash[:,:,:3][mask].astype(int)+[30,55,75])
    strip = Image.new("RGBA",(88,42))
    strip.paste(warning)
    strip.paste(Image.fromarray(flash),(44,0))
    save(PACK/"Hazards/chodem/warning.png",strip,["warning_00","warning_01"])
    for high in sorted((PACK/"DamagedProps").glob("*.png")):
        if high.stem.endswith("_damaged_floor"): continue
        source = Image.open(high).convert("RGBA")
        source = source.crop(source.getbbox())
        target = PACK.parent/"PixelPackDerived/FloorProps"/(high.stem+"_damaged.png")
        size = Image.open(target).size
        # Palette clusters retain dents and splits at the object's actual floor size.
        small = source.resize(size,Image.Resampling.LANCZOS)
        rgb = small.convert("RGB").quantize(18,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE).convert("RGBA")
        rgb.putalpha(small.getchannel("A").point(lambda x:255 if x>=128 else 0))
        save(high.with_name(high.stem+"_damaged_floor.png"),rgb)


if __name__ == "__main__":
    main()
