// Відображення лабіринту в текстурі (білий фон, темні стіни) і покрокова анімація генерації.
// Два режими анімації: за журналом змін стін (Steps) або за журналом виконання (Trace) – тоді кожен крок
// відповідає рядку псевдокоду, а на лабіринті підсвічуються клітини, з якими алгоритм працює саме зараз.
using System.Collections.Generic;
using UnityEngine;

namespace MazeStudy
{
    public class MazeView
    {
        public Texture2D Tex { get; private set; }
        public Maze Result { get; private set; }   // готовий лабіринт
        public int StepIndex { get; private set; } // скільки змін стін уже показано
        public int StepCount => Result?.Steps?.Count ?? 0;
        public Trace Tr { get; private set; }      // журнал виконання (null – анімація лише за змінами стін)
        public int TraceIndex { get; private set; } // скільки подій журналу виконання вже показано
        public int TraceCount => Tr?.Events.Count ?? 0;
        public int[] LineHits { get; private set; } // скільки разів виконано кожен рядок псевдокоду (до поточної події)
        public bool Animating => Tr != null ? TraceIndex < TraceCount : Result?.Steps != null && StepIndex < StepCount;
        public bool ShowPath, ShowAgent;

        // остання показана подія журналу виконання (null – ще не почали або вже завершили)
        public Trace.Ev? Current => Tr != null && TraceIndex > 0 && TraceIndex < TraceCount ? Tr.Events[TraceIndex - 1] : (Trace.Ev?)null;

        Maze shown; int cs; Color[] pix; int tw, th; bool startOpen;
        (int x, int y) cur = (-1, -1), cur2 = (-1, -1); bool region;
        static readonly Color Bg = Color.white, Wall = new Color(0.13f, 0.13f, 0.15f);
        static readonly Color PathC = new Color(1f, 0.78f, 0.35f), AgentC = new Color(0.72f, 0.85f, 1f);
        public static readonly Color Cur = new Color(0.95f, 0.45f, 0.15f);
        public static readonly Color Cur2 = new Color(0.72f, 0.6f, 0.92f), RegionC = new Color(1f, 0.93f, 0.62f);
        public static readonly Color StartC = new Color(0.2f, 0.7f, 0.35f), ExitC = new Color(0.85f, 0.25f, 0.25f);

        public void Set(Maze result, bool animate, int maxPx = 640, Trace trace = null)
        {
            Result = result;
            cs = Mathf.Max(3, maxPx / Mathf.Max(result.W, result.H));
            tw = result.W * cs + 1; th = result.H * cs + 1;
            if (Tex == null || Tex.width != tw || Tex.height != th) Tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            pix = new Color[tw * th];
            bool anim = animate && result.Steps != null;
            Tr = anim ? trace : null; TraceIndex = 0; LineHits = Tr != null ? new int[64] : null;
            // рекурсивний поділ починає з відкритого поля (перший крок журналу – закриття стіни)
            startOpen = anim && result.Steps.Count > 0 && !result.Steps[0].open;
            shown = anim ? new Maze(result.W, result.H, startOpen) : result.Clone();
            StepIndex = anim ? 0 : StepCount;
            cur = cur2 = (-1, -1); region = false; Redraw();
        }

        void ApplySteps(int upto)
        {
            for (; StepIndex < upto && StepIndex < StepCount; StepIndex++) { var s = Result.Steps[StepIndex]; shown.Set(s.x, s.y, s.nx, s.ny, s.open); }
        }

        // показати ще n кроків анімації (у режимі журналу виконання – n подій)
        public void Advance(int n)
        {
            if (!Animating) return;
            if (Tr != null)
            {
                for (int i = 0; i < n && TraceIndex < TraceCount; i++)
                {
                    var e = Tr.Events[TraceIndex++]; LineHits[e.line]++; ApplySteps(e.steps);
                    cur = (e.x, e.y); cur2 = (e.x2, e.y2); region = e.region;
                }
                if (TraceIndex >= TraceCount) { ApplySteps(StepCount); cur = cur2 = (-1, -1); region = false; }
            }
            else
            {
                for (int i = 0; i < n && StepIndex < StepCount; i++, StepIndex++)
                {
                    var s = Result.Steps[StepIndex]; shown.Set(s.x, s.y, s.nx, s.ny, s.open); cur = (s.nx, s.ny);
                }
                if (!Animating) cur = (-1, -1);
            }
            Redraw();
        }

