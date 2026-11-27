# Yêu cầu asset còn thiếu – Loạn Đấu Phố Đêm

Cập nhật: 27/11/2026. Tài liệu này gom **mọi asset cần thiết kế thêm** để hoàn thiện gameplay. Phần nào đã có asset thì
engine đã dùng (âm thanh trận, mối nguy môi trường của 4 stage) – xem mục 8.

Chi tiết tư thế combo / trên không vẫn theo `SPEC_combo_va_tren_khong.md`; tài liệu này nhắc lại phần cần thiết và bổ sung
những gì spec đó chưa có (kỹ năng khi đang bay, âm thanh, stage).

## 0. Tổng quan và thứ tự ưu tiên

| # | Hạng mục | Ai | Số lượng | Ưu tiên | Tình trạng |
|---|---|---|---|---|---|
| A | Combo J‑J‑J nối liền + đòn mạnh riêng | 6 nhân vật | 36 clip (~260 frame) | **Cao** | ✅ đã giao, engine đã dùng (`light_*_end`, `heavy`) |
| B | Đánh trên không, đá bổ, bị dây kéo | Long, Bảo, Bà Tư | 3 sheet `air_<nv>.png` (= 24 clip) | **Cao** | ✅ đã giao, engine đã dùng |
| C | Kỹ năng tung khi đang bay | Linh, Mai, Khang, Bà Tư, Bảo | 5 sheet `cast_*.png` (= 48 clip) | Trung bình | ✅ đã giao, engine đã dùng |
| D | Âm thanh còn thiếu (SFX chiêu, vung đòn, UI, còi xe…) | – | ~90 file | Trung bình | ✅ phần lớn đã giao và dùng; **còn thiếu** xướng trận (mục 5.4) và tiếng gồng nộ |
| E | Thoại thu âm thật | 6 nhân vật | 42 câu | Thấp (trước phát hành) | ❌ chưa có – vẫn giọng đọc máy |
| F | Stage: bệ nhảy, lớp tiền cảnh, mưa | 4 stage | xem mục 6 | Thấp | ⚠️ double jump + bệ đã có trong code; **cần vẽ lại 3 nền** cho bệ thấp xuống + clip `double_jump` – mục 6.2 |
| G | Sprite vật hư hỏng cỡ nhỏ | 12 vật | 12 ảnh | Thấp | ✅ đã giao, engine đã dùng |
| H | Gồng nộ (`rage_charge_*` + hào quang) | 6 nhân vật | 3 clip + 3 FX / người | – | ✅ asset bổ sung ngoài spec, engine đã dùng (giữ Đỡ + Kỹ năng) |

## 1. Quy chuẩn chung cho hình nhân vật

- Khung mỗi frame **224 × 176 px**, ghép ngang thành dải `<clip>.png` rộng `224 × số frame`.
- **Điểm chân (pivot) tại (80, 160)** tính từ góc trên‑trái ô; nhân vật nhìn sang **phải**.
  Frame trên không vẫn vẽ chân thấp nhất ở dòng 160 – engine tự nâng sprite theo độ cao nhảy.
- Cùng bảng màu (`Assets/Art/PixelPack/Palette`), cùng độ dày viền, cùng tỷ lệ người như `idle_fight`.
- PPU 64, lọc Point, không nén (importer tự đặt). `<clip>.anim` 60 mẫu/giây, key theo **tick** (1/60 s).
- Frame ra đòn: tay / chân / vũ khí phải **vươn xa nhất** ở đúng frame ghi trong bảng – hitbox được đo từ phần vươn ra.
- Không vẽ hiệu ứng tách rời (đạn, lửa, dây, giàn giáo…) dính vào người nếu engine đã vẽ riêng (xem mục C).

## 2. (A) Combo liền mạch + đòn mạnh – cả 6 nhân vật

Thư mục: `Assets/Art/PixelPack/BasicActions/<nv>/` (`long`, `bao`, `linh`, `mai`, `khang`, `ba_tu`).
Engine đã hỗ trợ sẵn `_end` (chỉ phát khi người chơi **không** bấm tiếp).

