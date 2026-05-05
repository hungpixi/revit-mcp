import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerConvertCadToFamilyTool(server: McpServer) {
  server.tool(
    "convert_cad_to_family",
    "Convert CAD blocks from linked files into Revit families. Supports creating door families (D01-D11), window families (W1-W10), and custom generic families from CAD geometry.",
    {
      linkedCADFileName: z
        .string()
        .describe("Name of the source CAD file"),
      blockName: z
        .string()
        .describe("CAD block name to convert (e.g., 'D01', 'W1', 'LV6')"),
      familyType: z
        .enum(["DOOR", "WINDOW", "GENERIC", "FURNITURE", "LOUVER"])
        .describe("Target Revit family category"),
      familyName: z
        .string()
        .describe("Name of the new Revit family to create"),
      width: z
        .number()
        .optional()
        .describe("Width parameter in mm (for door/window)"),
      height: z
        .number()
        .optional()
        .describe("Height parameter in mm (for door/window)"),
      depth: z
        .number()
        .optional()
        .describe("Depth parameter in mm"),
      preserveCADLayers: z
        .boolean()
        .default(true)
        .describe("Preserve CAD layer structure in family (default: true)"),
      makeParametric: z
        .boolean()
        .default(true)
        .describe("Create parametric dimensions in family (default: true)"),
    },
    async (args) => {
      const params = {
        linkedCADFileName: args.linkedCADFileName,
        blockName: args.blockName,
        familyType: args.familyType,
        familyName: args.familyName,
        width: args.width,
        height: args.height,
        depth: args.depth,
        preserveCADLayers: args.preserveCADLayers,
        makeParametric: args.makeParametric,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("convert_cad_to_family", params);
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
              text: `Convert CAD to family failed: ${
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