        // повернутися на n подій назад (лише в режимі журналу виконання): стан відновлюється програванням з початку
        public void Back(int n)
        {
            if (Tr == null) return;
            int target = Mathf.Max(0, TraceIndex - n);
            shown = new Maze(Result.W, Result.H, startOpen); StepIndex = 0; TraceIndex = 0; System.Array.Clear(LineHits, 0, LineHits.Length);
            cur = cur2 = (-1, -1); region = false;
            if (target > 0) Advance(target); else Redraw();
        }

        public void Finish()
        {
            if (Result == null) return;
            shown = Result.Clone(); StepIndex = StepCount; cur = cur2 = (-1, -1); region = false;
            if (Tr != null) { System.Array.Clear(LineHits, 0, LineHits.Length); foreach (var e in Tr.Events) LineHits[e.line]++; TraceIndex = TraceCount; }
            Redraw();
        }

        public void Redraw()
        {
            if (Result == null) return;
            for (int i = 0; i < pix.Length; i++) pix[i] = Bg;
            bool done = !Animating;
            if (done && ShowAgent) foreach (var (x, y) in AgentCells(Result)) Cell(x, y, AgentC);
            if (done && ShowPath) foreach (var (x, y) in Metrics.Path(Result, (0, 0), (Result.W - 1, Result.H - 1))) Cell(x, y, PathC);
            if (region && cur.x >= 0 && cur2.x >= 0)
                for (int y = Mathf.Min(cur.y, cur2.y); y <= Mathf.Max(cur.y, cur2.y); y++)
                    for (int x = Mathf.Min(cur.x, cur2.x); x <= Mathf.Max(cur.x, cur2.x); x++) Cell(x, y, RegionC);
            Cell(0, 0, StartC); Cell(Result.W - 1, Result.H - 1, ExitC);
            if (!region)
            {
                if (cur2.x >= 0) Cell(cur2.x, cur2.y, Cur2);
                if (cur.x >= 0) Cell(cur.x, cur.y, Cur);
            }
            // стіни: малюємо лише наявні стіни
            for (int y = 0; y < Result.H; y++)
                for (int x = 0; x < Result.W; x++)
                {
                    if (x == Result.W - 1 || !shown.East[x, y]) VLine((x + 1) * cs, y * cs, cs + 1, Wall);
                    if (y == Result.H - 1 || !shown.South[x, y]) HLine(x * cs, (y + 1) * cs, cs + 1, Wall);
                }
            HLine(0, 0, tw, Wall); VLine(0, 0, th, Wall);
            Tex.SetPixels(pix); Tex.Apply();
        }

        // клітини, які обробив алгоритмічний агент (той самий, що і в метриці «оброблено клітин агентом»)
        static IEnumerable<(int, int)> AgentCells(Maze m)
        {
            var rng = new System.Random(1); var seen = new bool[m.W, m.H]; var st = new Stack<(int, int)>(); st.Push((0, 0)); seen[0, 0] = true; var buf = new (int, int)[4];
            while (st.Count > 0)
            {
                var c = st.Pop(); yield return c; if (c == (m.W - 1, m.H - 1)) yield break;
                int n = m.Links(c.Item1, c.Item2, buf);
                for (int i = n - 1; i > 0; i--) { int j = rng.Next(i + 1); (buf[i], buf[j]) = (buf[j], buf[i]); }
                for (int i = 0; i < n; i++) if (!seen[buf[i].Item1, buf[i].Item2]) { seen[buf[i].Item1, buf[i].Item2] = true; st.Push(buf[i]); }
            }
        }

        // текстура: рядок 0 – низ, а клітина (0,0) – лівий верхній кут
        void Px(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < tw && y < th) pix[(th - 1 - y) * tw + x] = c; }
        void HLine(int x, int y, int len, Color c) { int t = cs >= 10 ? 2 : 1; for (int i = 0; i < len; i++) for (int k = 0; k < t; k++) Px(x + i, y + k, c); }
        void VLine(int x, int y, int len, Color c) { int t = cs >= 10 ? 2 : 1; for (int i = 0; i < len; i++) for (int k = 0; k < t; k++) Px(x + k, y + i, c); }
        void Cell(int x, int y, Color c) { int m = cs >= 8 ? 2 : 1; for (int yy = y * cs + m; yy < (y + 1) * cs - m + 1; yy++) for (int xx = x * cs + m; xx < (x + 1) * cs - m + 1; xx++) Px(xx, yy, c); }
    }
}
