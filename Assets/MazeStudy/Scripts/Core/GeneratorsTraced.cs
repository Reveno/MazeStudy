// Трасовані копії 10 алгоритмів – лише для покрокового показу «що зараз робить код» (вкладка «Генерація»).
// Логіка і порядок звернень до генератора випадкових чисел ті самі, що в Generators.cs; додано лише виклики t.At(...),
// які записують номер рядка псевдокоду, клітини, з якими працює алгоритм, і два лічильники стану.
// Час вимірюється ТІЛЬКИ за Generators.cs (він не змінювався). Те, що трасована копія дає той самий лабіринт і той самий
// журнал змін стін, перевіряє CheckEquivalence (вкладка «Перевірка», пункт 5).
using System;
using System.Collections.Generic;
using System.Text;

namespace MazeStudy
{
    // Журнал виконання: послідовність подій «рядок псевдокоду + стан»
    public class Trace
    {
        public struct Ev
        {
            public short line, x, y, x2, y2; // рядок псевдокоду (з 1); поточна клітина; друга клітина або протилежний кут області
            public bool region;              // true – (x,y)–(x2,y2) задають прямокутну область, а не дві клітини
            public int a, b;                 // два лічильники стану (їхні назви – у Listing)
            public int steps;                // скільки змін стін уже виконано на момент події
        }
        public readonly List<Ev> Events = new List<Ev>();
        public Maze M;
        public void At(int line, int x = -1, int y = -1, int x2 = -1, int y2 = -1, int a = 0, int b = 0, bool region = false)
            => Events.Add(new Ev { line = (short)line, x = (short)x, y = (short)y, x2 = (short)x2, y2 = (short)y2, region = region, a = a, b = b, steps = M.Steps.Count });
    }

    public static class GeneratorsTraced
    {
        public delegate Maze Gen(int w, int h, Random rng, Trace t);

        // порядок – як у Generators.All
        public static readonly Gen[] All = { RecursiveBacktracker, Prim, Kruskal, AldousBroder, Wilson, Eller, HuntAndKill, BinaryTree, RecursiveDivision, Hybrid };

