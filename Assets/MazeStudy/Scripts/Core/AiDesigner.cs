// ШІ-генератор лабіринту із заданими структурними характеристиками (елемент наукової новизни).
// 1) Набір даних: стенд генерує лабіринти всіма алгоритмами з різними параметрами й розмірами і вимірює метрики.
// 2) Нейронна мережа (MLP) вчиться передбачати метрики за (алгоритм, параметр, розмір) — «модель-сурогат».
//    Для порівняння навчається й лінійна регресія (базова модель, як у Yang et al., 2024).
// 3) Обернена задача: для цільових характеристик (складність, тупики, коридори) мережа перебирає алгоритми й параметр і обирає найближчі.
// 4) Доведення: еволюційний алгоритм змінює вже згенерований лабіринт, доки його метрики не наблизяться до цілі.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MazeStudy
{
    public static class AiDesigner
    {
        // Що передбачаємо (і чим задаємо ціль)
        public static readonly (string key, string label)[] Targets =
        {
            ("agent", "Клітин агента на клітину шляху"), ("dead_ends", "Частка тупиків"), ("corridor", "Середня довжина коридору, клітин"),
        };

        // Параметр алгоритму. Усі 10 алгоритмів використовуються точно за джерелами, без налаштувань
        // (гібрид — лише з порогом за формулою Yang et al.), тому ШІ обирає тільки алгоритм; параметр завжди 0.
        // Поле залишено, щоб формат моделі не змінювався.
        public static bool HasParam(int a) => false;
        public static string ParamName(int a) => "";
        public static double ParamValue(int a, double p) => 0;

        public static double[] Features(int a, double p, int size)
        {
            var f = new double[Generators.All.Length + 2];
            f[a] = 1; f[Generators.All.Length] = p; f[Generators.All.Length + 1] = Math.Log(size);
            return f;
        }

        public static double[] Measure(Maze m)
        {
            var mt = Metrics.Compute(m, 3);
            return new[] { mt["agent"], mt["dead_ends"], mt["corridor"] };
        }

        // ---------- 1. набір даних ----------
        public static List<(int a, double p, int size, double[] y)> Dataset(int seed, Action<float> progress = null)
        {
            var data = new List<(int, double, int, double[])>(); var rng = new Random(seed);
            int[] sizes = { 10, 15, 20, 25, 30, 40, 50 };
            var jobs = new List<(int a, double p, int s, int reps)>();
            foreach (int s in sizes)
                for (int a = 0; a < Generators.All.Length; a++)
                    jobs.Add((a, 0.0, s, 12));
            for (int j = 0; j < jobs.Count; j++)
            {
                var (a, p, s, reps) = jobs[j];
                for (int r = 0; r < reps; r++) data.Add((a, p, s, Measure(Generators.All[a].gen(s, s, new Random(rng.Next())))));
                progress?.Invoke((j + 1f) / jobs.Count);
            }
            return data;
        }

        // ---------- 2. навчання і оцінка ----------
        public class Model
        {
            public Mlp Net; public double[] OutStd;     // розкид кожної цілі (для зважування відстані)
            public double[] R2Net, R2Lin;               // якість на тестовій вибірці: нейромережа і лінійна регресія
            public int TrainN, TestN;
            public double[] Predict(int a, double p, int size) => Net.Predict(Features(a, p, size)).Select(v => Math.Max(0, v)).ToArray(); // характеристики не бувають від'ємними
        }

        public static Model Train(List<(int a, double p, int size, double[] y)> data, int seed = 1, int epochs = 400)
        {
            var rng = new Random(seed); var order = data.OrderBy(_ => rng.Next()).ToList();
            int nTest = order.Count / 5; var test = order.Take(nTest).ToList(); var train = order.Skip(nTest).ToList();
            var X = train.Select(d => Features(d.a, d.p, d.size)).ToList(); var Y = train.Select(d => d.y).ToList();
            var net = new Mlp(new[] { X[0].Length, 32, 32, Targets.Length }, seed); net.Train(X, Y, epochs);
            var lin = LinearFit(X, Y);
            int k = Targets.Length; var r2n = new double[k]; var r2l = new double[k]; var std = new double[k];
            for (int t = 0; t < k; t++)
            {
                var ys = test.Select(d => d.y[t]).ToArray(); double mean = ys.Average(), ssTot = ys.Sum(v => (v - mean) * (v - mean));
                double ssN = test.Sum(d => Sq(net.Predict(Features(d.a, d.p, d.size))[t] - d.y[t]));
                double ssL = test.Sum(d => Sq(LinPredict(lin, Features(d.a, d.p, d.size))[t] - d.y[t]));
                r2n[t] = 1 - ssN / Math.Max(1e-12, ssTot); r2l[t] = 1 - ssL / Math.Max(1e-12, ssTot);
                var all = data.Select(d => d.y[t]).ToArray(); double m2 = all.Average(); std[t] = Math.Max(1e-6, Math.Sqrt(all.Sum(v => (v - m2) * (v - m2)) / all.Length));
            }
            return new Model { Net = net, OutStd = std, R2Net = r2n, R2Lin = r2l, TrainN = train.Count, TestN = test.Count };
        }

        static double Sq(double v) => v * v;

        // лінійна регресія (найменші квадрати) з вільним членом — базова модель для порівняння
        static double[,] LinearFit(List<double[]> X, List<double[]> Y)
        {
            int n = X[0].Length + 1, k = Y[0].Length; var A = new double[n, n]; var Bm = new double[n, k];
            foreach (var (x, y) in X.Zip(Y, (x, y) => (x, y)))
            {
                var xx = x.Concat(new[] { 1.0 }).ToArray();
                for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) A[i, j] += xx[i] * xx[j]; for (int t = 0; t < k; t++) Bm[i, t] += xx[i] * y[t]; }
            }
            for (int i = 0; i < n; i++) A[i, i] += 1e-6; // гребенева поправка: ознаки one-hot лінійно залежні
            // розв'язок методом Гаусса
            for (int c = 0; c < n; c++)
            {
                int piv = c; for (int r = c + 1; r < n; r++) if (Math.Abs(A[r, c]) > Math.Abs(A[piv, c])) piv = r;
                for (int j = 0; j < n; j++) (A[c, j], A[piv, j]) = (A[piv, j], A[c, j]); for (int t = 0; t < k; t++) (Bm[c, t], Bm[piv, t]) = (Bm[piv, t], Bm[c, t]);
                for (int r = 0; r < n; r++) { if (r == c) continue; double f = A[r, c] / A[c, c]; for (int j = c; j < n; j++) A[r, j] -= f * A[c, j]; for (int t = 0; t < k; t++) Bm[r, t] -= f * Bm[c, t]; }
            }
            for (int r = 0; r < n; r++) for (int t = 0; t < k; t++) Bm[r, t] /= A[r, r];
            return Bm;
        }
        static double[] LinPredict(double[,] w, double[] x)
        {
            int n = x.Length, k = w.GetLength(1); var y = new double[k];
            for (int t = 0; t < k; t++) { double s = w[n, t]; for (int i = 0; i < n; i++) s += w[i, t] * x[i]; y[t] = s; }
            return y;
        }

        // ---------- 3. обернена задача: підбір алгоритму і параметра ----------
        public static double Distance(Model m, double[] y, double[] target, double[] weight)
        {
            double d = 0; for (int t = 0; t < y.Length; t++) d += weight[t] * Sq((y[t] - target[t]) / m.OutStd[t]);
            return Math.Sqrt(d);
        }

        public static List<(int a, double p, double[] pred, double dist)> Design(Model m, int size, double[] target, double[] weight)
        {
            var res = new List<(int, double, double[], double)>();
            for (int a = 0; a < Generators.All.Length; a++)
                foreach (double p in HasParam(a) ? Enumerable.Range(0, 21).Select(i => i / 20.0) : new[] { 0.0 })
                {
                    var y = m.Predict(a, p, size); res.Add((a, p, y, Distance(m, y, target, weight)));
                }
            return res.OrderBy(r => r.Item4).ToList();
        }

        // діапазон досяжних значень кожної цілі при заданому розмірі (для повзунків «низька — висока»)
        public static (double lo, double hi)[] Range(Model m, int size)
        {
            var r = new (double, double)[Targets.Length]; for (int t = 0; t < r.Length; t++) r[t] = (double.MaxValue, double.MinValue);
            for (int a = 0; a < Generators.All.Length; a++)
                foreach (double p in HasParam(a) ? Enumerable.Range(0, 11).Select(i => i / 10.0) : new[] { 0.0 })
                {
                    var y = m.Predict(a, p, size);
                    for (int t = 0; t < r.Length; t++) r[t] = (Math.Min(r[t].Item1, y[t]), Math.Max(r[t].Item2, y[t]));
                }
            for (int t = 0; t < r.Length; t++) r[t] = (Math.Max(0, r[t].Item1), r[t].Item2);
            return r;
        }

        public static Maze Generate(int a, double p, int size, int seed)
        {
            return Generators.All[a].gen(size, size, new Random(seed));
        }

        // ---------- 4. еволюційне доведення до цілі ----------
        // Мутація «заміна ребра» зберігає ідеальність і зв'язність лабіринту.
        public static (Maze best, double dist, List<double> history) Refine(Model m, Maze start, double[] target, double[] weight, int generations, int seed)
        {
            var rng = new Random(seed); var hist = new List<double>();
            double Fit(Maze z) => Distance(m, Measure(z), target, weight);
            var pop = new List<(Maze z, double d)> { (start.Clone(), Fit(start)) };
            const int Mu = 4, Lambda = 8;
            hist.Add(pop[0].d);
            for (int g = 0; g < generations; g++)
            {
                int parents = pop.Count;
                for (int i = 0; i < Lambda; i++)
                {
                    var c = pop[rng.Next(parents)].z.Clone(); int k = 1 + rng.Next(Math.Max(1, c.W * c.H / 60));
                    for (int j = 0; j < k; j++) Generators.SwapEdge(c, rng);
                    pop.Add((c, Fit(c)));
                }
                pop = pop.OrderBy(q => q.d).Take(Mu).ToList(); hist.Add(pop[0].d);
            }
            return (pop[0].z, pop[0].d, hist);
        }

        public static double[] TargetFor(Model m, int size, double[] levels)
        {
            var rg = Range(m, size); return levels.Select((v, i) => rg[i].lo + v * (rg[i].hi - rg[i].lo)).ToArray();
        }

        // випадкова ціль у межах досяжного: рівні складності, тупиків і коридорів — рівномірно в [0,1]
        public static double[] RandomLevels(Random rng) => new[] { rng.NextDouble(), rng.NextDouble(), rng.NextDouble() };

        // Оцінка на випадкових цілях: для кожної цілі — відстань до неї у трьох варіантів:
        // випадковий алгоритм/параметр (середнє з 3); вибір нейромережі (середнє з 3); вибір нейромережі + еволюційне доведення.
        public static List<(int size, string levels, string algo, double rnd, double ai, double refined)> Evaluate(Model m, int[] sizes, int targets, int refineGens, int seed)
        {
            var rng = new Random(seed); var res = new List<(int, string, string, double, double, double)>(); var w = new[] { 1.0, 1, 1 };
            foreach (int size in sizes)
                for (int t = 0; t < targets; t++)
                {
                    var lv = RandomLevels(rng); var tg = TargetFor(m, size, lv); var best = Design(m, size, tg, w)[0];
                    double dr = 0, da = 0; Maze first = null;
                    for (int i = 0; i < 3; i++)
                    {
                        var mr = Generate(rng.Next(Generators.All.Length), rng.NextDouble(), size, rng.Next()); dr += Distance(m, Measure(mr), tg, w) / 3;
                        var ma = Generate(best.a, best.p, size, rng.Next()); da += Distance(m, Measure(ma), tg, w) / 3; if (first == null) first = ma;
                    }
                    double df = Refine(m, first, tg, w, refineGens, rng.Next()).dist;
                    string an = Generators.All[best.a].name + (HasParam(best.a) ? $" ({ParamName(best.a)} = {ParamValue(best.a, best.p):0.##})" : "");
                    res.Add((size, string.Join(" ", lv.Select(v => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))), an, dr, da, df));
                }
            return res;
        }

        // збереження моделі + якості
        public static string Save(Model m)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(" ", m.OutStd.Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
            sb.AppendLine(string.Join(" ", m.R2Net.Concat(m.R2Lin).Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + $" {m.TrainN} {m.TestN}");
            sb.Append(m.Net.Save()); return sb.ToString();
        }

        public static Model Load(string text)
        {
            var lines = text.Split('\n'); var inv = System.Globalization.CultureInfo.InvariantCulture; int k = Targets.Length;
            var std = lines[0].Trim().Split(' ').Select(v => double.Parse(v, inv)).ToArray();
            var q = lines[1].Trim().Split(' ');
            return new Model
            {
                OutStd = std, R2Net = q.Take(k).Select(v => double.Parse(v, inv)).ToArray(), R2Lin = q.Skip(k).Take(k).Select(v => double.Parse(v, inv)).ToArray(),
                TrainN = int.Parse(q[2 * k]), TestN = int.Parse(q[2 * k + 1]), Net = Mlp.Load(string.Join("\n", lines.Skip(2))),
            };
        }
    }
}