| Clip | Frame | Tick mỗi frame | Ra đòn | Tư thế |
|---|---|---|---|---|
| `light_1` *(vẽ lại)* | 6 | 2,2,2,3,3,3 | 2–3 | Đứng thủ → đòn 1 → **dừng ở tư thế nối A**, không về đứng thủ |
| `light_1_end` *(mới)* | 3 | 3,3,4 | – | Tư thế nối A → `idle_fight` frame 0 |
| `light_2` *(vẽ lại)* | 6 | 2,2,2,3,3,3 | 2–3 | Bắt đầu đúng tư thế A → đòn 2 (tay/chân còn lại) → dừng ở tư thế nối B |
| `light_2_end` *(mới)* | 3 | 3,3,4 | – | Tư thế nối B → đứng thủ |
| `light_3` *(vẽ lại)* | 8 | 2,2,3,3,4,4,3,3 | 3–4 | Bắt đầu đúng tư thế B → đòn kết thúc mạnh, có vệt gió → về đứng thủ |
| `heavy` *(vẽ lại)* | 9 | 5,5,4,4,4,4,5,4,4 | 4–5 | Khác hẳn `light_3`; 3 frame đầu lấy đà rõ |

| Nhân vật | light_1 → light_2 → light_3 | heavy |
|---|---|---|
| Long (Vovinam) | đấm thẳng tay trước → đấm móc tay sau → đá vòng cầu | đấm thẳng tấn mã (bước tới, tay sau) |
| Bảo (công nhân, búa tạ) | tát trái → móc phải → thúc vai | đập hai tay chụm xuống đất |
| Linh (parkour) | chưởng nhanh → gối → đá xoay sau | đá lộn nhào (cartwheel) |
| Mai (đầu bếp, chảo) | chọc chảo → vụt chảo ngược → bổ chảo từ trên | vụt chảo vòng rộng |
| Khang (thợ điện) | đấm → chọc kìm → quất dây cáp | quất dây cáp 2 vòng |
| Bà Tư (gậy trúc, quạt) | chọc gậy → quét gậy ngang → bổ gậy | đâm gậy thẳng tầm xa |

Mỗi clip: `<clip>.png` + `<clip>.anim` + dòng `"<clip>": {"ticks": [...], "loop": false}` trong `basic-timings.json`.
Sprite cắt tên `<clip>_00`, `<clip>_01`, …

## 3. (B) Đòn trên không – Long, Bảo, Bà Tư

Khang, Linh, Mai đã xong. Vẽ **1 sheet cho mỗi người**: `ArtSource/PixelPack/AerialSheets/air_<nv>.png`
(giống `air_khang.png`: **4 cột × 6 hàng**, khoảng 1122 × 1402 px, nền trong suốt, vẽ phân giải cao – script tự thu về
đúng cỡ và ép bảng màu). Mỗi hàng 4 tư thế liên tiếp:

| Hàng | Nội dung | Sinh ra clip | Ghi chú |
|---|---|---|---|
| 1 | Đánh nhẹ trên không (J khi nhảy) | `air_light` | frame 3 vươn xa nhất |
| 2 | Đấm khi đang rơi | `fall_punch` | hướng chéo xuống |
| 3 | Đánh mạnh trên không (K khi nhảy) | `air_heavy` | vươn xa, phải đỡ đứng |
| 4 | Đá bổ khi rơi (↓+K) | `fall_kick`, `air_dive` (cột 1–3), `air_dive_land` (cột 4) | cột 3 là tư thế lao 45° giữ được lâu; cột 4 = chạm đất |
| 5 | Bị dây cáp của Khang kéo về phía trước | `pulled_front` | người chúi về trước, chân rời đất |
| 6 | Loạng choạng sau khi bị kéo tới | `pulled_recover` | về lại thế thủ ở cột 4 |

| Nhân vật | air_light | air_heavy | air_dive |
|---|---|---|---|
| Long | gối bay | đá bay ngang | cước bổ chéo xuống |
| Bảo | huých chỏ xuống | đè cả người về trước | rơi dậm đất 2 chân |
| Bà Tư | phẩy quạt | quét gậy trên không | chống gậy lao xuống |

## 4. (C) Kỹ năng khi đang bay – 5 nhân vật

Tung kỹ năng (L) giữa lúc nhảy hiện dùng clip đứng đất. Vẽ 1 sheet **4 cột × 5 hàng** (khoảng 1122 × 1402 px), mỗi hàng
một chiêu **theo đúng thứ tự dưới đây**, nhân vật **đang lơ lửng** (chân không chạm đất), **không vẽ vật thể/hiệu ứng**
(đạn, lửa, dây, giàn giáo… engine vẽ riêng). Script tạo bản `rise` (đang bay lên) và `fall` (đang rơi) từ cùng 4 tư thế.

