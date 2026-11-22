// Вкладка «ШІ-генератор»: лабіринт із заданими структурними характеристиками.
// Ціль задається рівнями 0…1 (від найменшого до найбільшого досяжного значення при цьому розмірі);
// нейромережа підбирає алгоритм і параметр, далі лабіринт генерується і за бажанням доводиться еволюційно.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace MazeStudy
{
    public partial class StudyApp
    {
        AiDesigner.Model aiModel; string aiStatus = ""; bool aiBusy;
        int aiSizeIdx = 1; static readonly int[] AiSizes = { 10, 15, 25, 50 };
        readonly double[] aiLevel = { 0.5, 0.5, 0.5 };
        List<(int a, double p, double[] pred, double dist)> aiPlan;
        Maze aiMaze; double[] aiFact; double aiDist = -1, aiDistStart = -1; int aiSeed; readonly MazeView aiView = new MazeView(); 

        void EnsureAiModel()
        {
            if (aiModel != null) return;
            var ta = Resources.Load<TextAsset>("ai_model");
            if (ta != null) { aiModel = AiDesigner.Load(ta.text); aiStatus = "Модель завантажено (навчена на даних стенду)."; }
            else aiStatus = "Модель ще не навчена – натисніть «Навчити модель».";
        }

        int AiSize => AiSizes[aiSizeIdx];
        double[] AiTarget()
        {
            var rg = AiDesigner.Range(aiModel, AiSize);
            return aiLevel.Select((v, i) => rg[i].lo + v * (rg[i].hi - rg[i].lo)).ToArray();
        }
        static readonly double[] AiWeight = { 1, 1, 1 };

        void DrawAi(float sw, float sh)
        {
            EnsureAiModel();
            float x = 24, y = 72;
            GUI.Label(new Rect(x, y, 300, 22), "Лабіринт із заданими характеристиками", h2); y += 24;
            GUI.Label(new Rect(x, y, 300, 36), "Модель прогнозує характеристики кожного алгоритму; обирається алгоритм, найближчий до цілі.", small); y += 36;
            GUI.Label(new Rect(x, y, 300, 20), "Розмір:", label); y += 22;
            for (int i = 0; i < AiSizes.Length; i++) if (SizeBtn(new Rect(x + i * 76, y, 72, 26), AiSizes[i], i == aiSizeIdx)) { aiSizeIdx = i; aiPlan = null; }
            y += 34;
            GUI.Label(new Rect(x, y, 300, 36), "Цільові характеристики (рівень від найменшого до найбільшого досяжного при цьому розмірі):", small); y += 36;
            if (Btn(new Rect(x, y, 300, 26), "Випадкова ціль")) { var lv = AiDesigner.RandomLevels(new System.Random(System.Guid.NewGuid().GetHashCode())); for (int k = 0; k < 3; k++) aiLevel[k] = lv[k]; aiPlan = null; }
            y += 32;
            if (aiModel == null) { DrawAiTrain(x, ref y); return; }
            var tg = AiTarget();
            for (int t = 0; t < AiDesigner.Targets.Length; t++)
            {
                GUI.Label(new Rect(x, y, 300, 20), $"{AiDesigner.Targets[t].label}: <b>{Fmt(t, tg[t])}</b>", label); y += 20;
                float nv = GUI.HorizontalSlider(new Rect(x, y + 2, 300, 16), (float)aiLevel[t], 0, 1);
                if (Mathf.Abs(nv - (float)aiLevel[t]) > 0.004f) { aiLevel[t] = nv; aiPlan = null; }
                GUI.Label(new Rect(x, y + 16, 150, 16), "низька", small); GUI.Label(new Rect(x + 150, y + 16, 150, 16), "висока", SmallR); y += 36;
            }
            GUI.enabled = !aiBusy;
            if (GUI.Button(new Rect(x, y, 300, 36), "1. Підібрати алгоритм", btnSel)) { aiPlan = AiDesigner.Design(aiModel, AiSize, tg, AiWeight); aiMaze = null; }
            y += 42;
            GUI.enabled = !aiBusy && aiPlan != null;
            if (GUI.Button(new Rect(x, y, 300, 32), "2. Згенерувати лабіринт", btn)) AiGenerate(tg);
            y += 38;
            GUI.enabled = !aiBusy && aiMaze != null;
            if (GUI.Button(new Rect(x, y, 300, 32), "3. Довести еволюцією (40 поколінь)", btn)) AiRefine(tg);
            GUI.enabled = true; y += 42;
            DrawAiTrain(x, ref y);

            // праворуч: план ШІ, лабіринт, порівняння «ціль / прогноз / факт»
            float rx = 350, rw = sw - 374;
            if (aiPlan == null) { GUI.Label(new Rect(rx, 120, rw, 60), "Налаштуйте цілі й натисніть «Підібрати алгоритм».", label); return; }
            GUI.Label(new Rect(rx, 72, rw, 22), "Прогноз моделі: три найближчі до цілі алгоритми", h2);
            float ty = 98;
            string[] heads = { "Алгоритм", "Прогноз: агент", "тупики", "коридор", "Відстань" };
            float[] cw = { 300, 130, 90, 90, 90 };
            float cx = rx; for (int i = 0; i < heads.Length; i++) { GUI.Label(new Rect(cx, ty, cw[i], 20), heads[i], new GUIStyle(small) { fontStyle = FontStyle.Bold }); cx += cw[i]; }
            ty += 22;
            foreach (var (a, p, pred, dist) in aiPlan.GroupBy(q => q.a).Select(g => g.First()).Take(3))
            {
                cx = rx; Box(new Rect(rx, ty + 21, rw, 1), Line);
                var cells = new[] { Generators.All[a].name, Fmt(0, pred[0]), Fmt(1, pred[1]), Fmt(2, pred[2]), dist.ToString("0.00") };
                for (int i = 0; i < cells.Length; i++) { GUI.Label(new Rect(cx, ty, cw[i], 20), cells[i], small); cx += cw[i]; }
                ty += 24;
            }
            if (aiMaze == null) return;
            float side = Mathf.Min(sh - ty - 40, 440);
            if (aiView.Tex) GUI.DrawTexture(new Rect(rx, ty + 12, side, side), aiView.Tex);
            float mx = rx + side + 24; float my = ty + 12;
            GUI.Label(new Rect(mx, my, 400, 22), "Ціль, прогноз і факт", h2); my += 26;
            var best = aiPlan[0];
            GUI.Label(new Rect(mx, my, 400, 20), $"{Generators.All[best.a].name} · seed {aiSeed}", small); my += 22;
            string[] h = { "Показник", "Ціль", "Прогноз", "Факт" }; float[] w2 = { 200, 70, 70, 70 };
            cx = mx; for (int i = 0; i < 4; i++) { GUI.Label(new Rect(cx, my, w2[i], 20), h[i], new GUIStyle(small) { fontStyle = FontStyle.Bold }); cx += w2[i]; }
            my += 22;
            for (int t = 0; t < AiDesigner.Targets.Length; t++)
            {
                cx = mx; var row = new[] { AiDesigner.Targets[t].label, Fmt(t, tg[t]), Fmt(t, best.pred[t]), Fmt(t, aiFact[t]) };
                for (int i = 0; i < 4; i++) { GUI.Label(new Rect(cx, my, w2[i], 20), row[i], small); cx += w2[i]; }
                Box(new Rect(mx, my + 20, 410, 1), Line); my += 23;
            }
            my += 8;
            GUI.Label(new Rect(mx, my, 420, 60), $"Відстань до цілі (менше – краще): <b>{aiDist:0.00}</b>" + (aiDistStart >= 0 && aiDistStart != aiDist ? $"  (до доведення {aiDistStart:0.00})" : ""), label);
        }

        void DrawAiTrain(float x, ref float y)
        {
            if (aiModel != null)
            {
                string[] sn = { "агент", "тупики", "коридор" }; string r2 = string.Join(", ", sn.Select((t, i) => $"{t} {aiModel.R2Net[i]:0.00}/{aiModel.R2Lin[i]:0.00}"));
                GUI.Label(new Rect(x, y, 300, 92), $"Якість прогнозу на тестових даних (80 відсотків даних для навчання, 20 для перевірки; коефіцієнт детермінації, від 0 до 1, більше – краще; нейромережа і лінійна регресія): {r2}. Навчальних прикладів: {aiModel.TrainN}.", small); y += 92;
            }
            GUI.enabled = !aiBusy;
            if (Btn(new Rect(x, y, 300, 28), aiBusy ? "Навчання…" : "Навчити модель заново")) TrainAi();
            GUI.enabled = true; y += 32;
            GUI.Label(new Rect(x, y, 300, 40), aiStatus, small); y += 40;
        }

        string Fmt(int t, double v) => t == 1 ? v.ToString("P0") : v.ToString("0.0");

        void AiGenerate(double[] tg)
        {
            var best = aiPlan[0]; aiSeed = SeedBook.Next(best.a);
            aiMaze = AiDesigner.Generate(best.a, best.p, AiSize, aiSeed);
            aiFact = AiDesigner.Measure(aiMaze); aiDist = AiDesigner.Distance(aiModel, aiFact, tg, AiWeight); aiDistStart = aiDist;
            aiView.ShowPath = true; aiView.Set(aiMaze, false, 440);
        }

        void AiRefine(double[] tg)
        {
            aiBusy = true; aiStatus = "Еволюційне доведення…"; var start = aiMaze; var model = aiModel; int s = aiSeed;
            Task.Run(() => AiDesigner.Refine(model, start, tg, AiWeight, 40, s)).ContinueWith(t =>
            {
                aiMaze = t.Result.best; aiFact = AiDesigner.Measure(aiMaze); aiDist = t.Result.dist; aiBusy = false;
                aiStatus = $"Доведено: відстань до цілі була {aiDistStart:0.00}, стала {aiDist:0.00} (менше – ближче до цілі)"; refreshAiView = true;
            });
        }
        volatile bool refreshAiView;

        void TrainAi()
        {
            aiBusy = true; aiStatus = "Збір даних і навчання (близько 15 секунд)…";
            Task.Run(() => { var d = AiDesigner.Dataset(System.Environment.TickCount); return AiDesigner.Train(d, 1, 600); }).ContinueWith(t =>
            {
                if (t.Exception != null) aiStatus = "Помилка: " + t.Exception.InnerException?.Message;
                else { aiModel = t.Result; aiStatus = "Модель навчено заново на нових даних."; aiPlan = null; }
                aiBusy = false;
            });
        }

        void UpdateAi() { if (refreshAiView) { refreshAiView = false; aiView.Set(aiMaze, false, 440); } }
    }
}
