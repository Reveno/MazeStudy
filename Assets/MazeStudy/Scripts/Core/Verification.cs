// Перевірка коректності алгоритмів (для розділу «Тестування»):
// 1) кожен лабіринт зв'язний (прохідний) і ідеальний (проходів = N − 1, тобто остовне дерево);
// 2) однаковий seed → однаковий лабіринт (відтворюваність);
// 3) журнал кроків, програний з початкового стану, дає той самий лабіринт (анімація показує справжній процес);
// 4) рівномірність на сітці 3×3: у неї рівно 192 остовні дерева. Олдос–Бродер і Вілсон за теорією дають кожне з
//    імовірністю 1/192; перевіряємо критерієм хі-квадрат (191 ступінь свободи, критичне значення 239,39 при α = 0,01);
// 5) трасовані копії алгоритмів (GeneratorsTraced.cs, покроковий показ коду) дають той самий результат, що й Generators.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MazeStudy
{
    public static class Verification
    {
        static string Key(Maze m)
        {
            var sb = new StringBuilder();
            for (int y = 0; y < m.H; y++) for (int x = 0; x < m.W - 1; x++) sb.Append(m.East[x, y] ? '1' : '0');
            for (int y = 0; y < m.H - 1; y++) for (int x = 0; x < m.W; x++) sb.Append(m.South[x, y] ? '1' : '0');
            return sb.ToString();
        }

        public static string Run(int uniformSamples = 192000)
        {
            var r = new StringBuilder();
            r.AppendLine("1–3. Зв'язність, ідеальність, відтворюваність, коректність журналу кроків");
            var sizes = new[] { (2, 2), (3, 3), (2, 7), (7, 2), (5, 9), (10, 10), (13, 6), (25, 25) };
            for (int a = 0; a < Generators.All.Length; a++)
            {
                var (name, gen) = Generators.All[a]; int total = 0, notConn = 0, loops = 0, nondet = 0, badLog = 0;
                foreach (var (w, h) in sizes)
                    for (int s = 0; s < 30; s++)
                    {
                        total++;
                        Generators.Record = false; var m = gen(w, h, new Random(s));
                        if (Metrics.Compute(m, 1)["connected"] != 1) notConn++;
                        if (m.PassageCount() != w * h - 1) loops++;
                        Generators.Record = true; var lg = gen(w, h, new Random(s)); Generators.Record = false;
                        if (Key(lg) != Key(m)) nondet++;
                        if (lg.Steps != null && lg.Steps.Count > 0)
                        {
                            var rep = new Maze(w, h, !lg.Steps[0].open);
                            foreach (var st in lg.Steps) rep.Set(st.x, st.y, st.nx, st.ny, st.open);
                            if (Key(rep) != Key(m)) badLog++;
                        }
                    }
                r.AppendLine($"  {name}: лабіринтів {total}; незв'язних {notConn}; з петлями {loops}; невідтворюваних {nondet}; помилок журналу {badLog}");
            }
            r.AppendLine();
            r.AppendLine($"4. Рівномірність на сітці 3 на 3 (192 остовні дерева, {uniformSamples} лабіринтів, критичне хі² = 239,39 при α = 0,01)");
            for (int a = 0; a < Generators.All.Length; a++)
            {
                var (name, gen) = Generators.All[a]; var cnt = new Dictionary<string, int>(); var rng = new Random(12345);
                for (int i = 0; i < uniformSamples; i++) { var k = Key(gen(3, 3, new Random(rng.Next()))); cnt[k] = cnt.TryGetValue(k, out var c) ? c + 1 : 1; }
                double e = uniformSamples / 192.0, chi = cnt.Values.Sum(v => (v - e) * (v - e) / e) + (192 - cnt.Count) * e;
                r.AppendLine($"  {name}: різних лабіринтів {cnt.Count}/192, хі-квадрат = {chi:F1}: {(chi < 239.386 ? "гіпотезу про рівномірність не відхилено" : "гіпотезу про рівномірність відхилено")}");
            }
            r.AppendLine();
            r.AppendLine("5. Трасовані копії для покрокового показу коду: той самий лабіринт і той самий журнал змін стін, що у вимірюваному коді");
            r.Append(GeneratorsTraced.CheckEquivalence());
            return r.ToString();
        }
    }
}