| File | Hàng 1 (L) | Hàng 2 (→+L) | Hàng 3 (↓+L) | Hàng 4 (↑+L) | Hàng 5 (K+L) |
|---|---|---|---|---|---|
| `cast_linh.png` | Phi Tiêu: vung tay ném quạt 3 tiêu | Lướt Bóng: thân nằm ngang lao tới | Móc Túi: vươn tay chộp | Đạp Trăng: xoay người đá lên | Vạn Ảnh: tư thế dồn lực rồi lao |
| `cast_mai.png` | Tạt Dầu Sôi: vung chảo hất cong | Xào Lửa: đẩy chảo ra trước (lửa engine vẽ) | Bình Gas Mini: thả bình xuống | Lật Chảo: giơ chảo chắn chéo lên | Lẩu Địa Ngục: hai tay úp nồi xuống |
| `cast_khang.png` | Kìm Điện: đâm kìm tới | *(Dây Cáp: engine dùng đấm trên không – vẽ tư thế ném bất kỳ để giữ bố cục)* | Cọc Tiếp Địa: cắm cọc chéo xuống | Chập Mạch: dang tay, người co lại | Mất Điện Toàn Phố: giơ kìm lên trời |
| `cast_ba_tu.png` | Quạt Phong: phẩy quạt rộng | Bước Hạc: chống gậy vọt qua | Gậy Quét: quét gậy thấp chéo xuống | Thế Chờ: thủ gậy trước ngực | Thập Bát Côn: xoay gậy liên hoàn |
| `cast_bao_air.png` | Quật Đất: giơ búa đập xuống | Húc Vai: vai lao tới | Bẻ Khóa: hai tay chộp | Quăng Búng: bắt và quật | Sập Giàn Giáo: vẫy tay gọi |

Bảo đã có sheet đứng đất `cast_bao.png` → sheet bay đặt tên **`cast_bao_air.png`** (script đã hỗ trợ hậu tố `_air`).

## 5. (D, E) Âm thanh còn thiếu

Định dạng giống bộ hiện có: **WAV mono 16‑bit 22 050 Hz**, cắt sát đầu, không reverb dài. Đặt trong
`Assets/Audio/PixelPack/<thư mục>/`. Mỗi SFX nên có 2–3 biến thể (`tên1.wav`, `tên2.wav`…) để tránh lặp.

### 5.1 SFX chiến đấu chung (`SFX/`)

| File | Dài | Dùng khi |
|---|---|---|
| `whoosh_light1..3` | 0.15 s | vung đòn nhẹ (kể cả trượt) |
| `whoosh_heavy1..3` | 0.3 s | vung đòn mạnh / vũ khí lớn |
| `block1..3` | 0.2 s | đỡ trúng (hiện dùng tạm hit_light đổi cao độ) |
| `guard_break` | 0.6 s | vỡ thế đỡ |
| `land1..2`, `bodyfall1..2` | 0.3 / 0.5 s | tiếp đất sau nhảy / ngã xuống sàn |
| `roll` | 0.35 s | lăn né |
| `pickup`, `throw1..2` | 0.2 / 0.3 s | nhặt / ném vật |
| `ko_impact` | 1.0 s | đòn kết liễu (có đuôi vang) |
| `super_flash` | 0.8 s | màn hình tối khi tung tuyệt chiêu |
| `brick_break` | 0.7 s | gạch vỡ (hiện dùng wood_break) |

### 5.2 SFX kỹ năng (`SFX/Skills/<nv>/<chiêu>.wav`) – 30 file

| Nhân vật | Âm thanh từng chiêu (L · →+L · ↓+L · ↑+L · K+L) |
|---|---|
| Long | luồng khí bay · gió cước bay · đòn chân kẹp + bụi · đấm thăng long (gió xoáy lên) · long hổ hội (liên hoàn + tiếng gầm) |
| Bảo | búa đập đất + sóng chấn động · húc vai nặng · bẻ khóa (rắc khớp) · quăng quật xuống đất · giàn giáo sắt đổ loảng xoảng |
| Linh | 3 phi tiêu xé gió · lướt (vút) · móc túi (sột soạt) · đạp trăng xoáy · vạn ảnh (nhiều vút chồng nhau) |
| Mai | dầu sôi xèo xèo · phun lửa liên tục (có đoạn lặp) · đặt bình gas (cạch) · lật chảo (keng) · lẩu sôi ùng ục + lửa lớn |
| Khang | kìm điện tạch · quăng dây (vút) + cuộn dây · cắm cọc + tia điện · vòng điện ù ù · cầu dao sập + bóng tối |
| Bà Tư | quạt gió mạnh · nhảy hạc + gậy chạm đất · gậy quét sát sàn · thế chờ (tiếng gậy xoay nhẹ) · 18 đòn gậy liên hoàn |

