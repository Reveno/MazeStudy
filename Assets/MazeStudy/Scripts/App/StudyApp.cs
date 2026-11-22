// MazeStudy – програмний стенд для дослідження алгоритмів генерації лабіринтів.
// Вкладки: «Генерація» (покрокова візуалізація + метрики одного лабіринту),
//          «Порівняння» (експеримент: усі алгоритми × розміри × N запусків, графіки на білому фоні, експорт CSV/PNG),
//          «Алгоритми» (довідка).
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MazeStudy
{
    public partial class StudyApp : MonoBehaviour
    {
        enum Tab { Generate, Compare, Verify, Ai, About, Llm } // Llm – вкладку прибрано з інтерфейсу (кнопки немає)
        Tab tab = Tab.Generate;
        string helpId; Vector2 helpScroll; // відкрита довідка (null – закрита)

        // ---- стан вкладки «Генерація» ----
        int algo, sizeIdx = 1, seed; string seedText = ""; bool hasMaze, paused, repeated;
        static readonly int[] Sizes = { 10, 15, 25, 50, 100 };
        bool animate = true; float speed = 60, stepAcc; readonly MazeView view = new MazeView();
        Dictionary<string, double> met; double genMs;
        // права панель вкладки «Генерація»: 0 – метрики й опис, 1 – псевдокод із поточним рядком, 2 – код C# (Generators.cs)
        int rightTab = 1; Trace trace; bool traceSame = true; Vector2 srcScroll; string[] srcBlocks;
        Font codeFont; GUIStyle codeSt, srcSt, numSt, segSt, segSel, segXsSt, segXs, logSt;
        const int TraceMaxSize = 50; // журнал виконання ведеться для сіток до 50×50 (далі подій надто багато)
        float PanelW => rightTab == 0 ? 298 : 452;

        // ---- стан вкладки «Порівняння» ----
        Experiment exp; bool running; int progress; string status = "";
        readonly bool[] expSize = { true, true, true, true }; static readonly int[] ExpSizes = { 10, 25, 50, 100 };
        int runsIdx = 1; static readonly int[] RunsOpt = { 10, 30, 50 };
        int repIdx = 0; static readonly int[] RepOpt = { 1, 5 }; // незалежних серій експерименту (перевірка повторюваності)
        string chartKey = "ms"; int chartSize = 50; bool lineChart, overview = true, stability;
        Rect chartRect; string saved = "";

        // ---- оформлення: світла тема ----
        GUIStyle title, h2, label, small, btn, btnSel, tabStyle, tabSel, cellR, field;
        Texture2D white; Font font, fontBold;
        static readonly Color Ink = new Color(0.1f, 0.1f, 0.12f), Muted = new Color(0.42f, 0.42f, 0.46f), Accent = new Color(0.16f, 0.47f, 0.84f);
        static readonly Color Panel = new Color(0.965f, 0.965f, 0.97f), Line = new Color(0.86f, 0.86f, 0.88f);
        const float VW = 1280, VH = 800;

        void Start()
        {
            Application.targetFrameRate = 60;
            var cam = Camera.main; if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.white; }
            // нічого не генеруємо автоматично – лише за кнопкою «Згенерувати»
            if (System.Environment.GetCommandLineArgs().Contains("-selftest")) StartCoroutine(SelfTest());
            if (System.Environment.GetCommandLineArgs().Contains("-codeshots")) StartCoroutine(CodeShots());
            if (System.Environment.GetCommandLineArgs().Contains("-tabtest")) StartCoroutine(TabTest());
        }

        // нова генерація з новим seed, якого ще не було в цього алгоритму
        void GenerateNew() { seed = SeedBook.Next(algo); seedText = seed.ToString(); repeated = false; Generate(); }
        // повтор за введеним seed (свідомо – для відтворюваності)
        void GenerateRepeat() { repeated = SeedBook.WasUsed(algo, seed); SeedBook.Mark(algo, seed); Generate(); }

        void Generate()
        {
            hasMaze = true; paused = false;
            int s = Sizes[sizeIdx]; var gen = Generators.All[algo].gen;
            Generators.Record = false; gen(s, s, new System.Random(seed)); // прогрів
            var sw = Stopwatch.StartNew(); var m = gen(s, s, new System.Random(seed)); genMs = sw.Elapsed.TotalMilliseconds; // час – без журналу
            Generators.Record = true; var logged = gen(s, s, new System.Random(seed)); Generators.Record = false; // той самий лабіринт із журналом
            met = Metrics.Compute(m);
            trace = null; traceSame = true;
            if (animate && s <= TraceMaxSize)
            {
                // трасована копія алгоритму: той самий seed → той самий лабіринт, плюс журнал «рядок псевдокоду + стан»
                trace = new Trace(); var tm = GeneratorsTraced.All[algo](s, s, new System.Random(seed), trace);
                traceSame = GeneratorsTraced.Same(logged, tm);
                view.Set(tm, true, 640, trace);
            }
            else view.Set(logged, animate);
            stepAcc = 0;
        }

        void Update()
        {
            UpdateAi();
            if (tab == Tab.Generate && view.Animating && !paused)
            {
                stepAcc += speed * Time.deltaTime; int n = (int)stepAcc;
                if (n > 0) { stepAcc -= n; view.Advance(n); }
            }
        }

        // ================== ІНТЕРФЕЙС ==================
        void Styles()
        {
            if (title != null) return;
            font = Resources.Load<Font>("Fonts/PTSans"); fontBold = Resources.Load<Font>("Fonts/PTSansBold");
            white = Texture2D.whiteTexture;
            Texture2D T(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }
            GUIStyle S(int size, Color c, bool bold = false) => new GUIStyle { font = bold ? fontBold : font, fontSize = size, wordWrap = true, richText = true, normal = { textColor = c } };
            title = S(24, Ink, true); h2 = S(16, Ink, true); label = S(14, Ink); small = S(12, Muted);
            cellR = new GUIStyle(S(13, Ink)) { alignment = TextAnchor.MiddleRight, wordWrap = false };
            btn = new GUIStyle(S(14, Ink)) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 8, 4, 4), wordWrap = false,
                normal = { background = T(new Color(0.93f, 0.93f, 0.95f)), textColor = Ink }, hover = { background = T(new Color(0.88f, 0.9f, 0.95f)), textColor = Ink }, active = { background = T(new Color(0.82f, 0.86f, 0.95f)), textColor = Ink } };
            btnSel = new GUIStyle(btn) { normal = { background = T(Accent), textColor = Color.white }, hover = { background = T(Accent), textColor = Color.white } };
            tabStyle = new GUIStyle(btn) { alignment = TextAnchor.MiddleCenter, fontSize = 15 }; tabSel = new GUIStyle(btnSel) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
            codeFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New" }, 12);
            codeSt = new GUIStyle(S(13, Ink)) { wordWrap = true, richText = true };
            srcSt = new GUIStyle { font = codeFont ? codeFont : font, fontSize = 11, wordWrap = true, richText = false, normal = { textColor = Ink } };
            logSt = new GUIStyle(S(12, Ink)) { wordWrap = false, richText = false, clipping = TextClipping.Clip };
            numSt = new GUIStyle(S(11, Muted)) { alignment = TextAnchor.UpperRight, wordWrap = false };
            segSt = new GUIStyle(tabStyle) { fontSize = 13, padding = new RectOffset(2, 2, 3, 3) }; segSel = new GUIStyle(tabSel) { fontSize = 13, padding = new RectOffset(2, 2, 3, 3) };
            // кнопки розміру на «Генерації»: п'ять в один ряд шириною 300, тому шрифт трохи менший
            segXsSt = new GUIStyle(segSt) { fontSize = 12, padding = new RectOffset(0, 0, 3, 3) }; segXs = new GUIStyle(segSel) { fontSize = 12, padding = new RectOffset(0, 0, 3, 3) };
            field = new GUIStyle(S(15, Ink)) { alignment = TextAnchor.MiddleCenter, normal = { background = T(Color.white), textColor = Ink }, focused = { background = T(Color.white), textColor = Ink }, border = new RectOffset(1, 1, 1, 1) };
        }

        bool Btn(Rect r, string t, bool on = false) => GUI.Button(r, t, on ? btnSel : btn);
        // Кнопка розміру сітки: «10 на 10» (компактний шрифт, щоб текст уміщався в кнопку)
        bool SizeBtn(Rect r, int n, bool on = false) => GUI.Button(r, Sz(n), on ? segSel : segSt);
        static string Sz(int n) => $"{n} на {n}";

        // ---------- довідка (тексти – HelpTexts.cs) ----------
        void OpenHelp(string id) { helpId = id; helpScroll = Vector2.zero; }
        void HelpBtn(Rect r, string id) { if (GUI.Button(r, "?", btn)) OpenHelp(id); }

        void DrawHelp(float sw, float sh)
        {
            if (!HelpTexts.All.TryGetValue(helpId, out var h)) { helpId = null; return; }
            Box(new Rect(0, 0, sw, sh), new Color(0, 0, 0, 0.35f));
            float w = Mathf.Min(860, sw - 80), hh = Mathf.Min(640, sh - 60); var r = new Rect((sw - w) / 2, (sh - hh) / 2, w, hh);
            Box(r, Color.white); Box(new Rect(r.x, r.y, r.width, 1), Line); Box(new Rect(r.x, r.yMax - 1, r.width, 1), Line);
            GUI.Label(new Rect(r.x + 24, r.y + 18, r.width - 180, 30), h.title, new GUIStyle(h2) { fontSize = 20 });
            if (GUI.Button(new Rect(r.xMax - 150, r.y + 16, 126, 32), "Зрозуміло ✕", btnSel)) { helpId = null; return; }
            var st = new GUIStyle(label) { fontSize = 15, wordWrap = true, richText = true };
            float tw = r.width - 64, th = st.CalcHeight(new GUIContent(h.body), tw);
            helpScroll = GUI.BeginScrollView(new Rect(r.x + 24, r.y + 62, r.width - 36, r.height - 80), helpScroll, new Rect(0, 0, tw, th + 10));
            GUI.Label(new Rect(0, 0, tw, th), h.body, st);
            GUI.EndScrollView();
        }
        void Box(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }

        void OnGUI()
        {
            Styles();
            float s = Mathf.Min(Screen.width / VW, Screen.height / VH);
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float sw = Screen.width / s, sh = Screen.height / s;
            Box(new Rect(0, 0, sw, sh), Color.white);
            // поки відкрита довідка, кліки миші не доходять до елементів під нею
            var ev = Event.current;
            bool blocked = helpId != null && (ev.isMouse || ev.type == EventType.ScrollWheel);
            if (helpId != null && ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape) { helpId = null; ev.Use(); }
            // клавіші вкладки «Генерація»: пробіл – пауза, стрілки – крок уперед / назад (коли курсор не в полі seed)
            if (helpId == null && tab == Tab.Generate && hasMaze && ev.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() != "seed")
            {
                if (ev.keyCode == KeyCode.Space) { paused = !paused; ev.Use(); }
                else if (ev.keyCode == KeyCode.RightArrow) { paused = true; view.Advance(1); ev.Use(); }
                else if (ev.keyCode == KeyCode.LeftArrow) { paused = true; view.Back(1); ev.Use(); }
            }
            if (!blocked) DrawMain(sw, sh);
            if (helpId != null) DrawHelp(sw, sh);
        }

        void DrawMain(float sw, float sh)
        {
            GUI.Label(new Rect(24, 12, 400, 30), "MazeStudy", title); GUI.Label(new Rect(24, 38, 420, 18), "дослідження алгоритмів генерації лабіринтів", small);
            string[] tabs = { "Генерація", "Порівняння", "Перевірка", "ШІ-генератор", "Алгоритми" };
            for (int i = 0; i < tabs.Length; i++) if (GUI.Button(new Rect(sw - 24 - (tabs.Length - i) * 134, 14, 128, 34), tabs[i], (int)tab == i ? tabSel : tabStyle)) tab = (Tab)i;
            string[] tabIds = { "tab_generate", "tab_compare", "tab_verify", "tab_ai", "tab_about", "tab_llm" };
            if (GUI.Button(new Rect(sw - 24 - tabs.Length * 134 - 128, 14, 122, 34), "Довідка", tabStyle)) OpenHelp(tabIds[(int)tab]);
            Box(new Rect(0, 58, sw, 1), Line);
            switch (tab)
            {
                case Tab.Generate: DrawGenerate(sw, sh); break;
                case Tab.Compare: DrawCompare(sw, sh); break;
                case Tab.Verify: DrawVerify(sw, sh); break;
                case Tab.Ai: DrawAi(sw, sh); break;
                case Tab.Llm: DrawLlm(sw, sh); break;
                case Tab.About: DrawAbout(sw, sh); break;
            }
        }

        // ---------- вкладка «Генерація» ----------
        void DrawGenerate(float sw, float sh)
        {
            float x = 24, y = 72;
            GUI.Label(new Rect(x, y, 300, 22), "Алгоритм", h2); y += 26;
            for (int i = 0; i < Generators.All.Length; i++)
                if (Btn(new Rect(x, y + i * 31, 300, 28), $"{i + 1}. {Generators.All[i].name}", i == algo) && i != algo) { algo = i; hasMaze = false; srcScroll = Vector2.zero; }
            y += Generators.All.Length * 31 + 8;
            GUI.Label(new Rect(x, y, 300, 22), "Розмір лабіринту", h2); y += 26;
            for (int i = 0; i < Sizes.Length; i++) if (GUI.Button(new Rect(x + i * 60, y, 59, 28), Sz(Sizes[i]), i == sizeIdx ? segXs : segXsSt) && i != sizeIdx) { sizeIdx = i; hasMaze = false; }
            y += 38;
            if (GUI.Button(new Rect(x, y, 300, 40), "▶  Згенерувати (новий seed)", btnSel)) GenerateNew();
            y += 48;
            GUI.Label(new Rect(x, y, 300, 20), $"Seed щоразу новий, без повторів (використано: {SeedBook.Count(algo)})", small); y += 22;
            GUI.SetNextControlName("seed");
            var nt = GUI.TextField(new Rect(x, y, 120, 28), seedText, 10, field); nt = new string(nt.Where(char.IsDigit).ToArray());
            if (nt != seedText) { seedText = nt; if (int.TryParse(nt, out var v) && v > 0) seed = v; }
            GUI.enabled = int.TryParse(seedText, out var sv) && sv > 0;
            if (Btn(new Rect(x + 128, y, 172, 28), "Повторити за seed")) GenerateRepeat();
            GUI.enabled = true; y += 36;
            GUI.Label(new Rect(x, y, 300, 22), "Візуалізація", h2); y += 26;
            if (Btn(new Rect(x, y, 300, 28), animate ? "Покрокова анімація: так" : "Покрокова анімація: ні", animate)) animate = !animate;
            y += 32;
            bool tr = hasMaze && view.Tr != null; // анімація йде за журналом виконання (рядки псевдокоду)
            GUI.enabled = tr && view.TraceIndex > 0;
            if (Btn(new Rect(x, y, 72, 28), "◀ Назад")) { paused = true; view.Back(1); }
            GUI.enabled = hasMaze && view.Animating;
            if (Btn(new Rect(x + 76, y, 72, 28), paused ? "▶ Далі" : "Пауза")) paused = !paused;
            if (Btn(new Rect(x + 152, y, 72, 28), "Крок ▶")) { paused = true; view.Advance(1); }
            if (Btn(new Rect(x + 228, y, 72, 28), "До кінця")) view.Finish();
            GUI.enabled = true; y += 34;
            GUI.Label(new Rect(x, y, 140, 22), $"Швидкість: {speed:F0} {(tr ? "подій" : "кр.")}/с", small);
            speed = GUI.HorizontalSlider(new Rect(x + 140, y + 6, 160, 16), speed, 5, 2000);
            y += 26;
            if (Btn(new Rect(x, y, 146, 28), "Шлях до виходу", view.ShowPath)) { view.ShowPath = !view.ShowPath; if (hasMaze) view.Redraw(); }
            if (Btn(new Rect(x + 154, y, 146, 28), "Відвідав агент", view.ShowAgent)) { view.ShowAgent = !view.ShowAgent; if (hasMaze) view.Redraw(); }
            y += 30;
            DrawParams(x, y);
            if (!hasMaze)
            {
                GUI.Label(new Rect(350, 300, sw - 350 - PanelW - 48, 80), $"Обрано: <b>{Generators.All[algo].name}</b>, {Sz(Sizes[sizeIdx])}.\nНатисніть «Згенерувати», щоб побачити, як алгоритм будує лабіринт.", new GUIStyle(label) { fontSize = 17, alignment = TextAnchor.MiddleCenter });
                DrawRight(sw, sh, false);
                return;
            }

            // лабіринт
            float mx = 350, my = 76, side = Mathf.Min(sh - my - 92, sw - mx - PanelW - 32); // знизу місце для статусу й легенди
            if (view.Tex) { float k = side / Mathf.Max(view.Tex.width, view.Tex.height); GUI.DrawTexture(new Rect(mx, my, view.Tex.width * k, view.Tex.height * k), view.Tex); }
            string st = !view.Animating ? $"Готово: {view.StepCount} змін стін · зелений – старт, червоний – вихід"
                : view.Tr != null ? $"Подія {view.TraceIndex} з {view.TraceCount}, змін стін {view.StepIndex} з {view.StepCount}{(paused ? " (пауза)" : "")}"
                : $"Крок {view.StepIndex} з {view.StepCount}{(paused ? " (пауза)" : "")} · оранжевим – поточна клітина";
            GUI.Label(new Rect(mx, my + side + 6, side, 22), st + $" · seed {seed}{(repeated ? " (повтор)" : "")}", small);
            // легенда кольорів
            float ly = my + side + 28;
            if (view.Tr != null)
            {
                float lx = mx; lx = Swatch(lx, ly, MazeView.Cur, "клітина, яку алгоритм обробляє зараз"); Swatch(lx, ly, MazeView.Cur2, "сусідня клітина, з якою він працює");
                ly += 18; lx = mx; lx = Swatch(lx, ly, MazeView.RegionC, "область або рядок"); lx = Swatch(lx, ly, MazeView.StartC, "вхід"); Swatch(lx, ly, MazeView.ExitC, "вихід (завжди в протилежних кутах)");
                ly += 22;
            }
            if (view.Tr != null && view.TraceIndex > 0) DrawLog(mx, ly, side, sh);
            DrawRight(sw, sh, true);
        }

        // кольоровий квадрат із підписом; повертає x для наступного елемента
        float Swatch(float x, float y, Color c, string text)
        {
            Box(new Rect(x, y + 2, 12, 12), c); float w = small.CalcSize(new GUIContent(text)).x;
            GUI.Label(new Rect(x + 17, y, w + 4, 18), text, small); return x + 17 + w + 16;
        }

        // журнал виконання під лабіринтом: останні події (найновіша – внизу, підсвічена)
        void DrawLog(float x, float y, float w, float sh)
        {
            int rows = Mathf.Min(9, (int)((sh - y - 46) / 19)); if (rows < 4) return;
            var code = GeneratorsTraced.Listing[algo].code;
            GUI.Label(new Rect(x, y, w, 20), "Журнал виконання (останні події)", h2); y += 26;
            int last = view.TraceIndex - 1, first = Mathf.Max(0, last - rows + 1);
            for (int i = first; i <= last; i++)
            {
                var e = view.Tr.Events[i];
                if (i == last && view.Animating) Box(new Rect(x, y, w, 19), new Color(1f, 0.87f, 0.68f));
                string cells = e.region ? $"область ({e.x}, {e.y}) – ({e.x2}, {e.y2})" : e.x < 0 ? "" : e.x2 >= 0 ? $"клітини ({e.x}, {e.y}) і ({e.x2}, {e.y2})" : $"клітина ({e.x}, {e.y})";
                string text = code[e.line - 1].Replace("<b>", "").Replace("</b>", "").Trim();
                GUI.Label(new Rect(x, y + 3, 40, 16), (i + 1).ToString(), numSt);
                GUI.Label(new Rect(x + 48, y + 1, w - 52, 18), $"рядок {e.line} · {(cells == "" ? "" : cells + " · ")}{text}", logSt);
                y += 19;
            }
        }

        // права панель: перемикач «Метрики / Псевдокод / Код C#»
        void DrawRight(float sw, float sh, bool withMetrics)
        {
            float pw = PanelW, px = sw - pw - 24, ry = 70;
            Box(new Rect(px, ry, pw, sh - ry - 20), Panel);
            string[] names = { "Метрики", "Псевдокод", "Код C#" }; float bw = (pw - 24 - 8) / 3;
            for (int i = 0; i < 3; i++) if (GUI.Button(new Rect(px + 12 + i * (bw + 4), ry + 8, bw, 26), names[i], rightTab == i ? segSel : segSt)) rightTab = i;
            ry += 44;
            if (rightTab == 0) DrawInfo(px + 12, ry, sh, withMetrics);
            else if (rightTab == 1) DrawPseudo(px + 12, ry, pw - 24);
            else DrawSource(px + 12, ry, pw - 24, sh);
        }

        // псевдокод алгоритму: підсвічено рядок, який виконується зараз; праворуч – скільки разів рядок уже виконано
        void DrawPseudo(float x, float y, float w)
        {
            var (code, la, lb) = GeneratorsTraced.Listing[algo];
            GUI.Label(new Rect(x, y, w, 22), Generators.All[algo].name, h2); y += 28;
            var ev = hasMaze ? view.Current : null; int curLine = ev.HasValue ? ev.Value.line : 0;
            var hits = hasMaze ? view.LineHits : null;
            for (int i = 0; i < code.Length; i++)
            {
                string t = code[i]; int ind = 0; while (ind < t.Length && t[ind] == ' ') ind++;
                float tx = x + 28 + ind / 4 * 16, tw2 = x + w - 52 - tx;
                var content = new GUIContent(t.Substring(ind)); float h = Mathf.Max(22, codeSt.CalcHeight(content, tw2) + 6);
                if (i + 1 == curLine) Box(new Rect(x, y, w, h), new Color(1f, 0.87f, 0.68f));
                GUI.Label(new Rect(x, y + 4, 20, 16), (i + 1).ToString(), numSt);
                GUI.Label(new Rect(tx, y + 3, tw2, h), content, codeSt);
                if (hits != null && hits[i + 1] > 0) GUI.Label(new Rect(x + w - 50, y + 4, 46, 16), hits[i + 1] + " раз", numSt);
                y += h;
            }
            y += 8; Box(new Rect(x, y, w, 1), Line); y += 8;
            GUI.Label(new Rect(x, y, w, 22), "Стан виконання", h2); y += 26;
            if (!hasMaze) { GUI.Label(new Rect(x, y, w, 60), "Натисніть «Згенерувати»: рядок псевдокоду, який виконується, підсвічується під час побудови лабіринту.", small); return; }
            if (view.Tr == null)
            {
                GUI.Label(new Rect(x, y, w, 60), animate ? $"Покрокове виконання коду доступне для сіток до {Sz(TraceMaxSize)}." : "Увімкніть покрокову анімацію, щоб бачити виконання коду рядок за рядком.", small);
                return;
            }
            RowW(x, ref y, w, "Подія", $"{view.TraceIndex} з {view.TraceCount}");
            RowW(x, ref y, w, "Змін стін", $"{view.StepIndex} з {view.StepCount}");
            if (ev.HasValue)
            {
                var e = ev.Value;
                if (e.region) RowW(x, ref y, w, "Область (жовта)", $"({e.x}, {e.y}) – ({e.x2}, {e.y2})");
                else
                {
                    RowW(x, ref y, w, "Поточна клітина (оранжева)", e.x >= 0 ? $"({e.x}, {e.y})" : "–");
                    RowW(x, ref y, w, "Друга клітина (фіолетова)", e.x2 >= 0 ? $"({e.x2}, {e.y2})" : "–");
                }
                RowW(x, ref y, w, la, e.a.ToString()); RowW(x, ref y, w, lb, e.b.ToString());
            }
            else RowW(x, ref y, w, "Стан", view.Animating ? "початок" : "виконання завершено");
            y += 8;
            GUI.Label(new Rect(x, y, w, 90), "Показано трасовану копію алгоритму: ті самі дії й ті самі випадкові числа, що у вимірюваному коді (вкладка «Код C#»). "
                + (traceSame ? "Лабіринт і журнал змін стін збігаються з вимірюваним кодом." : "<color=#b03a2e>УВАГА: результат трасованої копії відрізняється від вимірюваного коду.</color>")
                + " Клавіші: пробіл – пауза, стрілки вліво і вправо – крок назад і вперед.", small);
        }

        void RowW(float x, ref float y, float w, string k, string v)
        {
            GUI.Label(new Rect(x, y, w - 150, 20), k, small); GUI.Label(new Rect(x + w - 150, y, 150, 20), v, cellR);
            Box(new Rect(x, y + 20, w, 1), Line); y += 23;
        }

        // код C# обраного алгоритму з Generators.cs (саме він вимірюється в експерименті)
        void DrawSource(float x, float y, float w, float sh)
        {
            if (srcBlocks == null) srcBlocks = LoadSource();
            GUI.Label(new Rect(x, y, w, 22), "Generators.cs", h2); y += 24;
            GUI.Label(new Rect(x, y, w, 34), "Код алгоритму, час роботи якого вимірюється в експерименті. Для показу він не змінювався.", small); y += 38;
            string src = srcBlocks[algo];
            if (src == null) { GUI.Label(new Rect(x, y, w, 40), "Текст коду не вбудовано у цю збірку.", small); return; }
            var lines = src.Split('\n'); float vw = w - 18, total = 0; var hs = new float[lines.Length];
            for (int i = 0; i < lines.Length; i++) { hs[i] = Mathf.Max(13, srcSt.CalcHeight(new GUIContent(lines[i]), vw)); total += hs[i]; }
            srcScroll = GUI.BeginScrollView(new Rect(x, y, w, sh - y - 30), srcScroll, new Rect(0, 0, vw, total));
            float yy = 0; for (int i = 0; i < lines.Length; i++) { GUI.Label(new Rect(0, yy, vw, hs[i]), lines[i], srcSt); yy += hs[i]; }
            GUI.EndScrollView();
        }

        // Generators.cs вбудовується у збірку як текст (StudyBuild.Build); тут він ділиться на блоки за заголовками «// ---------- N. ...»
        static string[] LoadSource()
        {
            var res = new string[Generators.All.Length]; var ta = Resources.Load<TextAsset>("GeneratorsSource"); if (ta == null) return res;
            string s = ta.text.Replace("\r", "");
            for (int i = 0; i < res.Length; i++)
            {
                int a = s.IndexOf($"// ---------- {i + 1}. "), b = i + 1 < res.Length ? s.IndexOf($"// ---------- {i + 2}. ") : s.IndexOf("// ---------- допоміжне");
                if (a < 0 || b < a) continue;
                res[i] = string.Join("\n", s.Substring(a, b - a).TrimEnd().Split('\n').Select(l => l.StartsWith("        ") ? l.Substring(8) : l.TrimStart()));
            }
            return res;
        }

        void DrawInfo(float rx, float ry, float sh, bool withMetrics)
        {
            if (withMetrics) {
            GUI.Label(new Rect(rx, ry, 280, 22), "Метрики лабіринту", h2); ry += 28;
            Row(rx, ref ry, "Час генерації", $"{genMs:F3} мілісекунди");
            foreach (var k in Metrics.List) Row(rx, ref ry, k.label, k.percent ? met[k.key].ToString("P1") : met[k.key].ToString(k.fmt));
            Row(rx, ref ry, "Зв'язність (прохідність)", met["connected"] == 1 ? "так" : "ні");
            ry += 10; }
            var info = AlgorithmInfo.All[algo];
            GUI.Label(new Rect(rx, ry, 280, 22), Generators.All[algo].name, h2); ry += 24;
            GUI.Label(new Rect(rx, ry, 280, 20), info.cls, small); ry += 22;
            ry += Para(rx, ry, info.idea, label);
            ry += Para(rx, ry, "<b>Складність:</b> " + info.complexity, small);
            ry += Para(rx, ry, "<b>+</b> " + info.plus, small);
            ry += Para(rx, ry, "<b>−</b> " + info.minus, small);
        }

        // Гібрид: показуємо поріг переходу від Олдоса–Бродера до Вілсона – він завжди за формулою Yang et al. (2024), без ручного налаштування
        void DrawParams(float x, float y)
        {
            if (algo != 9) return;
            int N = Sizes[sizeIdx] * Sizes[sizeIdx];
            GUI.Label(new Rect(x, y, 300, 22), "Поріг гібрида", h2); y += 24;
            GUI.Label(new Rect(x, y, 300, 40), $"Олдос–Бродер до {Generators.YangCoverage(N):P0} клітин, далі – Вілсон. Поріг – за формулою статті Yang et al. (2024).", small);
        }

        void Row(float x, ref float y, string k, string v)
        {
            GUI.Label(new Rect(x, y, 200, 20), k, small); GUI.Label(new Rect(x + 180, y, 100, 20), v, cellR);
            Box(new Rect(x, y + 20, 280, 1), Line); y += 23;
        }
        float Para(float x, float y, string t, GUIStyle st) { float h = st.CalcHeight(new GUIContent(t), 280); GUI.Label(new Rect(x, y, 280, h), t, st); return h + 6; }

        // ---------- вкладка «Порівняння» ----------
        void DrawCompare(float sw, float sh)
        {
            float x = 24, y = 72;
            GUI.Label(new Rect(x, y, 300, 22), "Експеримент", h2); y += 26;
            GUI.Label(new Rect(x, y, 300, 40), "Усі 10 алгоритмів, однакові seed для всіх. Час вимірюється без побудови графіки.", small); y += 40;
            GUI.Label(new Rect(x, y, 300, 20), "Розміри:", label); y += 22;
            for (int i = 0; i < ExpSizes.Length; i++) if (SizeBtn(new Rect(x + i * 76, y, 72, 28), ExpSizes[i], expSize[i]) && !running) expSize[i] = !expSize[i];
            y += 36;
            GUI.Label(new Rect(x, y, 300, 20), "Скільки лабіринтів: запусків у серії та кількість серій", label); y += 22;
            for (int i = 0; i < RunsOpt.Length; i++) if (Btn(new Rect(x + i * 58, y, 54, 28), RunsOpt[i].ToString(), i == runsIdx) && !running) runsIdx = i;
            GUI.Label(new Rect(x + 176, y + 4, 20, 22), "і", new GUIStyle(label) { alignment = TextAnchor.UpperCenter });
            for (int i = 0; i < RepOpt.Length; i++) if (Btn(new Rect(x + 196 + i * 54, y, 50, 28), RepOpt[i].ToString(), i == repIdx) && !running) repIdx = i;
            y += 40;
            GUI.enabled = !running && expSize.Any(b => b);
            if (GUI.Button(new Rect(x, y, 300, 40), running ? "Виконується…" : "Запустити експеримент", btnSel)) StartCoroutine(RunExperiment());
            GUI.enabled = true; y += 48;
            if (running || exp != null)
            {
                Box(new Rect(x, y, 300, 10), Line); Box(new Rect(x, y, 300f * progress / Mathf.Max(1, exp?.Total ?? 1), 10), Accent); y += 16;
                GUI.Label(new Rect(x, y, 300, 40), status, small); y += 40;
            }
            if (exp == null || running) { GUI.Label(new Rect(360, 120, 700, 60), "Запустіть експеримент, щоб побачити графіки порівняння.", label); return; }

            GUI.Label(new Rect(x, y, 300, 22), "Вигляд", h2); y += 24;
            if (exp.Repeats < 2) stability = false;
            if (Btn(new Rect(x, y, 96, 28), "4 графіки", overview && !stability)) { overview = true; stability = false; }
            if (Btn(new Rect(x + 102, y, 96, 28), "Показник", !overview && !stability)) { overview = false; stability = false; }
            GUI.enabled = exp.Repeats > 1;
            if (Btn(new Rect(x + 204, y, 96, 28), "Стабільність", stability)) { stability = true; overview = false; lineChart = false; }
            GUI.enabled = true; y += 36;
            var sizes = exp.Sizes;
            if (!sizes.Contains(chartSize)) chartSize = sizes[sizes.Length - 1];
            if (overview)
            {
                GUI.Label(new Rect(x, y, 300, 20), "Розмір для графіків 2–4:", label); y += 22;
                for (int i = 0; i < sizes.Length; i++) if (SizeBtn(new Rect(x + i * 76, y, 72, 26), sizes[i], chartSize == sizes[i])) chartSize = sizes[i];
                y += 36;
                GUI.Label(new Rect(x, y, 300, 120), "1 – швидкість (час і його ріст із розміром)\n2 – довжина шляху і клітини, оброблені агентом\n3 – структура: тупики, коридори, розгалуження\n4 – різноманітність результатів", small); y += 74;
            }
            else
            {
                GUI.Label(new Rect(x, y, 300, 22), "Показник", h2); y += 24;
                var opts = new List<(string key, string label)> { ("ms", "Час генерації, мілісекунди") };
                opts.AddRange(Metrics.List.Select(k => (k.key, k.label))); opts.Add(("diversity", "Різноманітність результатів"));
                if (stability) opts.RemoveAll(o => o.key == "diversity" || o.key == "loops"); // для цих показників серій немає / завжди 0
                foreach (var (k, l) in opts) { if (Btn(new Rect(x, y, 300, 20), l, chartKey == k)) { chartKey = k; if (k != "ms") lineChart = false; } y += 22; }
                y += 6;
                for (int i = 0; i < sizes.Length; i++) if (SizeBtn(new Rect(x + i * 76, y, 72, 26), sizes[i], chartSize == sizes[i] && !lineChart)) { chartSize = sizes[i]; lineChart = false; }
                y += 32;
                if (!stability && chartKey == "ms" && sizes.Length > 1 && Btn(new Rect(x, y, 300, 26), "Залежність часу від розміру", lineChart)) lineChart = !lineChart;
                y += 34;
            }
            if (Btn(new Rect(x, y, 146, 30), "Зберегти PNG")) StartCoroutine(SavePng());
            if (Btn(new Rect(x + 154, y, 146, 30), "Зберегти CSV")) { var d = OutDir(); exp.SaveCsv(d); saved = d; }
            y += 36; if (saved != "") GUI.Label(new Rect(x, y, 300, 80), "Збережено: " + saved, small);

            chartRect = new Rect(350, 72, sw - 374, sh - 92);
            panelRects.Clear();
            if (stability && (chartKey == "diversity" || chartKey == "loops")) chartKey = "ms";
            if (overview) Overview(chartRect); else if (stability) StabilityTable(chartRect); else if (lineChart) LineChart(chartRect); else BarChart(chartRect);
        }

        string LabelOf(string key) => key == "ms" ? "Час генерації, мілісекунди" : key == "diversity" ? "Різноманітність результатів (частка стін, що відрізняються)" : Metrics.List.First(k => k.key == key).label;

        void BarChart(Rect r)
        {
            if (!exp.Sizes.Contains(chartSize)) chartSize = exp.Sizes[exp.Sizes.Length - 1];
            GUI.Label(new Rect(r.x, r.y, r.width, 26), $"{LabelOf(chartKey)} · {Sz(chartSize)}", h2);
            HelpBtn(new Rect(r.xMax - 30, r.y, 28, 26), "ch_bar");
            bool log = chartKey == "ms"; // час відрізняється на порядки – логарифмічна шкала, інакше швидкі алгоритми не видно
            GUI.Label(new Rect(r.x, r.y + 24, r.width, 20), chartKey == "diversity" ? $"20 лабіринтів на алгоритм" : $"Середнє з {exp.RunsTotal} запусків" + (exp.Repeats > 1 ? $" ({exp.Repeats} серій по {exp.Runs})" : "") + "; чорна риска – 95 % довірчий інтервал середнього" + (log ? "; логарифмічна шкала" : ""), small);
            var vals = exp.Algorithms.Select(a => chartKey == "diversity" ? (exp.DiversityOf[(a, chartSize)], 0.0) : (exp.Stat(a, chartSize, chartKey).mean, exp.Stat(a, chartSize, chartKey).ci)).ToList();
            double max = vals.Max(v => v.Item1 + v.Item2); if (max <= 0) max = 1;
            double lmin = log ? System.Math.Floor(System.Math.Log10(System.Math.Max(1e-4, vals.Min(v => v.Item1)))) : 0, lmax = log ? System.Math.Ceiling(System.Math.Log10(max)) : 0;
            double Frac(double v) => log ? (System.Math.Log10(System.Math.Max(v, 1e-4)) - lmin) / System.Math.Max(1e-9, lmax - lmin) : v / max;
            bool pct = chartKey != "ms" && chartKey != "diversity" && Metrics.List.First(k => k.key == chartKey).percent;
            float lw = 270, bw = r.width - lw - 90, rowH = Mathf.Min(44, (r.height - 70) / vals.Count), y0 = r.y + 56;
            if (log)
                for (double e = lmin; e <= lmax; e++)
                {
                    float gx = r.x + lw + (float)(bw * (e - lmin) / System.Math.Max(1e-9, lmax - lmin)); Box(new Rect(gx, y0 - 4, 1, vals.Count * rowH + 4), Line);
                    GUI.Label(new Rect(gx - 40, y0 + vals.Count * rowH + 2, 80, 18), System.Math.Pow(10, e).ToString(e < 0 ? "0.###" : "0") + " мс", new GUIStyle(small) { alignment = TextAnchor.UpperCenter });
                }
            for (int i = 0; i < vals.Count; i++)
            {
                float y = y0 + i * rowH; var (v, ci) = vals[i];
                GUI.Label(new Rect(r.x, y + rowH / 2 - 10, lw - 10, 20), Generators.All[exp.Algorithms[i]].name, label);
                Box(new Rect(r.x + lw, y + rowH * 0.2f, bw, rowH * 0.6f), new Color(0.95f, 0.95f, 0.96f));
                Box(new Rect(r.x + lw, y + rowH * 0.2f, (float)(bw * Frac(v)), rowH * 0.6f), Accent);
                if (ci > 0) { float a = (float)(bw * Frac(System.Math.Max(v - ci, v * 0.01))), b = (float)(bw * Frac(v + ci)); Box(new Rect(r.x + lw + a, y + rowH / 2 - 1, b - a, 2), Ink); Box(new Rect(r.x + lw + b - 1, y + rowH * 0.32f, 2, rowH * 0.36f), Ink); }
                string txt = pct ? v.ToString("P1") : chartKey == "ms" ? (v < 10 ? v.ToString("F3") : v.ToString("F1")) : v.ToString("F2");
                GUI.Label(new Rect(r.x + lw + bw + 6, y + rowH / 2 - 10, 84, 20), txt, label);
            }
        }

        // залежність часу від розміру, логарифмічна шкала по осі Y
        void LineChart(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 26), "Час генерації залежно від розміру лабіринту", h2);
            HelpBtn(new Rect(r.xMax - 30, r.y, 28, 26), "ch_line");
            GUI.Label(new Rect(r.x, r.y + 24, r.width, 20), "Шкала часу логарифмічна: кожна поділка – у 10 разів більше. Число k показує, як швидко росте час зі збільшенням лабіринту: k = 1 – удвічі більше клітин, удвічі довше", small);
            var p = new Rect(r.x + 60, r.y + 60, r.width - 340, r.height - 110);
            double lo = double.MaxValue, hi = 0;
            foreach (int a in exp.Algorithms) foreach (int s in exp.Sizes) { double v = exp.Stat(a, s, "ms").mean; lo = System.Math.Min(lo, v); hi = System.Math.Max(hi, v); }
            double l0 = System.Math.Floor(System.Math.Log10(System.Math.Max(lo, 1e-4))), l1 = System.Math.Ceiling(System.Math.Log10(hi));
            for (double e = l0; e <= l1; e++)
            {
                float yy = p.yMax - (float)((e - l0) / (l1 - l0)) * p.height; Box(new Rect(p.x, yy, p.width, 1), Line);
                GUI.Label(new Rect(p.x - 58, yy - 10, 54, 20), System.Math.Pow(10, e).ToString(e < 0 ? "0.###" : "0") + " мс", new GUIStyle(small) { alignment = TextAnchor.MiddleRight });
            }
            var sz = exp.Sizes; float X(int i) => p.x + (sz.Length == 1 ? p.width / 2 : p.width * i / (sz.Length - 1));
            for (int i = 0; i < sz.Length; i++) GUI.Label(new Rect(X(i) - 45, p.yMax + 4, 90, 20), Sz(sz[i]), new GUIStyle(small) { alignment = TextAnchor.UpperCenter });
            var pal = Pal; // ті самі кольори, що й на огляді
            for (int j = 0; j < exp.Algorithms.Length; j++)
            {
                int a = exp.Algorithms[j]; var col = pal[a % pal.Length]; Vector2? prev = null;
                for (int i = 0; i < sz.Length; i++)
                {
                    double v = exp.Stat(a, sz[i], "ms").mean; float yy = p.yMax - (float)((System.Math.Log10(System.Math.Max(v, 1e-4)) - l0) / (l1 - l0)) * p.height; var pt = new Vector2(X(i), yy);
                    if (prev.HasValue) Seg(prev.Value, pt, col); Box(new Rect(pt.x - 4, pt.y - 4, 8, 8), col); prev = pt;
                }
                float ly = p.y + j * 26;
                Box(new Rect(p.xMax + 24, ly + 8, 16, 4), col);
                GUI.Label(new Rect(p.xMax + 46, ly, 260, 22), $"{Generators.All[a].name} (k = {exp.GrowthExponent(a):F2})", small);
            }
        }

        void Seg(Vector2 a, Vector2 b, Color c)
        {
            // поворот навколо точки a в координатах інтерфейсу (GUIUtility.RotateAroundPivot зсуває лінії, коли інтерфейс масштабовано)
            var d = b - a; float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; var m = GUI.matrix;
            GUI.matrix = m * Matrix4x4.TRS(new Vector3(a.x, a.y, 0), Quaternion.Euler(0, 0, ang), Vector3.one) * Matrix4x4.TRS(new Vector3(-a.x, -a.y, 0), Quaternion.identity, Vector3.one);
            Box(new Rect(a.x, a.y - 1.25f, d.magnitude, 2.5f), c); GUI.matrix = m;
        }

        IEnumerator RunExperiment()
        {
            running = true; saved = ""; progress = 0;
            exp = new Experiment { Sizes = ExpSizes.Where((_, i) => expSize[i]).ToArray(), Runs = RunsOpt[runsIdx], Repeats = RepOpt[repIdx] };
            exp.Seeds = SeedBook.NextForAll(exp.Sizes.Length * exp.RunsTotal, Generators.All.Length); // нові, раніше не використані seed (кожна серія – свої)
            chartSize = exp.Sizes.Contains(50) ? 50 : exp.Sizes[exp.Sizes.Length - 1];
            var sw = Stopwatch.StartNew();
            foreach (var done in exp.RunStepwise())
            {
                progress = done; status = $"Виконано {done} з {exp.Total} (пар «алгоритм і розмір») · {sw.Elapsed.TotalSeconds:F0} с";
                yield return null;
            }
            status = $"Готово: {exp.Raw.Count} лабіринтів за {sw.Elapsed.TotalSeconds:F0} с"; running = false;
        }

        static string OutDir() => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "MazeStudy_results");

        // зберегти саму діаграму (біла область праворуч) як PNG – для вставки у Word
        IEnumerator SavePng()
        {
            yield return new WaitForEndOfFrame();
            float s = Mathf.Min(Screen.width / VW, Screen.height / VH);
            var full = ScreenCapture.CaptureScreenshotAsTexture();
            var dir = OutDir(); Directory.CreateDirectory(dir);
            string tag = overview ? $"overview_{chartSize}x{chartSize}" : $"chart_{chartKey}_{(lineChart ? "size" : chartSize + "x" + chartSize)}";
            var list = new List<(Rect r, string name)> { (chartRect, tag) };
            for (int i = 0; i < panelRects.Count; i++) list.Add((panelRects[i], $"{tag}_graph{i + 1}"));
            foreach (var (rect, name) in list)
            {
                int x0 = (int)(rect.x * s), y0 = (int)(Screen.height - rect.yMax * s), w = (int)(rect.width * s), h = (int)(rect.height * s);
                x0 = Mathf.Clamp(x0, 0, full.width - 1); y0 = Mathf.Clamp(y0, 0, full.height - 1); w = Mathf.Min(w, full.width - x0); h = Mathf.Min(h, full.height - y0);
                var crop = new Texture2D(w, h, TextureFormat.RGB24, false); crop.SetPixels(full.GetPixels(x0, y0, w, h)); crop.Apply();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), crop.EncodeToPNG()); Destroy(crop);
            }
            Destroy(full); saved = dir + (panelRects.Count > 0 ? $" (загальний + {panelRects.Count} окремих графіки)" : "");
        }

        // ---------- вкладка «Алгоритми» ----------
        Texture2D[] thumbs;
        void DrawAbout(float sw, float sh)
        {
            if (thumbs == null)
            {
                thumbs = new Texture2D[Generators.All.Length];
                // 16×16 зі шляхом до виходу – так видно характер алгоритму (звивистий шлях DFS, шлях по краю у Binary Tree тощо)
                for (int i = 0; i < thumbs.Length; i++) { var v = new MazeView { ShowPath = true }; v.Set(Generators.All[i].gen(16, 16, new System.Random(7)), false, 160); thumbs[i] = v.Tex; }
            }
            float cw = (sw - 72) / 2, ch = (sh - 90) / 5;
            for (int i = 0; i < Generators.All.Length; i++)
            {
                float x = 24 + (i % 2) * (cw + 24), y = 72 + (i / 2) * ch; var info = AlgorithmInfo.All[i];
                Box(new Rect(x, y, cw, ch - 8), Panel);
                float t = ch - 24; GUI.DrawTexture(new Rect(x + 8, y + 8, t, t), thumbs[i]);
                float tx = x + t + 20, tw = cw - t - 30;
                GUI.Label(new Rect(tx, y + 6, tw, 22), $"{i + 1}. {Generators.All[i].name}", h2);
                string sub = info.cls + " · " + info.complexity; float sh2 = small.CalcHeight(new GUIContent(sub), tw);
                GUI.Label(new Rect(tx, y + 28, tw, sh2), sub, small);
                GUI.Label(new Rect(tx, y + 32 + sh2, tw, ch - 40 - sh2), info.idea + "\n<color=#1a7f4b>+ " + info.plus + "</color>\n<color=#b03a2e>− " + info.minus + "</color>", new GUIStyle(label) { fontSize = 12 });
            }
        }

        // ---------- знімки панелі коду та перевірка трасованих копій: MazeStudy.exe -codeshots ----------
        IEnumerator CodeShots()
        {
            string dir = Path.Combine(Application.dataPath, "..", "codeshots"); Directory.CreateDirectory(dir);
            var log = new System.Text.StringBuilder();
            Application.logMessageReceived += (m, st, t) => { if (t == LogType.Exception || t == LogType.Error) log.AppendLine($"{t}: {m}\n{st}"); };
            yield return new WaitForSeconds(1);
            for (int a = 0; a < Generators.All.Length; a++)
            {
                algo = a; sizeIdx = 1; rightTab = 1; seed = 2026; seedText = seed.ToString(); animate = true; Generate(); paused = true;
                view.Advance(view.TraceCount * 2 / 5); yield return null; yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"code_{a}.png")); yield return new WaitForSeconds(0.3f);
            }
            algo = 0; Generate(); paused = true; view.Advance(view.TraceCount / 2); view.Back(3); rightTab = 2; yield return null; yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "source.png")); yield return new WaitForSeconds(0.3f);
            sizeIdx = 2; rightTab = 0; Generate(); view.ShowPath = true; view.ShowAgent = true; view.Finish(); yield return null; yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "metrics.png")); yield return new WaitForSeconds(0.3f);
            sizeIdx = 4; rightTab = 1; Generate(); yield return null; yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "big.png")); yield return new WaitForSeconds(0.3f);
            File.WriteAllText(Path.Combine(dir, "equivalence.txt"), GeneratorsTraced.CheckEquivalence());
            File.WriteAllText(Path.Combine(dir, "codeshots.log"), log.Length == 0 ? "OK" : log.ToString());
            Application.Quit();
        }

        // ---------- перевірка вкладок «ШІ-генератор» і «LLM»: MazeStudy.exe -tabtest ----------
        IEnumerator TabTest()
        {
            string dir = Path.Combine(Application.dataPath, "..", "tabtest"); Directory.CreateDirectory(dir);
            var log = new System.Text.StringBuilder(); var rep = new System.Text.StringBuilder();
            Application.logMessageReceived += (m, st, t) => { if (t == LogType.Exception || t == LogType.Error) log.AppendLine($"{t}: {m}\n{st}"); };
            yield return new WaitForSeconds(1);
            tab = Tab.Ai; yield return null; EnsureAiModel();
            rep.AppendLine("AI: модель завантажено = " + (aiModel != null));
            if (aiModel != null)
            {
                var rnd = new System.Random(4242); int better = 0, n = 6; double before = 0, after = 0;
                for (int c = 0; c < n; c++)
                {
                    for (int k = 0; k < 3; k++) aiLevel[k] = rnd.NextDouble();
                    var tg = AiTarget(); aiPlan = AiDesigner.Design(aiModel, AiSize, tg, AiWeight); AiGenerate(tg);
                    double d0 = aiDist; AiRefine(tg); while (aiBusy) yield return null; yield return null;
                    rep.AppendLine($"AI ціль {c + 1}: рівні {aiLevel[0]:0.00}/{aiLevel[1]:0.00}/{aiLevel[2]:0.00}; обрано {Generators.All[aiPlan[0].a].name}; відстань була {d0:0.00}, стала {aiDist:0.00}");
                    before += d0 / n; after += aiDist / n; if (aiDist < d0) better++;
                    if (c == 0) { yield return new WaitForSeconds(0.3f); ScreenCapture.CaptureScreenshot(Path.Combine(dir, "ai.png")); yield return new WaitForSeconds(0.3f); }
                }
                rep.AppendLine($"AI підсумок: середня відстань була {before:0.00}, стала {after:0.00}; поліпшено {better} з {n}");
            }
            tab = Tab.Llm; yield return new WaitForSeconds(3f);
            rep.AppendLine("LLM: моделей знайдено = " + (llmModels == null ? -1 : llmModels.Count) + "; модель: " + llmModel);
            if (llmModels != null && llmModels.Count > 0)
            {
                bool fin = false;
                StartCoroutine(AskLlm(LlmTools.SolvePrompt(llmGrid), (a, e) => { if (e != null) llmVerdict = "Не розв'язано: " + e; else { var c = LlmTools.CheckPath(llmGrid, a); llmVerdict = (c.ok ? "РОЗВ'ЯЗАНО: " : "НЕ розв'язано: ") + c.why; } llmAnswer = e ?? a; fin = true; }));
                float t0 = Time.realtimeSinceStartup; while (!fin && Time.realtimeSinceStartup - t0 < 240) yield return null;
                rep.AppendLine($"LLM: відповідь отримано = {fin}; час {Time.realtimeSinceStartup - t0:0} с; вердикт: {llmVerdict}");
                yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(dir, "llm.png")); yield return new WaitForSeconds(0.3f);
            }
            File.WriteAllText(Path.Combine(dir, "tabtest.txt"), rep.ToString() + "\nПОМИЛКИ: " + (log.Length == 0 ? "немає" : log.ToString()));
            Application.Quit();
        }

        // ---------- самоперевірка зібраної програми: MazeStudy.exe -selftest ----------
        IEnumerator SelfTest()
        {
            string dir = Path.Combine(Application.dataPath, "..", "selftest"); Directory.CreateDirectory(dir);
            var log = new System.Text.StringBuilder();
            Application.logMessageReceived += (m, st, t) => { if (t == LogType.Exception || t == LogType.Error) log.AppendLine($"{t}: {m}\n{st}"); };
            yield return new WaitForSeconds(1); ScreenCapture.CaptureScreenshot(Path.Combine(dir, "start.png")); yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < Generators.All.Length; i++)
            {
                algo = i; sizeIdx = 1; GenerateNew(); yield return new WaitForSeconds(1.2f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"gen_{i}.png")); yield return new WaitForSeconds(0.3f);
            }
            rightTab = 0; view.ShowPath = true; view.ShowAgent = true; view.Finish(); yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(dir, "gen_path.png")); yield return new WaitForSeconds(0.3f);
            tab = Tab.About; yield return new WaitForSeconds(0.5f); ScreenCapture.CaptureScreenshot(Path.Combine(dir, "about.png")); yield return new WaitForSeconds(0.3f);
            // перевірка: гістограми рівномірності для Вілсона і DFS
            tab = Tab.Verify;
            foreach (int ha in new[] { 4, 0 })
            {
                histAlgo = ha; verBusy = true; var hres = LlmTools.TreeHistogram(ha, 19200, 2026); histCounts = hres.counts; histChi = hres.chi; histFor = ha; verBusy = false;
                if (ha == 0) verText = Verification.Run(1920);
                yield return null; yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"verify_{ha}.png")); yield return new WaitForSeconds(0.3f);
            }
            tab = Tab.Ai; yield return null; EnsureAiModel();
            if (aiModel != null)
            {
                var lvl = new[] { 0.8, 0.7, 0.4 }; for (int k = 0; k < 3; k++) aiLevel[k] = lvl[k];
                var tg = AiTarget(); aiPlan = AiDesigner.Design(aiModel, AiSize, tg, AiWeight); AiGenerate(tg); yield return new WaitForSeconds(0.5f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "ai_plan.png")); yield return new WaitForSeconds(0.3f);
                AiRefine(tg); while (aiBusy) yield return null; yield return new WaitForSeconds(0.3f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "ai_refined.png")); yield return new WaitForSeconds(0.3f);
            }
            tab = Tab.Compare; expSize[3] = false; runsIdx = 0; repIdx = 1; yield return RunExperiment();
            overview = true; chartSize = 25; yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(dir, "cmp_overview.png")); yield return new WaitForSeconds(0.3f);
            yield return SavePng();
            OpenHelp("ch_balance"); yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(dir, "help_balance.png")); yield return new WaitForSeconds(0.3f);
            OpenHelp("tab_verify"); yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(dir, "help_verify.png")); yield return new WaitForSeconds(0.3f);
            helpId = null;
            overview = false; chartKey = "ms"; yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(dir, "cmp_time.png")); yield return new WaitForSeconds(0.3f);
            lineChart = true; yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(dir, "cmp_line.png")); yield return new WaitForSeconds(0.3f);
            lineChart = false; chartKey = "dead_ends"; yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(dir, "cmp_dead.png")); yield return new WaitForSeconds(0.3f);
            stability = true; chartKey = "agent"; yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(dir, "cmp_stability_agent.png")); yield return new WaitForSeconds(0.3f);
            chartKey = "ms"; yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(dir, "cmp_stability_ms.png")); yield return new WaitForSeconds(0.3f);
            stability = false; exp.SaveCsv(OutDir());
            File.WriteAllText(Path.Combine(dir, "selftest.log"), log.Length == 0 ? "OK" : log.ToString());
            Application.Quit();
        }
    }
}
