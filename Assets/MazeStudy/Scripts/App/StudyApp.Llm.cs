// Вкладка «LLM»: перевірка й аналіз лабіринтів локальними великими мовними моделями.
// Моделі працюють на цьому комп'ютері через Ollama (http://localhost:11434) – без інтернету.
// Список моделей береться з Ollama (/api/tags); запит – /api/generate. Під час запиту модель видно командою «ollama ps».
// Кожен запит і відповідь записуються в журнал: Робочий стіл/MazeStudy_results/llm_log.txt
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MazeStudy
{
    public partial class StudyApp
    {
        const string OllamaUrl = "http://localhost:11434";
        List<string> llmModels; string llmModel = "", llmStatus = "", llmPrompt = "", llmAnswer = "", llmVerdict = "";
        bool llmBusy, llmModelsRequested; float llmStart; int llmAlgo; Maze llmMaze; string[] llmGrid; readonly MazeView llmView = new MazeView();
        Vector2 llmScroll; Font monoFont;

        IEnumerator LoadModels()
        {
            llmStatus = "Підключення до Ollama…";
            using (var r = UnityWebRequest.Get(OllamaUrl + "/api/tags"))
            {
                r.timeout = 5; yield return r.SendWebRequest();
                if (r.result != UnityWebRequest.Result.Success) { llmStatus = "Ollama не відповідає на localhost:11434. Запустіть Ollama і натисніть «Оновити»."; llmModels = new List<string>(); yield break; }
                var tags = JsonUtility.FromJson<TagList>(r.downloadHandler.text);
                llmModels = tags.models.Select(m => m.name).OrderBy(n => n).ToList();
                if (llmModel == "" && llmModels.Count > 0) llmModel = llmModels.Contains("qwen2.5-coder:7b") ? "qwen2.5-coder:7b" : llmModels[0];
                llmStatus = $"Ollama працює, знайдено моделей: {llmModels.Count}.";
            }
        }
        [System.Serializable] class TagList { public TagModel[] models; }
        [System.Serializable] class TagModel { public string name; }
        [System.Serializable] class GenReq { public string model; public string prompt; public bool stream; public GenOpt options; }
        [System.Serializable] class GenOpt { public float temperature; public int num_predict; }
        [System.Serializable] class GenResp { public string response; public string error; }

        IEnumerator AskLlm(string prompt, System.Action<string, string> done)
        {
            llmBusy = true; llmStart = Time.realtimeSinceStartup; llmPrompt = prompt; llmAnswer = ""; llmVerdict = "";
            var body = JsonUtility.ToJson(new GenReq { model = llmModel, prompt = prompt, stream = true, options = new GenOpt { temperature = 0, num_predict = 600 } });
            using (var r = new UnityWebRequest(OllamaUrl + "/api/generate", "POST"))
            {
                r.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body)); r.downloadHandler = new DownloadHandlerBuffer();
                r.SetRequestHeader("Content-Type", "application/json"); r.timeout = 600;
                yield return r.SendWebRequest();
                // потокова відповідь (NDJSON): збираємо всі шматки; якщо Ollama перервала генерацію – частковий текст зберігається
                string text = r.downloadHandler.text ?? "", err = null; var sb = new System.Text.StringBuilder();
                foreach (var line in text.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    GenResp ev = null; try { ev = JsonUtility.FromJson<GenResp>(line); } catch { }
                    if (ev == null) continue;
                    if (!string.IsNullOrEmpty(ev.error)) { err = ev.error.Contains("repeat") ? "модель зациклилася – Ollama перервала відповідь (повторення токенів)" : ev.error; break; }
                    sb.Append(ev.response);
                }
                if (err == null && r.result != UnityWebRequest.Result.Success && sb.Length == 0) err = r.error;
                string ans = sb.ToString() + (err != null && sb.Length > 0 ? "\n[обрив: " + err + "]" : "");
                float sec = Time.realtimeSinceStartup - llmStart; llmBusy = false;
                done(ans, err != null && sb.Length == 0 ? err : null); // частковий текст перевіряється як звичайна відповідь
                try
                {
                    var dir = OutDir(); Directory.CreateDirectory(dir);
                    File.AppendAllText(Path.Combine(dir, "llm_log.txt"), $"===== {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} · {llmModel} · {sec:0.0} с\nЗАПИТ:\n{prompt}\nВІДПОВІДЬ:\n{(err ?? ans)}\nПЕРЕВІРКА: {llmVerdict}\n\n");
                }
                catch { }
            }
        }

        void NewLlmMaze()
        {
            int seed = SeedBook.Next(llmAlgo); llmMaze = Generators.All[llmAlgo].gen(7, 7, new System.Random(seed));
            llmGrid = LlmTools.ToAscii(llmMaze); llmView.ShowPath = false; llmView.Set(llmMaze, false, 300); llmAnswer = llmVerdict = llmPrompt = "";
        }

        void DrawLlm(float sw, float sh)
        {
            if (!llmModelsRequested) { llmModelsRequested = true; StartCoroutine(LoadModels()); }
            if (monoFont == null) monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New" }, 15);
            if (llmMaze == null) NewLlmMaze();
            float x = 24, y = 72;
            GUI.Label(new Rect(x, y, 300, 22), "Локальні мовні моделі (Ollama)", h2); y += 24;
            GUI.Label(new Rect(x, y, 300, 52), "Моделі працюють на цьому комп'ютері без інтернету. Під час запиту модель видно командою «ollama ps».", small); y += 52;
            GUI.Label(new Rect(x, y, 300, 40), llmStatus, small); y += 36;
            if (llmModels != null)
                foreach (var m in llmModels.Take(9)) { if (Btn(new Rect(x, y, 300, 24), m, m == llmModel) && !llmBusy) llmModel = m; y += 26; }
            if (Btn(new Rect(x, y, 300, 24), "Оновити список моделей") && !llmBusy) StartCoroutine(LoadModels());
            y += 34;
            GUI.Label(new Rect(x, y, 300, 22), "Лабіринт 7 на 7 для перевірки", h2); y += 24;
            if (Btn(new Rect(x, y, 36, 26), "◀")) { llmAlgo = (llmAlgo + Generators.All.Length - 1) % Generators.All.Length; NewLlmMaze(); }
            GUI.Label(new Rect(x + 40, y + 3, 220, 22), Generators.All[llmAlgo].name, new GUIStyle(label) { alignment = TextAnchor.UpperCenter });
            if (Btn(new Rect(x + 264, y, 36, 26), "▶")) { llmAlgo = (llmAlgo + 1) % Generators.All.Length; NewLlmMaze(); }
            y += 32;
            if (Btn(new Rect(x, y, 300, 26), "Новий лабіринт")) NewLlmMaze();
            y += 36;
            GUI.enabled = !llmBusy && !string.IsNullOrEmpty(llmModel);
            if (GUI.Button(new Rect(x, y, 300, 30), "1. Попросити пройти лабіринт", btnSel))
                StartCoroutine(AskLlm(LlmTools.SolvePrompt(llmGrid), (a, e) => { if (e != null) llmVerdict = "Не розв'язано: " + e; else { var c = LlmTools.CheckPath(llmGrid, a); llmVerdict = (c.ok ? "РОЗВ'ЯЗАНО: " : "НЕ розв'язано: ") + c.why + $" (найкоротший шлях – {2 * (Metrics.Path(llmMaze, (0, 0), (6, 6)).Count - 1)} ходів)"; } llmAnswer = e ?? a; }));
            y += 36;
            if (GUI.Button(new Rect(x, y, 300, 30), "2. Попросити оцінити складність", btnSel))
                StartCoroutine(AskLlm(LlmTools.JudgePrompt(llmGrid), (a, e) => { var met = Metrics.Compute(llmMaze); var r = LlmTools.ParseRating(a); llmVerdict = (e != null ? "Помилка: " + e : r.HasValue ? $"Оцінка моделі: {r}/10" : "Оцінку не знайдено") + $" · метрики стенду: агент обробив у {met["agent"]:0.0} раза більше клітин, ніж шлях, тупиків {met["dead_ends"]:P0}, шлях {met["path_len"]:0} клітин"; llmAnswer = e ?? a; }));
            y += 36;
            if (GUI.Button(new Rect(x, y, 300, 30), "3. Попросити згенерувати лабіринт 5 на 5", btnSel))
                StartCoroutine(AskLlm(LlmTools.GeneratePrompt(5), (a, e) => { if (e != null) llmVerdict = "Помилка: " + e; else { var c = LlmTools.CheckGenerated(a, 5); llmVerdict = (c.perfect ? "КОРЕКТНИЙ: " : "НЕКОРЕКТНИЙ: ") + c.why; } llmAnswer = e ?? a; }));
            GUI.enabled = true; y += 40;
            if (llmBusy) GUI.Label(new Rect(x, y, 300, 20), $"Модель думає… {Time.realtimeSinceStartup - llmStart:0} с", label);

            // праворуч: лабіринт (картинка і текст, який бачить модель), запит, відповідь, перевірка
            float rx = 350, rw = sw - 374;
            if (llmView.Tex) GUI.DrawTexture(new Rect(rx, 76, 260, 260), llmView.Tex);
            var mono = new GUIStyle { font = monoFont, fontSize = 15, normal = { textColor = Ink } };
            GUI.Label(new Rect(rx + 280, 72, 300, 20), "Так лабіринт бачить модель:", small);
            GUI.Label(new Rect(rx + 280, 94, 300, 280), string.Join("\n", llmGrid), mono);
            float ry = 350;
            GUI.Label(new Rect(rx, ry, rw, 22), "Перевірка відповіді", h2); ry += 24;
            GUI.Label(new Rect(rx, ry, rw, 40), string.IsNullOrEmpty(llmVerdict) ? "–" : llmVerdict, new GUIStyle(label) { fontStyle = FontStyle.Bold, normal = { textColor = llmVerdict.StartsWith("РОЗВ") || llmVerdict.StartsWith("КОРЕКТ") ? new Color(0.1f, 0.55f, 0.3f) : new Color(0.75f, 0.2f, 0.15f) } });
            ry += 44;
            GUI.Label(new Rect(rx, ry, rw, 22), $"Відповідь моделі {llmModel} (дослівно)", h2); ry += 24;
            var ans = new GUIStyle(mono) { fontSize = 13, wordWrap = true };
            string at = string.IsNullOrEmpty(llmAnswer) ? "–" : llmAnswer; float ah = ans.CalcHeight(new GUIContent(at), rw - 20);
            Box(new Rect(rx, ry, rw, sh - ry - 20), Panel);
            llmScroll = GUI.BeginScrollView(new Rect(rx + 6, ry + 6, rw - 6, sh - ry - 32), llmScroll, new Rect(0, 0, rw - 26, ah));
            GUI.Label(new Rect(0, 0, rw - 26, ah), at, ans);
            GUI.EndScrollView();
        }
    }
}
