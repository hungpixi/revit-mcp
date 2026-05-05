import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetLayersTool(server: McpServer) {
  server.tool(
    "get_layers",
    "Get all layers from linked CAD files in the current Revit project. Returns layer names, colors, linetypes, and lineweights. Useful for understanding CAD structure before automating CAD-to-Revit conversion.",
    {
      linkedCADFileName: z
        .string()
        .optional()
        .describe("Specific CAD file name to query. If omitted, returns layers from all linked CAD files."),
      includeUnusedLayers: z
        .boolean()
        .default(false)
        .describe("Include layers that contain no entities (default: false)"),
      sortBy: z
        .enum(["name", "entityCount", "color"])
        .default("name")
        .describe("Sort results by name, entityCount, or color (default: name)"),
    },
    async (args) => {
      const params = {
        linkedCADFileName: args.linkedCADFileName,
        includeUnusedLayers: args.includeUnusedLayers,
        sortBy: args.sortBy,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_layers", params);
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
              text: `Get layers failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
          isError: true,
        };
      }
    }
  );
}
