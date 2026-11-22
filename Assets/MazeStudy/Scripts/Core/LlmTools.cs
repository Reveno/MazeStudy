// Інструменти для експериментів із великими мовними моделями (LLM) і для перевірки рівномірності.
// LLM запускаються локально через Ollama (http://localhost:11434); тут — лише текст запитів і автоматична перевірка відповідей.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MazeStudy
{
    public static class LlmTools
    {
        // ---------- лабіринт у текстовому вигляді: '#' — стіна, '.' — прохід, 'S' — старт, 'E' — вихід ----------
        public static string[] ToAscii(Maze m)
        {
            int S = 2 * m.W + 1, T = 2 * m.H + 1; var g = new char[T][];
            for (int y = 0; y < T; y++) { g[y] = new char[S]; for (int x = 0; x < S; x++) g[y][x] = '#'; }
            for (int y = 0; y < m.H; y++)
                for (int x = 0; x < m.W; x++)
                {
                    g[2 * y + 1][2 * x + 1] = '.';
                    if (x < m.W - 1 && m.East[x, y]) g[2 * y + 1][2 * x + 2] = '.';
                    if (y < m.H - 1 && m.South[x, y]) g[2 * y + 2][2 * x + 1] = '.';
                }
            g[1][1] = 'S'; g[2 * m.H - 1][2 * m.W - 1] = 'E';
            return g.Select(r => new string(r)).ToArray();
        }

        public static string SolvePrompt(string[] grid) =>
            "Here is a maze. '#' is a wall, '.' is open floor, 'S' is the start, 'E' is the exit. Row 0 is the top line, column 0 is the left character.\n" +
            string.Join("\n", grid) + "\nFind a path from S to E moving one character at a time: U = up, D = down, L = left, R = right. You cannot move into '#'.\n" +
            "Answer with ONLY one line containing the moves as letters U, D, L, R (no spaces, no explanation).";

        public static string JudgePrompt(string[] grid) =>
            "Here is a maze. '#' is a wall, '.' is open floor, 'S' is the start, 'E' is the exit.\n" + string.Join("\n", grid) +
            "\nHow hard is it for a human player to find the way from S to E? Rate the difficulty from 1 (very easy) to 10 (very hard).\nAnswer with ONLY one integer.";

        public static string GeneratePrompt(int n) =>
            $"Generate a perfect maze (exactly one path between any two cells, all cells reachable) on a {n}x{n} grid of cells.\n" +
            $"Output ONLY {2 * n + 1} lines of {2 * n + 1} characters each: '#' = wall, '.' = open.\n" +
            $"Cells are at odd (row,col) positions (1,1),(1,3)...({2 * n - 1},{2 * n - 1}) and must be '.'. The outer border must be '#'.\n" +
            "A position between two neighbouring cells is '.' if there is a passage, otherwise '#'. Positions with both coordinates even are '#'.\nNo other text.";

        // перевірка шляху з відповіді моделі: береться найдовша послідовність із великих U/D/L/R
        public static (bool ok, int steps, string why) CheckPath(string[] grid, string answer)
        {
            var runs = Regex.Matches(answer ?? "", @"[UDLR](?:[\s,]*[UDLR])*").Cast<Match>().Select(m => Regex.Replace(m.Value, "[^UDLR]", "")).ToList();
            string moves = runs.Count > 0 ? runs.OrderByDescending(r => r.Length).First() : "";
            if (moves.Length == 0) return (false, 0, "немає ходів у відповіді");
            int y = 1, x = 1;
            for (int k = 0; k < moves.Length; k++)
            {
                switch (moves[k]) { case 'U': y--; break; case 'D': y++; break; case 'L': x--; break; case 'R': x++; break; }
                if (y < 0 || x < 0 || y >= grid.Length || x >= grid[y].Length || grid[y][x] == '#') return (false, k + 1, $"хід {k + 1} — у стіну");
                if (grid[y][x] == 'E') return (true, k + 1, $"вихід досягнуто за {k + 1} ходів");
            }
            return (false, moves.Length, $"після {moves.Length} ходів вихід не досягнуто");
        }

        public static int? ParseRating(string answer)
        {
            var m = Regex.Match(answer ?? "", @"\b(10|[1-9])\b"); return m.Success ? int.Parse(m.Groups[1].Value) : (int?)null;
        }

        // перевірка лабіринту, згенерованого моделлю: формат, сітка, зв'язність, ідеальність
        public static (bool format, bool grid, bool connected, bool perfect, string why) CheckGenerated(string answer, int n)
        {
            int S = 2 * n + 1;
            var lines = Regex.Replace(answer ?? "", "<think>.*?</think>", "", RegexOptions.Singleline).Split('\n')
                .Select(l => l.Trim().Trim('`')).Where(l => l.Length > 0 && l.All(c => c == '#' || c == '.')).ToList();
            if (lines.Count != S || lines.Any(l => l.Length != S)) return (false, false, false, false, $"формат: {lines.Count} рядків, довжини {string.Join(",", lines.Select(l => l.Length).Distinct())} (потрібно {S} на {S})");
            bool border = Enumerable.Range(0, S).All(i => lines[0][i] == '#' && lines[S - 1][i] == '#' && lines[i][0] == '#' && lines[i][S - 1] == '#');
            bool cells = Enumerable.Range(0, n).All(y => Enumerable.Range(0, n).All(x => lines[2 * y + 1][2 * x + 1] == '.'));
            bool nodes = Enumerable.Range(0, n + 1).All(y => Enumerable.Range(0, n + 1).All(x => lines[2 * y][2 * x] == '#'));
            if (!(border && cells && nodes)) return (true, false, false, false, "сітка: " + (!border ? "рамка не суцільна; " : "") + (!cells ? "клітини закриті; " : "") + (!nodes ? "вузли не стіни" : ""));
            var m = new Maze(n, n);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { if (x < n - 1 && lines[2 * y + 1][2 * x + 2] == '.') m.East[x, y] = true; if (y < n - 1 && lines[2 * y + 2][2 * x + 1] == '.') m.South[x, y] = true; }
            bool conn = Metrics.Compute(m, 1)["connected"] == 1, perfect = conn && m.PassageCount() == n * n - 1;
            return (true, true, conn, perfect, perfect ? "ідеальний лабіринт" : conn ? $"зв'язний, але з петлями ({m.PassageCount() - (n * n - 1)})" : "незв'язний");
        }

        // ---------- рівномірність: усі 192 остовні дерева сітки 3×3 і частота кожного ----------
        static List<string> trees;
        public static List<string> AllTrees3x3()
        {
            if (trees != null) return trees;
            var edges = new List<(int, int, int, int)>();
            for (int y = 0; y < 3; y++) for (int x = 0; x < 2; x++) edges.Add((x, y, x + 1, y));
            for (int y = 0; y < 2; y++) for (int x = 0; x < 3; x++) edges.Add((x, y, x, y + 1));
            trees = new List<string>();
            for (int mask = 0; mask < (1 << edges.Count); mask++)
            {
                if (CountBits(mask) != 8) continue; // остовне дерево 9 вершин має 8 ребер
                var m = new Maze(3, 3); for (int i = 0; i < edges.Count; i++) if ((mask >> i & 1) == 1) m.Carve(edges[i].Item1, edges[i].Item2, edges[i].Item3, edges[i].Item4);
                if (Metrics.Compute(m, 1)["connected"] == 1) trees.Add(Key(m));
            }
            return trees; // 192 дерева (теорема Кірхгофа для решітки 3×3)
        }
        static int CountBits(int v) { int c = 0; while (v != 0) { c += v & 1; v >>= 1; } return c; }

        public static string Key(Maze m)
        {
            var sb = new StringBuilder();
            for (int y = 0; y < m.H; y++) for (int x = 0; x < m.W - 1; x++) sb.Append(m.East[x, y] ? '1' : '0');
            for (int y = 0; y < m.H - 1; y++) for (int x = 0; x < m.W; x++) sb.Append(m.South[x, y] ? '1' : '0');
            return sb.ToString();
        }

        // частоти 192 дерев для алгоритму + хі-квадрат
        public static (int[] counts, double chi) TreeHistogram(int algo, int samples, int seed)
        {
            var all = AllTrees3x3(); var index = new Dictionary<string, int>(); for (int i = 0; i < all.Count; i++) index[all[i]] = i;
            var cnt = new int[all.Count]; var rng = new Random(seed);
            for (int i = 0; i < samples; i++) cnt[index[Key(Generators.All[algo].gen(3, 3, new Random(rng.Next())))]]++;
            double e = samples / (double)all.Count, chi = cnt.Sum(v => (v - e) * (v - e) / e);
            return (cnt, chi);
        }
    }
}
