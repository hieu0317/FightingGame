# Loạn Đấu Phố Đêm – Prototype (PixelPack)

Game đối kháng 2D pixel art, Unity 6000.6 + URP. Đồ hoạ lấy từ bộ **PixelPack** (`ArtSource/PixelPack` = nguồn,
`UnityProject/Assets/Art/PixelPack` = bản đã nhập vào Unity). Pipeline 3D/prerender cũ đã cất ở `_Archive/old_assets_2026-11-26/`.

## Chạy thử
1. Mở `UnityProject` bằng Unity 6000.6, mở scene **Assets/Scenes/Prototype.unity**, bấm Play.
2. Đặt Game view 1920×1080 (hoặc 640×360) để pixel sắc nét.
3. **Màn chọn nhân vật**: P1 A/D/W/S + J chọn (K huỷ), P2 mũi tên + `,` (`.` huỷ), hoặc bấm chuột (P1 rồi P2) →
   chọn sân (mặc định sân nhà của P2) → trận **3 hiệp thắng 2, mỗi hiệp 99 giây** (hết giờ: ai nhiều % máu hơn thắng).
   Esc: về màn chọn · hết trận: Enter chọn lại / F5 đấu lại. Muốn vào trận ngay: tắt `startWithSelect` trên `Match`.

| | Di chuyển | Nhảy | Cúi | Đánh nhẹ / mạnh | Đỡ (+hướng = lăn) | Kỹ năng | Tuyệt chiêu | Nhặt / ném |
|---|---|---|---|---|---|---|---|---|
| P1 | A / D | W | S | J (J-J-J) / K | I | L (+ W / S / hướng tới) | K + L | U |
| P2 | ← / → | ↑ | ↓ | , / . | ' | / (+ ↑ / ↓ / hướng tới) | . + / | ; |

