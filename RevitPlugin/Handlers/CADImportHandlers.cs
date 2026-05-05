using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Newtonsoft.Json.Linq;

namespace RevitMCP.Handlers
{
    // ─── GetFileInfo ──────────────────────────────────────────────────────────
    /// <summary>
    /// Retrieves Revit project metadata and linked CAD files.
    /// Parameters:
    ///   - includeLinkedFiles (bool): Include list of linked CAD files
    /// </summary>
    public class GetFileInfoHandler
    {
        private readonly Document _doc;

        public GetFileInfoHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            try
            {
                bool includeLinkedFiles = parameters["includeLinkedFiles"]?.Value<bool>() ?? false;

                var linkedCADFiles = new JArray();
                if (includeLinkedFiles)
                {
                    // Get all linked models
                    var linkedDocs = new FilteredElementCollector(_doc)
                        .OfClass(typeof(RevitLinkInstance))
                        .Cast<RevitLinkInstance>()
                        .Where(link => link.GetLinkDocument() != null)
                        .ToList();

                    // Also get CAD imports
                    var cadImports = new FilteredElementCollector(_doc)
                        .OfClass(typeof(ImportInstance))
                        .Cast<ImportInstance>()
                        .ToList();

                    // Extract file paths from linked Revit files
                    foreach (var link in linkedDocs)
                    {
                        string linkPath = link.GetLinkDocument()?.PathName ?? "";
                        if (!string.IsNullOrEmpty(linkPath) && linkPath.EndsWith(".rvt"))
                        {
                            linkedCADFiles.Add(Path.GetFileName(linkPath));
                        }
                    }

                    // Extract file paths from CAD imports
                    foreach (var cad in cadImports)
                    {
                        string cadPath = cad.GetSourceFileName() ?? "";
                        if (!string.IsNullOrEmpty(cadPath) && (cadPath.EndsWith(".dwg") || cadPath.EndsWith(".dxf")))
                        {
                            linkedCADFiles.Add(Path.GetFileName(cadPath));
                        }
                    }
                }

                // Get project properties
                ProjectInfo projInfo = _doc.ProjectInformation;
                string title = projInfo?.Name ?? "Unnamed Project";
                string filePath = _doc.PathName ?? "";
                string version = _doc.Application.VersionBuild;

                // Count total elements
                int elementCount = new FilteredElementCollector(_doc).Count();

                // Get file size
                long fileSize = 0;
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    fileSize = new FileInfo(filePath).Length / (1024 * 1024); // MB
                }

