import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerDetectClashesTool(server: McpServer) {
  server.tool(
    "detect_clashes",
    "Run interference/clash detection between two sets of Revit elements (or categories). Returns a list of clashing element pairs with their locations. Use for MEP vs Structure, MEP vs MEP, Architecture vs Structure coordination. Supports both hard clashes (physical overlap) and soft clashes (clearance violations).",
    {
      setA: z
        .object({
          categories: z
            .array(z.string())
            .optional()
            .describe(
              "Category names for set A, e.g. ['Structural Framing', 'Structural Columns']. Leave empty to use elementIds."
            ),
          elementIds: z
            .array(z.number())
            .optional()
            .describe("Specific element IDs for set A"),
          linkedModelId: z
            .number()
            .optional()
            .describe("If set A is from a Revit link, its element ID"),
        })
        .describe("First set of elements to check"),
      setB: z
        .object({
          categories: z
            .array(z.string())
            .optional()
            .describe(
              "Category names for set B, e.g. ['Ducts', 'Pipes', 'Conduits', 'Cable Trays']"
            ),
          elementIds: z.array(z.number()).optional().describe("Specific element IDs for set B"),
          linkedModelId: z
            .number()
            .optional()
            .describe("If set B is from a Revit link, its element ID"),
        })
        .describe("Second set of elements to check"),
      clashType: z
        .enum(["hard", "soft"])
        .default("hard")
        .describe(
          "'hard' = physical overlap; 'soft' = elements within clearance distance"
        ),
      clearanceMm: z
        .number()
        .optional()
        .describe(
          "For soft clashes: minimum required clearance in mm (e.g. 300 for pipe maintenance access)"
        ),
      limitResults: z
        .number()
        .optional()
        .default(100)
        .describe("Maximum number of clash results to return"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("detect_clashes", args);
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
              text: `Clash detection failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
