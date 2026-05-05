import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerSetElementParametersTool(server: McpServer) {
  server.tool(
    "set_element_parameters",
    "Set one or more parameter values on Revit elements. Supports batch operations across multiple elements. Supports string, number, and boolean values. Use this for data round-trip workflows: syncing material specs, cost codes, fire ratings, supplier info from external Excel/database back into the Revit model.",
    {
      updates: z
        .array(
          z.object({
            elementId: z.number().describe("Element ID to update"),
            parameters: z
              .array(
                z.object({
                  name: z
                    .string()
                    .describe(
                      "Parameter name (built-in name or shared parameter name)"
                    ),
                  value: z
                    .union([z.string(), z.number(), z.boolean()])
                    .describe("New value to set"),
                })
              )
              .min(1)
              .describe("Parameters to update on this element"),
          })
        )
        .min(1)
        .describe("List of element parameter update operations"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_element_parameters", args);
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
              text: `Set element parameters failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
