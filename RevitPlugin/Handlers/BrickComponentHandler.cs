using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCP.Handlers
{
    /// <summary>
    /// Handles the "create_brick_component" MCP command.
    /// Creates FillPattern + Material + FloorType/WallType in Revit
    /// from tile/brick specifications extracted from PDF/DWG.
    /// </summary>
    public class BrickComponentHandler
    {
        private readonly Document _doc;

        public BrickComponentHandler(Document doc)
        {
            _doc = doc;
        }

        public JObject Execute(JObject parameters)
        {
            var materialsArray = parameters["materials"] as JArray;
            if (materialsArray == null || materialsArray.Count == 0)
                return Error("No materials provided");

            bool overwrite = parameters["overwriteExisting"]?.Value<bool>() ?? false;
            string baseFloorTypeName = parameters["baseFloorTypeName"]?.Value<string>() ?? null;

            var results = new JArray();

            using (Transaction tx = new Transaction(_doc, "MCP: Create Brick Components"))
            {
                tx.Start();

                foreach (JObject spec in materialsArray)
                {
                    try
                    {
                        var result = CreateSingleMaterial(spec, overwrite, baseFloorTypeName);
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject
                        {
                            ["code"] = spec["code"]?.Value<string>() ?? "unknown",
                            ["status"] = "error",
                            ["message"] = ex.Message
                        });
                    }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["status"] = "success",
                ["created"] = results.Count(r => r["status"]?.Value<string>() == "created"),
                ["errors"] = results.Count(r => r["status"]?.Value<string>() == "error"),
                ["results"] = results
            };
        }

        // ─── Per-material creation ────────────────────────────────────────────

        private JObject CreateSingleMaterial(JObject spec, bool overwrite, string baseFloorTypeName)
        {
            string code         = spec["code"]?.Value<string>() ?? "MAT";
            string name         = spec["name"]?.Value<string>() ?? code;
            double widthMm      = spec["widthMm"]?.Value<double>() ?? 0;
            double heightMm     = spec["heightMm"]?.Value<double>() ?? 0;
            double thicknessMm  = spec["thicknessMm"]?.Value<double>() ?? 10;
            double jointMm      = spec["jointWidthMm"]?.Value<double>() ?? 3;
            string patternType  = spec["patternType"]?.Value<string>() ?? "stack";
            string colorHex     = spec["colorHex"]?.Value<string>() ?? "#CCCCCC";
            string category     = spec["category"]?.Value<string>() ?? "floor";
            bool makeFloor      = spec["createFloorType"]?.Value<bool>() ?? true;
            bool makeWall       = spec["createWallType"]?.Value<bool>() ?? false;

            Color color = HexToRevitColor(colorHex);

            // 1. Create or get FillPattern
            FillPatternElement fillPatternElem = null;
            ElementId fillPatternId = ElementId.InvalidElementId;

            if (widthMm > 0 && heightMm > 0)
            {
                fillPatternElem = GetOrCreateFillPattern(
                    code, widthMm, heightMm, jointMm, patternType, overwrite
                );
                fillPatternId = fillPatternElem?.Id ?? ElementId.InvalidElementId;
            }

            // 2. Create or get Material
            Material material = GetOrCreateMaterial(name, code, color, fillPatternId, overwrite);

            var result = new JObject
            {
                ["code"]       = code,
                ["status"]     = "created",
                ["materialId"] = material.Id.IntegerValue,
                ["fillPatternId"] = fillPatternId.IntegerValue,
                ["floorTypeId"]   = -1,
                ["wallTypeId"]    = -1,
            };

            // 3. Create FloorType
            if (makeFloor && category != "wall")
            {
                var floorType = GetOrCreateFloorType(
                    code, name, material, thicknessMm, baseFloorTypeName, overwrite
                );
                if (floorType != null)
                    result["floorTypeId"] = floorType.Id.IntegerValue;
            }

            // 4. Create WallType (for paint / plaster / cladding)
            if (makeWall || category == "wall")
            {
                var wallType = GetOrCreateWallType(
                    code, name, material, thicknessMm, overwrite
                );
                if (wallType != null)
                    result["wallTypeId"] = wallType.Id.IntegerValue;
            }

            return result;
        }

        // ─── FillPattern ──────────────────────────────────────────────────────

        private FillPatternElement GetOrCreateFillPattern(
            string code, double widthMm, double heightMm,
            double jointMm, string patternType, bool overwrite)
        {
            string patternName = $"MCP_{code}_Pattern";

            // Check existing
            var existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(fp => fp.GetFillPattern().Name == patternName);

            if (existing != null)
            {
                if (!overwrite) return existing;
                _doc.Delete(existing.Id);
            }

            double wFt = MmToFeet(widthMm);
            double hFt = MmToFeet(heightMm);
            double jFt = MmToFeet(jointMm);

            FillPattern fillPattern = BuildFillPattern(patternName, wFt, hFt, jFt, patternType);
            return FillPatternElement.Create(_doc, fillPattern);
        }

        private FillPattern BuildFillPattern(
            string name, double wFt, double hFt, double jFt, string patternType)
        {
            var grids = new List<FillGrid>();

            switch (patternType.ToLower())
            {
                case "running_bond":
                    // Horizontal lines spaced hFt apart, alternating offset of wFt/2
                    grids.Add(MakeFillGrid(0, hFt, 0,          wFt, jFt));
                    grids.Add(MakeFillGrid(0, hFt, wFt / 2.0,  wFt, jFt));
                    // Vertical joints
                    grids.Add(MakeFillGrid(Math.PI / 2, wFt, 0, hFt, jFt));
                    break;

                case "herringbone":
                    // Two diagonal sets at ±45°
                    double diagLen = Math.Sqrt(wFt * wFt + hFt * hFt);
                    grids.Add(MakeFillGrid( Math.PI / 4, diagLen, 0, diagLen, jFt));
                    grids.Add(MakeFillGrid(-Math.PI / 4, diagLen, 0, diagLen, jFt));
                    break;

                case "diagonal":
                    grids.Add(MakeFillGrid(Math.PI / 4, wFt, 0, wFt, jFt));
                    grids.Add(MakeFillGrid(-Math.PI / 4, hFt, 0, hFt, jFt));
                    break;

                case "basket_weave":
                    // 2x1 basket pattern
                    grids.Add(MakeFillGrid(0,            hFt, 0,      wFt * 2, jFt));
                    grids.Add(MakeFillGrid(Math.PI / 2,  wFt * 2, 0,  hFt,     jFt));
                    break;

                default: // "stack" — simple rectangular grid
                    grids.Add(MakeFillGrid(0,           hFt, 0, wFt, jFt));
                    grids.Add(MakeFillGrid(Math.PI / 2, wFt, 0, hFt, jFt));
                    break;
            }

            var pattern = new FillPattern(name, FillPatternTarget.Drafting,
                FillPatternHostOrientation.ToView);
            pattern.SetFillGrids(grids);
            return pattern;
        }

        private FillGrid MakeFillGrid(
            double angle, double offset, double shift,
            double segmentLength, double gapLength)
        {
            var grid = new FillGrid
            {
                Angle  = angle,
                Offset = offset,
                Shift  = shift,
                Origin = new UV(0, 0),
            };
            grid.SetSegments(new[] { segmentLength, gapLength });
            return grid;
        }

        // ─── Material ─────────────────────────────────────────────────────────

        private Material GetOrCreateMaterial(
            string name, string code, Color color,
            ElementId fillPatternId, bool overwrite)
        {
            string matName = $"MCP_{code} - {name}";

            var existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => m.Name == matName);

            if (existing != null)
            {
                if (!overwrite) return existing;
                _doc.Delete(existing.Id);
            }

            ElementId newId = Material.Create(_doc, matName);
            Material mat = _doc.GetElement(newId) as Material;

            mat.Color              = color;
            mat.Transparency       = 0;
            mat.Shininess          = 64;
            mat.Smoothness         = 50;

            if (fillPatternId != ElementId.InvalidElementId)
            {
                mat.SurfaceForegroundPatternId    = fillPatternId;
                mat.SurfaceForegroundPatternColor  = Darken(color, 0.6f);
                mat.SurfaceBackgroundPatternId     = ElementId.InvalidElementId;

                mat.CutForegroundPatternId         = fillPatternId;
                mat.CutForegroundPatternColor      = Darken(color, 0.5f);
            }

            return mat;
        }

        // ─── FloorType ───────────────────────────────────────────────────────

        private FloorType GetOrCreateFloorType(
            string code, string name, Material material,
            double thicknessMm, string baseTypeName, bool overwrite)
        {
            string typeName = $"MCP_Floor_{code} - {name}";

            var existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault(ft => ft.Name == typeName);

            if (existing != null)
            {
                if (!overwrite) return existing;
                _doc.Delete(existing.Id);
            }

            // Find base floor type to duplicate
            FloorType baseType = null;
            if (!string.IsNullOrEmpty(baseTypeName))
            {
                baseType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FloorType))
                    .Cast<FloorType>()
                    .FirstOrDefault(ft => ft.Name == baseTypeName);
            }
            baseType ??= new FilteredElementCollector(_doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault(ft => ft.IsFoundationSlab == false);

            if (baseType == null) return null;

            FloorType newType = baseType.Duplicate(typeName) as FloorType;

            // Modify compound structure — set finish layer (index 0 = top)
            CompoundStructure cs = newType.GetCompoundStructure();
            if (cs != null)
            {
                // Insert finish layer at top (index 0)
                double thicknessFt = MmToFeet(Math.Max(thicknessMm, 1));
                var finishLayer = new CompoundStructureLayer(
                    thicknessFt,
                    MaterialFunctionAssignment.Finish1,
                    material.Id
                );

                var layers = new List<CompoundStructureLayer>(cs.GetLayers()) { finishLayer };
                // Move finish to front
                layers.Insert(0, layers.Last());
                layers.RemoveAt(layers.Count - 1);

                cs.SetLayers(layers);
                newType.SetCompoundStructure(cs);
            }

            return newType;
        }

        // ─── WallType ────────────────────────────────────────────────────────

        private WallType GetOrCreateWallType(
            string code, string name, Material material,
            double thicknessMm, bool overwrite)
        {
            string typeName = $"MCP_Wall_{code} - {name}";

            var existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(wt => wt.Name == typeName);

            if (existing != null)
            {
                if (!overwrite) return existing;
                _doc.Delete(existing.Id);
            }

            // Find a basic wall type to duplicate
            WallType baseType = new FilteredElementCollector(_doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(wt => wt.Kind == WallKind.Basic);

            if (baseType == null) return null;

            WallType newType = baseType.Duplicate(typeName) as WallType;

            CompoundStructure cs = newType.GetCompoundStructure();
            if (cs != null)
            {
                double thicknessFt = MmToFeet(Math.Max(thicknessMm, 1));

                // Replace finish1 layer material, or insert new finish layer
                var layers = new List<CompoundStructureLayer>(cs.GetLayers());
                var finishIdx = layers.FindIndex(l => l.Function == MaterialFunctionAssignment.Finish1);

                if (finishIdx >= 0)
                {
                    layers[finishIdx] = new CompoundStructureLayer(
                        thicknessFt, MaterialFunctionAssignment.Finish1, material.Id
                    );
                }
                else
                {
                    layers.Insert(0, new CompoundStructureLayer(
                        thicknessFt, MaterialFunctionAssignment.Finish1, material.Id
                    ));
                }

                cs.SetLayers(layers);
                newType.SetCompoundStructure(cs);
            }

            return newType;
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private static double MmToFeet(double mm) => mm / 304.8;

        private static Color HexToRevitColor(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return new Color(r, g, b);
            }
            return new Color(204, 204, 204);
        }

        private static Color Darken(Color c, float factor)
        {
            return new Color(
                (byte)(c.Red   * factor),
                (byte)(c.Green * factor),
                (byte)(c.Blue  * factor)
            );
        }

        private static JObject Error(string message) =>
            new JObject { ["status"] = "error", ["message"] = message };
    }
}
