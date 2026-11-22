// 10 алгоритмів генерації лабіринтів — усі взяті з літератури:
// 1–6 — класичні графові алгоритми з табл. 1 статті Трофименко та ін. (2025): пошук у глибину, Прим, Крускал,
//        Олдос–Бродер, Вілсон, Еллер;
// 7–8 — класичні алгоритми з книги Buck (2015), розглянуті також у статті Марчук та ін. (2024): Hunt-and-Kill, Binary Tree;
// 9   — рекурсивний поділ (рекурсивний клас, Трофименко та ін., табл. 5);
// 10  — гібрид Олдоса–Бродера і Вілсона з порогом за формулою Yang et al. (2024).
// Усі алгоритми дають ідеальний лабіринт (остовне дерево сітки). Перевірки — Verification.cs.
using System;
using System.Collections.Generic;

namespace MazeStudy
{
    public static class Generators
    {
        public delegate Maze Gen(int w, int h, Random rng);

        public static readonly (string name, Gen gen)[] All =
        {
            ("Пошук у глибину (DFS)", RecursiveBacktracker),
            ("Алгоритм Прима", Prim),
            ("Алгоритм Крускала", Kruskal),
            ("Алгоритм Олдоса–Бродера", AldousBroder),
            ("Алгоритм Вілсона", Wilson),
            ("Алгоритм Еллера", Eller),
            ("Hunt-and-Kill", HuntAndKill),
            ("Binary Tree", BinaryTree),
            ("Рекурсивний поділ", RecursiveDivision),
            ("Гібрид Олдоса–Бродера і Вілсона", Hybrid),
        };

        // Якщо true — новий лабіринт веде журнал кроків (для анімації). Для вимірювання часу — false.
        [ThreadStatic] public static bool Record;
        static Maze New(int w, int h, bool open = false) { var m = new Maze(w, h, open); if (Record) m.Steps = new List<(int, int, int, int, bool)>(); return m; }

        // ---------- 1. Пошук у глибину (Recursive Backtracker) ----------
        // Іде у випадкову невідвідану сусідню клітину; у глухому куті повертається назад по стеку.
        public static Maze RecursiveBacktracker(int w, int h, Random rng)
        {
            var m = New(w, h);
            var vis = new bool[w, h]; var nb = new (int, int)[4]; var cand = new (int, int)[4];
            var stack = new Stack<(int, int)>();
            int sx = rng.Next(w), sy = rng.Next(h);
            stack.Push((sx, sy)); vis[sx, sy] = true;
            while (stack.Count > 0)
            {
                var (x, y) = stack.Peek();
                int k = 0, n = m.Neighbors(x, y, nb);
                for (int i = 0; i < n; i++) if (!vis[nb[i].Item1, nb[i].Item2]) cand[k++] = nb[i];
                if (k > 0) { var (nx, ny) = cand[rng.Next(k)]; m.Carve(x, y, nx, ny); vis[nx, ny] = true; stack.Push((nx, ny)); }
                else stack.Pop();
            }
            return m;
        }

        // ---------- 2. Алгоритм Прима (рандомізований) ----------
        // Дерево росте з «фронтиру»: щоразу випадкова клітина межі приєднується до випадкового сусіда в лабіринті.
        public static Maze Prim(int w, int h, Random rng)
        {
            var m = New(w, h);
            var inside = new bool[w, h]; var inF = new bool[w, h];
            var frontier = new List<(int, int)>(); var nb = new (int, int)[4]; var cand = new (int, int)[4];
            void Add(int ax, int ay)
            {
                inside[ax, ay] = true;
                int n = m.Neighbors(ax, ay, nb);
                for (int i = 0; i < n; i++) { var (nx, ny) = nb[i]; if (!inside[nx, ny] && !inF[nx, ny]) { inF[nx, ny] = true; frontier.Add((nx, ny)); } }
            }
            Add(rng.Next(w), rng.Next(h));
            while (frontier.Count > 0)
            {
                int i = rng.Next(frontier.Count); var (x, y) = frontier[i];
                frontier[i] = frontier[frontier.Count - 1]; frontier.RemoveAt(frontier.Count - 1);
                int k = 0, n = m.Neighbors(x, y, nb);
                for (int j = 0; j < n; j++) if (inside[nb[j].Item1, nb[j].Item2]) cand[k++] = nb[j];
                var (vx, vy) = cand[rng.Next(k)];
                m.Carve(x, y, vx, vy); Add(x, y);
            }
            return m;
        }

