import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetFileInfoTool(server: McpServer) {
  server.tool(
    "get_file_info",
    "Get comprehensive information about the current Revit project file including file path, title, version, central file location, linked CAD files, model performance metrics, and document settings.",
    {
      includeLinkedFiles: z
        .boolean()
        .default(true)
        .describe("Include information about all linked CAD and Revit files (default: true)"),
      includePerformanceMetrics: z
        .boolean()
        .default(false)
        .describe("Include performance metrics like element count and file size (default: false)"),
    },
    async (args) => {
      const params = {
        includeLinkedFiles: args.includeLinkedFiles,
        includePerformanceMetrics: args.includePerformanceMetrics,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_file_info", params);
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
              text: `Get file info failed: ${
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
