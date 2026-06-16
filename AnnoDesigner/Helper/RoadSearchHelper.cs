using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AnnoDesigner.Core.Extensions;
using AnnoDesigner.Core.Helper;
using AnnoDesigner.Core.Layout.Helper;
using AnnoDesigner.Core.Models;
using AnnoDesigner.Models;

namespace AnnoDesigner.Helper
{
    public static class RoadSearchHelper
    {
        private static readonly StatisticsCalculationHelper _statisticsCalculationHelper = new StatisticsCalculationHelper();

        /// <summary>
        /// Budget granularity. A dirt road (RoadInfluenceFactor 1.0) costs this much
        /// to traverse; a road with factor F costs <see cref="CostScale"/> / F. Choosing a
        /// value divisible by the common factors (e.g. 1.5 -&gt; 4) keeps costs integral.
        /// </summary>
        private const int CostScale = 6;

        private static void DoNothing(AnnoObject objectInRange) { }

        /// <summary>
        /// Grid cells an object's footprint covers. Orthogonal objects use their Position+Size box.
        /// Diagonal objects use the cells their rotated footprint overlaps, so a diagonal road's
        /// footprint spans a few cells and keeps the road chain connected for the flood.
        /// </summary>
        private static IEnumerable<(int x, int y)> GetFootprintCells(AnnoObject obj)
        {
            var rotationDegrees = obj.Rotation * 180.0 / Math.PI;
            var mod90 = ((rotationDegrees % 90) + 90) % 90;
            var isDiagonal = Math.Abs(mod90 - 45) < 1.0;

            if (!isDiagonal)
            {
                var x = (int)obj.Position.X;
                var y = (int)obj.Position.Y;
                var w = (int)obj.Size.Width;
                var h = (int)obj.Size.Height;
                for (var i = 0; i < w; i++)
                {
                    for (var j = 0; j < h; j++)
                    {
                        yield return (x + i, y + j);
                    }
                }
                yield break;
            }

            // Replicate the diagonal render geometry: the footprint is the Position+diagonalSize
            // rectangle rotated by -rotationDegrees around the (diagonally scaled) rotation centre.
            double annoW = obj.Size.Width;
            double annoH = obj.Size.Height;
            var diagW = MathHelper.GetDiagonalSize(annoW);
            var diagH = MathHelper.GetDiagonalSize(annoH);
            var scaleX = annoW != 0 ? diagW / annoW : 1.0;
            var scaleY = annoH != 0 ? diagH / annoH : 1.0;
            var pivotX = obj.Position.X + (obj.RotationCenter.X * scaleX);
            var pivotY = obj.Position.Y + (obj.RotationCenter.Y * scaleY);

            var rotate = new Matrix();
            rotate.RotateAt(-rotationDegrees, pivotX, pivotY);

            var p0 = rotate.Transform(new Point(obj.Position.X, obj.Position.Y));
            var p1 = rotate.Transform(new Point(obj.Position.X + diagW, obj.Position.Y));
            var p2 = rotate.Transform(new Point(obj.Position.X + diagW, obj.Position.Y + diagH));
            var p3 = rotate.Transform(new Point(obj.Position.X, obj.Position.Y + diagH));

            var minX = Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X));
            var maxX = Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X));
            var minY = Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y));
            var maxY = Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y));

            var inverse = rotate;
            inverse.Invert();

            for (var cy = (int)Math.Floor(minY); cy < (int)Math.Ceiling(maxY); cy++)
            {
                for (var cx = (int)Math.Floor(minX); cx < (int)Math.Ceiling(maxX); cx++)
                {
                    // a cell is covered if its centre maps back inside the unrotated rectangle
                    var local = inverse.Transform(new Point(cx + 0.5, cy + 0.5));
                    if (local.X >= obj.Position.X && local.X <= obj.Position.X + diagW &&
                        local.Y >= obj.Position.Y && local.Y <= obj.Position.Y + diagH)
                    {
                        yield return (cx, cy);
                    }
                }
            }
        }

        /// <summary>
        /// Creates offset 2D array from input AnnoObjects.
        /// Whole array is offset so that all input AnnoObjects are fully inside the array with one empty grid on each edge.
        /// Every input AnnoObject will be accessable from every index which is covered by that AnnoObject on the grid.
        /// If object covers multiple grid cells, it will be in the array at every index, which it covers.
        /// </summary>
        public static Moved2DArray<AnnoObject> PrepareGridDictionary(IEnumerable<AnnoObject> placedObjects)
        {
            if (placedObjects is null || placedObjects.WithoutIgnoredObjects().Count() < 1)
            {
                return null;
            }

            var statistics = _statisticsCalculationHelper.CalculateStatistics(placedObjects);
            (int x, int y) offset = ((int)statistics.MinX - 1, (int)statistics.MinY - 1);

            // make an array with one free grid cell on each edge
            var countY = (int)(statistics.MaxY - statistics.MinY + 2);
            var countX = (int)(statistics.MaxX - statistics.MinX + 2);
            var result = Enumerable.Range(0, countX)
                .Select(i => new AnnoObject[countY])
                .ToArrayWithCapacity(countX);

            // Sequential, with roads winning overlaps, so overlapping diagonal footprints resolve
            // identically every frame. A Parallel write-race here makes the influence flicker as
            // the grid is rebuilt on each mouse move.
            foreach (var placedObject in placedObjects.WithoutIgnoredObjects())
            {
                foreach (var (cx, cy) in GetFootprintCells(placedObject))
                {
                    var ax = cx - offset.x;
                    var ay = cy - offset.y;
                    if (ax < 0 || ax >= countX || ay < 0 || ay >= countY)
                    {
                        continue;
                    }

                    var existing = result[ax][ay];
                    if (existing is null || (placedObject.Road && !existing.Road))
                    {
                        result[ax][ay] = placedObject;
                    }
                }
            }

            return new Moved2DArray<AnnoObject>()
            {
                Array = result,
                Offset = offset
            };
        }

        /// <summary>
        /// Weighted breadth first search along objects which have the Road property set to true.
        /// The influence spreads along roads on a budget of <c>range * <see cref="CostScale"/></c>;
        /// entering a road tile costs <c><see cref="CostScale"/> / RoadInfluenceFactor</c>, so
        /// better roads (factor &gt; 1, e.g. Anno 117 paved/marble = 1.5) let the influence reach
        /// further. When every road has factor 1.0 this reduces to the previous tile-counting search.
        /// </summary>
        /// <returns>A grid (in <paramref name="gridDictionary"/> coordinate space) marking every
        /// road tile and start-object cell reached within range.</returns>
        public static bool[][] BreadthFirstSearch(
            IEnumerable<AnnoObject> placedObjects,
            IEnumerable<AnnoObject> startObjects,
            Func<AnnoObject, int> rangeGetter,
            Moved2DArray<AnnoObject> gridDictionary = null,
            Action<AnnoObject> inRangeAction = null)
        {
            var startList = startObjects as ICollection<AnnoObject> ?? startObjects.ToList();
            if (startList.Count == 0)
            {
                return new bool[0][];
            }

            gridDictionary = gridDictionary ?? PrepareGridDictionary(placedObjects);
            if (gridDictionary is null)
            {
                return new bool[0][];
            }

            inRangeAction = inRangeAction ?? DoNothing;

            var width = gridDictionary.Count;
            var height = gridDictionary[0].Length;

            // Highest remaining budget recorded for each cell (-1 = not reached yet).
            var budget = new int[width][];
            var covered = new bool[width][];
            for (var i = 0; i < width; i++)
            {
                budget[i] = new int[height];
                covered[i] = new bool[height];
                for (var j = 0; j < height; j++)
                {
                    budget[i][j] = -1;
                }
            }

            var visitedObjects = new HashSet<AnnoObject>();

            var maxBudget = 0;
            foreach (var startObject in startList)
            {
                maxBudget = Math.Max(maxBudget, rangeGetter(startObject) * CostScale);
            }

            // Dial's algorithm bucket queue, indexed by remaining budget.
            var buckets = new List<(int x, int y)>[maxBudget + 1];

            int CellCost(AnnoObject roadObject)
            {
                var factor = roadObject.RoadInfluenceFactor > 0 ? roadObject.RoadInfluenceFactor : 1.0;
                var cost = (int)Math.Round(CostScale / factor);
                return cost < 1 ? 1 : cost;
            }

            void Enqueue(int x, int y, int remaining)
            {
                if (remaining < 0 || remaining <= budget[x][y])
                {
                    return;
                }

                budget[x][y] = remaining;
                (buckets[remaining] ?? (buckets[remaining] = new List<(int x, int y)>())).Add((x, y));
            }

            // Queue an adjacent road tile, or highlight an adjacent non-road building reached from a road.
            void Relax(int x, int y, int fromBudget)
            {
                if (x < 0 || x >= width || y < 0 || y >= height)
                {
                    return;
                }

                var cellObject = gridDictionary[x][y];
                if (cellObject is null)
                {
                    return;
                }

                if (cellObject.Road)
                {
                    Enqueue(x, y, fromBudget - CellCost(cellObject));
                }
                else if (visitedObjects.Add(cellObject))
                {
                    inRangeAction(cellObject);
                }
            }

            // Seed: mark each start object's footprint covered and queue its adjacent road tiles.
            foreach (var startObject in startList)
            {
                var startBudget = rangeGetter(startObject) * CostScale;
                visitedObjects.Add(startObject);

                foreach (var (cx, cy) in GetFootprintCells(startObject))
                {
                    var ax = cx - gridDictionary.Offset.x;
                    var ay = cy - gridDictionary.Offset.y;
                    if (ax < 0 || ax >= width || ay < 0 || ay >= height)
                    {
                        continue;
                    }

                    covered[ax][ay] = true;

                    // queue any road tile orthogonally adjacent to this footprint cell
                    SeedRoad(ax + 1, ay, startBudget);
                    SeedRoad(ax - 1, ay, startBudget);
                    SeedRoad(ax, ay + 1, startBudget);
                    SeedRoad(ax, ay - 1, startBudget);
                }
            }

            void SeedRoad(int x, int y, int fromBudget)
            {
                if (x < 0 || x >= width || y < 0 || y >= height)
                {
                    return;
                }

                var cellObject = gridDictionary[x][y];
                if (cellObject != null && cellObject.Road)
                {
                    Enqueue(x, y, fromBudget - CellCost(cellObject));
                }
            }

            // Process cells from highest remaining budget to lowest. Budget only decreases
            // along a path, so the first time a cell is popped it holds its final maximum.
            for (var remaining = maxBudget; remaining >= 0; remaining--)
            {
                var bucket = buckets[remaining];
                if (bucket is null)
                {
                    continue;
                }

                foreach (var (x, y) in bucket)
                {
                    if (budget[x][y] != remaining)
                    {
                        continue; // stale entry; a higher budget was already finalized
                    }

                    covered[x][y] = true;

                    Relax(x + 1, y, remaining);
                    Relax(x - 1, y, remaining);
                    Relax(x, y + 1, remaining);
                    Relax(x, y - 1, remaining);
                }
            }

            return covered;
        }
    }
}
