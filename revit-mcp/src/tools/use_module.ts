import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerUseModuleTool(server: McpServer) {
  server.tool(
    "use_module",
    "Execute a predefined BIM module (reusable parametric operation) by name with custom input parameters. Modules are pre-built C# routines registered in the Revit plugin that encapsulate complex multi-step operations such as: auto-generating levels from a floor table, batch-creating wall types from a spec list, linking CAD files, running model health checks (purge unused, audit warnings), or setting up shared coordinate systems.",
    {
      moduleName: z
        .string()
        .describe(
          "Name of the registered module to execute. Available modules depend on the Revit plugin configuration. Examples: 'setup_levels_from_table', 'batch_create_wall_types', 'link_cad_file', 'purge_unused', 'audit_warnings', 'setup_shared_coordinates', 'batch_tag_elements', 'sync_parameters_from_excel'"
        ),
      parameters: z
        .record(z.union([z.string(), z.number(), z.boolean(), z.array(z.any())]))
        .optional()
        .describe(
          "Key-value parameters for the module. Schema depends on the specific module. E.g. for 'setup_levels_from_table': {levels: [{name:'Tang Ham', elevation:-1500}, {name:'Tang 1', elevation:0}]}"
        ),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("use_module", args);
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2),
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `Use module failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
