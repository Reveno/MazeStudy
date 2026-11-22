// «Огляд»: чотири графіки – по одному на кожен критерій із рекомендацій керівника.
//  1. Швидкість: час генерації залежно від розміру (логарифмічна шкала).
//  2. Довжина шляху і пошук виходу агентом: дві стовпчикові діаграми (скільки йти; у скільки разів агент проходить більше, ніж треба).
//     Риски – 95 % довірчі інтервали: за самим перекриттям інтервалів не можна зробити висновок про значущість різниці.
//  3. Структура: з чого складається лабіринт – тупики / коридори / розгалуження.
//  4. Різноманітність результатів (вісь не з нуля – інакше різницю не видно).
// Кожен алгоритм має свій номер і колір – однакові на всіх графіках. Підписи точок розсуваються, щоб не накладались.
// «Стабільність»: місце кожного алгоритму в кожній із незалежних серій експерименту.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MazeStudy
{
    public partial class StudyApp
    {
        readonly List<Rect> panelRects = new List<Rect>();
        static readonly string[] Short = { "DFS", "Прим", "Крускал", "Олдос–Бродер", "Вілсон", "Еллер", "Hunt-and-Kill", "Binary Tree", "Рек. поділ", "Гібрид" };
        // 10 добре розрізнюваних кольорів (палітра Tableau 10): сусідні за значенням алгоритми не мають схожих відтінків
        static readonly Color[] Pal =
        {
            Hex(0x1f77b4), Hex(0xff7f0e), Hex(0x2ca02c), Hex(0x9467bd), Hex(0xe377c2),
            Hex(0x8c564b), Hex(0x17becf), Hex(0xd62728), Hex(0x7f7f7f), Hex(0xbcbd22),
        };
        static Color Hex(int v) => new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);

        void Overview(Rect r)
        {
            float legendH = 44, gap = 14;
            var area = new Rect(r.x, r.y, r.width, r.height - legendH);
            float pw = (area.width - gap) / 2, ph = (area.height - gap) / 2;
            var p1 = new Rect(area.x, area.y, pw, ph); var p2 = new Rect(area.x + pw + gap, area.y, pw, ph);
            var p3 = new Rect(area.x, area.y + ph + gap, pw, ph); var p4 = new Rect(area.x + pw + gap, area.y + ph + gap, pw, ph);
            panelRects.AddRange(new[] { p1, p2, p3, p4 });
            SpeedPanel(p1); BalancePanel(p2); StructurePanel(p3); DiversityPanel(p4);
            // спільна легенда: номер і колір алгоритму
            float lx = r.x, ly = r.yMax - legendH + 6, cw = r.width / 5;
            for (int j = 0; j < exp.Algorithms.Length; j++)
            {
                int a = exp.Algorithms[j]; float x = lx + (j % 5) * cw, y = ly + (j / 5) * 18;
                Box(new Rect(x, y + 4, 10, 10), Pal[a]); GUI.Label(new Rect(x + 14, y, cw - 16, 18), $"{a + 1}. {Short[a]}", small);
            }
        }

        Rect PanelFrame(Rect p, string titleText, string sub, string help = null)
        {
            Outline(p, Line);
            if (help != null) HelpBtn(new Rect(p.xMax - 34, p.y + 6, 28, 24), help);
            GUI.Label(new Rect(p.x + 10, p.y + 6, p.width - 20, 22), titleText, h2);
            GUI.Label(new Rect(p.x + 10, p.y + 26, p.width - 20, 18), sub, small);
            return new Rect(p.x + 10, p.y + 50, p.width - 20, p.height - 58);
        }

        void Outline(Rect p, Color c)
        {
            Box(new Rect(p.x, p.y, p.width, 1), c); Box(new Rect(p.x, p.yMax - 1, p.width, 1), c);
            Box(new Rect(p.x, p.y, 1, p.height), c); Box(new Rect(p.xMax - 1, p.y, 1, p.height), c);
        }

        GUIStyle SmallR => new GUIStyle(small) { alignment = TextAnchor.MiddleRight, wordWrap = false };
        GUIStyle SmallC => new GUIStyle(small) { alignment = TextAnchor.UpperCenter, wordWrap = false };
        GUIStyle NumStyle(Color c) => new GUIStyle(small) { normal = { textColor = c }, fontStyle = FontStyle.Bold, wordWrap = false };

        static string Num(double v) => v >= 100 ? v.ToString("0") : v >= 10 ? v.ToString("0.#") : v >= 1 ? v.ToString("0.##") : v.ToString("0.###");

        // Підписи точок без накладання: для кожної точки пробуємо кілька позицій навколо неї і беремо першу вільну.
        void PlaceLabels(List<(Vector2 p, string text, Color c)> items, Rect bounds)
        {
            var taken = new List<Rect>();
            foreach (var it in items) taken.Add(new Rect(it.p.x - 6, it.p.y - 6, 12, 12)); // самі маркери теж зайняті
            var offs = new[] { new Vector2(8, -9), new Vector2(-22, -9), new Vector2(-7, -24), new Vector2(-7, 6), new Vector2(8, -22), new Vector2(8, 4), new Vector2(-22, -22), new Vector2(-22, 4), new Vector2(18, -9), new Vector2(-32, -9) };
            foreach (var it in items)
            {
                Rect best = default; bool found = false;
                foreach (var o in offs)
                {
                    var rc = new Rect(it.p.x + o.x, it.p.y + o.y, 16, 16);
                    if (rc.x < bounds.x || rc.xMax > bounds.xMax || rc.y < bounds.y || rc.yMax > bounds.yMax) continue;
                    if (taken.Any(t => t.Overlaps(rc))) continue;
                    best = rc; found = true; break;
                }
                if (!found) best = new Rect(it.p.x + 8, it.p.y - 9, 16, 16);
                taken.Add(best);
                GUI.Label(new Rect(best.x, best.y - 1, 24, 18), it.text, NumStyle(it.c));
            }
        }

        // 1. Швидкість
        void SpeedPanel(Rect p)
        {
            var c = PanelFrame(p, "1. Швидкість генерації", "Час у мілісекундах залежно від розміру; шкала логарифмічна, вище – повільніше", "ch_speed");
            var g = new Rect(c.x + 58, c.y + 4, c.width - 58 - 40, c.height - 26);
            double lo = double.MaxValue, hi = 0;
            foreach (int a in exp.Algorithms) foreach (int s in exp.Sizes) { double v = exp.Stat(a, s, "ms").mean; lo = System.Math.Min(lo, v); hi = System.Math.Max(hi, v); }
            double l0 = System.Math.Floor(System.Math.Log10(System.Math.Max(lo, 1e-4))), l1 = System.Math.Ceiling(System.Math.Log10(System.Math.Max(hi, 1e-3)));
            if (l1 <= l0) l1 = l0 + 1;
            float Y(double v) => g.yMax - (float)((System.Math.Log10(System.Math.Max(v, 1e-4)) - l0) / (l1 - l0)) * g.height;
            for (double e = l0; e <= l1; e++) { float yy = Y(System.Math.Pow(10, e)); Box(new Rect(g.x, yy, g.width, 1), Line); GUI.Label(new Rect(g.x - 58, yy - 9, 54, 18), Num(System.Math.Pow(10, e)), SmallR); }
            var sz = exp.Sizes; float X(int i) => g.x + (sz.Length == 1 ? g.width / 2 : g.width * i / (sz.Length - 1));
            for (int i = 0; i < sz.Length; i++) GUI.Label(new Rect(X(i) - 45, g.yMax + 4, 90, 18), Sz(sz[i]), SmallC);
            var ends = new List<(float y, int a, Vector2 pt)>();
            foreach (int a in exp.Algorithms)
            {
                Vector2? prev = null;
                for (int i = 0; i < sz.Length; i++)
                {
                    var pt = new Vector2(X(i), Y(exp.Stat(a, sz[i], "ms").mean));
                    if (prev.HasValue) Seg(prev.Value, pt, Pal[a]); Box(new Rect(pt.x - 3, pt.y - 3, 6, 6), Pal[a]); prev = pt;
                }
                if (prev.HasValue) ends.Add((prev.Value.y, a, prev.Value));
            }
            // номери біля кінців ліній: розсуваємо по вертикалі, щоб не накладались, і з'єднуємо тонкою лінією з точкою
            ends.Sort((u, v) => u.y.CompareTo(v.y));
            float gapY = 13; var ys = ends.Select(e => e.y).ToArray();
            for (int i = 1; i < ys.Length; i++) if (ys[i] - ys[i - 1] < gapY) ys[i] = ys[i - 1] + gapY;
            float over = ys.Length > 0 ? ys[ys.Length - 1] - (g.yMax + 6) : 0;
            if (over > 0) for (int i = 0; i < ys.Length; i++) ys[i] -= over;
            for (int i = 0; i < ends.Count; i++)
            {
                var (y0, a, pt) = ends[i]; float lx = g.xMax + 14;
                Seg(new Vector2(pt.x + 3, pt.y), new Vector2(lx - 2, ys[i]), new Color(Pal[a].r, Pal[a].g, Pal[a].b, 0.6f));
                GUI.Label(new Rect(lx, ys[i] - 9, 26, 18), (a + 1).ToString(), NumStyle(Pal[a]));
            }
        }

        // 2. Довжина шляху і складність пошуку виходу – дві стовпчикові діаграми, відсортовані за значенням
        // (точковий графік керівник визнала нерозбірливим; стовпці читаються однозначно: довше – більше)
        void BalancePanel(Rect p)
        {
            var c = PanelFrame(p, $"2. Довжина шляху і пошук виходу агентом · {Sz(chartSize)}",
                "Довжина шляху і скільки клітин обійшов агент; риска – похибка", "ch_balance");
            float half = (c.width - 16) / 2;
            BarList(new Rect(c.x, c.y, half, c.height), "Довжина шляху", "path_len", v => v.ToString("0"));
            BarList(new Rect(c.x + half + 16, c.y, half, c.height), "Оброблено агентом клітин", "agent", v => "у " + v.ToString("0.0") + " раза");
        }

        // Стовпці всіх алгоритмів за одним показником, від більшого до меншого; риска – 95 % довірчий інтервал
        void BarList(Rect r, string head, string key, System.Func<double, string> fmt)
        {
            GUI.Label(new Rect(r.x, r.y - 4, r.width, 18), head, new GUIStyle(small) { fontStyle = FontStyle.Bold, normal = { textColor = Ink } });
            var rows = exp.Algorithms.Select(a => (a, s: exp.Stat(a, chartSize, key))).OrderByDescending(t => t.s.mean).ToList();
            double max = rows.Max(t => t.s.mean + t.s.ci); if (max <= 0) max = 1;
            float vw = rows.Max(t => small.CalcSize(new GUIContent(fmt(t.s.mean))).x) + 8;
            float lw = 92, bw = r.width - lw - vw, rh = (r.height - 18) / rows.Count;
            for (int j = 0; j < rows.Count; j++)
            {
                var (a, s) = rows[j]; float y = r.y + 16 + j * rh, bh = rh * 0.62f, by = y + (rh - bh) / 2;
                GUI.Label(new Rect(r.x, y + rh / 2 - 9, lw - 4, 18), $"{a + 1}. {Short[a]}", new GUIStyle(small) { normal = { textColor = Ink }, wordWrap = false, clipping = TextClipping.Clip });
                float wpx = (float)(bw * s.mean / max); Box(new Rect(r.x + lw, by, wpx, bh), Pal[a]);
                float e0 = (float)(bw * (s.mean - s.ci) / max), e1 = (float)(bw * (s.mean + s.ci) / max);
                Box(new Rect(r.x + lw + e0, by + bh / 2 - 0.5f, e1 - e0, 1.5f), Ink); Box(new Rect(r.x + lw + e1 - 1, by + 2, 1.5f, bh - 4), Ink);
                GUI.Label(new Rect(r.x + lw + e1 + 4, y + rh / 2 - 9, vw, 18), fmt(s.mean), new GUIStyle(small) { wordWrap = false });
            }
        }

        static double Median(IEnumerable<double> v) { var a = v.OrderBy(x => x).ToArray(); return a.Length == 0 ? 0 : a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2; }

        // 3. Структура: тупики / коридори / розгалуження (частки клітин)
        void StructurePanel(Rect p)
        {
            var c = PanelFrame(p, $"3. Структура лабіринту · {Sz(chartSize)}", "Частки клітин: тупики (1 вихід), коридори (2), розгалуження (3–4)", "ch_structure");
            Color cd = new Color(0.92f, 0.41f, 0.2f), cc = new Color(0.78f, 0.8f, 0.84f), cb = new Color(0.16f, 0.47f, 0.84f);
            float lw = 110, bw = c.width - lw - 6, n = exp.Algorithms.Length, rh = (c.height - 22) / n;
            var onDark = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, wordWrap = false, normal = { textColor = Color.white } };
            var onLight = new GUIStyle(onDark) { normal = { textColor = Ink } };
            for (int j = 0; j < n; j++)
            {
                int a = exp.Algorithms[j]; float y = c.y + j * rh;
                double d = exp.Stat(a, chartSize, "dead_ends").mean, b = exp.Stat(a, chartSize, "branches").mean, k = System.Math.Max(0, 1 - d - b);
                GUI.Label(new Rect(c.x, y + rh / 2 - 9, lw - 6, 18), $"{a + 1}. {Short[a]}", new GUIStyle(small) { normal = { textColor = Ink }, wordWrap = false, clipping = TextClipping.Clip });
                float x = c.x + lw, h = rh * 0.7f, yy = y + rh * 0.15f;
                // відсотки на кожній частині, якщо вона достатньо широка для напису
                void Part(double frac, Color col, GUIStyle st)
                {
                    float wpx = (float)(bw * frac); Box(new Rect(x, yy, wpx, h), col);
                    if (wpx >= 26) GUI.Label(new Rect(x, yy, wpx, h), (frac * 100).ToString("0") + "%", st);
                    x += wpx;
                }
                Part(d, cd, onDark); Part(k, cc, onLight); Part(b, cb, onDark);
            }
            float ly = c.yMax - 16; var legend = new[] { (cd, "тупики"), (cc, "коридори"), (cb, "розгалуження") };
            for (int i = 0; i < 3; i++) { Box(new Rect(c.x + lw + i * 110, ly + 4, 10, 10), legend[i].Item1); GUI.Label(new Rect(c.x + lw + 14 + i * 110, ly, 96, 18), legend[i].Item2, small); }
        }

        // 4. Різноманітність. Значення лежать у вузькому діапазоні біля 0,5, тому вісь починається не з нуля, а трохи нижче мінімуму.
        void DiversityPanel(Rect p)
        {
            var vals = exp.Algorithms.Select(a => exp.DiversityOf.TryGetValue((a, chartSize), out var dv) ? dv : 0).ToArray();
            double lo = System.Math.Floor((vals.Min() - 0.01) * 50) / 50, hi = System.Math.Max(0.5, System.Math.Ceiling((vals.Max() + 0.002) * 100) / 100);
            if (hi <= lo) hi = lo + 0.02;
            var c = PanelFrame(p, $"4. Різноманітність результатів · {Sz(chartSize)}",
                $"Частка стін, що відрізняються (0,5 – незалежні лабіринти); вісь від {lo:0.00}", "ch_diversity");
            float lw = 110, bw = c.width - lw - 50, n = exp.Algorithms.Length, rh = (c.height - 20) / n;
            for (int t = 0; t <= 4; t++)
            {
                double v = lo + (hi - lo) * t / 4; float gx = c.x + lw + (float)(bw * t / 4);
                Box(new Rect(gx, c.y, 1, n * rh), Line);
                GUI.Label(new Rect(gx - 22, c.y + n * rh + 2, 44, 16), v.ToString("0.00"), SmallC);
            }
            for (int j = 0; j < n; j++)
            {
                int a = exp.Algorithms[j]; float y = c.y + j * rh; double v = vals[j];
                GUI.Label(new Rect(c.x, y + rh / 2 - 9, lw - 6, 18), $"{a + 1}. {Short[a]}", new GUIStyle(small) { normal = { textColor = Ink }, wordWrap = false, clipping = TextClipping.Clip });
                Box(new Rect(c.x + lw, y + rh * 0.2f, (float)(bw * System.Math.Max(0, (v - lo) / (hi - lo))), rh * 0.6f), Pal[a]);
                GUI.Label(new Rect(c.x + lw + bw + 4, y + rh / 2 - 9, 46, 18), v.ToString("0.000"), small);
            }
        }

        // «Стабільність»: місце алгоритму (1 – найменше значення) у кожній із незалежних серій.
        // Однакові місця в усіх серіях → висновки за цим показником надійні; якщо місця міняються – різниця в межах випадковості.
        void StabilityTable(Rect r)
        {
            if (!exp.Sizes.Contains(chartSize)) chartSize = exp.Sizes[exp.Sizes.Length - 1];
            var (ranks, spread) = exp.Ranks(chartSize, chartKey);
            int n = exp.Algorithms.Length, k = exp.Repeats;
            GUI.Label(new Rect(r.x, r.y, r.width, 26), $"Стабільність рангів · {LabelOf(chartKey)} · {Sz(chartSize)}", h2);
            HelpBtn(new Rect(r.xMax - 30, r.y, 28, 26), "ch_stability");
            GUI.Label(new Rect(r.x, r.y + 24, r.width, 40), $"{k} незалежних серій по {exp.Runs} запусків (у кожній серії свої seed). У клітинці – середнє серії і місце алгоритму (1 – найменше значення). " +
                "Зелений – місце однакове в усіх серіях; жовтий – міняється на 1 (сусіди майже рівні); червоний – міняється більше.", new GUIStyle(small) { wordWrap = true });
            float lw = 250, cw = Mathf.Min(110, (r.width - lw - 120) / k), rh = Mathf.Min(40, (r.height - 150) / (n + 1)), y0 = r.y + 72;
            var hdr = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            for (int s = 0; s < k; s++) GUI.Label(new Rect(r.x + lw + s * cw, y0, cw, rh), $"Серія {s + 1}", hdr);
            GUI.Label(new Rect(r.x + lw + k * cw, y0, 120, rh), "Розмах місць", hdr);
            bool pct = chartKey != "ms" && Metrics.List.First(m => m.key == chartKey).percent;
            var cell = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = false };
            int stable = 0;
            for (int i = 0; i < n; i++)
            {
                int a = exp.Algorithms[i]; float y = y0 + (i + 1) * rh;
                Box(new Rect(r.x, y, lw + k * cw + 120, 1), Line);
                GUI.Label(new Rect(r.x, y + rh / 2 - 10, lw - 8, 20), $"{a + 1}. {Generators.All[a].name}", new GUIStyle(label) { fontSize = 14, wordWrap = false, clipping = TextClipping.Clip });
                Color tone = spread[i] == 0 ? new Color(0.85f, 0.95f, 0.87f) : spread[i] == 1 ? new Color(1f, 0.95f, 0.8f) : new Color(0.98f, 0.84f, 0.82f);
                if (spread[i] == 0) stable++;
                for (int s = 0; s < k; s++)
                {
                    double v = exp.Stat(a, chartSize, chartKey, s).mean;
                    string txt = pct ? v.ToString("P1") : chartKey == "ms" ? (v < 10 ? v.ToString("F3") : v.ToString("F1")) : v.ToString("F2");
                    Box(new Rect(r.x + lw + s * cw + 2, y + 3, cw - 4, rh - 6), tone);
                    GUI.Label(new Rect(r.x + lw + s * cw, y, cw, rh), $"{txt}  <b>#{ranks[i, s]}</b>", cell);
                }
                GUI.Label(new Rect(r.x + lw + k * cw, y, 120, rh), spread[i] == 0 ? "0 (стабільно)" : spread[i].ToString(), cell);
            }
            float yb = y0 + (n + 1) * rh + 12;
            GUI.Label(new Rect(r.x, yb, r.width, 44), $"Місце не змінюється в {stable} з {n} алгоритмів. Алгоритми, що міняються місцями, за цим показником статистично не розрізняються – " +
                "у висновках їх слід описувати як рівні.", new GUIStyle(label) { fontSize = 14, wordWrap = true });
        }
    }
}
