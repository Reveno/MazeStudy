// Експеримент: кожен алгоритм × кожен розмір × N запусків з однаковими seed для всіх алгоритмів.
// Час генерації вимірюється без журналу кроків і без побудови графіки — лише сам алгоритм.
// Результат: сирі вимірювання + зведення (середнє, стандартне відхилення, 95 % довірчий інтервал) + різноманітність.
// Повторюваність: експеримент можна повторити кількома незалежними серіями (Repeats) з різними seed і перевірити,
// чи не міняються алгоритми місцями між серіями (стабільність рангів). Якщо міняються — вибірка замала.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MazeStudy
{
    public class Experiment
    {
        public int[] Sizes = { 10, 25, 50, 100 };
        public int Runs = 30;
        public int Repeats = 1; // кількість незалежних серій по Runs запусків (кожна — зі своїми seed)
        public int[] Algorithms = Enumerable.Range(0, Generators.All.Length).ToArray();
        // seed для кожної пари «розмір × запуск» (однакові для всіх алгоритмів — чесне порівняння).
        // Якщо не задано — випадкові. Усі використані seed записуються в raw.csv, тож прогін можна повторити.
        public int[] Seeds;
        public int RunsTotal => Runs * Repeats;
        int SeedFor(int sizeIdx, int run) => Seeds != null && Seeds.Length >= Sizes.Length * RunsTotal ? Seeds[sizeIdx * RunsTotal + run] : autoSeeds[sizeIdx * RunsTotal + run];
        public int SeriesOf(int run) => run / Runs;
        int[] autoSeeds;

        public readonly List<(int algo, int size, int run, int seed, double ms, Dictionary<string, double> m)> Raw = new List<(int, int, int, int, double, Dictionary<string, double>)>();
        public readonly Dictionary<(int algo, int size), double> DiversityOf = new Dictionary<(int, int), double>();

        // кількість задач для індикатора прогресу
        public int Total => Algorithms.Length * Sizes.Length;

        // Запускає експеримент; після кожної пари «алгоритм × розмір» повідомляє прогрес (для інтерфейсу — по кроку на кадр)
        public IEnumerable<int> RunStepwise()
        {
            int done = 0;
            var rnd = new Random(Guid.NewGuid().GetHashCode()); var uniq = new HashSet<int>(); autoSeeds = new int[Sizes.Length * RunsTotal]; stats.Clear();
            for (int i = 0; i < autoSeeds.Length; i++) { int v; do v = rnd.Next(1, int.MaxValue); while (!uniq.Add(v)); autoSeeds[i] = v; }
            for (int si = 0; si < Sizes.Length; si++)
            {
                int s = Sizes[si];
                foreach (int a in Algorithms)
                {
                    var gen = Generators.All[a].gen; Generators.Record = false;
                    gen(s, s, new Random(0)); // «прогрів» (JIT-компіляція не потрапляє у вимір)
                    var mazes = new List<Maze>();
                    for (int r = 0; r < RunsTotal; r++)
                    {
                        int seed = SeedFor(si, r); // однаковий seed для всіх алгоритмів → порівняння в однакових умовах
                        var sw = Stopwatch.StartNew(); var m = gen(s, s, new Random(seed)); double ms = sw.Elapsed.TotalMilliseconds;
                        Raw.Add((a, s, r, seed, ms, Metrics.Compute(m)));
                        if (mazes.Count < 20) mazes.Add(m);
                    }
                    DiversityOf[(a, s)] = Metrics.Diversity(mazes);
                    yield return ++done;
                }
            }
        }

        public void Run() { foreach (var _ in RunStepwise()) { } }

        // середнє, стандартне відхилення, половина 95 % довірчого інтервалу (за всіма серіями разом)
        // Результати кешуються: інтерфейс викликає Stat сотні разів на кадр.
        readonly Dictionary<(int, int, string, int), (double, double, double)> stats = new Dictionary<(int, int, string, int), (double, double, double)>();
        public (double mean, double sd, double ci) Stat(int algo, int size, string key) => Stat(algo, size, key, -1);

        // series = -1 — усі серії; інакше лише одна серія
        public (double mean, double sd, double ci) Stat(int algo, int size, string key, int series)
        {
            var ck = (algo, size, key, series);
            if (stats.TryGetValue(ck, out var cached)) return cached;
            var res = Compute(algo, size, key, series);
            if (Raw.Count > 0) stats[ck] = res;
            return res;
        }

        // Стабільність рангів: місце кожного алгоритму (1 — найменше значення показника) у кожній серії.
        // Повертає ранги [алгоритм, серія] і для кожного алгоритму розмах (найгірше − найкраще місце); 0 — місце не змінюється.
        public (int[,] ranks, int[] spread) Ranks(int size, string key)
        {
            int n = Algorithms.Length; var ranks = new int[n, Repeats]; var spread = new int[n];
            for (int k = 0; k < Repeats; k++)
            {
                var order = Enumerable.Range(0, n).OrderBy(i => Stat(Algorithms[i], size, key, k).mean).ToArray();
                for (int pos = 0; pos < n; pos++) ranks[order[pos], k] = pos + 1;
            }
            for (int i = 0; i < n; i++) { int lo = int.MaxValue, hi = 0; for (int k = 0; k < Repeats; k++) { lo = Math.Min(lo, ranks[i, k]); hi = Math.Max(hi, ranks[i, k]); } spread[i] = hi - lo; }
            return (ranks, spread);
        }

        (double mean, double sd, double ci) Compute(int algo, int size, string key, int series)
        {
            var v = Raw.Where(r => r.algo == algo && r.size == size && (series < 0 || SeriesOf(r.run) == series)).Select(r => key == "ms" ? r.ms : r.m[key]).ToList();
            if (v.Count == 0) return (double.NaN, 0, 0);
            double mean = v.Average(), sd = v.Count > 1 ? Math.Sqrt(v.Sum(x => (x - mean) * (x - mean)) / (v.Count - 1)) : 0;
            return (mean, sd, 1.96 * sd / Math.Sqrt(v.Count));
        }

        // Показник росту часу k у залежності t ~ N^k (N — кількість клітин), метод найменших квадратів у логарифмах
        public double GrowthExponent(int algo)
        {
            var pts = Sizes.Select(s => (Math.Log(s * s), Math.Log(Math.Max(1e-6, Stat(algo, s, "ms").mean)))).ToList();
            double mx = pts.Average(p => p.Item1), my = pts.Average(p => p.Item2);
            double num = pts.Sum(p => (p.Item1 - mx) * (p.Item2 - my)), den = pts.Sum(p => (p.Item1 - mx) * (p.Item1 - mx));
            return den > 0 ? num / den : 0;
        }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public void SaveCsv(string dir)
        {
            Directory.CreateDirectory(dir);
            var keys = Metrics.List.Select(k => k.key).Concat(new[] { "connected" }).ToArray();
            var raw = new StringBuilder("algorithm,size,series,run,seed,gen_ms," + string.Join(",", keys) + "\n");
            foreach (var r in Raw) raw.Append($"\"{Generators.All[r.algo].name}\",{r.size},{SeriesOf(r.run) + 1},{r.run},{r.seed},{r.ms.ToString("0.#####", Inv)}," + string.Join(",", keys.Select(k => r.m[k].ToString("0.#####", Inv))) + "\n");
            File.WriteAllText(Path.Combine(dir, "raw.csv"), raw.ToString(), new UTF8Encoding(true));

            var sum = new StringBuilder("algorithm,size,n,gen_ms,gen_ms_sd,gen_ms_ci," + string.Join(",", Metrics.List.Select(k => $"{k.key},{k.key}_sd,{k.key}_ci")) + ",diversity,growth_k\n");
            foreach (int a in Algorithms)
                foreach (int s in Sizes)
                {
                    int n = Raw.Count(r => r.algo == a && r.size == s); if (n == 0) continue;
                    var t = Stat(a, s, "ms");
                    sum.Append($"\"{Generators.All[a].name}\",{s},{n},{F(t.mean)},{F(t.sd)},{F(t.ci)},");
                    sum.Append(string.Join(",", Metrics.List.Select(k => { var st = Stat(a, s, k.key); return $"{F(st.mean)},{F(st.sd)},{F(st.ci)}"; })));
                    sum.Append($",{F(DiversityOf.TryGetValue((a, s), out var dv) ? dv : double.NaN)},{F(GrowthExponent(a))}\n");
                }
            File.WriteAllText(Path.Combine(dir, "summary.csv"), sum.ToString(), new UTF8Encoding(true));
            if (Repeats > 1) SaveStability(dir);
        }

        // stability.csv: середнє кожної серії і місце алгоритму в ній — для перевірки, чи не міняються алгоритми місцями
        public void SaveStability(string dir)
        {
            var keys = new[] { "ms" }.Concat(Metrics.List.Where(k => k.key != "loops").Select(k => k.key)).ToArray();
            var sb = new StringBuilder("metric,size,algorithm," + string.Join(",", Enumerable.Range(1, Repeats).Select(k => $"series{k}_mean")) + "," + string.Join(",", Enumerable.Range(1, Repeats).Select(k => $"series{k}_rank")) + ",rank_spread\n");
            foreach (var key in keys)
                foreach (int s in Sizes)
                {
                    var (ranks, spread) = Ranks(s, key);
                    for (int i = 0; i < Algorithms.Length; i++)
                        sb.Append($"{key},{s},\"{Generators.All[Algorithms[i]].name}\"," + string.Join(",", Enumerable.Range(0, Repeats).Select(k => F(Stat(Algorithms[i], s, key, k).mean))) + "," +
                                  string.Join(",", Enumerable.Range(0, Repeats).Select(k => ranks[i, k].ToString())) + $",{spread[i]}\n");
                }
            File.WriteAllText(Path.Combine(dir, "stability.csv"), sb.ToString(), new UTF8Encoding(true));
        }

        static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.#####", Inv);
    }
}
