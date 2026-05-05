import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetProjectInfoTool(server: McpServer) {
  server.tool(
    "get_project_info",
    "Get or set Revit project information including project name, number, address, client name, building type, author, and all project parameters. Also returns model health indicators: total warning count, element counts, file size, and active worksets. Use at the start of any BIM session to understand the project context.",
    {
      action: z
        .enum(["get", "set"])
        .default("get")
        .describe("'get' to read project info, 'set' to update project parameters"),
      updates: z
        .array(
          z.object({
            name: z
              .string()
              .describe(
                "Project info parameter name, e.g. 'Project Name', 'Project Number', 'Client Name', 'Building Type', 'Author', 'Project Address', 'Project Status', 'Organization Name'"
              ),
            value: z.string().describe("New value to set"),
          })
        )
        .optional()
        .describe("Only used when action='set'. Parameters to update."),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_project_info", args);
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
              text: `Get/set project info failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
