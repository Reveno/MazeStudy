// «Книга seed»: кожна нова генерація отримує випадковий seed, який ще НЕ використовувався для цього алгоритму.
// Використані seed зберігаються у файл (між запусками програми теж не повторюються).
// Повторити лабіринт можна лише свідомо – ввівши seed вручну (для відтворюваності експерименту).
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MazeStudy
{
    public static class SeedBook
    {
        static Dictionary<int, HashSet<int>> used;
        static readonly System.Random rng = new System.Random(System.Guid.NewGuid().GetHashCode());
        static string FilePath => Path.Combine(Application.persistentDataPath, "used_seeds.txt");

        static void Load()
        {
            if (used != null) return;
            used = new Dictionary<int, HashSet<int>>();
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var p = line.Split(';'); if (p.Length != 2) continue;
                    if (int.TryParse(p[0], out var a) && int.TryParse(p[1], out var s)) Set(a).Add(s);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("SeedBook: " + e.Message); }
        }

        static HashSet<int> Set(int algo) { if (!used.TryGetValue(algo, out var h)) used[algo] = h = new HashSet<int>(); return h; }

        // новий seed, якого ще не було в цього алгоритму (діапазон 1…2 147 483 646 – повтор практично неможливий, але перевіряємо)
        public static int Next(int algo)
        {
            Load(); var h = Set(algo); int s;
            do s = rng.Next(1, int.MaxValue); while (h.Contains(s));
            Mark(algo, s); return s;
        }

        // набір різних нових seed для експерименту: однакові для всіх алгоритмів (чесне порівняння), але раніше не використані жодним із них
        public static int[] NextForAll(int count, int algoCount)
        {
            Load(); var res = new List<int>(); var taken = new HashSet<int>();
            while (res.Count < count)
            {
                int s = rng.Next(1, int.MaxValue); bool ok = taken.Add(s);
                for (int a = 0; a < algoCount && ok; a++) if (Set(a).Contains(s)) ok = false;
                if (ok) res.Add(s);
            }
            for (int a = 0; a < algoCount; a++) foreach (var s in res) Mark(a, s, false);
            Save(); return res.ToArray();
        }

        public static bool WasUsed(int algo, int seed) { Load(); return Set(algo).Contains(seed); }
        public static int Count(int algo) { Load(); return Set(algo).Count; }

        public static void Mark(int algo, int seed, bool save = true)
        {
            Load();
            if (Set(algo).Add(seed) && save)
                try { File.AppendAllText(FilePath, $"{algo};{seed}\n"); } catch { }
        }

        static void Save()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var kv in used) foreach (var s in kv.Value) sb.Append($"{kv.Key};{s}\n");
                File.WriteAllText(FilePath, sb.ToString());
            }
            catch { }
        }
    }
}
