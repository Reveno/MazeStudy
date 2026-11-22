// Багатошаровий перцептрон (повнозв'язна нейронна мережа) без сторонніх бібліотек.
// Прихований шар: tanh; вихід: лінійний (регресія). Навчання: градієнтний спуск Adam, функція втрат — MSE.
// Входи й виходи нормуються (z-оцінка), параметри нормування зберігаються разом із вагами.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MazeStudy
{
    public class Mlp
    {
        public readonly int[] Sizes;                 // напр. {13, 32, 32, 4}
        readonly double[][,] W; readonly double[][] B; // ваги й зсуви кожного шару
        double[] inMean, inStd, outMean, outStd;

        public Mlp(int[] sizes, int seed = 1)
        {
            Sizes = sizes; var rng = new Random(seed);
            W = new double[sizes.Length - 1][,]; B = new double[sizes.Length - 1][];
            for (int l = 0; l < W.Length; l++)
            {
                W[l] = new double[sizes[l + 1], sizes[l]]; B[l] = new double[sizes[l + 1]];
                double sc = Math.Sqrt(1.0 / sizes[l]); // ініціалізація Ксав'є
                for (int i = 0; i < sizes[l + 1]; i++) for (int j = 0; j < sizes[l]; j++) W[l][i, j] = (rng.NextDouble() * 2 - 1) * sc * Math.Sqrt(3);
            }
        }

        // прямий прохід; acts — значення всіх шарів (потрібні для зворотного поширення)
        double[][] Forward(double[] x)
        {
            var acts = new double[Sizes.Length][]; acts[0] = x;
            for (int l = 0; l < W.Length; l++)
            {
                var a = new double[Sizes[l + 1]]; bool last = l == W.Length - 1;
                for (int i = 0; i < a.Length; i++)
                {
                    double s = B[l][i]; for (int j = 0; j < Sizes[l]; j++) s += W[l][i, j] * acts[l][j];
                    a[i] = last ? s : Math.Tanh(s);
                }
                acts[l + 1] = a;
            }
            return acts;
        }

        double[] NormIn(double[] x) { var r = new double[x.Length]; for (int i = 0; i < x.Length; i++) r[i] = (x[i] - inMean[i]) / inStd[i]; return r; }

        public double[] Predict(double[] x)
        {
            var y = Forward(NormIn(x))[Sizes.Length - 1]; var r = new double[y.Length];
            for (int i = 0; i < y.Length; i++) r[i] = y[i] * outStd[i] + outMean[i];
            return r;
        }

        static (double[] mean, double[] std) Stats(IList<double[]> v)
        {
            int n = v[0].Length; var m = new double[n]; var s = new double[n];
            foreach (var r in v) for (int i = 0; i < n; i++) m[i] += r[i] / v.Count;
            foreach (var r in v) for (int i = 0; i < n; i++) s[i] += (r[i] - m[i]) * (r[i] - m[i]) / v.Count;
            for (int i = 0; i < n; i++) s[i] = Math.Max(1e-6, Math.Sqrt(s[i]));
            return (m, s);
        }

        // Навчання; повертає MSE (у нормованих одиницях) по епохах
        public List<double> Train(IList<double[]> X, IList<double[]> Y, int epochs = 300, double lr = 0.003, int batch = 32, int seed = 1)
        {
            (inMean, inStd) = Stats(X); (outMean, outStd) = Stats(Y);
            var xs = X.Select(NormIn).ToArray();
            var ys = Y.Select(y => y.Select((v, i) => (v - outMean[i]) / outStd[i]).ToArray()).ToArray();
            var rng = new Random(seed); var idx = Enumerable.Range(0, xs.Length).ToArray(); var hist = new List<double>();
            // моменти Adam
            var mW = W.Select(w => new double[w.GetLength(0), w.GetLength(1)]).ToArray(); var vW = W.Select(w => new double[w.GetLength(0), w.GetLength(1)]).ToArray();
            var mB = B.Select(b => new double[b.Length]).ToArray(); var vB = B.Select(b => new double[b.Length]).ToArray();
            const double b1 = 0.9, b2 = 0.999, eps = 1e-8; int t = 0;
            for (int ep = 0; ep < epochs; ep++)
            {
                for (int i = idx.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (idx[i], idx[j]) = (idx[j], idx[i]); }
                double loss = 0;
                for (int s0 = 0; s0 < idx.Length; s0 += batch)
                {
                    var gW = W.Select(w => new double[w.GetLength(0), w.GetLength(1)]).ToArray(); var gB = B.Select(b => new double[b.Length]).ToArray();
                    int cnt = Math.Min(batch, idx.Length - s0);
                    for (int k = 0; k < cnt; k++)
                    {
                        var acts = Forward(xs[idx[s0 + k]]); var y = ys[idx[s0 + k]];
                        int L = W.Length; var delta = new double[Sizes[L]];
                        for (int i = 0; i < delta.Length; i++) { double e = acts[L][i] - y[i]; delta[i] = 2 * e / cnt; loss += e * e; }
                        for (int l = L - 1; l >= 0; l--)
                        {
                            for (int i = 0; i < Sizes[l + 1]; i++) { gB[l][i] += delta[i]; for (int j = 0; j < Sizes[l]; j++) gW[l][i, j] += delta[i] * acts[l][j]; }
                            if (l == 0) break;
                            var nd = new double[Sizes[l]];
                            for (int j = 0; j < Sizes[l]; j++) { double s = 0; for (int i = 0; i < Sizes[l + 1]; i++) s += W[l][i, j] * delta[i]; nd[j] = s * (1 - acts[l][j] * acts[l][j]); } // похідна tanh
                            delta = nd;
                        }
                    }
                    t++;
                    for (int l = 0; l < W.Length; l++)
                    {
                        for (int i = 0; i < W[l].GetLength(0); i++)
                        {
                            for (int j = 0; j < W[l].GetLength(1); j++)
                            {
                                double g = gW[l][i, j]; mW[l][i, j] = b1 * mW[l][i, j] + (1 - b1) * g; vW[l][i, j] = b2 * vW[l][i, j] + (1 - b2) * g * g;
                                W[l][i, j] -= lr * (mW[l][i, j] / (1 - Math.Pow(b1, t))) / (Math.Sqrt(vW[l][i, j] / (1 - Math.Pow(b2, t))) + eps);
                            }
                            double gb = gB[l][i]; mB[l][i] = b1 * mB[l][i] + (1 - b1) * gb; vB[l][i] = b2 * vB[l][i] + (1 - b2) * gb * gb;
                            B[l][i] -= lr * (mB[l][i] / (1 - Math.Pow(b1, t))) / (Math.Sqrt(vB[l][i] / (1 - Math.Pow(b2, t))) + eps);
                        }
                    }
                }
                hist.Add(loss / (idx.Length * Sizes[Sizes.Length - 1]));
            }
            return hist;
        }

        // ---------- збереження / завантаження (простий текстовий формат) ----------
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public string Save()
        {
            var sb = new StringBuilder(); sb.AppendLine(string.Join(" ", Sizes));
            void Line(IEnumerable<double> v) => sb.AppendLine(string.Join(" ", v.Select(d => d.ToString("R", Inv))));
            Line(inMean); Line(inStd); Line(outMean); Line(outStd);
            for (int l = 0; l < W.Length; l++) { Line(W[l].Cast<double>()); Line(B[l]); }
            return sb.ToString();
        }

        public static Mlp Load(string text)
        {
            var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
            double[] D(string l) => l.Split(' ').Select(v => double.Parse(v, Inv)).ToArray();
            var m = new Mlp(lines[0].Split(' ').Select(int.Parse).ToArray());
            m.inMean = D(lines[1]); m.inStd = D(lines[2]); m.outMean = D(lines[3]); m.outStd = D(lines[4]);
            for (int l = 0; l < m.W.Length; l++)
            {
                var w = D(lines[5 + 2 * l]); int r = m.W[l].GetLength(0), c = m.W[l].GetLength(1);
                for (int i = 0; i < r; i++) for (int j = 0; j < c; j++) m.W[l][i, j] = w[i * c + j];
                m.B[l] = D(lines[6 + 2 * l]);
            }
            return m;
        }
    }
}
