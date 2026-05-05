using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Commands
{
    /// <summary>
    /// Element-operation MCP commands: delete, override graphics, tag, etc.
    /// </summary>
    public static class OperateCommands
    {
        private const double MmToFeet = 1.0 / 304.8;
        private const double FeetToMm = 304.8;

        // ─────────────────────────────────────────────────────────────────────
        // delete_element
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Deletes one or more elements by id.
        ///
        /// params:
        ///   elementIds (array of int) – required
        /// </summary>
        public static JObject DeleteElement(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            var idArray = @params["elementIds"] as JArray;
            if (idArray == null || idArray.Count == 0)
                return Error("elementIds array is required");

            var results = new JArray();

            using (var tx = new Transaction(doc, "MCP: Delete Elements"))
            {
                tx.Start();

                foreach (var token in idArray)
                {
                    int eid = token.Value<int>();
                    try
                    {
                        Element elem = doc.GetElement(new ElementId(eid));
                        if (elem == null)
                        {
                            results.Add(new JObject
                            {
                                ["id"]      = eid,
                                ["status"]  = "error",
                                ["message"] = "Element not found"
                            });
                            continue;
                        }

                        ICollection<ElementId> deleted = doc.Delete(new ElementId(eid));
                        results.Add(new JObject
                        {
                            ["id"]      = eid,
                            ["status"]  = "deleted",
                            ["cascade"] = deleted.Count - 1 // additional elements removed
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject
                        {
                            ["id"]      = eid,
                            ["status"]  = "error",
                            ["message"] = ex.Message
                        });
                    }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["status"]  = "success",
                ["deleted"] = results.Count(r => r["status"]?.Value<string>() == "deleted"),
                ["results"] = results
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // operate_element
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Performs a named action on a list of elements.
        ///
        /// params:
        ///   action       (string) – Select | SetColor | SetTransparency | Hide |
        ///                           TempHide | Isolate | Unhide | ResetIsolate |
        ///                           Delete | Highlight | Move | Rotate
        ///   elementIds   (array of int)
        ///   color        (string)  – hex colour for SetColor (e.g. "#FF0000")
        ///   transparency (int)     – 0-100 for SetTransparency
        ///   move         (object)  – { x, y, z } mm for Move
        ///   rotateDeg    (double)  – degrees for Rotate
        /// </summary>
        public static JObject OperateElement(UIApplication uiApp, Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            string action = @params["action"]?.Value<string>() ?? "";
            var idArray   = @params["elementIds"] as JArray;

            if (string.IsNullOrWhiteSpace(action)) return Error("action is required");
            if (idArray == null || idArray.Count == 0) return Error("elementIds is required");

            var elementIds = idArray
                .Select(t => new ElementId(t.Value<int>()))
                .Where(id => doc.GetElement(id) != null)
                .ToList();

            if (elementIds.Count == 0) return Error("No valid elements found for given ids");

            View activeView = doc.ActiveView;
            var results = new JArray();

            switch (action)
            {
                // ── Selection ──────────────────────────────────────────────
                case "Select":
                case "Highlight":
                {
                    if (uiApp?.ActiveUIDocument == null)
                        return Error("No active UI document for selection");
                    uiApp.ActiveUIDocument.Selection.SetElementIds(elementIds);
                    if (action == "Highlight")
                        uiApp.ActiveUIDocument.ShowElements(elementIds);
                    return new JObject
                    {
                        ["status"]  = "success",
                        ["action"]  = action,
                        ["count"]   = elementIds.Count
                    };
                }

                // ── Delete ─────────────────────────────────────────────────
                case "Delete":
                {
                    using (var tx = new Transaction(doc, "MCP: Delete Elements (operate)"))
                    {
                        tx.Start();
                        foreach (ElementId eid in elementIds)
                        {
                            try
                            {
                                doc.Delete(eid);
                                results.Add(new JObject { ["id"] = eid.IntegerValue, ["status"] = "deleted" });
                            }
                            catch (Exception ex)
                            {
                                results.Add(new JObject
                                {
                                    ["id"] = eid.IntegerValue,
                                    ["status"] = "error",
                                    ["message"] = ex.Message
                                });
                            }
                        }
                        tx.Commit();
                    }
                    return new JObject { ["status"] = "success", ["results"] = results };
                }

                // ── SetColor ───────────────────────────────────────────────
                case "SetColor":
                {
                    if (activeView == null) return Error("No active view");
                    string colorHex = @params["color"]?.Value<string>() ?? "#FF0000";
                    Color revitColor = HexToColor(colorHex);

                    using (var tx = new Transaction(doc, "MCP: Set Color"))
                    {
                        tx.Start();
                        var ogs = new OverrideGraphicSettings();
                        ogs.SetProjectionLineColor(revitColor);
                        ogs.SetSurfaceForegroundPatternColor(revitColor);
                        ogs.SetSurfaceForegroundPatternVisible(true);

                        foreach (ElementId eid in elementIds)
                        {
                            activeView.SetElementOverrides(eid, ogs);
                            results.Add(new JObject { ["id"] = eid.IntegerValue, ["status"] = "colored" });
                        }
                        tx.Commit();
                    }
                    return new JObject { ["status"] = "success", ["results"] = results };
                }

                // ── SetTransparency ────────────────────────────────────────
                case "SetTransparency":
                {
                    if (activeView == null) return Error("No active view");
                    int transparency = Math.Max(0, Math.Min(100,
                        @params["transparency"]?.Value<int>() ?? 50));

                    using (var tx = new Transaction(doc, "MCP: Set Transparency"))
                    {
                        tx.Start();
                        var ogs = new OverrideGraphicSettings();
                        ogs.SetSurfaceTransparency(transparency);

                        foreach (ElementId eid in elementIds)
                        {
                            activeView.SetElementOverrides(eid, ogs);
                            results.Add(new JObject { ["id"] = eid.IntegerValue, ["status"] = "transparency_set" });
                        }
                        tx.Commit();
                    }
                    return new JObject { ["status"] = "success", ["results"] = results };
                }

                // ── Hide / Unhide ──────────────────────────────────────────
                case "Hide":
                {
                    if (activeView == null) return Error("No active view");
                    using (var tx = new Transaction(doc, "MCP: Hide Elements"))
                    {
                        tx.Start();
                        activeView.HideElements(elementIds);
                        tx.Commit();
                    }
                    return new JObject { ["status"] = "success", ["hidden"] = elementIds.Count };
                }

                case "Unhide":
                {
                    if (activeView == null) return Error("No active view");
                    using (var tx = new Transaction(doc, "MCP: Unhide Elements"))
                    {
                        tx.Start();
                        activeView.UnhideElements(elementIds);
                        tx.Commit();
                    }
                    return new JObject { ["status"] = "success", ["unhidden"] = elementIds.Count };
                }

                // ── Temporary Hide / Isolate ───────────────────────────────
                case "TempHide":
                {
                    if (uiApp?.ActiveUIDocument == null)
                        return Error("No active UI document");
                    uiApp.ActiveUIDocument.RequestViewChange(activeView);
                    using (var tx = new Transaction(doc, "MCP: Temp Hide"))
                    {
                        tx.Start();
                        activeView.HideElementsTemporary(elementIds);
                        tx.Commit();
                    }
                    return new JObject { ["status"] = "success", ["tempHidden"] = elementIds.Count };
                }

                case "Isolate":
                {
                    if (activeView == null) return Error("No active view");
                    using (var tx = new Transaction(doc, "MCP: Isolate Elements"))
                    {
                        tx.Start();
                        activeView.IsolateElementsTemporary(elementIds);
                        tx.Commit();
                    }
                    return new JObject { ["status"] = "success", ["isolated"] = elementIds.Count };
                }

                case "ResetIsolate":
                {
                    if (activeView == null) return Error("No active view");
                    using (var tx = new Transaction(doc, "MCP: Reset Temporary Hide/Isolate"))
                    {
                        tx.Start();
                        activeView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                        tx.Commit();
                    }
                    return new JObject { ["status"] = "success", ["message"] = "Temporary view mode reset" };
                }

                // ── Move ───────────────────────────────────────────────────
                case "Move":
                {
                    var movePt = @params["move"] as JObject;
                    if (movePt == null) return Error("move {x,y,z} is required");
                    double dx = (movePt["x"]?.Value<double>() ?? 0) * MmToFeet;
                    double dy = (movePt["y"]?.Value<double>() ?? 0) * MmToFeet;
                    double dz = (movePt["z"]?.Value<double>() ?? 0) * MmToFeet;

                    using (var tx = new Transaction(doc, "MCP: Move Elements"))
                    {
                        tx.Start();
                        foreach (ElementId eid in elementIds)
                        {
                            try
                            {
                                ElementTransformUtils.MoveElement(doc, eid, new XYZ(dx, dy, dz));
                                results.Add(new JObject { ["id"] = eid.IntegerValue, ["status"] = "moved" });
                            }
                            catch (Exception ex)
                            {
                                results.Add(new JObject
                                {
                                    ["id"] = eid.IntegerValue, ["status"] = "error",
                                    ["message"] = ex.Message
                                });
                            }
                        }
                        tx.Commit();
                    }
                    return new JObject { ["status"] = "success", ["results"] = results };
                }

                // ── Rotate ─────────────────────────────────────────────────
                case "Rotate":
                {
                    double deg = @params["rotateDeg"]?.Value<double>() ?? 0;
                    double rad = deg * Math.PI / 180.0;

                    using (var tx = new Transaction(doc, "MCP: Rotate Elements"))
                    {
                        tx.Start();
                        foreach (ElementId eid in elementIds)
                        {
                            try
                            {
                                BoundingBoxXYZ bb = doc.GetElement(eid).get_BoundingBox(null);
                                XYZ center = bb != null ? (bb.Min + bb.Max) / 2 : XYZ.Zero;
                                Line axis = Line.CreateBound(center, center + XYZ.BasisZ);
                                ElementTransformUtils.RotateElement(doc, eid, axis, rad);
                                results.Add(new JObject { ["id"] = eid.IntegerValue, ["status"] = "rotated" });
                            }
                            catch (Exception ex)
                            {
                                results.Add(new JObject
                                {
                                    ["id"] = eid.IntegerValue, ["status"] = "error",
                                    ["message"] = ex.Message
                                });
                            }
                        }
                        tx.Commit();
                    }
                    return new JObject { ["status"] = "success", ["results"] = results };
                }

                default:
                    return Error($"Unknown action: {action}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // color_elements
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Groups elements by a parameter value and applies a colour per group.
        ///
        /// params:
        ///   parameterName (string)        – parameter to group by
        ///   colorMap      (object)        – { "value": "#hexcolor", ... } optional overrides
        ///   defaultColor  (string)        – hex colour for unmapped values (default "#AAAAAA")
        ///   elementIds    (array of int)  – elements to colour; omit for all in active view
        /// </summary>
        public static JObject ColorElements(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            string paramName   = @params["parameterName"]?.Value<string>();
            var colorMap       = @params["colorMap"] as JObject;
            string defaultHex  = @params["defaultColor"]?.Value<string>() ?? "#AAAAAA";
            var idArray        = @params["elementIds"] as JArray;

            if (string.IsNullOrWhiteSpace(paramName))
                return Error("parameterName is required");

            View activeView = doc.ActiveView;
            if (activeView == null) return Error("No active view");

            // Resolve elements
            IEnumerable<Element> elements;
            if (idArray != null && idArray.Count > 0)
            {
                elements = idArray
                    .Select(t => doc.GetElement(new ElementId(t.Value<int>())))
                    .Where(e => e != null);
            }
            else
            {
                elements = new FilteredElementCollector(doc, activeView.Id)
                    .WhereElementIsNotElementType()
                    .ToElements();
            }

            // Group by parameter value
            var groups = new Dictionary<string, List<ElementId>>();
            foreach (Element elem in elements)
            {
                Parameter p = elem.LookupParameter(paramName);
                string val = p != null ? p.AsValueString() ?? p.AsString() ?? "Unknown" : "NoParam";
                if (!groups.ContainsKey(val)) groups[val] = new List<ElementId>();
                groups[val].Add(elem.Id);
            }

            // Build colour palette
            var palette = GenerateColorPalette(groups.Count);
            var groupResults = new JArray();
            int paletteIdx = 0;

            using (var tx = new Transaction(doc, "MCP: Color Elements by Parameter"))
            {
                tx.Start();

                foreach (var kvp in groups)
                {
                    string value = kvp.Key;
                    Color color;

                    if (colorMap != null && colorMap[value] != null)
                        color = HexToColor(colorMap[value].Value<string>());
                    else if (paletteIdx < palette.Count)
                        color = palette[paletteIdx++];
                    else
                        color = HexToColor(defaultHex);

                    var ogs = new OverrideGraphicSettings();
                    ogs.SetSurfaceForegroundPatternColor(color);
                    ogs.SetSurfaceForegroundPatternVisible(true);
                    ogs.SetProjectionLineColor(color);

                    foreach (ElementId eid in kvp.Value)
                        activeView.SetElementOverrides(eid, ogs);

                    groupResults.Add(new JObject
                    {
                        ["value"] = value,
                        ["count"] = kvp.Value.Count,
                        ["color"] = ColorToHex(color)
                    });
                }

                tx.Commit();
            }

            return new JObject
            {
                ["status"] = "success",
                ["groups"] = groupResults.Count,
                ["breakdown"] = groupResults
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // tag_all_walls
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates an IndependentTag for each visible wall in the active view.
        ///
        /// params:
        ///   tagTypeId    (int)    – TagSymbol element id (optional; uses first available)
        ///   leader       (bool)   – default false
        ///   orientation  (string) – "Horizontal" | "Vertical" (default "Horizontal")
        /// </summary>
        public static JObject TagAllWalls(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            View activeView = doc.ActiveView;
            if (activeView == null) return Error("No active view");

            int tagTypeId   = @params["tagTypeId"]?.Value<int>() ?? -1;
            bool leader     = @params["leader"]?.Value<bool>() ?? false;
            string orientStr = @params["orientation"]?.Value<string>() ?? "Horizontal";
            TagOrientation orient = orientStr.Equals("Vertical", StringComparison.OrdinalIgnoreCase)
                ? TagOrientation.Vertical : TagOrientation.Horizontal;

            // Find a wall tag type
            FamilySymbol tagSymbol = tagTypeId != -1
                ? doc.GetElement(new ElementId(tagTypeId)) as FamilySymbol
                : GetFirstTagSymbol(doc, BuiltInCategory.OST_WallTags);

            if (tagSymbol == null)
                return Error("No wall tag type found in document. Load a wall tag family first.");

            // Collect walls visible in view
            var walls = new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .ToList();

            if (walls.Count == 0)
                return new JObject { ["status"] = "success", ["tagged"] = 0, ["message"] = "No walls in view" };

            var results = new JArray();

            using (var tx = new Transaction(doc, "MCP: Tag All Walls"))
            {
                tx.Start();

                if (!tagSymbol.IsActive) tagSymbol.Activate();

                foreach (Wall wall in walls)
                {
                    try
                    {
                        XYZ tagPt = GetElementMidpoint(wall);
                        Reference wallRef = new Reference(wall);

                        IndependentTag tag = IndependentTag.Create(
                            doc, tagSymbol.Id, activeView.Id, wallRef, leader, orient, tagPt);

                        results.Add(new JObject
                        {
                            ["wallId"]  = wall.Id.IntegerValue,
                            ["tagId"]   = tag.Id.IntegerValue,
                            ["status"]  = "tagged"
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject
                        {
                            ["wallId"]  = wall.Id.IntegerValue,
                            ["status"]  = "error",
                            ["message"] = ex.Message
                        });
                    }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["status"] = "success",
                ["tagged"] = results.Count(r => r["status"]?.Value<string>() == "tagged"),
                ["errors"] = results.Count(r => r["status"]?.Value<string>() == "error"),
                ["results"] = results
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // tag_all_rooms
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a RoomTag for each placed room visible in the active floor plan view.
        ///
        /// params:
        ///   tagTypeId   (int)  – RoomTagType id (optional)
        ///   leader      (bool) – default false
        /// </summary>
        public static JObject TagAllRooms(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            ViewPlan floorPlan = doc.ActiveView as ViewPlan;
            if (floorPlan == null)
                return Error("Active view must be a floor plan to tag rooms");

            int tagTypeId = @params["tagTypeId"]?.Value<int>() ?? -1;
            bool leader   = @params["leader"]?.Value<bool>() ?? false;

            // Find a room tag type
            RoomTagType tagType = tagTypeId != -1
                ? doc.GetElement(new ElementId(tagTypeId)) as RoomTagType
                : new FilteredElementCollector(doc)
                    .OfClass(typeof(RoomTagType))
                    .Cast<RoomTagType>()
                    .FirstOrDefault();

            if (tagType == null)
                return Error("No RoomTagType found. Load a room tag family first.");

            // Rooms associated with this view's level
            var rooms = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement))
                .OfType<Room>()
                .Where(r => r.Area > 0 && r.LevelId == floorPlan.GenLevel?.Id)
                .ToList();

            var results = new JArray();

            using (var tx = new Transaction(doc, "MCP: Tag All Rooms"))
            {
                tx.Start();

                foreach (Room room in rooms)
                {
                    try
                    {
                        LocationPoint lp = room.Location as LocationPoint;
                        if (lp == null) continue;

                        UV pt = new UV(lp.Point.X, lp.Point.Y);
                        RoomTag tag = doc.Create.NewRoomTag(
                            new LinkElementId(room.Id), pt, floorPlan.Id);

                        // Change to the desired tag type
                        if (tag.RoomTagType?.Id != tagType.Id)
                            tag.ChangeTypeId(tagType.Id);

                        results.Add(new JObject
                        {
                            ["roomId"]  = room.Id.IntegerValue,
                            ["tagId"]   = tag.Id.IntegerValue,
                            ["status"]  = "tagged"
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject
                        {
                            ["roomId"]  = room.Id.IntegerValue,
                            ["status"]  = "error",
                            ["message"] = ex.Message
                        });
                    }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["status"] = "success",
                ["tagged"] = results.Count(r => r["status"]?.Value<string>() == "tagged"),
                ["errors"] = results.Count(r => r["status"]?.Value<string>() == "error"),
                ["results"] = results
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static XYZ GetElementMidpoint(Element elem)
        {
            if (elem.Location is LocationCurve lc)
                return lc.Curve.Evaluate(0.5, true);

            if (elem.Location is LocationPoint lp)
                return lp.Point;

            BoundingBoxXYZ bb = elem.get_BoundingBox(null);
            if (bb != null) return (bb.Min + bb.Max) / 2;

            return XYZ.Zero;
        }

        private static FamilySymbol GetFirstTagSymbol(Document doc, BuiltInCategory tagCat)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(tagCat)
                .Cast<FamilySymbol>()
                .FirstOrDefault();
        }

        private static Color HexToColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return new Color(170, 170, 170);
            hex = hex.TrimStart('#');
            if (hex.Length < 6) return new Color(170, 170, 170);
            try
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return new Color(r, g, b);
            }
            catch { return new Color(170, 170, 170); }
        }

        private static string ColorToHex(Color c) =>
            $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}";

        /// <summary>Generates a visually distinct palette for N groups.</summary>
        private static List<Color> GenerateColorPalette(int count)
        {
            var palette = new List<Color>();
            // Golden-ratio hue distribution for maximum separation
            double goldenRatio = 0.618033988749895;
            double h = 0.1;
            for (int i = 0; i < count; i++)
            {
                h = (h + goldenRatio) % 1.0;
                var (r, g, b) = HsvToRgb(h, 0.65, 0.90);
                palette.Add(new Color((byte)r, (byte)g, (byte)b));
            }
            return palette;
        }

        private static (int r, int g, int b) HsvToRgb(double h, double s, double v)
        {
            int hi = (int)(h * 6) % 6;
            double f  = h * 6 - Math.Floor(h * 6);
            double p  = v * (1 - s);
            double q  = v * (1 - f * s);
            double t  = v * (1 - (1 - f) * s);

            double r, g, b;
            switch (hi)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }

            return ((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private static JObject Error(string msg) =>
            new JObject { ["status"] = "error", ["message"] = msg };
    }
}
