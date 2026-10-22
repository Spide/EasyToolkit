using System;
using UnityEngine;

namespace Easy.SimulationLod
{
    /// <summary>
    /// The segments (districts) of one map, usually baked by a level tool: which segment each grid cell belongs
    /// to and which segments touch. Answers "which segment is this position in" with one array lookup. The grid
    /// lies on the XZ plane, centred on this transform (its rotation is ignored).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Easy Toolkit/Simulation LOD/Simulation Segment Map")]
    public sealed class SimulationSegmentMap : MonoBehaviour
    {
        /// <summary>Cell value for "no segment".</summary>
        public const byte None = byte.MaxValue;

        [SerializeField] private int columns;
        [SerializeField] private int rows;
        [SerializeField] private float cellSize = 2f;
        [SerializeField] private string[] segmentNames = Array.Empty<string>();
        [SerializeField] private byte[] cells = Array.Empty<byte>();
        [Tooltip("Pairs of touching segments: a0, b0, a1, b1, ...")]
        [SerializeField] private int[] neighbourPairs = Array.Empty<int>();

        public int SegmentCount => segmentNames.Length;
        public int Columns => columns;
        public int Rows => rows;
        public float CellSize => cellSize;
        public string SegmentName(int segment) => segmentNames[segment];

        /// <summary>
        /// <paramref name="segmentOfCell"/> is row by row from the northern (+Z) row, <see cref="None"/> where a cell
        /// has no segment. At most 255 segments.
        /// </summary>
        public void Configure(int columnCount, int rowCount, float size, string[] names, byte[] segmentOfCell, int[] pairs)
        {
            if (columnCount < 0 || rowCount < 0) throw new ArgumentOutOfRangeException(nameof(columnCount));
            if (size <= 0f) throw new ArgumentOutOfRangeException(nameof(size));
            if (names == null || segmentOfCell == null || pairs == null) throw new ArgumentNullException(names == null ? nameof(names) : segmentOfCell == null ? nameof(segmentOfCell) : nameof(pairs));
            if (names.Length > None) throw new ArgumentException("At most 255 segments.", nameof(names));
            if (segmentOfCell.Length != columnCount * rowCount) throw new ArgumentException("One value per cell is required.", nameof(segmentOfCell));
            if (pairs.Length % 2 != 0) throw new ArgumentException("Neighbours come in pairs.", nameof(pairs));
            columns = columnCount;
            rows = rowCount;
            cellSize = size;
            segmentNames = names;
            cells = segmentOfCell;
            neighbourPairs = pairs;
        }

        /// <summary>The segment at a world position, or -1 outside the map or in a cell without a segment.</summary>
        public int SegmentAt(Vector3 worldPosition)
        {
            if (columns <= 0 || rows <= 0 || cellSize <= 0f) return -1;
            Vector3 local = worldPosition - transform.position;
            int column = Mathf.FloorToInt(local.x / cellSize + columns * 0.5f);
            int row = Mathf.FloorToInt(rows * 0.5f - local.z / cellSize);
            if (column < 0 || row < 0 || column >= columns || row >= rows) return -1;
            byte value = cells[row * columns + column];
            return value == None || value >= SegmentCount ? -1 : value;
        }

        public SimulationSegmentGraph BuildGraph()
        {
            var graph = new SimulationSegmentGraph(Mathf.Max(1, SegmentCount));
            for (int i = 0; i + 1 < neighbourPairs.Length; i += 2)
            {
                int a = neighbourPairs[i], b = neighbourPairs[i + 1];
                if (a >= 0 && b >= 0 && a < graph.Count && b < graph.Count) graph.Connect(a, b);
            }

            return graph;
        }
    }
}
