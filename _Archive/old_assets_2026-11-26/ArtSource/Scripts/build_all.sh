#!/usr/bin/env bash
# Dựng lại toàn bộ asset Giai đoạn 1 (Prototype) từ script. Chạy từ thư mục gốc project:
#   bash ArtSource/Scripts/build_all.sh
set -euo pipefail
cd "$(dirname "$0")/../.."
BLENDER="${BLENDER:-/Applications/Blender.app/Contents/MacOS/Blender}"
run() { echo "== $1"; "$BLENDER" -b --factory-startup -P "ArtSource/Scripts/$1.py" 2>&1 | grep -E "^RESULT|Error|Traceback" | cut -c1-300; }
run make_template          # palette.png + template_asset.blend (phải chạy trước)
run prop_ghe_nhua
run prop_chai_thuy_tinh
run prop_thung_phi
run char_proto
run stage_chodem_blockout  # import ngược FBX ở trên → chạy sau cùng
run render_sprites         # 2D: sprite prop + nhân vật tạm (từ .blend ở trên)
run render_stage_layers    # 2D: lớp parallax + layout JSON Chợ Đêm
python3 ArtSource/Scripts/make_sheet.py ghe_nhua chai_thuy_tinh thung_phi
