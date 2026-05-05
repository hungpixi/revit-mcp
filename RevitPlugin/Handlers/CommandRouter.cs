using System;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCP.Handlers
{
    /// <summary>
    /// Routes JSON-RPC method names to the correct handler.
    /// Integrate this into the existing SocketService / CommandSet in revit-mcp-plugin.
    ///
    /// Usage inside your CommandSet.Execute():
    ///   var router = new CommandRouter(document);
    ///   return router.Dispatch(methodName, parameters);
    /// </summary>
    public class CommandRouter
    {
        private readonly Document _doc;

        public CommandRouter(Document doc)
        {
            _doc = doc;
        }

        public JObject Dispatch(string method, JObject parameters)
        {
            try
            {
                return method switch
                {
                    // ─── CAD Import Tools (Phase 1.5) ─────────────────────────────
                    "get_file_info"             => new GetFileInfoHandler(_doc).Execute(parameters),
                    "get_cad_entities"          => new GetCADEntitiesHandler(_doc).Execute(parameters),
                    "get_layers"                => new GetLayersHandler(_doc).Execute(parameters),
                    "get_cad_block_info"        => new GetCADBlockInfoHandler(_doc).Execute(parameters),
                    "convert_cad_to_family"     => new ConvertCADToFamilyHandler(_doc).Execute(parameters),
                    "link_cad_files"            => new LinkCadFilesHandler(_doc).Execute(parameters),

                    // ─── Level & Grid Tools (Phase 2A) ───────────────────────────
                    "create_level"              => new CreateLevelHandler(_doc).Execute(parameters),
                    "create_grid"               => new CreateGridHandler(_doc).Execute(parameters),
                    "create_grid_intersections" => new GridIntersectionHandler(_doc).Execute(parameters),

                    // ─── Element Query Tools (Phase 2B) ──────────────────────────
                    "get_current_view_elements" => new GetCurrentViewElementsHandler(_doc).Execute(parameters),
                    "get_selected_elements"     => new GetSelectedElementsHandler(_doc).Execute(parameters),
                    "get_element_properties"    => new GetElementPropertiesHandler(_doc).Execute(parameters),
                    "analyze_model_statistics"  => new AnalyzeModelStatisticsHandler(_doc).Execute(parameters),

                    // ─── Existing Handlers ────────────────────────────────────────
                    "create_brick_component"    => new BrickComponentHandler(_doc).Execute(parameters),
                    "modify_element"            => new ModifyElementHandler(_doc).Execute(parameters),
                    "set_element_parameters"    => new SetElementParametersHandler(_doc).Execute(parameters),
                    "search_modules"            => new SearchModulesHandler(_doc).Execute(parameters),
                    "create_view_sheet"         => new CreateViewSheetHandler(_doc).Execute(parameters),
                    "detect_clashes"            => new DetectClashesHandler(_doc).Execute(parameters),
                    "create_schedule"           => new CreateScheduleHandler(_doc).Execute(parameters),
                    "export_model"              => new ExportModelHandler(_doc).Execute(parameters),
                    "load_family"               => new LoadFamilyHandler(_doc).Execute(parameters),
                    "create_wall_type"          => new CreateWallTypeHandler(_doc).Execute(parameters),
                    "get_project_info"          => new GetProjectInfoHandler(_doc).Execute(parameters),
                    _                           => Error($"Unknown method: {method}")
                };
            }
            catch (Exception ex)
            {
                return Error($"Handler exception in '{method}': {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static JObject Error(string msg) =>
            new JObject { ["status"] = "error", ["message"] = msg };
    }
}
