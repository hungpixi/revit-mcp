using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Commands
{
    /// <summary>
    /// Element-creation MCP commands.
    /// Input coordinates are in millimetres and are converted to feet (/304.8)
    /// before passing to the Revit API.
    /// </summary>
    public static class CreateCommands
    {
        private const double MmToFeet = 1.0 / 304.8;

        // ─────────────────────────────────────────────────────────────────────
        // create_point_based_element
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Places one or more FamilyInstances at point locations.
        ///
        /// params:
        ///   typeId   (int)           – FamilySymbol element id
        ///   levelId  (int)           – Level element id
        ///   points   (array)         – [{ x, y, z, rotationDeg? }] in mm
        ///   offset   (double)        – base level offset in mm (default 0)
        ///   structural (bool)        – default false
        /// </summary>
        public static JObject CreatePointBased(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            int typeId = @params["typeId"]?.Value<int>() ?? -1;
            int levelId = @params["levelId"]?.Value<int>() ?? -1;
            double offsetMm = @params["offset"]?.Value<double>() ?? 0;
            bool structural = @params["structural"]?.Value<bool>() ?? false;
            var points = @params["points"] as JArray;

            if (typeId == -1)         return Error("typeId is required");
            if (points == null || points.Count == 0) return Error("points array is required");

            FamilySymbol symbol = doc.GetElement(new ElementId(typeId)) as FamilySymbol;
            if (symbol == null) return Error($"FamilySymbol {typeId} not found");

            Level level = levelId != -1
                ? doc.GetElement(new ElementId(levelId)) as Level
                : GetFirstLevel(doc);
            if (level == null) return Error("No valid level found");

            var created = new JArray();

            using (var tx = new Transaction(doc, "MCP: Create Point-Based Elements"))
            {
                tx.Start();

                // Activate symbol if needed
                if (!symbol.IsActive) symbol.Activate();

                foreach (JObject pt in points)
                {
                    try
                    {
                        double x = (pt["x"]?.Value<double>() ?? 0) * MmToFeet;
                        double y = (pt["y"]?.Value<double>() ?? 0) * MmToFeet;
                        double z = (pt["z"]?.Value<double>() ?? 0) * MmToFeet;
                        double rotDeg = pt["rotationDeg"]?.Value<double>() ?? 0;

                        XYZ location = new XYZ(x, y, z);

                        FamilyInstance inst = doc.Create.NewFamilyInstance(
                            location, symbol, level,
                            structural ? StructuralType.Column : StructuralType.NonStructural);

                        // Apply offset parameter
                        if (Math.Abs(offsetMm) > 0.001)
                        {
                            Parameter offsetParam =
                                inst.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)
                                ?? inst.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                            offsetParam?.Set(offsetMm * MmToFeet);
                        }

                        // Rotation around Z
                        if (Math.Abs(rotDeg) > 0.001)
                        {
                            Line axis = Line.CreateBound(location, location + XYZ.BasisZ);
                            ElementTransformUtils.RotateElement(
                                doc, inst.Id, axis, rotDeg * Math.PI / 180.0);
                        }

                        created.Add(new JObject
                        {
                            ["id"]     = inst.Id.IntegerValue,
                            ["status"] = "created",
                            ["x"]      = Math.Round(x * 304.8, 1),
                            ["y"]      = Math.Round(y * 304.8, 1)
                        });
                    }
                    catch (Exception ex)
                    {
                        created.Add(new JObject
                        {
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
                ["created"] = created.Count(c => c["status"]?.Value<string>() == "created"),
                ["results"] = created
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // create_line_based_element
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates wall, beam, or generic line-based elements between pairs of
        /// start/end points.
        ///
        /// params:
        ///   elementType (string) – "wall" | "beam" | "family"
        ///   typeId      (int)    – WallType or FamilySymbol id
        ///   levelId     (int)    – base/work-plane level
        ///   height      (double) – wall height in mm (walls only; default 3000)
        ///   lines       (array)  – [{ x1,y1,z1,x2,y2,z2 }] in mm
        /// </summary>
        public static JObject CreateLineBased(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            string elementType = @params["elementType"]?.Value<string>() ?? "wall";
            int typeId    = @params["typeId"]?.Value<int>() ?? -1;
            int levelId   = @params["levelId"]?.Value<int>() ?? -1;
            double heightMm = @params["height"]?.Value<double>() ?? 3000;
            var lines = @params["lines"] as JArray;

            if (lines == null || lines.Count == 0) return Error("lines array is required");

            Level level = levelId != -1
                ? doc.GetElement(new ElementId(levelId)) as Level
                : GetFirstLevel(doc);
            if (level == null) return Error("No valid level found");

            var results = new JArray();

            using (var tx = new Transaction(doc, "MCP: Create Line-Based Elements"))
            {
                tx.Start();

                foreach (JObject ln in lines)
                {
                    try
                    {
                        double x1 = (ln["x1"]?.Value<double>() ?? 0) * MmToFeet;
                        double y1 = (ln["y1"]?.Value<double>() ?? 0) * MmToFeet;
                        double z1 = (ln["z1"]?.Value<double>() ?? level.Elevation);
                        double x2 = (ln["x2"]?.Value<double>() ?? 0) * MmToFeet;
                        double y2 = (ln["y2"]?.Value<double>() ?? 0) * MmToFeet;
                        double z2 = (ln["z2"]?.Value<double>() ?? level.Elevation);

                        // z1/z2 are in mm unless explicitly set as feet; convert only if they came from params
                        if (ln["z1"] != null) z1 = ln["z1"].Value<double>() * MmToFeet;
                        if (ln["z2"] != null) z2 = ln["z2"].Value<double>() * MmToFeet;

                        XYZ start = new XYZ(x1, y1, z1);
                        XYZ end   = new XYZ(x2, y2, z2);

                        if (start.DistanceTo(end) < 0.001)
                        {
                            results.Add(new JObject { ["status"] = "error", ["message"] = "Start and end are too close" });
                            continue;
                        }

                        Line line = Line.CreateBound(start, end);
                        int createdId = -1;

                        switch (elementType.ToLower())
                        {
                            case "wall":
                            {
                                WallType wt = typeId != -1
                                    ? doc.GetElement(new ElementId(typeId)) as WallType
                                    : GetFirstWallType(doc);
                                if (wt == null) throw new InvalidOperationException("No WallType found");

                                Wall wall = Wall.Create(
                                    doc, line, wt.Id, level.Id,
                                    heightMm * MmToFeet,
                                    /* offset */ 0,
                                    /* flip */ false,
                                    /* structural */ false);
                                createdId = wall.Id.IntegerValue;
                                break;
                            }

                            case "beam":
                            {
                                FamilySymbol sym = typeId != -1
                                    ? doc.GetElement(new ElementId(typeId)) as FamilySymbol
                                    : GetFirstBeamSymbol(doc);
                                if (sym == null) throw new InvalidOperationException("No beam FamilySymbol found");
                                if (!sym.IsActive) sym.Activate();

                                FamilyInstance beam = doc.Create.NewFamilyInstance(
                                    line, sym, level, StructuralType.Beam);
                                createdId = beam.Id.IntegerValue;
                                break;
                            }

                            default: // "family" — generic line-based
                            {
                                FamilySymbol sym = typeId != -1
                                    ? doc.GetElement(new ElementId(typeId)) as FamilySymbol
                                    : null;
                                if (sym == null) throw new InvalidOperationException("typeId required for family line-based");
                                if (!sym.IsActive) sym.Activate();

                                FamilyInstance fi = doc.Create.NewFamilyInstance(
                                    line, sym, level, StructuralType.NonStructural);
                                createdId = fi.Id.IntegerValue;
                                break;
                            }
                        }

                        results.Add(new JObject { ["id"] = createdId, ["status"] = "created" });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject { ["status"] = "error", ["message"] = ex.Message });
                    }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["status"]  = "success",
                ["created"] = results.Count(r => r["status"]?.Value<string>() == "created"),
                ["results"] = results
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // create_surface_based_element  (Floor)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a Floor from a closed boundary loop.
        ///
        /// params:
        ///   levelId   (int)   – Level element id
        ///   typeId    (int)   – FloorType element id (optional)
        ///   offset    (double)  – height offset in mm (default 0)
        ///   structural (bool) – default false
        ///   boundary  (array) – [{ x, y }] points in mm forming a closed loop
        /// </summary>
        public static JObject CreateSurfaceBased(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            int levelId   = @params["levelId"]?.Value<int>() ?? -1;
            int typeId    = @params["typeId"]?.Value<int>() ?? -1;
            double offsetMm = @params["offset"]?.Value<double>() ?? 0;
            bool structural = @params["structural"]?.Value<bool>() ?? false;
            var boundary  = @params["boundary"] as JArray;

            if (boundary == null || boundary.Count < 3)
                return Error("boundary requires at least 3 points");

            Level level = levelId != -1
                ? doc.GetElement(new ElementId(levelId)) as Level
                : GetFirstLevel(doc);
            if (level == null) return Error("No valid level found");

            FloorType floorType = typeId != -1
                ? doc.GetElement(new ElementId(typeId)) as FloorType
                : GetFirstFloorType(doc);
            if (floorType == null) return Error("No valid FloorType found");

            // Build CurveArray from boundary points
            var curveArray = new CurveArray();
            var xyzPts = boundary.Select(p => new XYZ(
                (p["x"]?.Value<double>() ?? 0) * MmToFeet,
                (p["y"]?.Value<double>() ?? 0) * MmToFeet,
                level.Elevation + offsetMm * MmToFeet
            )).ToList();

            // Close the loop
            for (int i = 0; i < xyzPts.Count; i++)
            {
                XYZ p1 = xyzPts[i];
                XYZ p2 = xyzPts[(i + 1) % xyzPts.Count];
                if (p1.DistanceTo(p2) > 0.001)
                    curveArray.Append(Line.CreateBound(p1, p2));
            }

            Floor floor;
            using (var tx = new Transaction(doc, "MCP: Create Floor"))
            {
                tx.Start();
                floor = doc.Create.NewFloor(curveArray, floorType, level, structural);

                if (floor != null && Math.Abs(offsetMm) > 0.001)
                {
                    Parameter offParam =
                        floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                    offParam?.Set(offsetMm * MmToFeet);
                }

                tx.Commit();
            }

            return new JObject
            {
                ["status"]  = "success",
                ["id"]      = floor?.Id.IntegerValue ?? -1,
                ["message"] = floor != null ? "Floor created" : "Floor creation failed"
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // create_grid
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a rectangular grid of named grid lines.
        ///
        /// params:
        ///   xCoords (array of double) – X positions in mm
        ///   yCoords (array of double) – Y positions in mm
        ///   xLength (double)          – length of X-direction lines in mm (default 20000)
        ///   yLength (double)          – length of Y-direction lines in mm (default 20000)
        ///   xNames  (array of string) – optional names; defaults to 1,2,3…
        ///   yNames  (array of string) – optional names; defaults to A,B,C…
        /// </summary>
        public static JObject CreateGrid(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            var xCoords = (@params["xCoords"] as JArray)?.Select(t => t.Value<double>()).ToList();
            var yCoords = (@params["yCoords"] as JArray)?.Select(t => t.Value<double>()).ToList();
            double xLenMm = @params["xLength"]?.Value<double>() ?? 20000;
            double yLenMm = @params["yLength"]?.Value<double>() ?? 20000;
            var xNames = (@params["xNames"] as JArray)?.Select(t => t.Value<string>()).ToList();
            var yNames = (@params["yNames"] as JArray)?.Select(t => t.Value<string>()).ToList();

            if ((xCoords == null || xCoords.Count == 0) &&
                (yCoords == null || yCoords.Count == 0))
                return Error("xCoords or yCoords must be provided");

            xCoords ??= new List<double>();
            yCoords ??= new List<double>();

            double halfY = yLenMm * MmToFeet / 2.0;
            double halfX = xLenMm * MmToFeet / 2.0;

            var results = new JArray();

            using (var tx = new Transaction(doc, "MCP: Create Grids"))
            {
                tx.Start();

                // X-direction grids (vertical lines): constant X, varying Y
                for (int i = 0; i < xCoords.Count; i++)
                {
                    try
                    {
                        double xFt = xCoords[i] * MmToFeet;
                        Line line = Line.CreateBound(
                            new XYZ(xFt, -halfY, 0),
                            new XYZ(xFt,  halfY, 0));
                        Grid grid = Grid.Create(doc, line);

                        string name = (xNames != null && i < xNames.Count)
                            ? xNames[i]
                            : (i + 1).ToString();
                        try { grid.Name = name; } catch { /* duplicate name — skip rename */ }

                        results.Add(new JObject
                        {
                            ["id"]        = grid.Id.IntegerValue,
                            ["name"]      = grid.Name,
                            ["direction"] = "X",
                            ["status"]    = "created"
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject { ["status"] = "error", ["message"] = ex.Message });
                    }
                }

                // Y-direction grids (horizontal lines): constant Y, varying X
                for (int i = 0; i < yCoords.Count; i++)
                {
                    try
                    {
                        double yFt = yCoords[i] * MmToFeet;
                        Line line = Line.CreateBound(
                            new XYZ(-halfX, yFt, 0),
                            new XYZ( halfX, yFt, 0));
                        Grid grid = Grid.Create(doc, line);

                        // Default Y-direction names: A, B, C…
                        string name = (yNames != null && i < yNames.Count)
                            ? yNames[i]
                            : ((char)('A' + i)).ToString();
                        try { grid.Name = name; } catch { }

                        results.Add(new JObject
                        {
                            ["id"]        = grid.Id.IntegerValue,
                            ["name"]      = grid.Name,
                            ["direction"] = "Y",
                            ["status"]    = "created"
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject { ["status"] = "error", ["message"] = ex.Message });
                    }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["status"]  = "success",
                ["created"] = results.Count(r => r["status"]?.Value<string>() == "created"),
                ["grids"]   = results
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // create_level
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates one or more levels at given elevations.
        ///
        /// params:
        ///   levels (array) – [{ elevation (mm), name? }]
        ///                    OR elevation can be a flat array of numbers
        /// </summary>
        public static JObject CreateLevel(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            var levelsParam = @params["levels"] as JArray;
            if (levelsParam == null || levelsParam.Count == 0)
                return Error("levels array is required");

            var results = new JArray();

            using (var tx = new Transaction(doc, "MCP: Create Levels"))
            {
                tx.Start();

                for (int i = 0; i < levelsParam.Count; i++)
                {
                    try
                    {
                        double elevMm;
                        string name = null;

                        var item = levelsParam[i];
                        if (item.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                        {
                            elevMm = item["elevation"]?.Value<double>() ?? 0;
                            name   = item["name"]?.Value<string>();
                        }
                        else
                        {
                            elevMm = item.Value<double>();
                        }

                        Level level = Level.Create(doc, elevMm * MmToFeet);
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            try { level.Name = name; } catch { }
                        }

                        results.Add(new JObject
                        {
                            ["id"]            = level.Id.IntegerValue,
                            ["name"]          = level.Name,
                            ["elevationMm"]   = Math.Round(elevMm, 1),
                            ["status"]        = "created"
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject { ["status"] = "error", ["message"] = ex.Message });
                    }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["status"]  = "success",
                ["created"] = results.Count(r => r["status"]?.Value<string>() == "created"),
                ["levels"]  = results
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // create_room
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Places rooms at UV point locations on a given level.
        /// The level must have closed room-bounding elements already in place.
        ///
        /// params:
        ///   levelId (int)   – Level element id
        ///   rooms   (array) – [{ x, y, name?, number? }] in mm
        /// </summary>
        public static JObject CreateRoom(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            int levelId = @params["levelId"]?.Value<int>() ?? -1;
            var rooms   = @params["rooms"] as JArray;

            if (rooms == null || rooms.Count == 0) return Error("rooms array is required");

            Level level = levelId != -1
                ? doc.GetElement(new ElementId(levelId)) as Level
                : GetFirstLevel(doc);
            if (level == null) return Error("No valid level found");

            var results = new JArray();

            using (var tx = new Transaction(doc, "MCP: Create Rooms"))
            {
                tx.Start();

                foreach (JObject r in rooms)
                {
                    try
                    {
                        double x = (r["x"]?.Value<double>() ?? 0) * MmToFeet;
                        double y = (r["y"]?.Value<double>() ?? 0) * MmToFeet;

                        UV pt = new UV(x, y);
                        Room room = doc.Create.NewRoom(level, pt);

                        string roomName   = r["name"]?.Value<string>();
                        string roomNumber = r["number"]?.Value<string>();

                        if (!string.IsNullOrWhiteSpace(roomName))
                        {
                            Parameter nameParam = room.get_Parameter(BuiltInParameter.ROOM_NAME);
                            nameParam?.Set(roomName);
                        }

                        if (!string.IsNullOrWhiteSpace(roomNumber))
                        {
                            Parameter numParam = room.get_Parameter(BuiltInParameter.ROOM_NUMBER);
                            numParam?.Set(roomNumber);
                        }

                        results.Add(new JObject
                        {
                            ["id"]     = room.Id.IntegerValue,
                            ["name"]   = room.Name,
                            ["status"] = "created"
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject { ["status"] = "error", ["message"] = ex.Message });
                    }
                }

                tx.Commit();
            }

            return new JObject
            {
                ["status"]  = "success",
                ["created"] = results.Count(r => r["status"]?.Value<string>() == "created"),
                ["rooms"]   = results
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // create_structural_framing_system
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a beam system (BeamSystem) on a rectangular boundary.
        ///
        /// params:
        ///   levelId     (int)    – Level element id
        ///   typeId      (int)    – FamilySymbol for beams
        ///   x1,y1,x2,y2 (double) – boundary rectangle corners in mm
        ///   spacingMm   (double) – beam spacing in mm (default 1200)
        ///   direction   (string) – "X" | "Y" (default "X")
        ///   layoutRule  (string) – "FixedDistance" | "MaximumSpacing" (default "MaximumSpacing")
        /// </summary>
        public static JObject CreateStructuralFraming(Document doc, JObject @params)
        {
            if (doc == null) return Error("No active document");

            int levelId   = @params["levelId"]?.Value<int>() ?? -1;
            int typeId    = @params["typeId"]?.Value<int>() ?? -1;
            double x1mm   = @params["x1"]?.Value<double>() ?? 0;
            double y1mm   = @params["y1"]?.Value<double>() ?? 0;
            double x2mm   = @params["x2"]?.Value<double>() ?? 6000;
            double y2mm   = @params["y2"]?.Value<double>() ?? 6000;
            double spacingMm = @params["spacingMm"]?.Value<double>() ?? 1200;
            string direction = @params["direction"]?.Value<string>() ?? "X";
            string layoutRule = @params["layoutRule"]?.Value<string>() ?? "MaximumSpacing";

            Level level = levelId != -1
                ? doc.GetElement(new ElementId(levelId)) as Level
                : GetFirstLevel(doc);
            if (level == null) return Error("No valid level found");

            FamilySymbol symbol = typeId != -1
                ? doc.GetElement(new ElementId(typeId)) as FamilySymbol
                : GetFirstBeamSymbol(doc);
            if (symbol == null) return Error("No beam FamilySymbol found");

            double x1 = x1mm * MmToFeet, y1 = y1mm * MmToFeet;
            double x2 = x2mm * MmToFeet, y2 = y2mm * MmToFeet;
            double z  = level.Elevation;

            // Build the boundary curve loop
            var loop = new CurveArray();
            loop.Append(Line.CreateBound(new XYZ(x1, y1, z), new XYZ(x2, y1, z)));
            loop.Append(Line.CreateBound(new XYZ(x2, y1, z), new XYZ(x2, y2, z)));
            loop.Append(Line.CreateBound(new XYZ(x2, y2, z), new XYZ(x1, y2, z)));
            loop.Append(Line.CreateBound(new XYZ(x1, y2, z), new XYZ(x1, y1, z)));

            BeamSystem beamSystem;

            using (var tx = new Transaction(doc, "MCP: Create Beam System"))
            {
                tx.Start();

                if (!symbol.IsActive) symbol.Activate();

                beamSystem = BeamSystem.Create(doc, loop, level, false);

                if (beamSystem != null)
                {
                    // Set beam type
                    beamSystem.BeamSymbolId = symbol.Id;

                    // Configure layout rule and spacing.
                    // The Revit API uses LayoutRule (base class) stored on BeamSystem.LayoutRule.
                    // LayoutRuleSingleParameter is the common subclass exposing a Spacing property.
                    double spacingFt = spacingMm * MmToFeet;
                    bool useFixedDistance = layoutRule.Equals("FixedDistance",
                        StringComparison.OrdinalIgnoreCase);

                    try
                    {
                        if (useFixedDistance)
                            beamSystem.LayoutRule = new LayoutRuleFixedDistance(spacingFt);
                        else
                            beamSystem.LayoutRule = new LayoutRuleMaximumSpacing(spacingFt);
                    }
                    catch
                    {
                        // Fallback: if constructors not available, update the existing rule
                        if (beamSystem.LayoutRule is LayoutRuleSingleParameter sp)
                            sp.Spacing = spacingFt;
                    }

                    // Beam direction
                    XYZ dir = direction.Equals("Y", StringComparison.OrdinalIgnoreCase)
                        ? XYZ.BasisY : XYZ.BasisX;
                    beamSystem.Direction = dir;
                }

                tx.Commit();
            }

            return new JObject
            {
                ["status"]  = "success",
                ["id"]      = beamSystem?.Id.IntegerValue ?? -1,
                ["message"] = beamSystem != null ? "BeamSystem created" : "BeamSystem creation failed"
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static Level GetFirstLevel(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();
        }

        private static WallType GetFirstWallType(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(wt => wt.Kind == WallKind.Basic);
        }

        private static FloorType GetFirstFloorType(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault(ft => !ft.IsFoundationSlab);
        }

        private static FamilySymbol GetFirstBeamSymbol(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .FirstOrDefault();
        }

        private static JObject Error(string msg) =>
            new JObject { ["status"] = "error", ["message"] = msg };
    }
}
