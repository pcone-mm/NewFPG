using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NewFPG.Forging
{
    public static class ForgingShapeUtility
    {
        public static int NormalizeRotationSteps(int rotationSteps)
        {
            int normalized = rotationSteps % 4;
            return normalized < 0 ? normalized + 4 : normalized;
        }

        public static Vector2Int RotatedSize(ForgingMaterialDefinition material, int rotationSteps)
        {
            int width = material != null ? Mathf.Max(1, material.shapeWidth) : 1;
            int height = material != null ? Mathf.Max(1, material.shapeHeight) : 1;
            return RotatedSize(width, height, rotationSteps);
        }

        public static Vector2Int RotatedSize(int width, int height, int rotationSteps)
        {
            rotationSteps = NormalizeRotationSteps(rotationSteps);
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            return rotationSteps % 2 == 0
                ? new Vector2Int(width, height)
                : new Vector2Int(height, width);
        }

        public static List<Vector2Int> RotatedCells(ForgingMaterialDefinition material, int rotationSteps)
        {
            List<Vector2Int> sourceCells = material != null && material.cells != null
                ? material.cells
                : new List<Vector2Int> { Vector2Int.zero };
            return RotatedCells(sourceCells, material != null ? material.shapeWidth : 1, material != null ? material.shapeHeight : 1, rotationSteps);
        }

        public static List<Vector2Int> RotatedCells(
            IReadOnlyList<Vector2Int> sourceCells,
            int width,
            int height,
            int rotationSteps)
        {
            rotationSteps = NormalizeRotationSteps(rotationSteps);
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            List<Vector2Int> cells = new List<Vector2Int>();
            if (sourceCells == null || sourceCells.Count == 0)
            {
                sourceCells = new List<Vector2Int> { Vector2Int.zero };
            }

            for (int i = 0; i < sourceCells.Count; i++)
            {
                Vector2Int cell = sourceCells[i];
                switch (rotationSteps)
                {
                    case 1:
                        cells.Add(new Vector2Int(height - 1 - cell.y, cell.x));
                        break;
                    case 2:
                        cells.Add(new Vector2Int(width - 1 - cell.x, height - 1 - cell.y));
                        break;
                    case 3:
                        cells.Add(new Vector2Int(cell.y, width - 1 - cell.x));
                        break;
                    default:
                        cells.Add(cell);
                        break;
                }
            }

            return cells;
        }

        public static void NormalizeCells(List<Vector2Int> cells, int width, int height, bool ensureOneCell)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            if (cells == null)
            {
                return;
            }

            HashSet<Vector2Int> unique = new HashSet<Vector2Int>();
            for (int i = cells.Count - 1; i >= 0; i--)
            {
                Vector2Int cell = cells[i];
                if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height || !unique.Add(cell))
                {
                    cells.RemoveAt(i);
                }
            }

            if (ensureOneCell && cells.Count == 0)
            {
                cells.Add(Vector2Int.zero);
            }
        }
    }

    public static class ForgingAssetPathUtility
    {
        public static string GetAssetPath(Object asset, string fallbackPath)
        {
#if UNITY_EDITOR
            string path = asset != null ? AssetDatabase.GetAssetPath(asset) : string.Empty;
            return string.IsNullOrWhiteSpace(path) ? fallbackPath : path;
#else
            return fallbackPath;
#endif
        }

        public static T LoadAssetAtPath<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
#else
            return null;
#endif
        }
    }
}
