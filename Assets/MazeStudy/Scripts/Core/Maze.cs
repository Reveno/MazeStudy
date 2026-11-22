// Модель лабіринту: прямокутна сітка W×H клітин; між сусідніми клітинами — або стіна, або прохід.
// З погляду теорії графів: клітини — вершини, проходи — ребра. «Ідеальний» лабіринт = остовне дерево сітки.
// Ядро не залежить від UnityEngine — його можна тестувати і запускати окремо (консольний бенчмарк).
using System;
using System.Collections.Generic;

namespace MazeStudy
{
    public class Maze
    {
        public readonly int W, H;
        public readonly bool[,] East;   // прохід (x,y) -> (x+1,y)
        public readonly bool[,] South;  // прохід (x,y) -> (x,y+1)

        // Журнал кроків для покрокової візуалізації: кожна зміна стіни (відкрили/закрили) у порядку виконання.
        // null — журнал не ведеться (для вимірювання часу журнал вимкнено, щоб не впливати на результат).
        public List<(int x, int y, int nx, int ny, bool open)> Steps;

        public Maze(int w, int h, bool openAll = false)
        {
            W = w; H = h;
            East = new bool[w, h]; South = new bool[w, h];
            if (openAll)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++) { East[x, y] = x < w - 1; South[x, y] = y < h - 1; }
        }

        public void Carve(int x, int y, int nx, int ny) => Set(x, y, nx, ny, true);

        public void Set(int x, int y, int nx, int ny, bool open)
        {
            if (nx == x + 1) East[x, y] = open;
            else if (nx == x - 1) East[nx, y] = open;
            else if (ny == y + 1) South[x, y] = open;
            else if (ny == y - 1) South[x, ny] = open;
            Steps?.Add((x, y, nx, ny, open));
        }

        public bool IsOpen(int x, int y, int nx, int ny)
        {
            if (nx == x + 1) return East[x, y];
            if (nx == x - 1) return East[nx, y];
            if (ny == y + 1) return South[x, y];
            if (ny == y - 1) return South[x, ny];
            return false;
        }

        static readonly int[] DX = { 1, -1, 0, 0 }, DY = { 0, 0, 1, -1 };

        // сусідні клітини в межах сітки (незалежно від стін)
        public int Neighbors(int x, int y, (int, int)[] buf)
        {
            int n = 0;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + DX[d], ny = y + DY[d];
                if (nx >= 0 && ny >= 0 && nx < W && ny < H) buf[n++] = (nx, ny);
            }
            return n;
        }

        // сусіди, до яких є прохід (ступінь вершини)
        public int Links(int x, int y, (int, int)[] buf)
        {
            int n = 0;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + DX[d], ny = y + DY[d];
                if (nx >= 0 && ny >= 0 && nx < W && ny < H && IsOpen(x, y, nx, ny)) buf[n++] = (nx, ny);
            }
            return n;
        }

        public int PassageCount()
        {
            int c = 0;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { if (East[x, y]) c++; if (South[x, y]) c++; }
            return c;
        }

        public Maze Clone()
        {
            var m = new Maze(W, H);
            Array.Copy(East, m.East, East.Length); Array.Copy(South, m.South, South.Length);
            return m;
        }
    }
}