### 5.3 Mối nguy môi trường + nền (`SFX/Hazards/`, `Ambience/`)

| File | Dài | Dùng khi |
|---|---|---|
| `moto_horn` | 2.0 s | biển báo xe máy (2 giây trước khi xe lao vào) – **hiện im lặng** |
| `moto_pass` | 1.5 s | xe chạy ngang qua sân (có hiệu ứng doppler) – **hiện im lặng** |
| `crane_chain` | 1.5 s | cần cẩu hạ móc (hiện tạm metal_hit) |
| `brick_impact` | 0.8 s | gạch rơi chạm đất |
| `burner_ignite` / `burner_roar` | 0.5 / 1.5 s | bếp bắt đầu bùng / đang bùng (hiện tạm fire) |
| `powerbox_hum` (lặp) / `powerbox_zap` | 2 s / 0.8 s | tủ điện ù ù / phóng điện |
| `Ambience/rain` (lặp) | 8 s | trời mưa ở Sân Thượng (phát chồng lên nền) |

### 5.4 Giao diện + xướng trận (`UI/`, `Announcer/`)

- `UI/move`, `UI/confirm`, `UI/cancel`, `UI/stage_pick` (0.1–0.4 s).
- `Announcer/`: `round_1`, `round_2`, `round_3`, `round_final`, `fight` ("Đánh!"), `ko` ("K.O.!"), `time_over` ("Hết giờ!"),
  `draw` ("Hoà!"), `perfect` (tuỳ chọn), `win_<nv>` ("Long thắng!" × 6). Một giọng xướng thống nhất, có năng lượng.

### 5.5 Thoại nhân vật (`Voices/<nv>/00..06.wav`) – thu âm thật

Hiện là giọng đọc máy để canh nhịp. Engine đang gán (đổi được trong `Assets/Data/AudioLibrary.asset`):

| File | Engine dùng cho | Gợi ý nội dung | Dài |
|---|---|---|---|
| `00` | tung kỹ năng (L) | tiếng hô ngắn "Hà!", "Đỡ này!" | ≤ 0.6 s |
| `01` | vào trận | câu chào sân | ≤ 1.5 s |
| `02` | bị hạ gục | tiếng kêu khi K.O. | ≤ 1.0 s |
| `03` | tung tuyệt chiêu | tên tuyệt chiêu ("Long Hổ Hội!") | ≤ 1.5 s |
| `04` | *(chưa dùng)* | bị đòn mạnh | ≤ 0.5 s |
| `05` | *(chưa dùng)* | khiêu khích | ≤ 1.5 s |
| `06` | thắng trận | câu thắng | ≤ 2.5 s |

**Cần xác nhận:** nội dung thật của 7 file thoại hiện có – nếu khác bảng trên, sửa chỉ số trong `AudioLibrary.asset`.

> Khi giao 5.1–5.4: cần thêm trường tương ứng trong `AudioLibrary` và lời gọi trong `Scripts/Game/MatchAudio.cs`
> (phần dùng tạm đã đánh dấu chú thích "chưa có tiếng còi", "tạm" trong code).

## 6. (F) Stage

| Stage | Thiếu | Cần vẽ / làm |
|---|---|---|
| Chợ Đêm | 2 mái hiên để nhảy lên (GDD) | tách mái hiên khỏi `Stages/chodem.png` thành sprite riêng + toạ độ mặt trên (px) để làm bệ; lớp tiền cảnh nếu muốn người đứng sau cột |
| Công Trường | nhiều tầng giàn giáo, sàn không đều | sprite từng tầng giàn giáo + toạ độ mặt sàn; bóng đổ tròn rời (hiện dùng bóng vẽ sẵn trong `hazard.png`) |
| Bếp Quán Lẩu | – | (tuỳ chọn) bản bếp tắt hẳn để vẽ lúc nghỉ – hiện dùng frame tàn lửa nhỏ |
| Sân Thượng | bệ cao thấp, bồn nước | sprite bệ + toạ độ; (tuỳ chọn) bọt nước bắn khi tiếp đất lúc mưa, sàn ướt phản sáng |