        // Псевдокод (рядки нумеруються з 1; відступ – 4 пробіли на рівень) і назви двох лічильників стану
        public static readonly (string[] code, string a, string b)[] Listing =
        {
            (new[] {
                "обрати випадкову початкову клітину, позначити відвіданою, покласти в стек",
                "<b>поки</b> стек не порожній:",
                "    взяти клітину c з вершини стека",
                "    <b>якщо</b> c має невідвіданих сусідів:",
                "        обрати випадкового невідвіданого сусіда n",
                "        прорізати прохід між c і n, позначити n, покласти n у стек",
                "    <b>інакше</b>:",
                "        зняти c зі стека (повернення назад)" }, "Клітин у стеку", "Відвідано клітин"),
            (new[] {
                "обрати випадкову клітину, додати в лабіринт; її сусідів – у межу",
                "<b>поки</b> межа не порожня:",
                "    взяти випадкову клітину c з межі",
                "    обрати випадкового сусіда v, що вже в лабіринті",
                "    прорізати прохід між c і v",
                "    додати c в лабіринт; її нових сусідів – у межу" }, "Клітин у межі", "Клітин у лабіринті"),
            (new[] {
                "кожна клітина – окрема множина; усі стіни перемішати",
                "<b>для кожної</b> стіни між клітинами c і n (у випадковому порядку):",
                "    <b>якщо</b> c і n належать до різних множин:",
                "        об'єднати множини, прорізати прохід між c і n",
                "    <b>інакше</b>: стіну залишити (прохід утворив би цикл)" }, "Розглянуто стін", "Множин залишилось"),
            (new[] {
                "обрати випадкову початкову клітину c, позначити відвіданою",
                "<b>поки</b> є невідвідані клітини:",
                "    обрати випадкового сусіда n",
                "    <b>якщо</b> n ще не відвідана:",
                "        прорізати прохід між c і n, позначити n",
                "    перейти в n, навіть якщо n уже відвідана" }, "Невідвіданих клітин", "Кроків блукання"),
            (new[] {
                "перемішати клітини; одну додати в лабіринт",
                "<b>для кожної</b> клітини s, якої ще немає в лабіринті:",
                "    блукати від s; у кожній клітині запам'ятовувати напрям виходу (повторний візит перезаписує напрям – петля стирається)",
                "    зупинитися, коли блукання потрапило в клітину лабіринту",
                "    пройти від s за запам'ятованими напрямами:",
                "        прорізати прохід, додати клітину в лабіринт" }, "Клітин у лабіринті", "Кроків цього блукання"),
            (new[] {
                "<b>для кожного</b> рядка сітки (згори донизу):",
                "    клітинам без множини дати нові множини",
                "    <b>для кожної</b> пари сусідніх клітин рядка:",
                "        якщо вони з різних множин, то з імовірністю 1/2 (в останньому рядку – завжди) об'єднати: прорізати прохід праворуч",
                "    <b>якщо</b> рядок останній – завершити",
                "    <b>для кожної</b> множини рядка:",
                "        обрати випадково кілька клітин множини (хоча б одну) і прорізати з них проходи вниз",
                "    наступний рядок успадковує множини клітин, з яких прорізано прохід униз" }, "Рядок", "Множин у рядку"),
            (new[] {
                "обрати випадкову початкову клітину c, позначити відвіданою",
                "<b>поки</b> є невідвідані клітини:",
                "    <b>якщо</b> c має невідвіданих сусідів:",
                "        обрати невідвіданого сусіда n, прорізати прохід, перейти в n",
                "    <b>інакше</b> «полювання»:",
                "        сканувати сітку рядок за рядком: шукати першу невідвідану клітину h з відвіданим сусідом",
                "        прорізати прохід від h до випадкового відвіданого сусіда, перейти в h" }, "Невідвіданих клітин", "Полювань"),
            (new[] {
                "<b>для кожної</b> клітини c (рядок за рядком):",
                "    <b>якщо</b> c має сусіда і на півночі, і на сході:",
                "        з імовірністю 1/2 прорізати прохід на північ, інакше – на схід",
                "    <b>інакше якщо</b> є лише північний сусід: прорізати прохід на північ",
                "    <b>інакше якщо</b> є лише східний сусід: прорізати прохід на схід",
                "    <b>інакше</b> (правий верхній кут): нічого не робити" }, "Оброблено клітин", "Прорізано проходів"),
            (new[] {
                "почати з поля без внутрішніх стін; покласти все поле в стек",
                "<b>поки</b> стек не порожній:",
                "    взяти область зі стека; якщо її ширина або висота менша за 2 – пропустити",
                "    обрати напрям стіни: упоперек довшої сторони (якщо сторони рівні – випадково)",
                "    обрати випадкове місце стіни та випадковий прохід у ній",
                "    поставити стіну через усю область, залишивши один прохід",
                "    покласти обидві половини області в стек" }, "Областей у стеку", "Поставлено стін"),
            (new[] {
                "обчислити поріг за формулою зі статті Yang та ін. (2024); обрати випадкову початкову клітину c",
                "фаза 1, Олдос–Бродер: <b>поки</b> в лабіринті менше клітин, ніж поріг:",
                "    обрати випадкового сусіда n",
                "    <b>якщо</b> n не в лабіринті: прорізати прохід між c і n, додати n",
                "    перейти в n",
                "фаза 2, Вілсон: <b>для кожної</b> клітини s поза лабіринтом:",
                "    блукати від s, запам'ятовуючи напрям виходу з кожної клітини, доки блукання не потрапить у лабіринт",
                "    пройти від s за напрямами: прорізати проходи, додати клітини" }, "Клітин у лабіринті", "Поріг, клітин"),
        };

        static Maze New(int w, int h, Trace t, bool open = false)
        {
            var m = new Maze(w, h, open) { Steps = new List<(int, int, int, int, bool)>() };
            t.M = m; return m;
        }