Đang cầm vật: J = vung vật. Đòn thường trúng có thể huỷ sang kỹ năng (bấm L ngay sau khi trúng).
Gồng nộ: giữ Đỡ rồi giữ thêm Kỹ năng (P1 I + L, P2 ' + /) – đứng yên tích năng lượng (~5 giây đầy thanh), không đỡ được đòn; nhả ra thì thu thế.
Trên không: J / K = đánh nhẹ / mạnh trên không (1 đòn mỗi lần nhảy, phải đỡ đứng); đang rơi + ↓ + K = đá bổ lao chéo xuống.
Nhảy lần 2: bấm nhảy lần nữa giữa không (đổi được hướng, thêm 1 đòn trên không). Bệ (Chợ Đêm, Công Trường, Sân Thượng):
nhảy 2 lần để lên, ↓ + nhảy để xuống, đi ra mép thì rơi.

Đòn trên không đã có hình cho Khang, Linh, Mai; Long, Bảo, Bà Tư vẫn dùng hình tạm (yêu cầu vẽ: `ArtSource/PixelPack/SPEC_combo_va_tren_khong.md`).

F1 / F2 đổi nhân vật P1 / P2 · F3 đổi stage · F5 chơi lại. Chế độ luyện tập (năng lượng vô hạn) bật mặc định trên `Match`.

## Đổi asset → dựng lại
Thêm/sửa ảnh trong PixelPack hoặc `catalog.json` → chạy

```bash
python3 Tools/import_aerial_sheets.py      # sheet vẽ tay AerialSheets/air_<nv>.png, cast_<nv>.png → clip chuẩn 224×176
python3 Tools/extract_floor_props.py
python3 Tools/analyze_skill_frames.py
```

`import_aerial_sheets.py`: `air_<nv>` (6 hàng: đấm trên không · đấm khi rơi · đá trên không · đá bổ khi rơi · bị kéo ·
loạng choạng sau khi bị kéo) → `BasicActions/<nv>/air_light, fall_punch, air_heavy, fall_kick, air_dive, air_dive_land,
pulled_front, pulled_recover`; `cast_<nv>` (5 hàng = 5 chiêu, vẽ KHÔNG kèm vật thể) → `AerialSkills/<nv>/<chiêu>/rise|fall`
(vẽ đang bay) hoặc `ground` (vẽ đứng đất). Vật thể rời `SkillObjects/` (giàn giáo, móc, đoạn dây cáp) do engine vẽ riêng:
dây co giãn từ tay người ném tới móc, giàn giáo rơi từ trên xuống và chỉ gây sát thương khi chạm đất.
Kiểm tra: menu **Loan Dau → PixelPack → Dựng + kiểm tra đòn trên không & dây kéo** (log `AERIAL_ART_MISSING` liệt kê hình còn thiếu).

(tạo sprite vật dụng nằm sàn đúng tỷ lệ người vào `Assets/Art/PixelPackDerived/FloorProps/` – ảnh `PixelPack/Props` vẽ kiểu icon
nên quá to khi đặt lên sàn; sprite sàn được tách từ chính các clip tương tác) → menu **Loan Dau → PixelPack → Dựng dữ liệu + scene**, rồi
**Loan Dau → PixelPack → Kiểm tra**. Chạy lại an toàn: clip và sprite được cập nhật, số liệu cân bằng bạn đã chỉnh trong Inspector được giữ.

| Thành phần | Vị trí | Ghi chú |
|---|---|---|
| Import pixel-perfect | `Editor/PixelPack/PixelPackTextureImporter.cs` | Mọi ảnh PPU 64, Point, không nén (stage trước để 32 → lệch cỡ pixel) |
| Dữ liệu | `Assets/Data/{Fighters,Props,Stages}` | Sinh từ `catalog.json` + thư mục PixelPack |
| Clip | `SpriteClip` trong FighterData | Đổi từ `.anim`, giữ nhịp frame gốc, tính theo tick (60/giây) |
| Mô phỏng | `Scripts/Core/FightSim.cs` | Tất định, số nguyên (1/16 px), `MatchState` sao chép được → nền cho rollback |
| Hiển thị + input | `Scripts/Game/MatchRunner.cs` | Chỉ đọc trạng thái; camera pixel-perfect 640×360 |

Quy ước toạ độ: 64 px = 1 unit; sàn ở y = 0; x = 0 giữa sân; ảnh stage 768×432 px = 12 × 6.75 unit, mặt sàn cách mép dưới ảnh 60 px
(`StageData.floorFromBottomPx`, chỉnh riêng từng stage nếu chân nhân vật chưa khớp mặt đường).

## Đã có
- 6 nhân vật, mỗi người 5 chiêu (16 frame) gán theo cột Input của catalog.
- Nhặt → cầm → vung / ném 12 vật dụng bằng clip tương tác riêng của từng nhân vật; trúng thì trừ máu theo bảng GDD;
  độ bền = số lần trúng, hết bền hoặc vật Dễ vỡ chạm đất → sprite `DamagedProps`. Xe máy: người khác đẩy lăn, Bảo nhấc lên ném được.
- 4 stage: người chơi xuất phát ±110 px, vật Lớn ở giữa, vũ khí Vừa sau lưng mỗi người (±175), vật ném gần mép (±260) – GDD mục 5.

## Chiến đấu (hitbox)
- Mỗi chiêu là 1 đòn 16 frame (file 0–3 = keyframe gốc, 4–15 = frame nội suy; thứ tự phát lấy từ `.anim`).
  `Tools/analyze_skill_frames.py` đo phần tay/chân/hiệu ứng vươn ra ở từng frame → cửa sổ ra đòn = các frame vươn ≥ 60% đỉnh,
  hộp = đúng phần vươn ra. Tuyệt chiêu nhiều đòn chia đều cửa sổ. Loại chiêu, sát thương, thuộc tính theo bảng GDD trong
  `Editor/PixelPack/PixelPackCombat.cs` (tăng `CombatVersion` khi đổi bảng; số chỉnh tay trong Inspector được giữ).
- Đỡ (giữ I; đòn thấp phải ↓+I), vỡ thế đỡ → choáng 1 s, ngã → dậy bất tử 0.5 s, combo giảm 10%/hit (tối thiểu 30%),
  tung tối đa 3 lần, năng lượng +5 đánh trúng / +3 bị đánh / +8 phản đòn (F6 tắt chế độ luyện tập để dùng năng lượng thật).
- Đạn (chưởng, phi tiêu, dây cáp, sóng đất), bẫy (bình gas mini, cọc tiếp địa), vùng (dầu sôi, lẩu địa ngục, sập giàn giáo),
  phản đạn (Lật Chảo, Quạt Phong), cướp vật (Móc Túi), phản đòn (Thế Chờ). **F4** hiện hitbox để căn chỉnh.

## Phản ứng thuộc tính (GDD mục 4)
| Thuộc tính | Gặp | Kết quả |
|---|---|---|
| Dễ cháy | lửa (chiêu Mai, vùng cháy, nổ) | cháy 4 s; đánh/ném trúng gây bỏng 10/s trong 3 s |
| Chất nổ | lửa · bị chiêu đánh trúng 2 lần · ném va chạm | nổ bán kính 150 px, 150 sát thương, hất tung CẢ HAI BÊN, kích nổ / đốt dây chuyền |
| Kim loại | điện (chiêu Khang, vùng giật, ái lực Khang trong 120 px) | nhiễm điện 5 s; trúng thì tê thêm 0.3 s |
| Chất lỏng | vỡ (hết độ bền) | vũng 10 s, trơn trượt; gặp điện → vùng giật; sơn gặp lửa → vùng cháy |
Ái lực: Mai ném vật gì cũng bốc cháy (`FighterData.throwsIgnite`), Khang nhiễm điện kim loại quanh mình (`electrifyRadiusPx`).
Hình lửa/điện/nổ/vũng: `Assets/Data/FxLibrary.asset` (từ `PixelPack/InteractionFX`).

## Động tác chung + hiệu ứng va chạm (BasicActions, CombatFX)
- 22 clip mỗi nhân vật: đứng thủ, đi tới/lùi, cúi, nhảy 3 pha, đỡ, đỡ trúng, lăn, bị đánh nhẹ/mạnh, choáng, ngã, đứng dậy,
  vào trận, thắng, thua (`FighterData.moves`). Đầu trận diễn clip vào trận rồi mới cho điều khiển; hết máu → K.O.
- Đòn thường `FighterData.normals`: hitbox đo từ dải sprite (frame 4–5 = lúc tay/chân duỗi), sát thương 25/25/40 (J) và 80 (K,
  phá thế đỡ ×3 + đánh bay).
- Tia lửa va chạm nhẹ/mạnh, chữ BỐP! (đòn mạnh) / RẮC! (vật vỡ), bụi tiếp đất, vòng sóng khi vỡ thế đỡ/nổ, mảnh vỡ văng ra,
  viền sáng nhấp nháy trên vật nhặt được. Viền + mảnh vỡ do `Tools/extract_floor_props.py` tạo cùng tỷ lệ sprite sàn.

## UI (UI Toolkit, `Scripts/UI/GameUI.cs`)
- HUD: chân dung, tên, thanh máu (vệt trễ khi mất máu, nháy khi dưới 25%), năng lượng 4 vạch (sáng khi đầy), thanh đỡ,
  chấm hiệp thắng, đồng hồ, số hit combo, banner HIỆP n / ĐÁNH! / K.O. / HẾT GIỜ / thắng trận, banner cắt cảnh tuyệt chiêu.
- Hình + font Inter (đủ dấu tiếng Việt) từ `PixelPack/UI` qua `Assets/Data/UiSkin.asset`; `Assets/UI/LoanDauPanel.asset`
  màn gốc 640×360 (phóng ×3 lên 1080p). Chân dung/banner/logo lọc Bilinear + mipmap (ảnh vẽ hiển thị thu nhỏ), còn lại Point.
- File `PixelPack/UI/FightHUD.uxml`, `HUD.uss`, `CharacterSelect.uxml` giữ nguyên làm tham khảo (giao diện dựng bằng code theo `hud_preview.png`).

## Âm thanh (`Scripts/Game/MatchAudio.cs`, `Assets/Data/AudioLibrary.asset`)
- Nhạc menu ở màn chọn; nhạc + âm nền theo stage trong trận. SFX đánh nhẹ/mạnh, đỡ, vỡ thế đỡ, ngã, nổ, lửa (vật/người bốc cháy),
  điện; vũ khí đánh trúng / vật vỡ phát tiếng theo vật liệu (kim loại, gỗ, nhựa, thuỷ tinh).
- Thoại: vào trận, tung kỹ năng, tuyệt chiêu, bị K.O., thắng trận – chỉ số file `Voices/<nv>/0x.wav` chỉnh trong `AudioLibrary`.
- Chỉ đọc sự kiện/trạng thái mô phỏng, không ảnh hưởng tính tất định. Trong Editor, chưa dựng lại dữ liệu thì tự nạp từ `Assets/Audio/PixelPack`.

## Mối nguy môi trường (`Scripts/Core/FightSim.Hazards.cs`, GDD mục 5)
Bật/tắt bằng `stageHazards` trên `Match` (self-test cũ chạy với mối nguy tắt). Hình: `PixelPack/Hazards/<stage>/hazard.png`.
| Stage | Mối nguy |
|---|---|
| Chợ Đêm | Từ giây 20, mỗi 30 giây xe máy chạy ngang (đổi chiều mỗi lượt), biển "!" nhấp nháy ở mép màn hình 2 giây trước. 90 sát thương + ngã, không đỡ được – nhảy qua để né. Vật dễ vỡ / vật nổ trên đường bị phá |
| Công Trường | Từ giây 12, mỗi 18 giây cần cẩu thả gạch vào chỗ người chơi đang đứng (luân phiên P1/P2), bóng đổ báo trước 1.5 giây. 120 sát thương + ngã, phá vật dễ vỡ / chất lỏng, kích nổ vật nổ |
| Bếp Quán Lẩu | Bếp giữa sân: từ giây 8, mỗi 12 giây lửa nhen 1 giây rồi bùng 1.5 giây – 25 sát thương + bỏng mỗi 0.5 giây, đốt vật Dễ cháy trong 60 px (không kích nổ bình gas) |
| Sân Thượng | Mưa 20 giây / tạnh 15 giây (bắt đầu giây 10): cả sân trơn. Tủ điện giữa sân bị đòn đánh trúng → phóng điện 60 sát thương + choáng trong 90 px (mưa: 130 px), biến vũng nước thành vùng giật; nghỉ 4 giây mới phóng lại |

## Chưa có (cần asset hoặc làm tiếp)
Danh sách asset cần thiết kế thêm + quy chuẩn: **`ArtSource/PixelPack/SPEC_asset_con_thieu.md`**.
- Hồi chiêu; bệ nhảy (mái hiên, giàn giáo) – cần hình + toạ độ trước.
- Sprite hư hỏng nằm sàn được thu nhỏ tự động từ `DamagedProps` (hơi mềm nét); nên vẽ bản nhỏ riêng nếu muốn sắc hơn.