### 6.1 Double jump + bệ một chiều (đã làm trong code – 27/11/2026)

- Nhảy lần 2 giữa không (bấm nhảy lần nữa), đổi được hướng bay; lần nhảy 2 được thêm 1 đòn trên không. Cả 6 nhân vật
  (tắt riêng bằng `canDoubleJump` trong `Fighter_<nv>.asset`).
- Bệ một chiều: nhảy xuyên từ dưới lên, đáp từ trên xuống, **↓ + nhảy** để xuống, đi ra mép thì rơi.
  Toạ độ đọc từ `stage-platforms.json` (`Scripts/Core/FightSim.Platforms.cs`).
- Tầm nhảy (px trên mặt sàn): Bảo 57 / 115 · Bà Tư 64 / 128 · Long, Khang, Mai 71 / 142 · Linh 87 / 173 (1 lần / 2 lần).
  → Bệ đặt **≤ 105 px**: nhảy 1 lần không tới, nhảy 2 lần thì cả 6 người tới được.

**Tạm thời:** bệ vẽ trên nền đang cao 123–340 px (không ai tới được), nên builder hạ bệ xuống mức ở bảng dưới và
**vẽ thêm sprite bệ ở chỗ mới** → hiện trên màn hình mỗi bệ thấy 2 lần (bản cũ trên nền + bản mới đứng được).
Hết trùng khi giao nền vẽ lại (mục 6.2).

### 6.2 Sprite cần vẽ / sửa

**Bắt buộc – vẽ lại 3 ảnh nền** `Stages/<stage>.png` (768 × 432, mặt sàn ở dòng y = 372 tính từ trên xuống): dời bệ xuống
đúng độ cao mới, xoá bệ ở chỗ cũ (vẽ lại tường / trời phía sau cho liền). Mặt bệ phải là **một đường ngang phẳng**,
đủ dày để chân đứng lên nhìn chắc.

| Stage | Bệ | Mặt bệ hiện tại (y trên ảnh) | Mặt bệ mới (y trên ảnh) | Cao hơn sàn | Chiều ngang mặt bệ (x trên ảnh) |
|---|---|---|---|---|---|
| Chợ Đêm | `awning_cart` (mái hiên xe đẩy) | 159 | **272** | 100 px | 171 – 322 |
| Chợ Đêm | `awning_stall` (mái hiên sạp) | 151 | **268** | 104 px | 665 – 767 |
| Công Trường | `scaffold_low` (giàn giáo thấp) | 171 | **272** | 100 px | 1 – 161 |
| Công Trường | `scaffold_upper` (giàn giáo cao) | 32 | **222** | 150 px | 74 – 248 |
| Sân Thượng | `parapet_platform` (lan can) | 249 | **272** | 100 px | 313 – 554 |
| Sân Thượng | `roof_ledge` (gờ mái) | 134 | **245** | 127 px | 0 – 173 |
| Sân Thượng | `tank_platform` (bồn nước) | 38 | **222** | 150 px | 94 – 166 |

- Giữ chiều ngang như cũ được thì code không phải sửa; đổi chiều ngang thì cập nhật đa giác trong `Tools/build_stage_layers.py`.
- Bệ 150 px (giàn giáo cao, bồn nước) chỉ lên được từ bệ thấp bên cạnh → nên để hai bệ chồng lên nhau theo chiều ngang
  ít nhất ~40 px (giàn giáo cao 74–248 đã chồng lên giàn giáo thấp 1–161).
- Người đứng dưới bệ 100 px vẫn đi qua lại bình thường (bệ nằm sau lưng nhân vật), nên phần dưới mặt bệ vẽ mỏng / thoáng
  (mái tôn, sàn giàn giáo, gờ bê tông) – khối đặc cao trông như người đi xuyên tường.
- Bếp Quán Lẩu: không có bệ (giữ nguyên). Muốn thêm thì vẽ 1–2 kệ / bàn ở ≤ 105 px và thêm vào `stage-platforms.json`.

