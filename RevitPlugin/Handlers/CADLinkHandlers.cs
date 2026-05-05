using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCP.Handlers
{
    /// <summary>
    /// Links one or more DWG files into the active Revit document.
    ///
    /// Parameters:
    ///   - filePaths (string[]) required: absolute DWG paths
    ///   - placement (string) optional: "origin" (default)
    ///   - skipIfAlreadyLinked (bool) optional: default true
    /// </summary>
    public class LinkCadFilesHandler
    {
        private readonly Document _doc;

        public LinkCadFilesHandler(Document doc) { _doc = doc; }

        public JObject Execute(JObject parameters)
        {
            try
            {
                if (_doc == null)
                    return HandlerResponse.Error("No active document");

                var filePathsToken = parameters["filePaths"] as JArray;
                if (filePathsToken == null || filePathsToken.Count == 0)
                    return HandlerResponse.Error("filePaths (string[]) is required");

                string placement = parameters["placement"]?.Value<string>() ?? "origin";
                bool skipIfAlreadyLinked = parameters["skipIfAlreadyLinked"]?.Value<bool>() ?? true;

                var filePaths = filePathsToken.ToObject<List<string>>() ?? new List<string>();
                var results = new JArray();

                // Pre-index already-linked CAD by source file name for quick skip checks
                var existingCadImports = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ImportInstance))
                    .Cast<ImportInstance>()
                    .ToList();

                Func<string, bool> alreadyLinked = (absPath) =>
                {
                    string name = Path.GetFileName(absPath) ?? "";
                    if (string.IsNullOrWhiteSpace(name)) return false;
                    return existingCadImports.Any(ii =>
                    {
                        string instName = ii?.Name ?? "";
                        if (string.IsNullOrWhiteSpace(instName)) return false;
                        return instName.EndsWith(name, StringComparison.OrdinalIgnoreCase);
                    });
                };

                int linkedCount = 0;

                using (var tx = new Transaction(_doc, "MCP: Link CAD files"))
                {
                    tx.Start();

                    foreach (string absPath in filePaths)
                    {
                        string fileName = Path.GetFileName(absPath) ?? absPath ?? "";

                        if (string.IsNullOrWhiteSpace(absPath) || !File.Exists(absPath))
                        {
                            results.Add(new JObject
                            {
                                ["file"] = fileName,
                                ["status"] = "error",
                                ["message"] = "File not found"
                            });
                            continue;
                        }

                        if (skipIfAlreadyLinked && alreadyLinked(absPath))
                        {
                            results.Add(new JObject
                            {
                                ["file"] = fileName,
                                ["status"] = "skipped",
                                ["reason"] = "already linked"
                            });
                            continue;
                        }

                        try
                        {
                            var opts = new DWGImportOptions
                            {
                                Placement = ImportPlacement.Origin,
                                Unit = ImportUnit.Default,
                                AutoCorrectAlmostVHLines = true,
                                ThisViewOnly = false,
                                VisibleLayersOnly = false
                            };

                            // Placement currently supports only origin (future: center-to-center, shared coords, etc.)
                            if (!string.Equals(placement, "origin", StringComparison.OrdinalIgnoreCase))
                            {
                                // Keep behavior predictable; don't fail the whole call
                                results.Add(new JObject
                                {
                                    ["file"] = fileName,
                                    ["status"] = "warning",
                                    ["message"] = $"Unsupported placement '{placement}', using 'origin'"
                                });
                            }

                            ElementId importId;
                            bool ok = _doc.Link(absPath, opts, _doc.ActiveView, out importId);

                            if (!ok || importId == null || importId == ElementId.InvalidElementId)
                            {
                                results.Add(new JObject
                                {
                                    ["file"] = fileName,
                                    ["status"] = "error",
                                    ["message"] = "Document.Link returned false"
                                });
                                continue;
                            }

                            linkedCount++;
                            var el = _doc.GetElement(importId);

                            results.Add(new JObject
                            {
                                ["file"] = fileName,
                                ["status"] = "linked",
                                ["importId"] = importId.IntegerValue,
                                ["instanceName"] = el?.Name ?? ""
                            });
                        }
                        catch (Exception ex)
                        {
                            results.Add(new JObject
                            {
                                ["file"] = fileName,
                                ["status"] = "error",
                                ["message"] = ex.Message
                            });
                        }
                    }

                    tx.Commit();
                }

                return HandlerResponse.Success(new JObject
                {
                    ["linked"] = linkedCount,
                    ["results"] = results
                });
            }
            catch (Exception ex)
            {
                return HandlerResponse.Error($"link_cad_files failed: {ex.Message}");
            }
        }
    }
}