        // ---------- 1. Пошук у глибину ----------
        public static Maze RecursiveBacktracker(int w, int h, Random rng, Trace t)
        {
            var m = New(w, h, t);
            var vis = new bool[w, h]; var nb = new (int, int)[4]; var cand = new (int, int)[4];
            var stack = new Stack<(int, int)>();
            int sx = rng.Next(w), sy = rng.Next(h), seen = 1;
            stack.Push((sx, sy)); vis[sx, sy] = true;
            t.At(1, sx, sy, a: 1, b: 1);
            while (stack.Count > 0)
            {
                var (x, y) = stack.Peek();
                t.At(3, x, y, a: stack.Count, b: seen);
                int k = 0, n = m.Neighbors(x, y, nb);
                for (int i = 0; i < n; i++) if (!vis[nb[i].Item1, nb[i].Item2]) cand[k++] = nb[i];
                if (k > 0)
                {
                    var (nx, ny) = cand[rng.Next(k)];
                    t.At(5, x, y, nx, ny, stack.Count, seen);
                    m.Carve(x, y, nx, ny); vis[nx, ny] = true; stack.Push((nx, ny)); seen++;
                    t.At(6, nx, ny, x, y, stack.Count, seen);
                }
                else { stack.Pop(); t.At(8, x, y, a: stack.Count, b: seen); }
            }
            return m;
        }

        // ---------- 2. Алгоритм Прима ----------
        public static Maze Prim(int w, int h, Random rng, Trace t)
        {
            var m = New(w, h, t);
            var inside = new bool[w, h]; var inF = new bool[w, h]; int cnt = 0;
            var frontier = new List<(int, int)>(); var nb = new (int, int)[4]; var cand = new (int, int)[4];
            void Add(int ax, int ay)
            {
                inside[ax, ay] = true; cnt++;
                int n = m.Neighbors(ax, ay, nb);
                for (int i = 0; i < n; i++) { var (nx, ny) = nb[i]; if (!inside[nx, ny] && !inF[nx, ny]) { inF[nx, ny] = true; frontier.Add((nx, ny)); } }
            }
            int fx = rng.Next(w), fy = rng.Next(h);
            Add(fx, fy);
            t.At(1, fx, fy, a: frontier.Count, b: cnt);
            while (frontier.Count > 0)
            {
                int i = rng.Next(frontier.Count); var (x, y) = frontier[i];
                frontier[i] = frontier[frontier.Count - 1]; frontier.RemoveAt(frontier.Count - 1);
                t.At(3, x, y, a: frontier.Count, b: cnt);
                int k = 0, n = m.Neighbors(x, y, nb);
                for (int j = 0; j < n; j++) if (inside[nb[j].Item1, nb[j].Item2]) cand[k++] = nb[j];
                var (vx, vy) = cand[rng.Next(k)];
                t.At(4, x, y, vx, vy, frontier.Count, cnt);
                m.Carve(x, y, vx, vy);
                t.At(5, x, y, vx, vy, frontier.Count, cnt);
                Add(x, y);
                t.At(6, x, y, a: frontier.Count, b: cnt);
            }
            return m;
        }

        // ---------- 3. Алгоритм Крускала ----------
        public static Maze Kruskal(int w, int h, Random rng, Trace t)
        {
            var m = New(w, h, t);
            var parent = new int[w * h]; for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            var edges = AllWalls(w, h); Shuffle(edges, rng);
            int sets = w * h, looked = 0;
            t.At(1, a: 0, b: sets);
            foreach (var (x, y, nx, ny) in edges)
            {
                int a = Find(y * w + x), b = Find(ny * w + nx); looked++;
                t.At(2, x, y, nx, ny, looked, sets);
                if (a != b) { parent[a] = b; m.Carve(x, y, nx, ny); sets--; t.At(4, x, y, nx, ny, looked, sets); }
                else t.At(5, x, y, nx, ny, looked, sets);
            }
            return m;
        }

        // ---------- 4. Алгоритм Олдоса–Бродера ----------
        public static Maze AldousBroder(int w, int h, Random rng, Trace t)
        {
            var m = New(w, h, t);
            var vis = new bool[w, h]; var nb = new (int, int)[4];
            int x = rng.Next(w), y = rng.Next(h), left = w * h - 1, walk = 0; vis[x, y] = true;
            t.At(1, x, y, a: left, b: 0);
            while (left > 0)
            {
                int n = m.Neighbors(x, y, nb); var (nx, ny) = nb[rng.Next(n)]; walk++;
                t.At(3, x, y, nx, ny, left, walk);
                if (!vis[nx, ny]) { m.Carve(x, y, nx, ny); vis[nx, ny] = true; left--; t.At(5, nx, ny, x, y, left, walk); }
                else t.At(6, nx, ny, x, y, left, walk);
                x = nx; y = ny;
            }
            return m;
        }

