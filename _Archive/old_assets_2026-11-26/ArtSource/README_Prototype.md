# Loạn Đấu Phố Đêm – Giai đoạn 1 (Prototype): asset Blender

Mọi asset sinh 100% từ script trong `ArtSource/Scripts/`. Muốn sửa: đổi tham số ở đầu script rồi chạy lại:

```bash
bash ArtSource/Scripts/build_all.sh        # dựng lại tất cả (~1 phút, Blender headless)
```

## Hướng đồ hoạ: 2D prerender (chốt 26/11/2026)
Game chạy **2D hoàn toàn**. Model 3D trong Blender chỉ là *nguồn* để render sprite, không đưa vào game.

```
script dựng (.py) → .blend → render_sprites.py / render_stage_layers.py → PNG + *.sprites.json → Unity (Sprite)
```

| Quy chuẩn | Giá trị |
|---|---|
| Camera render | Orthographic, nhìn từ −Y, nghiêng xuống **12°** (thấy mặt trên → cảm giác 2.5D) |
| Mật độ | **256 px / mét** = Pixels Per Unit 256 trong Unity; mọi sprite cùng tỷ lệ |
| Màu | Workbench, view transform *Standard* (không bạc màu), viền đen, nền trong suốt |
| Toạ độ game | x = mét ngang; y = độ cao đã chiếu = z·cos 12° (bục cao 2.0 m → y = 1.956) |
| Hướng nhân vật | Sprite quay **phải**, chéo 25° về camera; quay trái = lật ngang (flipX) |

Đầu ra trong `UnityProject/Assets/Art/Sprites/`:
- `Props/<key>/`: `<key>.png` (nằm sàn, pivot = điểm chạm sàn), `<key>_held.png` (pivot = điểm cầm, đầu đánh hướng lên),
  `<key>_broken_NN.png` (mảnh vỡ, kèm `offset_m`), `<key>_dented.png`, `<key>.sprites.json`.
- `Characters/char_proto_p{1,2}/`: sprite idle + vị trí socket tay (`hand_r_px`).
- `Stages/ChoDem/`: 4 lớp parallax cùng khung 8192 px (`bg`, `mid`, `play`, `fg`) + `chodem_layout.json`
  (vùng đấu, 2 bục one-way, tường biên, spawn, hazard, hệ số parallax gợi ý). Đọc bằng `StageLayout2D.Parse()`.

Unity: `SpriteAssetPostprocessor` tự đặt Sprite, PPU, pivot từ JSON (sửa JSON → ảnh tự import lại).
Ảnh xem trước bố cục 2D: `ArtSource/Renders/stage_chodem/chodem_2d_composite.png`.

**Phần 3D bên dưới (FBX, collider 3D, PropAssetInfo, mảnh vỡ PhysX) giờ chỉ còn giá trị tham khảo**; có thể dừng xuất FBX vào Unity.
Animation nhân vật vẫn lấy từ Mixamo: rig model 3D → render từng frame ra sprite sheet (bước tiếp theo).

## Đã xong (tracker Asset Bible mục 8)

| Asset | Script | FBX (Unity) | Tam giác / ngân sách |
|---|---|---|---|
| palette.png + template_asset.blend | `make_template.py` | `Art/Textures/palette.png` | – |
| Ghế nhựa đỏ (Vừa, Cùn) | `prop_ghe_nhua.py` | `Art/Props/prop_ghe_nhua.fbx` | 384 / 500 · 6 mảnh vỡ |
| Chai thủy tinh (Nhỏ, Dễ vỡ) | `prop_chai_thuy_tinh.py` | `Art/Props/prop_chai_thuy_tinh.fbx` | 176 / 200 · 5 mảnh vỡ |
| Thùng phi (Lớn, Kim loại) | `prop_thung_phi.py` | `Art/Props/prop_thung_phi.fbx` | 632 / 800 · bản móp `_dented` |
| Nhân vật tạm capsule P1/P2 | `char_proto.py` | `Art/Characters/char_proto_p{1,2}.fbx` | 212 |
| Blockout Chợ Đêm + quầy hàng ×3 màu | `stage_chodem_blockout.py` | `Art/Stages/ChoDem/*.fbx` | mọi module ≤ 508 / 1.000 |

Ảnh duyệt: `ArtSource/Renders/` (`*_sheet.png` = trước · bên · 3/4 · tư thế cầm tay · mảnh vỡ).

## Cấu trúc mỗi prop (FBX)

```
prop_ghe_nhua            Empty gốc, pivot = điểm cầm
├─ ghe_nhua              mesh hiển thị (M_Palette)
├─ ghe_nhua_grip         Empty: +Z = hướng đầu đánh
├─ ghe_nhua_col          collider đơn giản (tắt MeshRenderer ở Unity)
└─ ghe_nhua_broken       Empty
   └─ ghe_nhua_broken_01..06   mảnh vỡ, cùng pivot gốc (ẩn mặc định)
```