        // ---------- 3. Алгоритм Крускала (рандомізований) ----------
        // Стіни у випадковому порядку; стіну прибирають, якщо вона розділяє різні множини (система неперетинних множин, DSU).
        public static Maze Kruskal(int w, int h, Random rng)
        {
            var m = New(w, h);
            var parent = new int[w * h]; for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            var edges = AllWalls(w, h); Shuffle(edges, rng);
            foreach (var (x, y, nx, ny) in edges)
            {
                int a = Find(y * w + x), b = Find(ny * w + nx);
                if (a != b) { parent[a] = b; m.Carve(x, y, nx, ny); }
            }
            return m;
        }

        // ---------- 4. Алгоритм Олдоса–Бродера ----------
        // Випадкове блукання; прохід прорізається при першому вході в нову клітину. Рівномірно випадкове остовне дерево.
        public static Maze AldousBroder(int w, int h, Random rng)
        {
            var m = New(w, h);
            var vis = new bool[w, h]; var nb = new (int, int)[4];
            int x = rng.Next(w), y = rng.Next(h), left = w * h - 1; vis[x, y] = true;
            while (left > 0)
            {
                int n = m.Neighbors(x, y, nb); var (nx, ny) = nb[rng.Next(n)];
                if (!vis[nx, ny]) { m.Carve(x, y, nx, ny); vis[nx, ny] = true; left--; }
                x = nx; y = ny;
            }
            return m;
        }

        // ---------- 5. Алгоритм Вілсона ----------
        // Блукання зі стиранням петель (loop-erased random walk) до вже побудованої частини. Теж рівномірний, але швидший.
        public static Maze Wilson(int w, int h, Random rng)
        {
            var m = New(w, h);
            var inMaze = new bool[w, h];
            var cells = new List<(int, int)>(); for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) cells.Add((x, y));
            Shuffle(cells, rng);
            var f = cells[cells.Count - 1]; cells.RemoveAt(cells.Count - 1); inMaze[f.Item1, f.Item2] = true;
            WilsonWalks(m, inMaze, cells, rng);
            return m;
        }

        // блукання Вілсона з кожної клітини списку, що ще не в лабіринті
        static void WilsonWalks(Maze m, bool[,] inMaze, List<(int, int)> starts, Random rng)
        {
            var nb = new (int, int)[4]; var next = new (int, int)[m.W, m.H];
            foreach (var (sx, sy) in starts)
            {
                if (inMaze[sx, sy]) continue;
                int x = sx, y = sy;
                while (!inMaze[x, y]) { int n = m.Neighbors(x, y, nb); var nn = nb[rng.Next(n)]; next[x, y] = nn; (x, y) = nn; } // петлі стираються: зберігається лише останній напрям
                x = sx; y = sy;
                while (!inMaze[x, y]) { var (nx, ny) = next[x, y]; m.Carve(x, y, nx, ny); inMaze[x, y] = true; x = nx; y = ny; }
            }
        }

