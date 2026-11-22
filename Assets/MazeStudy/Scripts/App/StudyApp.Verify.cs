// Вкладка «Перевірка»: автоматичні перевірки коректності алгоритмів і гістограма рівномірності на сітці 3×3.
// Сітка 3×3 має рівно 192 можливі ідеальні лабіринти (остовні дерева). Рівномірний алгоритм (Олдос–Бродер, Вілсон)
// дає кожен із імовірністю 1/192 – стовпчики гістограми майже однакові; нерівномірні дають «гребінку» або пропуски.
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace MazeStudy
{
    public partial class StudyApp
    {
        string verText = ""; bool verBusy; int histAlgo = 4, histSamplesIdx = 0; static readonly int[] HistSamples = { 19200, 96000 };
        int[] histCounts; double histChi; int histFor = -1; string histStatus = "";
        Vector2 verScroll;

        void DrawVerify(float sw, float sh)
        {
            float x = 24, y = 72;
            GUI.Label(new Rect(x, y, 300, 22), "Перевірка коректності", h2); y += 24;
            string why = "Навіщо: переконатися, що кожен алгоритм будує правильний лабіринт – усі клітини з'єднані, циклів немає, той самий seed дає той самий лабіринт. Займає близько хвилини.";
            float wh = small.CalcHeight(new GUIContent(why), 300); GUI.Label(new Rect(x, y, 300, wh), why, small); y += wh + 6;
            GUI.enabled = !verBusy;
            if (GUI.Button(new Rect(x, y, 300, 34), verBusy ? "Виконується…" : "Запустити перевірку", btnSel))
            {
                verBusy = true; verText = "Виконується (близько хвилини)…";
                Task.Run(() => Verification.Run(19200)).ContinueWith(t => { verText = t.Exception == null ? t.Result : "Помилка: " + t.Exception.InnerException?.Message; verBusy = false; });
            }
            GUI.enabled = true; y += 44;

            GUI.Label(new Rect(x, y, 300, 22), "Чи є перекіс (сітка 3 на 3)", h2); y += 24;
            for (int i = 0; i < Generators.All.Length; i++)
                if (Btn(new Rect(x + (i % 2) * 154, y + (i / 2) * 28, 146, 25), Short[i], i == histAlgo)) histAlgo = i;
            y += 5 * 28 + 6;
            GUI.Label(new Rect(x, y, 120, 20), "Лабіринтів:", small);
            for (int i = 0; i < HistSamples.Length; i++) if (Btn(new Rect(x + 90 + i * 106, y - 3, 100, 24), HistSamples[i].ToString("N0"), i == histSamplesIdx)) histSamplesIdx = i;
            y += 28;
            GUI.enabled = !verBusy;
            if (GUI.Button(new Rect(x, y, 300, 32), "Побудувати гістограму", btn))
            {
                verBusy = true; int a = histAlgo, n = HistSamples[histSamplesIdx]; histStatus = "Генерація…";
                Task.Run(() => LlmTools.TreeHistogram(a, n, 2026)).ContinueWith(t => { (histCounts, histChi) = t.Result; histFor = a; histStatus = ""; verBusy = false; });
            }
            GUI.enabled = true; y += 40;
            GUI.Label(new Rect(x, y, 300, 130), histStatus != "" ? histStatus : "На сітці 3 на 3 існує рівно 192 різні лабіринти, тому можна перевірити, чи алгоритм будує їх однаково часто. 19 200 – по 100 генерацій на кожен варіант, 96 000 – по 500: чим більше, тим точніше. Стовпчик – скільки разів випав лабіринт, пунктир – скільки має бути, якщо перекосу немає.", small);

            // праворуч угорі – гістограма, внизу – текст перевірки
            float rx = 350, rw = sw - 374, hh = (sh - 92) * 0.55f;
            var hr = new Rect(rx, 72, rw, hh); panelRects.Clear(); chartRect = hr;
            Box(new Rect(hr.x, hr.y, hr.width, 1), Line); Box(new Rect(hr.x, hr.yMax, hr.width, 1), Line);
            if (histCounts == null) GUI.Label(new Rect(rx + 10, 120, rw - 20, 60), "Оберіть алгоритм і натисніть «Побудувати гістограму».", label);
            else
            {
                int total = histCounts.Sum(), distinct = histCounts.Count(v => v > 0); double e = total / 192.0; bool uniform = histChi < 239.386;
                GUI.Label(new Rect(rx + 8, hr.y + 6, rw - 50, 22), $"{Generators.All[histFor].name}: частота кожного з 192 можливих лабіринтів 3 на 3 (впорядковано за спаданням)", h2);
                HelpBtn(new Rect(rx + rw - 36, hr.y + 6, 28, 24), "ch_uniform");
                GUI.Label(new Rect(rx + 8, hr.y + 28, rw - 16, 20), $"{total:N0} лабіринтів · різних {distinct} з 192 · критерій хі-квадрат {histChi:N1} (межа 239,39; менше межі – перекосу немає) – " + (uniform ? "<color=#1a8a4a><b>гіпотезу про рівномірність не відхилено</b></color>" : "<color=#c23b2e><b>гіпотезу про рівномірність відхилено</b></color>") + " · пунктир – очікувана частота", new GUIStyle(small) { richText = true });
                var g = new Rect(rx + 50, hr.y + 56, rw - 64, hh - 76);
                var sorted = histCounts.OrderByDescending(v => v).ToArray(); double max = System.Math.Max(sorted[0], e * 1.3);
                for (int t = 0; t <= 4; t++) { float yy = g.yMax - g.height * t / 4; Box(new Rect(g.x, yy, g.width, 1), Line); GUI.Label(new Rect(g.x - 50, yy - 9, 46, 18), (max * t / 4).ToString("N0"), SmallR); }
                float bw = g.width / sorted.Length;
                for (int i = 0; i < sorted.Length; i++) { float h = (float)(g.height * sorted[i] / max); Box(new Rect(g.x + i * bw, g.yMax - h, Mathf.Max(1, bw - 0.6f), h), uniform ? new Color(0.1f, 0.62f, 0.38f) : Accent); }
                float ey = g.yMax - (float)(g.height * e / max);
                for (float px = g.x; px < g.xMax; px += 10) Box(new Rect(px, ey - 1, 6, 2), new Color(0.85f, 0.25f, 0.2f));
            }
            GUI.Label(new Rect(rx, hr.yMax + 10, rw, 22), "Результати перевірки коректності", h2);
            var tr = new Rect(rx, hr.yMax + 36, rw, sh - hr.yMax - 56);
            var mono = new GUIStyle(small) { fontSize = 13, normal = { textColor = Ink }, wordWrap = true };
            float th = mono.CalcHeight(new GUIContent(verText == "" ? "Натисніть «Запустити перевірку»." : verText), rw - 20);
            verScroll = GUI.BeginScrollView(tr, verScroll, new Rect(0, 0, rw - 20, th));
            GUI.Label(new Rect(0, 0, rw - 20, th), verText == "" ? "Натисніть «Запустити перевірку»." : verText, mono);
            GUI.EndScrollView();
        }
    }
}
