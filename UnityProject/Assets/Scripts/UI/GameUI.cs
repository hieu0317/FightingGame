using System;
using LoanDau.Core;
using LoanDau.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoanDau.UI
{
    /// <summary>
    /// UI Toolkit (Asset Bible mục 7): HUD trong trận + màn chọn nhân vật / sân.
    /// Toàn bộ cây giao diện dựng bằng code từ UiSkin (PixelPack/UI), toạ độ theo màn hình gốc 640×360
    /// (PanelSettings phóng ×3 lên 1080p → hình pixel sắc nét). Chỉ đọc trạng thái mô phỏng, không sửa.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameUI : MonoBehaviour
    {
        public UiSkin skin;

        UIDocument doc;
        VisualElement root, hud, select;

        // ------------------------------------------------------------------ HUD
        class Side
        {
            public VisualElement portrait, hpFill, hpTrail, energyFill, guardFill, cutIn;
            public Label name, combo;
            public VisualElement[] pips;
            public float trail = 1f, cutInStart = -10f;
            public int lastHp;
        }

        readonly Side[] sides = { new(), new() };
        Label clock, roundLabel, bannerTitle, bannerSub, hint;
        FightSim sim;

        public Action<string> PlaySelectSound;
        public bool SelectOpen => select != null && select.style.display == DisplayStyle.Flex;

        void OnEnable()
        {
            doc = GetComponent<UIDocument>();
        }

        VisualElement Root()
        {
            if (root != null) return root;
            doc ??= GetComponent<UIDocument>();
            root = doc.rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            if (skin && skin.font) root.style.unityFontDefinition = FontDefinition.FromFont(skin.font);
            root.style.color = skin ? skin.text : Color.white;
            return root;
        }

        // ---- tiện ích dựng phần tử
        static VisualElement Box(VisualElement parent, float w = -1, float h = -1)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            if (w >= 0) e.style.width = w;
            if (h >= 0) e.style.height = h;
            parent?.Add(e);
            return e;
        }

        static void Abs(VisualElement e, float? left = null, float? top = null, float? right = null, float? bottom = null)
        {
            e.style.position = Position.Absolute;
            if (left.HasValue) e.style.left = left.Value;
            if (top.HasValue) e.style.top = top.Value;
            if (right.HasValue) e.style.right = right.Value;
            if (bottom.HasValue) e.style.bottom = bottom.Value;
        }

        static void Img(VisualElement e, Sprite s, bool cover = false)
        {
            e.style.backgroundImage = s ? new StyleBackground(s) : new StyleBackground(StyleKeyword.None);
            e.style.backgroundSize = cover ? new BackgroundSize(BackgroundSizeType.Cover)
                                           : new BackgroundSize(Length.Percent(100), Length.Percent(100));
            if (cover) e.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top);
        }

        static Label Text(VisualElement parent, string text, float size, bool bold = false)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.textShadow = new TextShadow { offset = new Vector2(1, 1), blurRadius = 0, color = new Color(0, 0, 0, 0.85f) };
            parent?.Add(l);
            return l;
        }

        static void Border(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
        }

        /// <summary>
        /// Thanh: ảnh khung (PixelPack/UI/*_frame.png) là thanh RỖNG có ruột tối đặc → làm nền dưới cùng;
        /// vệt trễ + phần lấp đầy vẽ đè lên trên, lùi vào 1 px để viền sáng của khung vẫn hiện. P2 lấp từ phải sang.
        /// </summary>
        static VisualElement Bar(VisualElement parent, Sprite frame, Sprite fill, float w, float h, bool right,
                                 out VisualElement fillEl, Color? trailColor, out VisualElement trailEl)
        {
            var b = Box(parent, w, h);
            b.style.marginTop = 2;
            if (frame) Img(b, frame);
            else b.style.backgroundColor = new Color(0.12f, 0.08f, 0.12f, 0.9f);
            var inner = Box(b);
            Abs(inner, 1, 1, 1, 1);
            trailEl = null;
            if (trailColor.HasValue)
            {
                trailEl = Box(inner);
                Abs(trailEl, right ? null : 0, 0, right ? 0 : null, 0);
                trailEl.style.width = Length.Percent(100);
                trailEl.style.backgroundColor = trailColor.Value;
            }
            fillEl = Box(inner);
            Abs(fillEl, right ? null : 0, 0, right ? 0 : null, 0);
            fillEl.style.width = Length.Percent(100);
            if (fill)
            {
                // ảnh giữ nguyên kích thước thanh đầy, phần tử hẹp lại thì ảnh bị CẮT chứ không bị ép (vạch năng lượng đứng yên)
                Img(fillEl, fill);
                fillEl.style.backgroundSize = new BackgroundSize(new Length(w - 2), new Length(h - 2));
                fillEl.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left);
                fillEl.style.overflow = Overflow.Hidden;
            }
            else fillEl.style.backgroundColor = Color.white;
            if (right) fillEl.style.scale = new Scale(new Vector3(-1, 1, 1));         // P2: hình lấp đầy lật gương
            return b;
        }

        // ------------------------------------------------------------------ HUD
        public void ShowHud(FightSim s, FighterData[] pair)
        {
            sim = s;
            Root();
            if (select != null) select.style.display = DisplayStyle.None;
            hud?.RemoveFromHierarchy();
            hud = Box(root);
            Abs(hud, 0, 0, 0, 0);

            var top = Box(hud);
            Abs(top, 8, 6, 8, null);
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;

            for (int i = 0; i < 2; i++)
            {
                bool right = i == 1;
                var d = pair[i];
                var sd = sides[i] = new Side { lastHp = d.maxHp };
                var block = Box(top, 250);
                block.style.flexDirection = right ? FlexDirection.RowReverse : FlexDirection.Row;

                var frame = Box(block, 40, 40);
                Border(frame, 2, d.color);
                frame.style.backgroundColor = skin ? skin.panel : Color.black;
                sd.portrait = Box(frame);
                sd.portrait.style.flexGrow = 1;
                Img(sd.portrait, d.portrait, true);
                if (right) sd.portrait.style.scale = new Scale(new Vector3(-1, 1, 1));

                var meters = Box(block);
                meters.style.flexGrow = 1;
                meters.style.marginLeft = right ? 0 : 5;
                meters.style.marginRight = right ? 5 : 0;
                meters.style.alignItems = right ? Align.FlexEnd : Align.FlexStart;
                sd.name = Text(meters, d.displayName, 8, true);
                Bar(meters, skin?.hpFrame, skin?.hpFill, 186, 12, right, out sd.hpFill, new Color(1f, 0.95f, 0.85f, 0.85f), out sd.hpTrail);
                Bar(meters, skin?.energyFrame, skin?.energyFill, 148, 8, right, out sd.energyFill, null, out _);
                Bar(meters, skin?.guardFrame, skin?.guardFill, 120, 6, right, out sd.guardFill, null, out _);
                var pips = Box(meters);
                pips.style.flexDirection = right ? FlexDirection.RowReverse : FlexDirection.Row;
                pips.style.marginTop = 3;
                sd.pips = new VisualElement[FightSim.RoundsToWin];
                for (int k = 0; k < sd.pips.Length; k++)
                {
                    sd.pips[k] = Box(pips, 10, 10);
                    sd.pips[k].style.marginRight = sd.pips[k].style.marginLeft = 2;
                    Img(sd.pips[k], skin?.roundEmpty);
                }
                sd.combo = Text(hud, "", 18, true);
                Abs(sd.combo, right ? null : 14, 78, right ? 14 : null, null);
                sd.combo.style.color = new Color(1f, 0.82f, 0.25f);

                // cắt cảnh tuyệt chiêu: banner trượt vào từ phía người tung chiêu
                sd.cutIn = Box(hud, 256, 60);
                Abs(sd.cutIn, right ? null : 0, 100, right ? 0 : null, null);
                Img(sd.cutIn, d.superArt);
                if (right) sd.cutIn.style.scale = new Scale(new Vector3(-1, 1, 1));
                sd.cutIn.style.display = DisplayStyle.None;
            }

            var center = Box(hud, 80);
            Abs(center, 280, 2, null, null);
            center.style.alignItems = Align.Center;
            clock = Text(center, "99", 26, true);
            clock.style.color = skin ? skin.accent : Color.yellow;
            roundLabel = Text(center, "HIỆP 1", 7, true);

            var bannerBox = Box(hud);
            Abs(bannerBox, 0, 128, 0, null);
            bannerBox.style.alignItems = Align.Center;
            bannerTitle = Text(bannerBox, "", 40, true);
            bannerTitle.style.unityTextOutlineWidth = 1;
            bannerTitle.style.unityTextOutlineColor = Color.black;
            bannerSub = Text(bannerBox, "", 11, true);

            hint = Text(hud, "", 6);
            Abs(hint, 8, null, 8, 4);
            hint.style.opacity = 0.75f;
            hint.style.whiteSpace = WhiteSpace.Normal;
        }

        /// <summary>Tuyệt chiêu bắt đầu → banner cắt cảnh 0.9 giây.</summary>
        public void OnSimEvent(in SimEvent e)
        {
            if (e.kind == SimEventKind.Super && e.prop is 0 or 1) sides[e.prop].cutInStart = Time.time;
        }

        public void UpdateHud(FightSim s, bool training)
        {
            if (hud == null || s == null) return;
            sim = s;
            var st = s.State;
            for (int i = 0; i < 2; i++)
            {
                var sd = sides[i];
                var f = s.Fighter(i);
                var d = s.Fighters[i];
                float hp = Mathf.Clamp01(f.hp / (float)d.maxHp);
                if (f.hp > sd.lastHp) sd.trail = hp;                        // hồi máu (hiệp mới): vệt trễ theo ngay
                sd.lastHp = f.hp;
                sd.trail = Mathf.Max(hp, Mathf.MoveTowards(sd.trail, hp, Time.deltaTime * 0.35f));
                sd.hpFill.style.width = Length.Percent(hp * 100);
                sd.hpTrail.style.width = Length.Percent(sd.trail * 100);
                sd.hpFill.style.unityBackgroundImageTintColor = hp < 0.25f && (int)(Time.time * 4) % 2 == 0 ? new Color(1, 0.6f, 0.6f) : Color.white;
                sd.energyFill.style.width = Length.Percent(f.energy);
                sd.energyFill.style.unityBackgroundImageTintColor = f.energy >= 100 ? Color.Lerp(Color.white, new Color(1f, 0.9f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.time * 10)) : Color.white;
                sd.guardFill.style.width = Length.Percent(f.guard * 100f / FightSim.GuardMax);
                for (int k = 0; k < sd.pips.Length; k++) Img(sd.pips[k], k < s.Wins(i) ? skin?.roundWin : skin?.roundEmpty);

                var o = s.Fighter(1 - i);
                sd.combo.text = o.comboTaken >= 2 ? $"{o.comboTaken} HIT" : "";

                float k01 = (Time.time - sd.cutInStart) / 0.9f;
                bool show = k01 >= 0 && k01 < 1;
                sd.cutIn.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show)
                {
                    // trượt vào nhanh, đứng yên, trượt ra
                    float x = k01 < 0.2f ? Mathf.Lerp(-260, 0, k01 / 0.2f) : k01 > 0.8f ? Mathf.Lerp(0, -260, (k01 - 0.8f) / 0.2f) : 0;
                    sd.cutIn.style.translate = new Translate(i == 0 ? x : -x, 0);
                }
            }
            clock.text = training ? "∞" : s.SecondsLeft.ToString("00");
            roundLabel.text = $"HIỆP {st.round}";

            string title = "", sub = "";
            if (s.InIntro)
            {
                bool fight = st.tick - st.roundStartTick >= s.IntroTicks - 24;
                title = fight ? "ĐÁNH!" : $"HIỆP {st.round}";
                sub = fight ? "" : st.round == 1 ? $"{s.Fighters[0].displayName}  vs  {s.Fighters[1].displayName}" : "";
            }
            else if (s.RoundOver)
            {
                title = st.timeOver ? "HẾT GIỜ" : st.winner < 0 ? "HOÀ" : "K.O.";
                if (s.MatchOver)
                    sub = st.matchWinner < 0 ? "Trận hoà" : $"{s.Fighters[st.matchWinner].displayName} THẮNG TRẬN!   Enter: chọn lại · F5: đấu lại";
                else if (st.winner >= 0) sub = $"{s.Fighters[st.winner].displayName} thắng hiệp {st.round}";
            }
            bannerTitle.text = title;
            bannerTitle.style.color = title == "ĐÁNH!" ? new Color(1f, 0.82f, 0.25f) : title == "K.O." ? new Color(1f, 0.24f, 0.67f) : Color.white;
            bannerSub.text = sub;
            hint.text = "P1: A/D · W nhảy · S cúi · J nhẹ (J-J-J) · K mạnh · trên không J/K, đang rơi S+K đá bổ · I đỡ (+hướng lăn) · L kỹ năng · K+L tuyệt chiêu · U nhặt/ném     "
                        + "P2: mũi tên · , nhẹ · . mạnh · ' đỡ · / kỹ năng · ; nhặt/ném     Esc: chọn lại · F4 hitbox · F6 luyện tập";
        }

        public void HideHud()
        {
            if (hud != null) hud.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------ màn chọn nhân vật + sân
        FighterData[] fighters;
        StageData[] stages;
        Action<int, int, int> onDone;
        readonly int[] cursor = new int[2];
        readonly bool[] locked = new bool[2];
        int stageCursor;
        VisualElement[] cards = Array.Empty<VisualElement>();
        Label[][] badges = Array.Empty<Label[]>();
        VisualElement[] stageCards = Array.Empty<VisualElement>();
        VisualElement stageRow;
        readonly VisualElement[] previewImg = new VisualElement[2];
        readonly Label[] previewName = new Label[2], previewInfo = new Label[2];
        Label selectHint;

        static readonly Color P1Color = new(0.95f, 0.25f, 0.3f), P2Color = new(0.3f, 0.6f, 1f);

        public void ShowSelect(FighterData[] fs, StageData[] ss, int p1, int p2, Action<int, int, int> done)
        {
            fighters = fs;
            stages = ss;
            onDone = done;
            cursor[0] = Mathf.Clamp(p1, 0, fs.Length - 1);
            cursor[1] = Mathf.Clamp(p2, 0, fs.Length - 1);
            locked[0] = locked[1] = false;
            Root();
            HideHud();
            select?.RemoveFromHierarchy();
            select = new VisualElement();
            root.Add(select);
            Abs(select, 0, 0, 0, 0);
            select.style.backgroundColor = new Color(0.06f, 0.06f, 0.11f, 0.97f);
            select.style.alignItems = Align.Center;
            select.style.display = DisplayStyle.Flex;

            var logo = Box(select, 306, 44);
            logo.style.marginTop = 10;
            Img(logo, skin?.logo);
            var t = Text(select, "CHỌN NHÂN VẬT", 9, true);
            t.style.color = skin ? skin.accent : Color.yellow;
            t.style.marginBottom = 6;

            var middle = Box(select);
            middle.style.flexDirection = FlexDirection.Row;
            middle.style.alignItems = Align.Center;
            for (int side = 0; side < 2; side++)
            {
                if (side == 1) BuildGrid(middle);
                var col = Box(middle, 130);
                col.style.alignItems = Align.Center;
                var img = previewImg[side] = Box(col, 110, 110);
                Border(img, 2, side == 0 ? P1Color : P2Color);
                if (side == 1) img.style.scale = new Scale(new Vector3(-1, 1, 1));
                var tag = Text(col, side == 0 ? "1P" : "2P", 10, true);
                tag.style.color = side == 0 ? P1Color : P2Color;
                previewName[side] = Text(col, "", 9, true);
                previewInfo[side] = Text(col, "", 6);
                previewInfo[side].style.whiteSpace = WhiteSpace.Normal;
                previewInfo[side].style.unityTextAlign = TextAnchor.UpperCenter;
                previewInfo[side].style.width = 124;
            }

            stageRow = Box(select);
            stageRow.style.flexDirection = FlexDirection.Row;
            stageRow.style.marginTop = 8;
            stageCards = new VisualElement[ss.Length];
            for (int i = 0; i < ss.Length; i++)
            {
                int idx = i;
                var c = stageCards[i] = new VisualElement();
                stageRow.Add(c);
                c.style.width = 96;
                c.style.height = 66;
                c.style.marginLeft = c.style.marginRight = 3;
                var thumb = Box(c, 96, 54);
                Img(thumb, ss[i].background, true);
                thumb.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Bottom);
                var lbl = Text(c, ss[i].displayName, 6, true);
                lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                c.RegisterCallback<ClickEvent>(_ => { if (locked[0] && locked[1]) { stageCursor = idx; Confirm(); } });
            }
            selectHint = Text(select, "", 6);
            selectHint.style.marginTop = 6;
            selectHint.style.opacity = 0.8f;
            Refresh();
        }

        void BuildGrid(VisualElement parent)
        {
            var grid = Box(parent, 3 * 100);
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.Center;
            cards = new VisualElement[fighters.Length];
            badges = new Label[fighters.Length][];
            for (int i = 0; i < fighters.Length; i++)
            {
                int idx = i;
                var d = fighters[i];
                var c = cards[i] = new VisualElement();
                grid.Add(c);
                c.style.width = 94;
                c.style.height = 74;
                c.style.marginLeft = c.style.marginRight = c.style.marginTop = c.style.marginBottom = 3;
                c.style.backgroundColor = new Color(0.14f, 0.13f, 0.2f);
                Border(c, 2, d.color);
                Img(c, d.portrait, true);
                var bar = Box(c);
                Abs(bar, 0, null, 0, 0);
                bar.style.backgroundColor = new Color(0, 0, 0, 0.6f);
                var n = Text(bar, d.displayName, 6, true);
                n.style.paddingLeft = 3;
                var b1 = Text(c, "1P", 8, true);
                Abs(b1, 2, 1, null, null);
                b1.style.color = P1Color;
                var b2 = Text(c, "2P", 8, true);
                Abs(b2, null, 1, 2, null);
                b2.style.color = P2Color;
                badges[i] = new[] { b1, b2 };
                c.RegisterCallback<ClickEvent>(_ =>
                {
                    int side = !locked[0] ? 0 : !locked[1] ? 1 : -1;
                    if (side < 0) return;
                    cursor[side] = idx;
                    locked[side] = true;
                    OnLockChanged();
                });
            }
        }

        void OnLockChanged()
        {
            PlaySelectSound?.Invoke("confirm");
            if (locked[0] && locked[1])
            {
                // mặc định: sân nhà của P2 (người "đến thăm" sân của đối thủ)
                stageCursor = Mathf.Max(0, Array.FindIndex(stages, s => s.homeFighterId == fighters[cursor[1]].id));
            }
            Refresh();
        }

        void Refresh()
        {
            if (select == null) return;
            for (int i = 0; i < cards.Length; i++)
            {
                for (int side = 0; side < 2; side++)
                {
                    var b = badges[i][side];
                    b.style.display = cursor[side] == i ? DisplayStyle.Flex : DisplayStyle.None;
                    b.style.backgroundColor = locked[side] && cursor[side] == i ? (side == 0 ? P1Color : P2Color) : new Color(0, 0, 0, 0.5f);
                    b.style.color = locked[side] && cursor[side] == i ? Color.white : side == 0 ? P1Color : P2Color;
                }
                bool hover = cursor[0] == i || cursor[1] == i;
                Border(cards[i], hover ? 3 : 2, hover ? Color.white : fighters[i].color);
            }
            for (int side = 0; side < 2; side++)
            {
                var d = fighters[cursor[side]];
                Img(previewImg[side], d.portrait, true);
                previewName[side].text = d.displayName + (locked[side] ? "  ✓" : "");
                previewInfo[side].text = $"{d.playstyle}\nHP {d.maxHp} · {d.propAffinity}";
            }
            bool stagePhase = locked[0] && locked[1];
            stageRow.style.opacity = stagePhase ? 1f : 0.35f;
            for (int i = 0; i < stageCards.Length; i++)
                Border(stageCards[i], 2, stagePhase && i == stageCursor ? (skin ? skin.accent : Color.yellow) : new Color(0, 0, 0, 0));
            selectHint.text = stagePhase
                ? "Chọn sân: A/D hoặc ←/→ · J hoặc , để vào trận · K / . quay lại · hoặc bấm chuột vào sân"
                : "P1: A/D/W/S chọn · J xác nhận · K huỷ     P2: mũi tên · , xác nhận · . huỷ     Chuột: bấm thẻ (P1 rồi P2)";
        }

        void Confirm()
        {
            PlaySelectSound?.Invoke("stage_pick");
            select.style.display = DisplayStyle.None;
            onDone?.Invoke(cursor[0], cursor[1], stageCursor);
        }

        void Update()
        {
            if (!SelectOpen || fighters == null) return;
            bool stagePhase = locked[0] && locked[1];
            if (stagePhase)
            {
                int dir = (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) ? 1 : 0)
                          - (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) ? 1 : 0);
                if (dir != 0) { PlaySelectSound?.Invoke("move"); stageCursor = (stageCursor + dir + stages.Length) % stages.Length; Refresh(); }
                if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Comma) || Input.GetKeyDown(KeyCode.Return)) { Confirm(); return; }
                if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.Period)) { PlaySelectSound?.Invoke("cancel"); locked[1] = false; Refresh(); }
                return;
            }
            Move(0, KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S, KeyCode.J, KeyCode.K);
            Move(1, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.Comma, KeyCode.Period);
        }

        void Move(int side, KeyCode left, KeyCode right, KeyCode up, KeyCode down, KeyCode ok, KeyCode back)
        {
            if (Input.GetKeyDown(back) && locked[side]) { PlaySelectSound?.Invoke("cancel"); locked[side] = false; Refresh(); return; }
            if (locked[side]) return;
            int n = fighters.Length, c = cursor[side];
            if (Input.GetKeyDown(left)) c = (c + n - 1) % n;
            if (Input.GetKeyDown(right)) c = (c + 1) % n;
            if (Input.GetKeyDown(up)) c = (c + n - 3) % n;
            if (Input.GetKeyDown(down)) c = (c + 3) % n;
            if (c != cursor[side]) { PlaySelectSound?.Invoke("move"); cursor[side] = c; Refresh(); }
            if (Input.GetKeyDown(ok)) { locked[side] = true; OnLockChanged(); }
        }
    }
}