        // ---------- 6. Алгоритм Еллера ----------
        // Рядок за рядком; у пам'яті лише множини поточного рядка (O(W)) — можна генерувати нескінченний лабіринт.
        public static Maze Eller(int w, int h, Random rng)
        {
            var m = New(w, h);
            var sets = new int[w]; for (int i = 0; i < w; i++) sets[i] = -1;
            int nextId = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++) if (sets[x] < 0) sets[x] = nextId++;
                bool last = y == h - 1;
                for (int x = 0; x < w - 1; x++)
                    if (sets[x] != sets[x + 1] && (last || rng.NextDouble() < 0.5))
                    {
                        m.Carve(x, y, x + 1, y);
                        int old = sets[x + 1], nw = sets[x];
                        for (int i = 0; i < w; i++) if (sets[i] == old) sets[i] = nw;
                    }
                if (last) break;
                var groups = new Dictionary<int, List<int>>();
                for (int x = 0; x < w; x++) { if (!groups.TryGetValue(sets[x], out var l)) groups[sets[x]] = l = new List<int>(); l.Add(x); }
                var ns = new int[w]; for (int i = 0; i < w; i++) ns[i] = -1;
                foreach (var kv in groups)
                {
                    var xs = kv.Value; Shuffle(xs, rng);
                    int k = 1 + rng.Next(xs.Count); // кожна множина — хоча б один прохід униз
                    for (int i = 0; i < k; i++) { m.Carve(xs[i], y, xs[i], y + 1); ns[xs[i]] = kv.Key; }
                }
                sets = ns;
            }
            return m;
        }

        // ---------- 7. Hunt-and-Kill ----------
        // Як пошук у глибину, але в глухому куті не повертається назад, а «полює»: сканує сітку рядок за рядком
        // у пошуках невідвіданої клітини поруч із відвіданою, приєднує її і продовжує блукання з неї.
        public static Maze HuntAndKill(int w, int h, Random rng)
        {
            var m = New(w, h);
            var vis = new bool[w, h]; var nb = new (int, int)[4]; var cand = new (int, int)[4];
            int x = rng.Next(w), y = rng.Next(h), left = w * h - 1; vis[x, y] = true;
            while (left > 0)
            {
                int k = 0, n = m.Neighbors(x, y, nb);
                for (int i = 0; i < n; i++) if (!vis[nb[i].Item1, nb[i].Item2]) cand[k++] = nb[i];
                if (k > 0) { var (nx, ny) = cand[rng.Next(k)]; m.Carve(x, y, nx, ny); x = nx; y = ny; vis[x, y] = true; left--; continue; } // «вбивство»: іти далі
                bool found = false; // «полювання»: перша невідвідана клітина з відвіданим сусідом
                for (int hy = 0; hy < h && !found; hy++)
                    for (int hx = 0; hx < w && !found; hx++)
                    {
                        if (vis[hx, hy]) continue;
                        k = 0; n = m.Neighbors(hx, hy, nb);
                        for (int i = 0; i < n; i++) if (vis[nb[i].Item1, nb[i].Item2]) cand[k++] = nb[i];
                        if (k > 0) { var (vx, vy) = cand[rng.Next(k)]; m.Carve(hx, hy, vx, vy); x = hx; y = hy; vis[x, y] = true; left--; found = true; }
                    }
            }
            return m;
        }

        // ---------- 8. Binary Tree ----------
        // Для кожної клітини випадково прорізає прохід на північ або на схід (якщо одного з напрямків немає — інший).
        // Найпростіший і найшвидший, але з сильним діагональним зміщенням: верхній рядок і правий стовпець — суцільні коридори.
        public static Maze BinaryTree(int w, int h, Random rng)
        {
            var m = New(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool north = y > 0, east = x < w - 1;
                    if (north && east) { if (rng.Next(2) == 0) m.Carve(x, y, x, y - 1); else m.Carve(x, y, x + 1, y); }
                    else if (north) m.Carve(x, y, x, y - 1);
                    else if (east) m.Carve(x, y, x + 1, y);
                }
            return m;
        }

        // ---------- 9. Рекурсивний поділ ----------
        // Порожнє поле ділиться стіною з одним проходом; далі так само кожна половина. Єдиний, що «додає стіни».
        public static Maze RecursiveDivision(int w, int h, Random rng)
        {
            var m = New(w, h, true);
            var st = new Stack<(int, int, int, int)>(); st.Push((0, 0, w, h));
            while (st.Count > 0)
            {
                var (x0, y0, rw, rh) = st.Pop();
                if (rw < 2 || rh < 2) continue;
                bool horiz = rw < rh || (rw == rh && rng.NextDouble() < 0.5);
                if (horiz)
                {
                    int wy = y0 + rng.Next(rh - 1), gap = x0 + rng.Next(rw);
                    for (int x = x0; x < x0 + rw; x++) if (x != gap) m.Set(x, wy, x, wy + 1, false);
                    st.Push((x0, y0, rw, wy - y0 + 1)); st.Push((x0, wy + 1, rw, y0 + rh - wy - 1));
                }
                else
                {
                    int wx = x0 + rng.Next(rw - 1), gap = y0 + rng.Next(rh);
                    for (int y = y0; y < y0 + rh; y++) if (y != gap) m.Set(wx, y, wx + 1, y, false);
                    st.Push((x0, y0, wx - x0 + 1, rh)); st.Push((wx + 1, y0, x0 + rw - wx - 1, rh));
                }
            }
            return m;
        }

        // ---------- 10. Гібрид Олдоса–Бродера і Вілсона (Yang et al., 2024) ----------
        // Олдос–Бродер швидко покриває перші клітини, але довго шукає останні; Вілсон — навпаки.
        // Перемикання — за покриттям (часткою клітин), як у Yang et al.: AB до частки, яку дає формула статті (YangCoverage), далі — Вілсон.
        // Поріг завжди за формулою статті — ручного налаштування немає, щоб алгоритм відповідав джерелу без змін.
        // УВАГА: кожен складник окремо рівномірний, але гібрид при проміжних порогах — ні (див. Verification.Uniformity):
        // доведення рівномірності (Nunes et al., 2022) стосується інших моментів перемикання і повних графів, а не решітки.
        public static Maze Hybrid(int w, int h, Random rng) => HybridAtThreshold(w, h, rng, YangCoverage(w * h));

        // Той самий гібрид із довільним порогом. Це НЕ окремий генератор і в порівнянні не використовується:
        // лише для перевірки твердження статті про рівномірність при різних порогах (Verification).
        public static Maze HybridAtThreshold(int w, int h, Random rng, double sw)
        {
            var m = New(w, h);
            var inMaze = new bool[w, h]; var nb = new (int, int)[4];
            int N = w * h, count = 1, x = rng.Next(w), y = rng.Next(h); inMaze[x, y] = true;
            while (count < N * sw)
            {
                int n = m.Neighbors(x, y, nb); var (nx, ny) = nb[rng.Next(n)];
                if (!inMaze[nx, ny]) { m.Carve(x, y, nx, ny); inMaze[nx, ny] = true; count++; }
                x = nx; y = ny;
            }
            var rest = new List<(int, int)>(); for (int sy = 0; sy < h; sy++) for (int sx = 0; sx < w; sx++) if (!inMaze[sx, sy]) rest.Add((sx, sy));
            WilsonWalks(m, inMaze, rest, rng);
            return m;
        }

        // Оптимальна частка покриття за Yang et al. (2024), «Final relationship»: поліном 6-го степеня від розміру карти.
        // Розмір нормується min–max на інтервалі 100…10 000 клітин, як у статті (припущення щодо нормування — уточнити).
        public static double YangCoverage(int cells)
        {
            double m = Math.Max(0, Math.Min(1, (cells - 100.0) / (10000 - 100)));
            double c = -10.522289687741 * Math.Pow(m, 6) + 17.0755712129219 * Math.Pow(m, 5) + 8.7305043885282 * Math.Pow(m, 4)
                       - 31.9438842640165 * Math.Pow(m, 3) + 22.0572275319729 * m * m - 6.01193589227465 * m + 0.625123916505794;
            return Math.Max(0, Math.Min(1, c));
        }

        // ---------- допоміжне ----------
        // Мутація «заміна ребра» (використовується ШІ-модулем для доведення лабіринту до цілі, AiDesigner.Refine):
        // відкрити випадкову стіну — утвориться цикл; закрити випадковий прохід на цьому циклі — лабіринт залишається ідеальним.
        public static void SwapEdge(Maze m, Random rng) => EdgeSwap(m, rng);

        static void EdgeSwap(Maze m, Random rng)
        {
            int x, y, nx, ny;
            while (true)
            {
                x = rng.Next(m.W); y = rng.Next(m.H);
                if (rng.Next(2) == 0) { nx = x + 1; ny = y; } else { nx = x; ny = y + 1; }
                if (nx < m.W && ny < m.H && !m.IsOpen(x, y, nx, ny)) break;
            }
            var path = Metrics.Path(m, (x, y), (nx, ny));
            m.Carve(x, y, nx, ny);
            int i = rng.Next(path.Count - 1);
            m.Set(path[i].Item1, path[i].Item2, path[i + 1].Item1, path[i + 1].Item2, false);
        }

        static List<(int, int, int, int)> AllWalls(int w, int h)
        {
            var e = new List<(int, int, int, int)>();
            for (int y = 0; y < h; y++) for (int x = 0; x < w - 1; x++) e.Add((x, y, x + 1, y));
            for (int y = 0; y < h - 1; y++) for (int x = 0; x < w; x++) e.Add((x, y, x, y + 1));
            return e;
        }

        static void Shuffle<T>(List<T> l, Random rng) { for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); } }
    }
}