        // ---------- 5. Алгоритм Вілсона ----------
        public static Maze Wilson(int w, int h, Random rng, Trace t)
        {
            var m = New(w, h, t);
            var inMaze = new bool[w, h];
            var cells = new List<(int, int)>(); for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) cells.Add((x, y));
            Shuffle(cells, rng);
            var f = cells[cells.Count - 1]; cells.RemoveAt(cells.Count - 1); inMaze[f.Item1, f.Item2] = true;
            t.At(1, f.Item1, f.Item2, a: 1, b: 0);
            int count = 1;
            WilsonWalks(m, inMaze, cells, rng, t, 2, 3, 4, 6, ref count, -1);
            return m;
        }

        // блукання Вілсона; lStart/lWalk/lHit/lCarve – номери рядків псевдокоду; fixedB ≥ 0 – показувати його замість кількості кроків блукання
        static void WilsonWalks(Maze m, bool[,] inMaze, List<(int, int)> starts, Random rng, Trace t, int lStart, int lWalk, int lHit, int lCarve, ref int count, int fixedB)
        {
            var nb = new (int, int)[4]; var next = new (int, int)[m.W, m.H];
            foreach (var (sx, sy) in starts)
            {
                if (inMaze[sx, sy]) continue;
                t.At(lStart, sx, sy, a: count, b: fixedB >= 0 ? fixedB : 0);
                int x = sx, y = sy, ws = 0;
                while (!inMaze[x, y])
                {
                    int n = m.Neighbors(x, y, nb); var nn = nb[rng.Next(n)]; next[x, y] = nn; ws++;
                    t.At(lWalk, x, y, nn.Item1, nn.Item2, count, fixedB >= 0 ? fixedB : ws);
                    (x, y) = nn;
                }
                if (lHit != lWalk) t.At(lHit, x, y, a: count, b: fixedB >= 0 ? fixedB : ws);
                x = sx; y = sy;
                while (!inMaze[x, y])
                {
                    var (nx, ny) = next[x, y]; m.Carve(x, y, nx, ny); inMaze[x, y] = true; count++;
                    t.At(lCarve, x, y, nx, ny, count, fixedB >= 0 ? fixedB : ws);
                    x = nx; y = ny;
                }
            }
        }