Sau khi có nền mới: sửa toạ độ trong `Tools/build_stage_layers.py` → chạy lại script (sinh `StageModules` + `stage-platforms.json`)
→ Unity: **Dựng dữ liệu + scene**. Builder tự nhận bệ ≤ 105 px là "đã nằm đúng trên nền" và thôi vẽ sprite bệ thừa.

**Nên có – clip nhảy lần 2** `BasicActions/<nv>/double_jump.png` + `.anim`, cả 6 nhân vật (hiện dùng lại `jump_start`):

| | |
|---|---|
| Khung | 224 × 176, pivot (80, 160), nhìn sang phải – như mọi clip BasicActions |
| Số frame | 4–6, tổng ~16–20 tick (0.3 giây), không lặp; hết clip engine chuyển về `jump_air` |
| Dáng | bật người lần 2 giữa không: co gối lộn 1 vòng nhanh (Linh, Mai, Long) hoặc đạp chân vào không khí, tay vung lên (Bảo, Bà Tư, Khang) |
| Lưu ý | không vẽ bụi / vệt gió dính vào người; chân thấp nhất vẫn ở dòng 160 |
| Thời gian | thêm `"double_jump"` vào `ArtSource/PixelPack/basic-timings.json` |

**Tuỳ chọn:** tiếng nhảy lần 2 `Audio/PixelPack/SFX/double_jump.wav` (hiện mượn `whoosh_light1`).

`drop_shadow.png` (Công Trường) chưa dùng: `hazard.png` đã vẽ sẵn bóng trong cả 8 frame, thêm nữa sẽ thành 2 bóng.

## 7. (G) Nâng chất lượng (tuỳ chọn)

- `DamagedProps` cỡ nhỏ cho sàn: 12 ảnh `<vật>_damaged_floor.png`, cùng tỷ lệ `PixelPackDerived/FloorProps/<vật>.png`
  (ghế ~21 px cao, thùng phi ~48 px) – thay bản thu nhỏ tự động bị mềm nét.
- Biển báo xe máy riêng (`Hazards/chodem/warning.png`, ~40 × 40 px, 2 frame nhấp nháy) – hiện engine cắt từ frame 0 của
  `hazard.png`.

## 8. Đã có asset và engine đã dùng (không cần vẽ thêm)

- **Âm thanh trận**: nhạc menu + nhạc/nền 4 stage; SFX đánh nhẹ/mạnh, đỡ, vỡ thế đỡ, nổ, lửa, điện, va chạm và vỡ theo
  vật liệu (kim loại / gỗ / nhựa / thuỷ tinh); thoại vào trận / kỹ năng / tuyệt chiêu / K.O. / thắng
  (`Scripts/Game/MatchAudio.cs`, `Assets/Data/AudioLibrary.asset`).
- **Mối nguy môi trường** (`Scripts/Core/FightSim.Hazards.cs`, hình `Hazards/<stage>/hazard.png`):
  Chợ Đêm xe máy mỗi 30 giây + biển báo 2 giây · Công Trường cần cẩu thả gạch vào chỗ có bóng đổ mỗi 18 giây ·
  Bếp bùng lửa mỗi 12 giây, đốt người và vật Dễ cháy gần bếp · Sân Thượng mưa 20 giây (sàn trơn), tủ điện bị đánh → phóng điện
  (mưa thì lan xa hơn, biến vũng nước thành vùng giật).
- **Đợt giao 27/11/2026**: combo nối liền + đòn mạnh, đòn trên không Long/Bảo/Bà Tư, kỹ năng khi bay 5 nhân vật, SFX chiêu /
  vung đòn / đỡ / tiếp đất / ném / UI / mối nguy / mưa, vật hư hỏng cỡ nhỏ, biển báo xe máy 2 frame,
  giàn giáo chạm đất + đổ sụp (`CombatFX/scaffold_impact`), gồng nộ + hào quang 6 nhân vật.

## 9. Nhập asset và kiểm tra

```bash
python3 Tools/import_aerial_sheets.py      # sheet air_<nv>.png, cast_<nv>.png, cast_<nv>_air.png
python3 Tools/analyze_skill_frames.py      # đo lại hitbox từ hình mới
```

Trong Unity:
1. **Loan Dau → PixelPack → Dựng dữ liệu + scene** – dựng dữ liệu, chạy self‑test (có kiểm tra mối nguy + AudioLibrary).
2. **Loan Dau → PixelPack → Dựng + kiểm tra đòn trên không & dây kéo** – log `AERIAL_ART_MISSING` phải là `none`.
