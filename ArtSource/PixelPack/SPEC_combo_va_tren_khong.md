# Yêu cầu asset: combo liền mạch · đánh trên không · đá bổ khi rơi

Bổ sung cho `PixelPack/BasicActions/<nhân vật>/` – cả 6 nhân vật: `long`, `bao`, `linh`, `mai`, `khang`, `ba_tu`.
Engine đã làm sẵn: thả file vào đúng tên → chạy menu **Loan Dau → PixelPack → Dựng dữ liệu + scene** là dùng được
(thiếu file nào thì engine tự dùng hình tạm cho clip đó).

## 1. Vì sao cần vẽ lại

| Vấn đề hiện tại | Hệ quả |
|---|---|
| `light_1`, `light_2`, `light_3` đều **bắt đầu và kết thúc ở tư thế đứng thủ** | Nối J-J-J thì nhân vật "về đứng → ra đòn → về đứng", giật, không có lực nối |
| `light_1` = `punch`; `light_3` = `heavy` = `kick` (cùng một cú đá ngang) | Combo đơn điệu, đòn mạnh không khác đòn nhẹ cuối |
| Không có clip đánh khi đang nhảy / đang rơi | Bấm J trên không thì nhân vật đấm ở tư thế đứng mà lơ lửng |

## 2. Quy chuẩn kỹ thuật (giữ y như BasicActions hiện có)

- **Khung mỗi frame 224 × 176 px**, ghép ngang thành 1 dải: `<clip>.png` rộng `224 × số frame`.
- **Điểm chân (pivot) tại (80, 160)** tính từ góc trên-trái mỗi ô, tức pivot chuẩn hoá `(0.357, 0.091)`, nhân vật nhìn sang **phải**.
  - Frame trên không (`air_*`) cũng vẽ như frame đứng đất: **điểm thấp nhất của chân nằm ở dòng 160**, không tự vẽ nhân vật
    cao lên – độ cao bay do engine tính và tự nâng sprite lên.
- Cắt sprite trong `.png.meta`: tên `<clip>_00`, `<clip>_01`, … (như `light_1_00`).
- `<clip>.anim`: `m_SampleRate: 60`, key theo **tick** ở bảng dưới (1 tick = 1/60 s), không lặp (trừ khi ghi "lặp").
- Thêm dòng vào `basic-timings.json`: `"<clip>": {"ticks": [...], "loop": false}`.
- Cùng bảng màu, cùng độ dày nét viền, cùng tỷ lệ người như `idle_fight`.

## 3. Combo liền mạch J → J → J

**Nguyên tắc:** frame cuối của đòn trước **trùng tư thế** với frame đầu của đòn sau ("tư thế nối"). Phần thu tay về đứng thủ
tách thành clip `_end` riêng – engine chỉ phát khi người chơi **không** bấm tiếp.

```
light_1 ──(tư thế nối A)──▶ light_2 ──(tư thế nối B)──▶ light_3 (kết thúc về đứng thủ)
   └─ không bấm tiếp: light_1_end (A → đứng thủ)      └─ không bấm tiếp: light_2_end (B → đứng thủ)
```

| Clip | Frame | Tick mỗi frame | Ra đòn (frame) | Ghi chú tư thế |
|---|---|---|---|---|
| `light_1` | 6 | 2,2,2,3,3,3 | 2–3 | Đứng thủ → đòn 1 → **dừng ở tư thế nối A** (tay vẫn đang thu nửa chừng, trọng tâm dồn trước). KHÔNG về đứng thủ |
| `light_1_end` | 3 | 3,3,4 | – | Tư thế nối A → đứng thủ (`idle_fight` frame 0) |
| `light_2` | 6 | 2,2,2,3,3,3 | 2–3 | **Bắt đầu đúng tư thế nối A** → đòn 2 (tay/chân còn lại) → dừng ở tư thế nối B |
| `light_2_end` | 3 | 3,3,4 | – | Tư thế nối B → đứng thủ |
| `light_3` | 8 | 2,2,3,3,4,4,3,3 | 3–4 | **Bắt đầu đúng tư thế nối B** → đòn kết thúc mạnh, có vệt chém/vệt gió → thu về đứng thủ |
| `heavy` | 9 | 5,5,4,4,4,4,5,4,4 | 4–5 | **Khác hẳn `light_3`**: đòn nặng của riêng nhân vật (xem mục 5), có rung người lấy đà rõ ở 3 frame đầu |

