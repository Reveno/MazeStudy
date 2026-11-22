// Метрики лабіринту — за критеріями з рекомендацій керівника:
// час виконання і складність алгоритму (вимірюються в Experiment), різноманітність результатів (Diversity),
// довжина шляху, кількість розгалужень, баланс між складністю та прохідністю (тупики, петлі, оброблені агентом клітини відносно довжини шляху).
// Старт — клітина (0,0), вихід — (W-1,H-1).
using System;
using System.Collections.Generic;

namespace MazeStudy
{
    public static class Metrics
    {
        // Ключ, підпис (укр.) і формат — однакові для інтерфейсу, CSV і графіків
        public static readonly (string key, string label, string fmt, bool percent)[] List =
        {
            ("path_len", "Довжина шляху до виходу, клітин", "F1", false),
            ("path_share", "Частка клітин на шляху", "P1", true),
            ("tortuosity", "Коефіцієнт подовження шляху", "F2", false),
            ("path_turns", "Поворотів на шляху", "F1", false),
            ("path_decisions", "Розвилок на шляху", "F1", false),
            ("dead_ends", "Частка тупиків", "P1", true),
            ("branches", "Частка розгалужень (3–4 виходи)", "P1", true),
            ("corridor", "Середня довжина коридору, клітин", "F2", false),
            ("loops", "Петель (циклів)", "F1", false),
            ("agent", "Клітин агента на клітину шляху", "F2", false),
        };

        // ---------- пошук у ширину: відстані від клітини ----------
        public static int[,] Bfs(Maze m, int sx, int sy, out (int, int)[,] prev)
        {
            var d = new int[m.W, m.H]; prev = new (int, int)[m.W, m.H];
            for (int y = 0; y < m.H; y++) for (int x = 0; x < m.W; x++) d[x, y] = -1;
            var q = new Queue<(int, int)>(); q.Enqueue((sx, sy)); d[sx, sy] = 0; var buf = new (int, int)[4];
            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue(); int n = m.Links(x, y, buf);
                for (int i = 0; i < n; i++) { var (nx, ny) = buf[i]; if (d[nx, ny] < 0) { d[nx, ny] = d[x, y] + 1; prev[nx, ny] = (x, y); q.Enqueue((nx, ny)); } }
            }
            return d;
        }

        // найкоротший шлях (список клітин від s до g)
        public static List<(int, int)> Path(Maze m, (int, int) s, (int, int) g)
        {
            var d = Bfs(m, s.Item1, s.Item2, out var prev); var path = new List<(int, int)>();
            if (d[g.Item1, g.Item2] < 0) return path;
            for (var c = g; ; c = prev[c.Item1, c.Item2]) { path.Add(c); if (c == s) break; }
            path.Reverse(); return path;
        }

        // «Наївний гравець»: пошук у глибину з випадковим вибором напрямку. Скільки клітин він відвідає на одну клітину шляху.
        // 1,0 — ішов одразу правильно; чим більше — тим довше блукав.
        public static double AgentOverhead(Maze m, Random rng)
        {
            var seen = new bool[m.W, m.H]; var st = new Stack<(int, int)>(); st.Push((0, 0)); seen[0, 0] = true;
            int visits = 0; var goal = (m.W - 1, m.H - 1); var buf = new (int, int)[4];
            while (st.Count > 0)
            {
                var c = st.Pop(); visits++;
                if (c == goal) break;
                int n = m.Links(c.Item1, c.Item2, buf);
                for (int i = n - 1; i > 0; i--) { int j = rng.Next(i + 1); (buf[i], buf[j]) = (buf[j], buf[i]); }
                for (int i = 0; i < n; i++) if (!seen[buf[i].Item1, buf[i].Item2]) { seen[buf[i].Item1, buf[i].Item2] = true; st.Push(buf[i]); }
            }
            return (double)visits / Path(m, (0, 0), goal).Count;
        }

