import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerModifyElementTool(server: McpServer) {
  server.tool(
    "modify_element",
    "Modify one or more existing Revit elements. Supports moving, rotating, resizing, and updating any parameter value. All spatial units in millimeters (mm), angles in degrees.",
    {
      modifications: z
        .array(
          z.object({
            elementId: z.number().describe("The ID of the element to modify"),
            move: z
              .object({
                x: z.number().describe("Delta X in mm"),
                y: z.number().describe("Delta Y in mm"),
                z: z.number().describe("Delta Z in mm"),
              })
              .optional()
              .describe("Move element by this vector offset"),
            rotateDegrees: z
              .number()
              .optional()
              .describe("Rotate element by this angle in degrees around its vertical axis"),
            parameters: z
              .array(
                z.object({
                  name: z.string().describe("Parameter name (built-in or shared)"),
                  value: z
                    .union([z.string(), z.number(), z.boolean()])
                    .describe("New parameter value"),
                })
              )
              .optional()
              .describe("Parameters to set on this element"),
            typeId: z
              .number()
              .optional()
              .describe("Change the element to this family type ID"),
            levelId: z
              .number()
              .optional()
              .describe("Move element to this level ID"),
            offset: z
              .number()
              .optional()
              .describe("Set the offset from level in mm"),
          })
        )
        .describe("List of element modifications to apply"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("modify_element", args);
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
              text: `Modify element failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