Custom property trên Empty gốc (đọc qua `AssetPostprocessor.OnPostprocessGameObjectWithUserProperties`):
`prop_size`, `weapon_type`, `hit_dmg`, `throw_dmg`, `durability`, `traits`, `tri_count`,
`rest_euler_blender_deg` + `rest_offset_blender` (cách đặt prop đứng tự nhiên trên sàn).

## Quyết định cần biết

1. **Pivot / trục**: theo Asset Bible (mới hơn GDD) – pivot ở *mép trước mặt ghế*, không phải đáy chân như prompt mẫu trong GDD.
   Đầu đánh hướng +Z, nên ghế (xoay −139.5° X) và chai (lộn ngược) trong file nằm ở **tư thế cầm tay**.
   Khi spawn trên sàn, Unity xoay theo `rest_euler_blender_deg` (đã kiểm chứng bằng import ngược FBX trong ảnh `stage_chodem_*`).
   Thùng phi (cỡ Lớn) pivot ở tâm khối, không xoay.
2. **Collider**: chỉ để tham chiếu kích thước/PhysX cho mảnh vụn. GDD yêu cầu combat dùng AABB tự tính (tất định cho rollback) → lấy bounds của `_col` làm dữ liệu cho `PropData`.
3. **Blockout Chợ Đêm**: Asset Bible gợi ý dựng bằng ProBuilder trong Unity; ở đây dựng sẵn trong Blender để có ngay sân thử.
   Vùng đấu X −12..12, sâu Y −1.5..1.5, sàn Z = 0; 2 mái hiên làm bục cao 2.0 m tại X = ±6 (rộng 4 m).
   Marker: `spawn_p1/p2` (X = ∓4), 8 `spawn_prop_xx` đối xứng (vật lớn ở giữa X = ±1.3), `hazard_xemay_start/end`.
   Cột trước của mái hiên đứng ở mép trước làn đấu – nếu che nhân vật thì dời ra sau ở `stage_chodem_blockout.py`.
4. **Vật liệu**: FBX không nhúng ảnh. Trong Unity tạo `Materials/M_Palette` (URP Lit/Unlit, texture `palette.png`,
   Filter Mode = **Point**, Compression = None, tắt mipmap) và remap mọi model vào đó.

## Import vào Unity (tự động)
`UnityProject/Assets/Editor/Art/ArtAssetPostprocessor.cs` tự xử lý mọi file trong `Assets/Art/`:

| Đối tượng | Việc postprocessor làm |
|---|---|
| `palette.png` | Point filter, không nén, không mipmap; lần đầu tự tạo `Art/Materials/M_Palette.mat` |
| mọi `.fbx` | Scale 1, Normals = Import, không material nhúng → gán `M_Palette`; dựng lại cây phân cấp từ `fbx_parent` |
| `*_col` | bỏ renderer → `MeshCollider` convex (prop) / `BoxCollider` (stage) |
| `*_broken` | ẩn; mỗi mảnh có `Rigidbody` kinematic + `BoxCollider` (`PropAssetInfo.Shatter()` bật lên) |
| `*_dented` | ẩn (`PropAssetInfo.ShowDented()`) |
| `prop_*.fbx` | `PropAssetInfo` trên root + tạo/cập nhật `Assets/Data/Props/PropData_*.asset` |
| `char_*.fbx` | `CharacterSockets`; nhân vật rig Mixamo (không phải `char_proto`) → Rig = Humanoid |
| `stage_*.fbx` | `StageMarker` cho `spawn_p*`, `spawn_prop_*`, `hazard_*`; `StageLayout` trên root; xoay root 180° Y |

- **PropData**: nhóm *Cân bằng* (cỡ, loại, sát thương, độ bền) chỉ được điền **lần đầu** từ Blender, sau đó chỉnh trong Inspector,
  import lại không ghi đè. Nhóm *Từ Blender* (model, grip, AABB, tư thế nằm sàn) luôn cập nhật.
- **Quy ước trục trong Unity**: nhân vật nhìn +Z, đầu đánh của vũ khí +Y, stage nhìn từ camera ở −Z (hậu cảnh ở +Z, P1 bên trái).
- **Đổi hệ trục Blender → Unity**: (x, y, z) → (−x, z, −y), cài trong `BlenderSpace.cs`.
- Sửa logic postprocessor thì tăng `Version` để Unity import lại mọi model.

Kiểm tra: menu **Loan Dau → Art → Kiểm tra asset**, hoặc chạy batch:

```bash
/Applications/Unity/Hub/Editor/6000.6.11f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD/UnityProject" -executeMethod LoanDau.EditorTools.ArtImportSelfTest.RunBatch -logFile -
```

## Chưa làm
- Animation chung từ Mixamo trên nhân vật mẫu Mixamo (tracker Giai đoạn 1) – cần tài khoản Adobe.
- Project Unity dùng **URP 17.6** (`Assets/Settings/URP_Pipeline.asset`); `M_Palette` dùng shader URP/Lit.
  Thiết lập lại bằng menu **Loan Dau → Setup → Chuyển sang URP** (an toàn khi chạy nhiều lần).