        public static Dictionary<string, double> Compute(Maze m, int agentRuns = 5)
        {
            int N = m.W * m.H; var buf = new (int, int)[4];
            int dead = 0, branch = 0;
            for (int y = 0; y < m.H; y++)
                for (int x = 0; x < m.W; x++) { int k = m.Links(x, y, buf); if (k == 1) dead++; else if (k >= 3) branch++; }
            // зв'язність: усі клітини досяжні зі старту
            var d0 = Bfs(m, 0, 0, out _); bool connected = true;
            foreach (var v in d0) if (v < 0) connected = false;
            var sol = Path(m, (0, 0), (m.W - 1, m.H - 1));
            int turns = 0, decisions = 0;
            for (int i = 1; i < sol.Count - 1; i++)
            {
                int dx1 = sol[i].Item1 - sol[i - 1].Item1, dy1 = sol[i].Item2 - sol[i - 1].Item2, dx2 = sol[i + 1].Item1 - sol[i].Item1, dy2 = sol[i + 1].Item2 - sol[i].Item2;
                if (dx1 != dx2 || dy1 != dy2) turns++;
                if (m.Links(sol[i].Item1, sol[i].Item2, buf) >= 3) decisions++;
            }
            // коридори: зв'язні ділянки з клітин, що мають рівно 2 виходи
            var inC = new bool[m.W, m.H]; int corridors = 0, corrCells = 0; var cs = new Stack<(int, int)>(); var b2 = new (int, int)[4];
            for (int y = 0; y < m.H; y++)
                for (int x = 0; x < m.W; x++)
                {
                    if (inC[x, y] || m.Links(x, y, buf) != 2) continue;
                    corridors++; cs.Push((x, y)); inC[x, y] = true;
                    while (cs.Count > 0)
                    {
                        var (cx, cy) = cs.Pop(); corrCells++; int n = m.Links(cx, cy, b2);
                        for (int i = 0; i < n; i++) { var (nx, ny) = b2[i]; if (!inC[nx, ny] && m.Links(nx, ny, buf) == 2) { inC[nx, ny] = true; cs.Push((nx, ny)); } }
                    }
                }
            double agent = 0; for (int r = 0; r < agentRuns; r++) agent += AgentOverhead(m, new Random(r + 1)) / agentRuns;
            return new Dictionary<string, double>
            {
                ["path_len"] = sol.Count, ["path_share"] = (double)sol.Count / N,
                ["tortuosity"] = (sol.Count - 1.0) / (m.W + m.H - 2), ["path_turns"] = turns, ["path_decisions"] = decisions,
                ["dead_ends"] = (double)dead / N, ["branches"] = (double)branch / N,
                ["corridor"] = corridors > 0 ? (double)corrCells / corridors : 0,
                ["loops"] = m.PassageCount() - (N - 1), // цикломатичне число: 0 для ідеального лабіринту
                ["agent"] = agent, ["connected"] = connected ? 1 : 0,
            };
        }

        // Різноманітність результатів: середня частка стін, що відрізняються, між парами лабіринтів одного алгоритму
        // (різні seed). 0 — алгоритм завжди дає той самий лабіринт; ≈0,5 — лабіринти майже незалежні.
        public static double Diversity(IList<Maze> ms)
        {
            if (ms.Count < 2) return 0;
            int W = ms[0].W, H = ms[0].H, edges = (W - 1) * H + W * (H - 1); double sum = 0; int pairs = 0;
            for (int a = 0; a < ms.Count; a++)
                for (int b = a + 1; b < ms.Count; b++)
                {
                    int diff = 0;
                    for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                        { if (x < W - 1 && ms[a].East[x, y] != ms[b].East[x, y]) diff++; if (y < H - 1 && ms[a].South[x, y] != ms[b].South[x, y]) diff++; }
                    sum += (double)diff / edges; pairs++;
                }
            return sum / pairs;
        }
    }
}