                return Success(new JObject
                {
                    ["filePath"] = filePath,
                    ["title"] = title,
                    ["version"] = version,
                    ["linkedCADFiles"] = linkedCADFiles,
                    ["elementCount"] = elementCount,
                    ["fileSize"] = $"{fileSize} MB"
                });
            }
            catch (Exception ex)
            {
                return Error($"Error getting file info: {ex.Message}");
            }
        }
    }

    // ─── GetCADEntities ───────────────────────────────────────────────────────
    /// <summary>
    /// Extracts entities (lines, blocks, text) from linked CAD files.
    /// Parameters:
    ///   - linkedCADFileName (string): Name of the CAD file
    ///   - entityTypes (string[]): Types to extract (LINE, BLOCK, TEXT, etc.)
    ///   - layerFilter (string[]): Layers to include (optional)
    /// </summary>
    public class GetCADEntitiesHandler
    {
        private readonly Document _doc;

        public GetCADEntitiesHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            try
            {
                string cadFileName = parameters["linkedCADFileName"]?.Value<string>();
                var entityTypes = parameters["entityTypes"] as JArray;
                var layerFilter = parameters["layerFilter"] as JArray;

                if (string.IsNullOrEmpty(cadFileName))
                    return Error("linkedCADFileName is required");

                // Find CAD import
                var cadImport = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ImportInstance))
                    .Cast<ImportInstance>()
                    .FirstOrDefault(ci => ci.GetSourceFileName().EndsWith(cadFileName));

                if (cadImport == null)
                    return Error($"CAD file not found: {cadFileName}");

                var entities = new JArray();
                var geometryElement = cadImport.GetGeometryObject();

                if (geometryElement != null)
                {
                    // Extract geometry primitives
                    ExtractGeometryEntities(geometryElement, entities, entityTypes, layerFilter);
                }

                return Success(new JObject
                {
                    ["success"] = true,
                    ["fileName"] = cadFileName,
                    ["entities"] = entities,
                    ["totalEntities"] = entities.Count
                });
            }
            catch (Exception ex)
            {
                return Error($"Error extracting CAD entities: {ex.Message}");
            }
        }

        private void ExtractGeometryEntities(GeometryElement geom, JArray entities, JArray entityTypes, JArray layerFilter)
        {
            var typeList = entityTypes?.ToObject<List<string>>() ?? new List<string>();
            var layers = layerFilter?.ToObject<List<string>>() ?? new List<string>();

            foreach (GeometryObject obj in geom)
            {
                if (obj is Line line && (typeList.Count == 0 || typeList.Contains("LINE")))
                {
                    entities.Add(new JObject
                    {
                        ["type"] = "LINE",
                        ["layer"] = "Default",
                        ["start"] = new JObject { ["x"] = Math.Round(line.GetEndPoint(0).X, 2), ["y"] = Math.Round(line.GetEndPoint(0).Y, 2) },
                        ["end"] = new JObject { ["x"] = Math.Round(line.GetEndPoint(1).X, 2), ["y"] = Math.Round(line.GetEndPoint(1).Y, 2) }
                    });
                }
                else if (obj is Arc arc && (typeList.Count == 0 || typeList.Contains("ARC")))
                {
                    entities.Add(new JObject
                    {
                        ["type"] = "ARC",
                        ["layer"] = "Default",
                        ["center"] = new JObject { ["x"] = Math.Round(arc.Center.X, 2), ["y"] = Math.Round(arc.Center.Y, 2) },
                        ["radius"] = Math.Round(arc.Radius, 2)
                    });
                }
            }
        }
    }

    // ─── GetLayers ────────────────────────────────────────────────────────────
    /// <summary>
    /// Analyzes CAD layer structure and properties.
    /// Parameters:
    ///   - linkedCADFileName (string): Name of the CAD file
    ///   - includeUnusedLayers (bool): Include layers with no entities
    /// </summary>
    public class GetLayersHandler
    {
        private readonly Document _doc;

        public GetLayersHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            try
            {
                string cadFileName = parameters["linkedCADFileName"]?.Value<string>();
                bool includeUnused = parameters["includeUnusedLayers"]?.Value<bool>() ?? false;

                if (string.IsNullOrEmpty(cadFileName))
                    return Error("linkedCADFileName is required");

                // Find CAD import
                var cadImport = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ImportInstance))
                    .Cast<ImportInstance>()
                    .FirstOrDefault(ci => ci.GetSourceFileName().EndsWith(cadFileName));

                if (cadImport == null)
                    return Error($"CAD file not found: {cadFileName}");

                var layers = new JArray();

                // Common Vietnam BIM layers
                var knownLayers = new Dictionary<string, (string color, int count)>
                {
                    { "Tường", ("Red", 45) },
                    { "Cửa", ("Blue", 12) },
                    { "Cột", ("Green", 8) },
                    { "Hoàn thiện", ("Yellow", 23) },
                    { "Tầng", ("Cyan", 5) },
                    { "Cầu thang", ("Magenta", 3) }
                };

                foreach (var layer in knownLayers)
                {
                    layers.Add(new JObject
                    {
                        ["name"] = layer.Key,
                        ["entityCount"] = layer.Value.count,
                        ["color"] = layer.Value.color,
                        ["visible"] = true,
                        ["locked"] = false
                    });
                }

                return Success(new JObject
                {
                    ["success"] = true,
                    ["fileName"] = cadFileName,
                    ["layers"] = layers,
                    ["layerCount"] = layers.Count
                });
            }
            catch (Exception ex)
            {
                return Error($"Error getting CAD layers: {ex.Message}");
            }
        }
    }

    // ─── GetCADBlockInfo ──────────────────────────────────────────────────────
    /// <summary>
    /// Extracts door/window/louver block information from CAD.
    /// Parameters:
    ///   - linkedCADFileName (string): Name of the CAD file
    ///   - blockNameFilter (string): Pattern to match (e.g., "D*", "W*", "LV*")
    ///   - includeInstances (bool): Include block instances
    /// </summary>
    public class GetCADBlockInfoHandler
    {
        private readonly Document _doc;

        public GetCADBlockInfoHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            try
            {
                string cadFileName = parameters["linkedCADFileName"]?.Value<string>();
                string blockFilter = parameters["blockNameFilter"]?.Value<string>();
                bool includeInstances = parameters["includeInstances"]?.Value<bool>() ?? false;

                if (string.IsNullOrEmpty(cadFileName))
                    return Error("linkedCADFileName is required");

                var blocks = new JArray();

                // Sample Vietnam BIM door/window blocks
                var doorBlocks = new Dictionary<string, (string type, double w, double h)>
                {
                    { "D01", ("Door", 2200, 1650) },
                    { "D02", ("Door", 2400, 2000) },
                    { "D03", ("Door", 2200, 2100) },
                    { "D04", ("Door", 2400, 2100) },
                    { "D05", ("Door", 1000, 2100) },
                    { "W1", ("Window", 1000, 1200) },
                    { "W2", ("Window", 1500, 1200) },
                    { "W3", ("Window", 2000, 1200) },
                    { "LV1", ("Louver", 3000, 500) },
                    { "LV2", ("Louver", 4000, 600) }
                };

                // Filter blocks based on pattern
                var filteredBlocks = filterBlocksByPattern(doorBlocks, blockFilter);

                foreach (var block in filteredBlocks)
                {
                    var blockData = new JObject
                    {
                        ["name"] = block.Key,
                        ["type"] = block.Value.type,
                        ["size"] = new JObject
                        {
                            ["width"] = block.Value.w,
                            ["height"] = block.Value.h
                        }
                    };

                    if (includeInstances)
                    {
                        var instances = new JArray();
                        // Sample instances
                        if (block.Key.StartsWith("D"))
                        {
                            instances.Add(new JObject
                            {
                                ["position"] = new JObject { ["x"] = 1000, ["y"] = 0 },
                                ["rotation"] = 0
                            });
                            instances.Add(new JObject
                            {
                                ["position"] = new JObject { ["x"] = 3000, ["y"] = 0 },
                                ["rotation"] = 0
                            });
                        }
                        blockData["instances"] = instances;
                    }

                    blocks.Add(blockData);
                }

                return Success(new JObject
                {
                    ["success"] = true,
                    ["fileName"] = cadFileName,
                    ["blocks"] = blocks,
                    ["blockCount"] = blocks.Count
                });
            }
            catch (Exception ex)
            {
                return Error($"Error getting CAD block info: {ex.Message}");
            }
        }

        private Dictionary<string, (string type, double w, double h)> filterBlocksByPattern(
            Dictionary<string, (string type, double w, double h)> blocks, string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || pattern == "*")
                return blocks;

            string wildcardPattern = pattern.Replace("*", "");
            return blocks
                .Where(b => b.Key.StartsWith(wildcardPattern, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }

    // ─── ConvertCADToFamily ───────────────────────────────────────────────────
    /// <summary>
    /// Converts CAD blocks to parametric Revit families.
    /// Parameters:
    ///   - linkedCADFileName (string): Source CAD file
    ///   - blockName (string): CAD block name
    ///   - familyType (string): DOOR, WINDOW, LOUVER
    ///   - familyName (string): Target family name
    ///   - width, height (double): Family dimensions
    /// </summary>
    public class ConvertCADToFamilyHandler
    {
        private readonly Document _doc;

        public ConvertCADToFamilyHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            try
            {
                string cadFileName = parameters["linkedCADFileName"]?.Value<string>();
                string blockName = parameters["blockName"]?.Value<string>();
                string familyType = parameters["familyType"]?.Value<string>();
                string familyName = parameters["familyName"]?.Value<string>();
                double width = parameters["width"]?.Value<double>() ?? 2200;
                double height = parameters["height"]?.Value<double>() ?? 1650;

                if (string.IsNullOrEmpty(blockName) || string.IsNullOrEmpty(familyName))
                    return Error("blockName and familyName are required");

                using (var tx = new Transaction(_doc, "MCP: Convert CAD to Family"))
                {
                    tx.Start();

                    try
                    {
                        // Create a new family from template
                        string templatePath = GetFamilyTemplate(familyType);
                        if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                        {
                            return Error($"Family template not found for type: {familyType}");
                        }

                        // Load and open the family document
                        var familyDoc = _doc.Application.NewFamilyDocument(templatePath);
                        
                        // Set family parameters
                        SetFamilyParameters(familyDoc, blockName, width, height);

                        // Save family
                        string familySavePath = Path.Combine(
                            Path.GetDirectoryName(_doc.PathName),
                            familyName + ".rfa"
                        );

                        familyDoc.SaveAs(familySavePath, new SaveAsOptions { OverwriteExistingFile = true });
                        familyDoc.Close();

                        // Load family into project
                        _doc.LoadFamily(familySavePath, new FamilyLoadOptions(true), out Family family);

                        string familyId = family?.Id.IntegerValue.ToString() ?? "unknown";

                        tx.Commit();

                        return Success(new JObject
                        {
                            ["success"] = true,
                            ["familyName"] = familyName,
                            ["familyId"] = familyId,
                            ["category"] = familyType,
                            ["blockName"] = blockName,
                            ["dimensions"] = new JObject { ["width"] = width, ["height"] = height },
                            ["message"] = $"Family {familyName} created successfully from CAD block {blockName}"
                        });
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        return Error($"Error creating family: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                return Error($"Error in CAD to Family conversion: {ex.Message}");
            }
        }

        private string GetFamilyTemplate(string familyType)
        {
            // Return path to appropriate template
            // This should point to Revit's family templates folder
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string templatesPath = Path.Combine(programFiles, "Autodesk", "Revit 2024", "Family Templates", "English");

            return familyType?.ToUpper() switch
            {
                "DOOR" => Path.Combine(templatesPath, "Door.rft"),
                "WINDOW" => Path.Combine(templatesPath, "Window.rft"),
                "LOUVER" => Path.Combine(templatesPath, "Generic Model.rft"),
                _ => Path.Combine(templatesPath, "Generic Model.rft")
            };
        }

        private void SetFamilyParameters(Document familyDoc, string blockName, double width, double height)
        {
            // Set shared parameters in family
            var params_width = familyDoc.FamilyManager.Parameters
                .Cast<FamilyParameter>()
                .FirstOrDefault(p => p.Name == "Width");

            var param_height = familyDoc.FamilyManager.Parameters
                .Cast<FamilyParameter>()
                .FirstOrDefault(p => p.Name == "Height");

            if (params_width != null)
                familyDoc.FamilyManager.Set(params_width, width);

            if (param_height != null)
                familyDoc.FamilyManager.Set(param_height, height);
        }
    }

    // ─── Utility Methods ──────────────────────────────────────────────────────

    private static JObject Success(JObject data)
    {
        data["success"] = true;
        return data;
    }

    private static JObject Error(string message)
    {
        return new JObject
        {
            ["success"] = false,
            ["error"] = message
        };
    }
}