Ra đòn ở frame nào thì tay/chân/vũ khí phải **vươn ra xa nhất** ở frame đó – engine đo hitbox từ chính phần vươn ra này.

## 4. Đánh trên không và đá bổ khi rơi

| Clip | Frame | Tick mỗi frame | Ra đòn | Bấm | Ghi chú tư thế |
|---|---|---|---|---|---|
| `air_light` | 6 | 2,2,3,3,4,4 | 2–3 | J khi đang nhảy | Đòn nhanh trên không (gối / đấm chéo xuống). Frame đầu nối từ `jump_air`, frame cuối quay lại dáng `jump_air` |
| `air_heavy` | 7 | 3,3,3,4,4,4,4 | 3–4 | K khi đang nhảy | Đòn mạnh trên không, vươn xa (đá bay ngang / vung vũ khí). Phải đỡ **đứng** |
| `air_dive` | 4 | 2,2,3,3 | 1–3 | ↓ + K (hoặc ↓ + J) khi **đang rơi** | Lao chéo xuống 45° (đá bổ / dậm / đập). Engine giữ frame cuối cho tới khi chạm đất → frame cuối phải là tư thế lao giữ được lâu |
| `air_dive_land` | 5 | 3,3,3,3,4 | – | tự động | Chạm đất sau `air_dive`: dậm chân, bụi, đứng dậy về thủ |

## 5. Gợi ý động tác riêng từng nhân vật

| Nhân vật | light_1 → light_2 → light_3 | heavy | air_light | air_heavy | air_dive |
|---|---|---|---|---|---|
| **Long** (Vovinam) | đấm thẳng tay trước → đấm móc tay sau → đá vòng cầu | đấm thẳng tấn mã (bước tới, tay sau) | gối bay | đá bay ngang | cước bổ chéo xuống |
| **Bảo** (công nhân, búa tạ) | tát trái → móc phải → thúc vai | đập hai tay chụm xuống đất | huých chỏ xuống | đè cả người về trước | rơi dậm đất (2 chân) |
| **Linh** (parkour) | chưởng nhanh → gối → đá xoay sau | đá lộn nhào (cartwheel) | gối trên không | đá xoay 360° | đá chéo 45° kiểu lướt |
| **Mai** (đầu bếp, chảo) | chọc chảo → vụt chảo ngược → bổ chảo từ trên | vụt chảo vòng rộng | đập chảo xuống | vụt chảo ngang | úp chảo đập đất |
| **Khang** (thợ điện, kìm + dây) | đấm → chọc kìm → quất dây cáp | quất dây cáp 2 vòng | đá thẳng | quất dây chéo | dậm xuống tóe tia lửa |
| **Bà Tư** (gậy trúc, quạt) | chọc gậy → quét gậy ngang → bổ gậy | đâm gậy thẳng tầm xa | phẩy quạt | quét gậy trên không | chống gậy lao xuống |

## 6. Danh sách file mỗi nhân vật

```
BasicActions/<nv>/light_1.png + .anim        light_1_end.png + .anim
BasicActions/<nv>/light_2.png + .anim        light_2_end.png + .anim
BasicActions/<nv>/light_3.png + .anim        heavy.png + .anim          (vẽ lại, ghi đè file cũ)
BasicActions/<nv>/air_light.png + .anim      air_heavy.png + .anim
BasicActions/<nv>/air_dive.png + .anim       air_dive_land.png + .anim
```

Tổng: 10 clip × 6 nhân vật = 60 clip (≈ 330 frame).

## 7. Sau khi thả file vào

```bash
python3 Tools/analyze_skill_frames.py     # đo hitbox từ hình mới
```

Rồi trong Unity: **Loan Dau → PixelPack → Dựng dữ liệu + scene**. Bộ kiểm tra sẽ báo clip nào còn đang dùng hình tạm.
