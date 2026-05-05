import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetCadEntitiesTool(server: McpServer) {
  server.tool(
    "get_cad_entities",
    "Retrieve entities from linked CAD files in Revit. Returns information about CAD layers, lines, blocks, circles, and other geometric elements. Useful for analyzing imported CAD geometry before converting to Revit elements.",
    {
      linkedCADFileName: z
        .string()
        .optional()
        .describe("Name of the linked CAD file (e.g., 'A-000.dwg'). If omitted, returns info from all linked CAD files."),
      entityTypes: z
        .array(z.enum(["LINE", "POLYLINE", "CIRCLE", "ARC", "BLOCK", "TEXT", "HATCH", "DIMENSION"]))
        .optional()
        .describe("Filter by entity types (e.g., ['LINE', 'BLOCK']). Omit to get all types."),
      layerFilter: z
        .array(z.string())
        .optional()
        .describe("Filter by CAD layer names (e.g., ['Tường', 'Cửa', 'Cột']). Omit for all layers."),
      includeMetadata: z
        .boolean()
        .default(true)
        .describe("Include extended metadata like layer color, linetype, lineweight (default: true)"),
    },
    async (args) => {
      const params = {
        linkedCADFileName: args.linkedCADFileName,
        entityTypes: args.entityTypes,
        layerFilter: args.layerFilter,
        includeMetadata: args.includeMetadata,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_cad_entities", params);
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
              text: `Get CAD entities failed: ${
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
