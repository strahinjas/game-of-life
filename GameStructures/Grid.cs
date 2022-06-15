using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GameStructures
{
    [DataContract]
    public sealed class Grid
    {
        [DataMember]
        public readonly List<List<Cell>> Cells;

        public Grid(int n)
        {
            Cells = new List<List<Cell>>(n);

            for (int i = 0; i < n; i++)
            {
                Cells.Add(new List<Cell>(n));
                for (int j = 0; j < n; j++)
                {
                    Cells[i].Add(null);
                }
            }
        }

        public Grid(Grid other)
        {
            var n = other.GetSize();
            Cells = new List<List<Cell>>(n);

            for (int x = 0; x < n; x++)
            {
                Cells.Add(new List<Cell>(n));
                for (int y = 0; y < n; y++)
                {
                    Cells[x].Add(other[x, y]);
                }
            }
        }

        public Cell this[int x, int y]
        {
            get => Cells[x][y];
            set => Cells[x][y] = value;
        }

        public int GetSize() => Cells.Count;

        public Grid GetGhostGrid()
        {
            var n = GetSize();
            var grid = new Grid(n + 2);

            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    grid[x + 1, y + 1] = Cells[x][y];

                    if (x == 0)
                    {
                        grid[n + 1, y + 1] = Cells[x][y];
                        if (y == 0) grid[n + 1, n + 1] = Cells[x][y];
                        else if (y == n - 1) grid[n + 1, 0] = Cells[x][y];
                    }
                    else if (x == n - 1)
                    {
                        grid[0, y + 1] = Cells[x][y];
                        if (y == 0) grid[0, n + 1] = Cells[x][y];
                        else if (y == n - 1) grid[0, 0] = Cells[x][y];
                    }
                }
            }

            return grid;
        }

        public List<List<bool>> ToBoolGrid()
        {
            var n = GetSize();
            var result = new List<List<bool>>(n);

            for (int x = 0; x < n; x++)
            {
                result.Add(new List<bool>(n));
                for (int y = 0; y < n; y++)
                {
                    result[x].Add(Cells[x][y].IsAlive);
                }
            }

            return result;
        }

        public List<List<char>> ToCharGrid()
        {
            var n = GetSize();
            var result = new List<List<char>>(n);

            for (int x = 0; x < n; x++)
            {
                result.Add(new List<char>(n));
                for (int y = 0; y < n; y++)
                {
                    result[x].Add(Cells[x][y].ToChar());
                }
            }

            return result;
        }

        public static Grid MergeBlocks(List<Grid> blocks)
        {
            const int NW = 0;
            const int NE = 1;
            const int SW = 2;
            const int SE = 3;

            var result = new Grid(blocks[NW]);

            if (blocks.Count > 1)
            {
                result.Cells.AddRange(blocks[SW].Cells);

                var size = result.Cells.Count;
                for (int x = 0; x < size; x++)
                {
                    if (x < size / 2) result.Cells[x].AddRange(blocks[NE].Cells[x]);
                    else result.Cells[x].AddRange(blocks[SE].Cells[x - size / 2]);
                }
            }

            return result;
        }
    }
}