        // ---------- 6. Алгоритм Еллера ----------
        public static Maze Eller(int w, int h, Random rng, Trace t)
        {
            var m = New(w, h, t);
            var sets = new int[w]; for (int i = 0; i < w; i++) sets[i] = -1;
            int nextId = 0; var distinct = new HashSet<int>();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++) if (sets[x] < 0) sets[x] = nextId++;
                bool last = y == h - 1;
                distinct.Clear(); for (int x = 0; x < w; x++) distinct.Add(sets[x]); int sc = distinct.Count;
                t.At(2, 0, y, w - 1, y, y + 1, sc, true);
                for (int x = 0; x < w - 1; x++)
                {
                    t.At(3, x, y, x + 1, y, y + 1, sc);
                    if (sets[x] != sets[x + 1] && (last || rng.NextDouble() < 0.5))
                    {
                        m.Carve(x, y, x + 1, y);
                        int old = sets[x + 1], nw = sets[x];
                        for (int i = 0; i < w; i++) if (sets[i] == old) sets[i] = nw;
                        sc--; t.At(4, x, y, x + 1, y, y + 1, sc);
                    }
                }
                if (last) { t.At(5, 0, y, w - 1, y, y + 1, sc, true); break; }
                var groups = new Dictionary<int, List<int>>();
                for (int x = 0; x < w; x++) { if (!groups.TryGetValue(sets[x], out var l)) groups[sets[x]] = l = new List<int>(); l.Add(x); }
                var ns = new int[w]; for (int i = 0; i < w; i++) ns[i] = -1;
                foreach (var kv in groups)
                {
                    var xs = kv.Value; Shuffle(xs, rng);
                    int k = 1 + rng.Next(xs.Count);
                    for (int i = 0; i < k; i++) { m.Carve(xs[i], y, xs[i], y + 1); ns[xs[i]] = kv.Key; t.At(7, xs[i], y, xs[i], y + 1, y + 1, sc); }
                }
                sets = ns;
                t.At(8, 0, y + 1, w - 1, y + 1, y + 2, sc, true);
            }
            return m;
        }

        // ---------- 7. Hunt-and-Kill ----------
        public static Maze HuntAndKill(int w, int h, Random rng, Trace t)
        {
            var m = New(w, h, t);
            var vis = new bool[w, h]; var nb = new (int, int)[4]; var cand = new (int, int)[4];
            int x = rng.Next(w), y = rng.Next(h), left = w * h - 1, hunts = 0; vis[x, y] = true;
            t.At(1, x, y, a: left, b: 0);
            while (left > 0)
            {
                int k = 0, n = m.Neighbors(x, y, nb);
                for (int i = 0; i < n; i++) if (!vis[nb[i].Item1, nb[i].Item2]) cand[k++] = nb[i];
                t.At(3, x, y, a: left, b: hunts);
                if (k > 0)
                {
                    var (nx, ny) = cand[rng.Next(k)]; m.Carve(x, y, nx, ny);
                    t.At(4, nx, ny, x, y, left - 1, hunts);
                    x = nx; y = ny; vis[x, y] = true; left--; continue;
                }
                hunts++; t.At(5, x, y, a: left, b: hunts);
                bool found = false;
                for (int hy = 0; hy < h && !found; hy++)
                {
                    t.At(6, 0, hy, w - 1, hy, left, hunts, true);
                    for (int hx = 0; hx < w && !found; hx++)
                    {
                        if (vis[hx, hy]) continue;
                        k = 0; n = m.Neighbors(hx, hy, nb);
                        for (int i = 0; i < n; i++) if (vis[nb[i].Item1, nb[i].Item2]) cand[k++] = nb[i];
                        if (k > 0)
                        {
                            var (vx, vy) = cand[rng.Next(k)]; m.Carve(hx, hy, vx, vy); x = hx; y = hy; vis[x, y] = true; left--; found = true;
                            t.At(7, hx, hy, vx, vy, left, hunts);
                        }
                    }
                }
            }
            return m;
        }

        // ---------- 8. Binary Tree ----------
        public static Maze BinaryTree(int w, int h, Random rng, Trace t)
        {
            var m = New(w, h, t); int done = 0, pass = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool north = y > 0, east = x < w - 1; done++;
                    if (north && east)
                    {
                        if (rng.Next(2) == 0) { m.Carve(x, y, x, y - 1); pass++; t.At(3, x, y, x, y - 1, done, pass); }
                        else { m.Carve(x, y, x + 1, y); pass++; t.At(3, x, y, x + 1, y, done, pass); }
                    }
                    else if (north) { m.Carve(x, y, x, y - 1); pass++; t.At(4, x, y, x, y - 1, done, pass); }
                    else if (east) { m.Carve(x, y, x + 1, y); pass++; t.At(5, x, y, x + 1, y, done, pass); }
                    else t.At(6, x, y, a: done, b: pass);
                }
            return m;
        }

        // ---------- 9. Рекурсивний поділ ----------
        public static Maze RecursiveDivision(int w, int h, Random rng, Trace t)
        {
            var m = New(w, h, t, true); int walls = 0;
            var st = new Stack<(int, int, int, int)>(); st.Push((0, 0, w, h));
            t.At(1, 0, 0, w - 1, h - 1, 1, 0, true);
            while (st.Count > 0)
            {
                var (x0, y0, rw, rh) = st.Pop();
                t.At(3, x0, y0, x0 + rw - 1, y0 + rh - 1, st.Count, walls, true);
                if (rw < 2 || rh < 2) continue;
                bool horiz = rw < rh || (rw == rh && rng.NextDouble() < 0.5);
                t.At(4, x0, y0, x0 + rw - 1, y0 + rh - 1, st.Count, walls, true);
                if (horiz)
                {
                    int wy = y0 + rng.Next(rh - 1), gap = x0 + rng.Next(rw);
                    t.At(5, gap, wy, gap, wy + 1, st.Count, walls);
                    for (int x = x0; x < x0 + rw; x++) if (x != gap) m.Set(x, wy, x, wy + 1, false);
                    walls++; t.At(6, x0, wy, x0 + rw - 1, wy + 1, st.Count, walls, true);
                    st.Push((x0, y0, rw, wy - y0 + 1)); st.Push((x0, wy + 1, rw, y0 + rh - wy - 1));
                }
                else
                {
                    int wx = x0 + rng.Next(rw - 1), gap = y0 + rng.Next(rh);
                    t.At(5, wx, gap, wx + 1, gap, st.Count, walls);
                    for (int y = y0; y < y0 + rh; y++) if (y != gap) m.Set(wx, y, wx + 1, y, false);
                    walls++; t.At(6, wx, y0, wx + 1, y0 + rh - 1, st.Count, walls, true);
                    st.Push((x0, y0, wx - x0 + 1, rh)); st.Push((wx + 1, y0, x0 + rw - wx - 1, rh));
                }
                t.At(7, x0, y0, x0 + rw - 1, y0 + rh - 1, st.Count, walls, true);
            }
            return m;
        }

        // ---------- 10. Гібрид Олдоса–Бродера і Вілсона ----------
        public static Maze Hybrid(int w, int h, Random rng, Trace t)
        {
            double sw = Generators.YangCoverage(w * h);
            var m = New(w, h, t);
            var inMaze = new bool[w, h]; var nb = new (int, int)[4];
            int N = w * h, count = 1, x = rng.Next(w), y = rng.Next(h); inMaze[x, y] = true;
            int thr = (int)Math.Ceiling(N * sw);
            t.At(1, x, y, a: 1, b: thr);
            while (count < N * sw)
            {
                int n = m.Neighbors(x, y, nb); var (nx, ny) = nb[rng.Next(n)];
                t.At(3, x, y, nx, ny, count, thr);
                if (!inMaze[nx, ny]) { m.Carve(x, y, nx, ny); inMaze[nx, ny] = true; count++; t.At(4, nx, ny, x, y, count, thr); }
                else t.At(5, nx, ny, x, y, count, thr);
                x = nx; y = ny;
            }
            var rest = new List<(int, int)>(); for (int sy = 0; sy < h; sy++) for (int sx = 0; sx < w; sx++) if (!inMaze[sx, sy]) rest.Add((sx, sy));
            WilsonWalks(m, inMaze, rest, rng, t, 6, 7, 7, 8, ref count, thr);
            return m;
        }

        // ---------- допоміжне (ті самі, що в Generators.cs) ----------
        static List<(int, int, int, int)> AllWalls(int w, int h)
        {
            var e = new List<(int, int, int, int)>();
            for (int y = 0; y < h; y++) for (int x = 0; x < w - 1; x++) e.Add((x, y, x + 1, y));
            for (int y = 0; y < h - 1; y++) for (int x = 0; x < w; x++) e.Add((x, y, x, y + 1));
            return e;
        }

        static void Shuffle<T>(List<T> l, Random rng) { for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); } }

        // чи збігаються два лабіринти й (якщо є) їхні журнали змін стін
        public static bool Same(Maze a, Maze b)
        {
            if (a.W != b.W || a.H != b.H) return false;
            for (int y = 0; y < a.H; y++) for (int x = 0; x < a.W; x++) if (a.East[x, y] != b.East[x, y] || a.South[x, y] != b.South[x, y]) return false;
            if (a.Steps != null && b.Steps != null)
            {
                if (a.Steps.Count != b.Steps.Count) return false;
                for (int i = 0; i < a.Steps.Count; i++) if (a.Steps[i] != b.Steps[i]) return false;
            }
            return true;
        }

        // Перевірка: трасована копія з тим самим seed дає той самий лабіринт і той самий журнал змін стін, що й Generators.cs
        public static string CheckEquivalence(int seeds = 20)
        {
            var sizes = new[] { 2, 3, 5, 10, 15, 25, 50 }; var r = new StringBuilder();
            for (int a = 0; a < All.Length; a++)
            {
                int total = 0, bad = 0;
                foreach (int s in sizes)
                    for (int seed = 1; seed <= seeds; seed++)
                    {
                        total++;
                        Generators.Record = true; var orig = Generators.All[a].gen(s, s, new Random(seed)); Generators.Record = false;
                        var traced = All[a](s, s, new Random(seed), new Trace());
                        if (!Same(orig, traced)) bad++;
                    }
                r.AppendLine($"  {Generators.All[a].name}: порівняно {total}; розбіжностей {bad}");
            }
            return r.ToString();
        }
    }
}
