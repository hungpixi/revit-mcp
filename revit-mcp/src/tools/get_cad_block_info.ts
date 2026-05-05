import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetCadBlockInfoTool(server: McpServer) {
  server.tool(
    "get_cad_block_info",
    "Get detailed information about CAD blocks in linked files. Returns block names, insertion points, scales, rotations, and properties. Essential for converting CAD blocks (doors D01-D11, windows W1-W10) to Revit families.",
    {
      linkedCADFileName: z
        .string()
        .optional()
        .describe("Specific CAD file to query. If omitted, searches all linked CAD files."),
      blockNameFilter: z
        .string()
        .optional()
        .describe("Filter by block name pattern (e.g., 'D*' for all door blocks, 'W*' for windows)"),
      includeInstances: z
        .boolean()
        .default(true)
        .describe("Include information about individual block instances (default: true)"),
      includeAttributes: z
        .boolean()
        .default(true)
        .describe("Include CAD block attributes and properties (default: true)"),
    },
    async (args) => {
      const params = {
        linkedCADFileName: args.linkedCADFileName,
        blockNameFilter: args.blockNameFilter,
        includeInstances: args.includeInstances,
        includeAttributes: args.includeAttributes,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_cad_block_info", params);
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
              text: `Get CAD block info failed: ${
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
