using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCP.Handlers
{
    // ─── LoadFamily ───────────────────────────────────────────────────────────

    public class LoadFamilyHandler
    {
        private readonly Document _doc;
        public LoadFamilyHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            var families = parameters["families"] as JArray;
            if (families == null || families.Count == 0)
                return Error("No families provided");

            var results = new JArray();

            using (var tx = new Transaction(_doc, "MCP: Load Families"))
            {
                tx.Start();

                foreach (JObject fSpec in families)
                {
                    string rfaPath = fSpec["rfaPath"]?.Value<string>();
                    bool overwrite = fSpec["overwriteExisting"]?.Value<bool>() ?? true;

                    if (string.IsNullOrEmpty(rfaPath) || !File.Exists(rfaPath))
                    {
                        results.Add(new JObject { ["path"] = rfaPath, ["status"] = "file_not_found" });
                        continue;
                    }

                    try
                    {
                        bool loaded = _doc.LoadFamily(rfaPath, new FamilyLoadOptions(overwrite), out Family family);

                        if (!loaded && family == null)
                        {
                            results.Add(new JObject { ["path"] = rfaPath, ["status"] = "already_exists_not_overwritten" });
                            continue;
                        }

                        var typeParamsArray = fSpec["typeParameters"] as JArray;
                        var typeResults = new JArray();

                        if (typeParamsArray != null && family != null)
                        {
                            foreach (JObject tSpec in typeParamsArray)
                            {
                                string typeName = tSpec["typeName"]?.Value<string>();
                                var sym = family.GetFamilySymbolIds()
                                    .Select(id => _doc.GetElement(id) as FamilySymbol)
                                    .FirstOrDefault(s => s?.Name == typeName);

                                if (sym == null) continue;
                                if (!sym.IsActive) sym.Activate();

                                foreach (JObject p in tSpec["parameters"] as JArray ?? new JArray())
                                {
                                    string pName = p["name"]?.Value<string>();
                                    var param = sym.LookupParameter(pName);
                                    if (param == null || param.IsReadOnly) continue;

                                    var val = p["value"];
                                    switch (param.StorageType)
                                    {
                                        case StorageType.String:  param.Set(val?.Value<string>() ?? ""); break;
                                        case StorageType.Double:  param.Set(val?.Value<double>() ?? 0); break;
                                        case StorageType.Integer: param.Set(val?.Value<int>() ?? 0); break;
                                    }
                                }

                                typeResults.Add(new JObject { ["typeName"] = typeName, ["status"] = "params_set" });
                            }
                        }

                        results.Add(new JObject
                        {
                            ["path"]    = rfaPath,
                            ["status"]  = loaded ? "loaded" : "already_exists",
                            ["familyId"] = family?.Id.IntegerValue ?? -1,
                            ["types"]   = typeResults
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject { ["path"] = rfaPath, ["status"] = "error", ["message"] = ex.Message });
                    }
                }

                tx.Commit();
            }

            return new JObject { ["status"] = "success", ["families"] = results };
        }

        private static JObject Error(string msg) => new JObject { ["status"] = "error", ["message"] = msg };
    }

    // ─── FamilyLoadOptions ────────────────────────────────────────────────────

    public class FamilyLoadOptions : IFamilyLoadOptions
    {
        private readonly bool _overwrite;
        public FamilyLoadOptions(bool overwrite) { _overwrite = overwrite; }
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = _overwrite;
            return _overwrite;
        }
        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse,
            out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = _overwrite;
            return _overwrite;
        }
    }

    // ─── ExportModel ─────────────────────────────────────────────────────────

    public class ExportModelHandler
    {
        private readonly Document _doc;
        public ExportModelHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            string exportType  = parameters["exportType"]?.Value<string>() ?? "PDF";
            string outputPath  = parameters["outputPath"]?.Value<string>() ?? "";
            string scope       = parameters["scope"]?.Value<string>() ?? "entire_model";
            var sheetNumbers   = (parameters["sheetNumbers"] as JArray)?.Select(s => s.Value<string>()).ToList();
            string naming      = parameters["namingConvention"]?.Value<string>()
                              ?? "{SheetNumber}_{SheetName}.pdf";

            if (!Directory.Exists(outputPath))
            {
                try { Directory.CreateDirectory(outputPath); }
                catch (Exception ex) { return Error($"Cannot create output directory: {ex.Message}"); }
            }

            var results = new JArray();

            switch (exportType.ToUpper())
            {
                case "IFC":
                    results.Add(ExportIFC(outputPath, parameters));
                    break;

                case "PDF":
                    results = ExportPDF(outputPath, sheetNumbers, naming);
                    break;

                case "DWG":
                case "DXF":
                    results = ExportDWG(outputPath, scope, exportType.ToUpper());
                    break;

                case "NWC":
                    results.Add(ExportNWC(outputPath));
                    break;

                default:
                    return Error($"Unsupported export type: {exportType}");
            }

            return new JObject
            {
                ["status"]      = "success",
                ["exportType"]  = exportType,
                ["outputPath"]  = outputPath,
                ["results"]     = results
            };
        }

        private JObject ExportIFC(string outputPath, JObject parameters)
        {
            var opts = parameters["ifcOptions"] as JObject;
            string version = opts?["version"]?.Value<string>() ?? "IFC4";
            bool baseQty   = opts?["exportBaseQuantities"]?.Value<bool>() ?? true;

            var ifcOpts = new IFCExportOptions
            {
                FileVersion = version == "IFC4" ? IFCVersion.IFC4 : IFCVersion.IFC2x3,
                ExportBaseQuantities = baseQty
            };

            string fileName = $"{_doc.ProjectInformation.Number ?? "Project"}_IFC.ifc";
            string fullPath = Path.Combine(outputPath, fileName);

            using (var tx = new Transaction(_doc, "MCP: Export IFC"))
            {
                tx.Start();
                _doc.Export(outputPath, fileName, ifcOpts);
                tx.Commit();
            }

            return new JObject
            {
                ["file"]   = fullPath,
                ["status"] = File.Exists(fullPath) ? "exported" : "failed"
            };
        }

        private JArray ExportPDF(string outputPath, List<string> sheetNumbers, string naming)
        {
            var results = new JArray();

            var allSheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .ToList();

            var targetSheets = sheetNumbers != null && sheetNumbers.Count > 0
                ? allSheets.Where(s => sheetNumbers.Contains(s.SheetNumber)).ToList()
                : allSheets;

            var printManager = _doc.PrintManager;
            printManager.SelectNewPrintDriver("Microsoft Print to PDF");
            printManager.PrintRange = PrintRange.Select;
            printManager.PrintToFile = true;

            foreach (var sheet in targetSheets)
            {
                string fileName = ApplyNamingConvention(naming, sheet, _doc.ProjectInformation)
                    .Replace("/", "-").Replace("\\", "-");
                string fullPath = Path.Combine(outputPath, fileName);

                printManager.PrintToFileName = fullPath;

                var viewSet = new ViewSet();
                viewSet.Insert(sheet);
                printManager.ViewSheetSetting.CurrentViewSheetSet.Views = viewSet;

                try
                {
                    printManager.SubmitPrint(sheet);
                    results.Add(new JObject { ["sheet"] = sheet.SheetNumber, ["file"] = fullPath, ["status"] = "printed" });
                }
                catch (Exception ex)
                {
                    results.Add(new JObject { ["sheet"] = sheet.SheetNumber, ["status"] = "error", ["message"] = ex.Message });
                }
            }

            return results;
        }

        private JArray ExportDWG(string outputPath, string scope, string format)
        {
            var results = new JArray();
            var dwgOpts  = new DWGExportOptions { FileVersion = ACADVersion.R2013 };

            var views = scope == "current_view"
                ? new List<View> { _doc.ActiveView }
                : new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<View>().ToList();

            using (var tx = new Transaction(_doc, "MCP: Export DWG"))
            {
                tx.Start();
                foreach (var view in views)
                {
                    string fileName = $"{view.Name.Replace("/", "-")}.{format.ToLower()}";
                    try
                    {
                        _doc.Export(outputPath, fileName, new List<ElementId> { view.Id }, dwgOpts);
                        results.Add(new JObject { ["view"] = view.Name, ["file"] = Path.Combine(outputPath, fileName), ["status"] = "exported" });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject { ["view"] = view.Name, ["status"] = "error", ["message"] = ex.Message });
                    }
                }
                tx.Commit();
            }

            return results;
        }

        private JObject ExportNWC(string outputPath)
        {
            string fileName = $"{_doc.ProjectInformation.Number ?? "Project"}.nwc";
            var opts = new NavisworksExportOptions { ExportScope = NavisworksExportScope.Model };

            using (var tx = new Transaction(_doc, "MCP: Export NWC"))
            {
                tx.Start();
                _doc.Export(outputPath, fileName, opts);
                tx.Commit();
            }

            return new JObject { ["file"] = Path.Combine(outputPath, fileName), ["status"] = "exported" };
        }

        private string ApplyNamingConvention(string pattern, ViewSheet sheet, ProjectInfo info)
        {
            return pattern
                .Replace("{ProjectNumber}", info?.Number ?? "")
                .Replace("{SheetNumber}", sheet.SheetNumber)
                .Replace("{SheetName}", sheet.Name)
                .Replace("{Date}", DateTime.Now.ToString("yyyyMMdd"));
        }

        private static JObject Error(string msg) => new JObject { ["status"] = "error", ["message"] = msg };
    }

    // ─── CreateWallType ───────────────────────────────────────────────────────

    public class CreateWallTypeHandler
    {
        private readonly Document _doc;
        public CreateWallTypeHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            var wallTypes = parameters["wallTypes"] as JArray;
            if (wallTypes == null) return Error("No wallTypes provided");

            var results = new JArray();

            using (var tx = new Transaction(_doc, "MCP: Create Wall Types"))
            {
                tx.Start();

                foreach (JObject spec in wallTypes)
                {
                    try
                    {
                        string typeName    = spec["typeName"]?.Value<string>() ?? "MCP Wall Type";
                        string baseType    = spec["baseTypeName"]?.Value<string>();
                        var layers         = spec["layers"] as JArray;

                        WallType baseWallType = null;
                        if (!string.IsNullOrEmpty(baseType))
                        {
                            baseWallType = new FilteredElementCollector(_doc)
                                .OfClass(typeof(WallType))
                                .Cast<WallType>()
                                .FirstOrDefault(wt => wt.Name == baseType);
                        }
                        baseWallType ??= new FilteredElementCollector(_doc)
                            .OfClass(typeof(WallType))
                            .Cast<WallType>()
                            .FirstOrDefault(wt => wt.Kind == WallKind.Basic);

                        if (baseWallType == null) { results.Add(Error("No base wall type found")); continue; }

                        WallType newType = baseWallType.Duplicate(typeName) as WallType;
                        CompoundStructure cs = newType.GetCompoundStructure();

                        if (cs != null && layers != null)
                        {
                            var newLayers = new List<CompoundStructureLayer>();

                            foreach (JObject layerSpec in layers)
                            {
                                string funcStr  = layerSpec["function"]?.Value<string>() ?? "Core";
                                string matName  = layerSpec["materialName"]?.Value<string>() ?? "";
                                double thickMm  = layerSpec["thicknessMm"]?.Value<double>() ?? 10;

                                var func = funcStr switch
                                {
                                    "Finish1"  => MaterialFunctionAssignment.Finish1,
                                    "Finish2"  => MaterialFunctionAssignment.Finish2,
                                    "Substrate"=> MaterialFunctionAssignment.Substrate,
                                    "Thermal"  => MaterialFunctionAssignment.Insulation,
                                    "Membrane" => MaterialFunctionAssignment.Membrane,
                                    _          => MaterialFunctionAssignment.Structure
                                };

                                // Find or create material
                                var mat = FindOrCreateSimpleMaterial(matName);
                                double thickFt = thickMm / 304.8;

                                newLayers.Add(new CompoundStructureLayer(thickFt, func, mat?.Id ?? ElementId.InvalidElementId));
                            }

                            cs.SetLayers(newLayers);
                            newType.SetCompoundStructure(cs);
                        }

                        results.Add(new JObject
                        {
                            ["typeName"] = typeName,
                            ["typeId"]   = newType.Id.IntegerValue,
                            ["status"]   = "created"
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject { ["status"] = "error", ["message"] = ex.Message });
                    }
                }

                tx.Commit();
            }

            return new JObject { ["status"] = "success", ["wallTypes"] = results };
        }

        private Material FindOrCreateSimpleMaterial(string name)
        {
            var existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null) return existing;

            var id = Material.Create(_doc, name);
            return _doc.GetElement(id) as Material;
        }

        private static JObject Error(string msg) => new JObject { ["status"] = "error", ["message"] = msg };
    }
}
