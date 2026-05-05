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
                    "create_brick_component"    => new BrickComponentHandler(_doc).Execute(parameters),
                    "modify_element"            => new ModifyElementHandler(_doc).Execute(parameters),
                    "get_element_parameters"    => new GetElementParametersHandler(_doc).Execute(parameters),
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
