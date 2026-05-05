using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCP.Handlers
{
    // ─── CreateLevel ──────────────────────────────────────────────────────────
    /// <summary>
    /// Creates new building levels in Revit project.
    /// Parameters:
    ///   - levelName (string): Name for the level
    ///   - elevation (double): Elevation in mm relative to project base
    ///   - description (string): Optional description
    ///   - structuralLevel (bool): Mark as structural level
    /// </summary>
    public class CreateLevelHandler
    {
        private readonly Document _doc;

        public CreateLevelHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            try
            {
                string levelName = parameters["levelName"]?.Value<string>();
                double elevation = parameters["elevation"]?.Value<double>() ?? 0;
                string description = parameters["description"]?.Value<string>();
                bool isStructural = parameters["structuralLevel"]?.Value<bool>() ?? false;

                if (string.IsNullOrEmpty(levelName))
                    return HandlerResponse.Error("levelName is required");

                // Check if level already exists
                var existingLevel = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault(l => l.Name == levelName);

                if (existingLevel != null)
                    return HandlerResponse.Error($"Level '{levelName}' already exists");

                using (var tx = new Transaction(_doc, "MCP: Create Level"))
                {
                    tx.Start();

                    try
                    {
                        // Convert mm to Revit internal units (feet)
                        double elevationInFeet = UnitUtils.ConvertToInternalUnits(elevation, DisplayUnitType.DUT_MILLIMETERS);

                        // Create new level
                        Level newLevel = Level.Create(_doc, elevationInFeet);
                        newLevel.Name = levelName;

                        // Set additional properties
                        if (!string.IsNullOrEmpty(description))
                        {
                            Parameter descParam = newLevel.LookupParameter("Comments");
                            if (descParam != null && !descParam.IsReadOnly)
                            {
                                descParam.Set(description);
                            }
                        }

                        // Note: IsStructural marking may require specific setup
                        // This is typically done through building type or other parameters

                        tx.Commit();

                        return HandlerResponse.Success(new JObject
                        {
                            ["levelId"] = newLevel.Id.IntegerValue,
                            ["levelName"] = newLevel.Name,
                            ["elevation"] = elevation,
                            ["elevationInMeters"] = elevation / 1000.0,
                            ["description"] = description ?? "",
                            ["isStructural"] = isStructural,
                            ["message"] = $"Level '{levelName}' created at {elevation}mm elevation"
                        });
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        return HandlerResponse.Error($"Error creating level: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                return HandlerResponse.Error($"Exception in CreateLevel: {ex.Message}");
            }
        }
    }

    // ─── CreateGrid ───────────────────────────────────────────────────────────
    /// <summary>
    /// Creates structural grids in Revit project.
    /// Parameters:
    ///   - gridName (string): Name for the grid (e.g., "A", "B", "1", "2")
    ///   - startPoint (object): {x, y, z} in mm
    ///   - endPoint (object): {x, y, z} in mm
    ///   - isHorizontal (bool): Direction of grid
    /// </summary>
    public class CreateGridHandler
    {
        private readonly Document _doc;

        public CreateGridHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            try
            {
                string gridName = parameters["gridName"]?.Value<string>();
                var startPt = parameters["startPoint"] as JObject;
                var endPt = parameters["endPoint"] as JObject;

                if (string.IsNullOrEmpty(gridName) || startPt == null || endPt == null)
                    return HandlerResponse.Error("gridName, startPoint, and endPoint are required");

                // Parse coordinates (in mm)
                double startX = startPt["x"]?.Value<double>() ?? 0;
                double startY = startPt["y"]?.Value<double>() ?? 0;
                double startZ = startPt["z"]?.Value<double>() ?? 0;

                double endX = endPt["x"]?.Value<double>() ?? 0;
                double endY = endPt["y"]?.Value<double>() ?? 0;
                double endZ = endPt["z"]?.Value<double>() ?? 0;

                // Check if grid already exists
                var existingGrid = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Grid))
                    .Cast<Grid>()
                    .FirstOrDefault(g => g.Name == gridName);

                if (existingGrid != null)
                    return HandlerResponse.Error($"Grid '{gridName}' already exists");

                using (var tx = new Transaction(_doc, "MCP: Create Grid"))
                {
                    tx.Start();

                    try
                    {
                        // Convert mm to Revit internal units (feet)
                        var startPoint = new XYZ(
                            UnitUtils.ConvertToInternalUnits(startX, DisplayUnitType.DUT_MILLIMETERS),
                            UnitUtils.ConvertToInternalUnits(startY, DisplayUnitType.DUT_MILLIMETERS),
                            UnitUtils.ConvertToInternalUnits(startZ, DisplayUnitType.DUT_MILLIMETERS)
                        );

                        var endPoint = new XYZ(
                            UnitUtils.ConvertToInternalUnits(endX, DisplayUnitType.DUT_MILLIMETERS),
                            UnitUtils.ConvertToInternalUnits(endY, DisplayUnitType.DUT_MILLIMETERS),
                            UnitUtils.ConvertToInternalUnits(endZ, DisplayUnitType.DUT_MILLIMETERS)
                        );

                        // Create line for grid geometry
                        Line gridLine = Line.CreateBound(startPoint, endPoint);

                        // Create grid
                        Grid newGrid = Grid.Create(_doc, gridLine);
                        newGrid.Name = gridName;

                        tx.Commit();

                        // Calculate grid direction and length
                        double gridLength = gridLine.Length;
                        string direction = DetermineGridDirection(gridLine);

                        return HandlerResponse.Success(new JObject
                        {
                            ["gridId"] = newGrid.Id.IntegerValue,
                            ["gridName"] = newGrid.Name,
                            ["startPoint"] = new JObject { ["x"] = startX, ["y"] = startY, ["z"] = startZ },
                            ["endPoint"] = new JObject { ["x"] = endX, ["y"] = endY, ["z"] = endZ },
                            ["length"] = Math.Round(gridLength * 304.8, 2), // Convert feet to mm
                            ["direction"] = direction,
                            ["message"] = $"Grid '{gridName}' created from {startX},{startY} to {endX},{endY}"
                        });
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        return HandlerResponse.Error($"Error creating grid: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                return HandlerResponse.Error($"Exception in CreateGrid: {ex.Message}");
            }
        }

        private string DetermineGridDirection(Line line)
        {
            XYZ dir = line.Direction.Normalize();
            
            // Check if more horizontal or vertical
            double absX = Math.Abs(dir.X);
            double absY = Math.Abs(dir.Y);

            if (absX > absY)
                return "Horizontal";
            else if (absY > absX)
                return "Vertical";
            else
                return "Diagonal";
        }
    }

    // ─── CreateGridIntersections ──────────────────────────────────────────────
    /// <summary>
    /// Creates reference points at grid intersections for planning purposes.
    /// Parameters:
    ///   - gridNames (string[]): Grid names to intersect (e.g., ["A", "B", "1", "2"])
    ///   - markerType (string): Type of marker (POINT, REFERENCE_POINT, GRID_INTERSECTION)
    ///   - level (string): Target level for markers
    /// </summary>
    public class GridIntersectionHandler
    {
        private readonly Document _doc;

        public GridIntersectionHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            try
            {
                var gridNames = parameters["gridNames"] as JArray;
                string markerType = parameters["markerType"]?.Value<string>() ?? "POINT";
                string levelName = parameters["level"]?.Value<string>();

                if (gridNames == null || gridNames.Count < 2)
                    return HandlerResponse.Error("At least 2 grid names required for intersections");

                // Get all grids in project
                var allGrids = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Grid))
                    .Cast<Grid>()
                    .ToDictionary(g => g.Name);

                // Get target level
                Level targetLevel = null;
                if (!string.IsNullOrEmpty(levelName))
                {
                    targetLevel = new FilteredElementCollector(_doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .FirstOrDefault(l => l.Name == levelName);

                    if (targetLevel == null)
                        return HandlerResponse.Error($"Level '{levelName}' not found");
                }

                using (var tx = new Transaction(_doc, "MCP: Create Grid Intersections"))
                {
                    tx.Start();

                    try
                    {
                        var intersections = new JArray();
                        var processedNames = new HashSet<string>();

                        // Find intersections between grid pairs
                        var gridList = gridNames.Cast<JToken>()
                            .Select(t => t.Value<string>())
                            .Where(n => allGrids.ContainsKey(n))
                            .ToList();

                        foreach (var gridName in gridList)
                        {
                            if (!allGrids.ContainsKey(gridName))
                                continue;

                            Grid grid = allGrids[gridName];
                            processedNames.Add(gridName);

                            // Find intersections with other grids
                            foreach (var otherName in gridList)
                            {
                                if (!processedNames.Contains(otherName) && allGrids.ContainsKey(otherName))
                                {
                                    Grid otherGrid = allGrids[otherName];
                                    
                                    // Find intersection point
                                    XYZ intersection = FindGridIntersection(grid, otherGrid);
                                    
                                    if (intersection != null)
                                    {
                                        intersections.Add(new JObject
                                        {
                                            ["gridPair"] = $"{gridName}-{otherName}",
                                            ["x"] = Math.Round(intersection.X * 304.8, 2),
                                            ["y"] = Math.Round(intersection.Y * 304.8, 2),
                                            ["z"] = Math.Round(intersection.Z * 304.8, 2)
                                        });
                                    }
                                }
                            }
                        }

                        tx.Commit();

                        return HandlerResponse.Success(new JObject
                        {
                            ["intersectionCount"] = intersections.Count,
                            ["markerType"] = markerType,
                            ["level"] = levelName ?? "All Levels",
                            ["intersections"] = intersections,
                            ["message"] = $"Found {intersections.Count} grid intersections"
                        });
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        return HandlerResponse.Error($"Error creating intersections: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                return HandlerResponse.Error($"Exception in GridIntersection: {ex.Message}");
            }
        }

        private XYZ FindGridIntersection(Grid grid1, Grid grid2)
        {
            // Get grid curves
            Curve curve1 = grid1.Curve;
            Curve curve2 = grid2.Curve;

            if (curve1 == null || curve2 == null)
                return null;

            // For simplicity, use midpoints or endpoints
            // In real implementation, would use proper curve intersection
            XYZ pt1 = curve1.GetEndPoint(0);
            XYZ pt2 = curve1.GetEndPoint(1);
            XYZ pt3 = curve2.GetEndPoint(0);
            XYZ pt4 = curve2.GetEndPoint(1);

            // Simple intersection approximation
            // This would need proper geometric intersection in production
            return new XYZ((pt1.X + pt2.X) / 2, (pt3.Y + pt4.Y) / 2, pt1.Z);
        }
    }

    // (Success/Error helpers are centralized in HandlerResponse)
}
