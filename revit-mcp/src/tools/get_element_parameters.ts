import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetElementParametersTool(server: McpServer) {
  server.tool(
    "get_element_parameters",
    "Read all parameters (or specific named parameters) of one or more Revit elements by their element IDs. Returns parameter names, values, storage type, and whether they are instance or type parameters. Essential for BIM data extraction and model auditing.",
    {
      elementIds: z
        .array(z.number())
        .describe("List of element IDs to read parameters from"),
      parameterNames: z
        .array(z.string())
        .optional()
        .describe(
          "Optional: only return these named parameters. If omitted, all parameters are returned."
        ),
      includeTypeParameters: z
        .boolean()
        .optional()
        .default(true)
        .describe("Whether to include type-level parameters (default: true)"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_element_parameters", args);
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
              text: `Get element parameters failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
