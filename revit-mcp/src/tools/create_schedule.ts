import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateScheduleTool(server: McpServer) {
  server.tool(
    "create_schedule",
    "Create a Revit schedule (quantity takeoff / material list / element list) for a specified category with chosen fields. Supports filtering, sorting, grouping, and totals. Use for automated quantity takeoffs, material schedules (floor tiles FT-01, FT-02), door/window schedules (D01-D11, W1-W10), and room/area schedules.",
    {
      scheduleName: z.string().describe("Name for the new schedule view"),
      category: z
        .string()
        .describe(
          "Revit category to schedule, e.g. 'Walls', 'Floors', 'Doors', 'Windows', 'Rooms', 'Structural Framing'"
        ),
      fields: z
        .array(z.string())
        .describe(
          "Parameter names to include as columns, e.g. ['Mark', 'Family and Type', 'Width', 'Height', 'Area', 'Count', 'Level', 'Comments']"
        ),
      filters: z
        .array(
          z.object({
            field: z.string().describe("Parameter name to filter on"),
            operator: z
              .enum([
                "equals",
                "not_equals",
                "greater",
                "less",
                "contains",
                "begins_with",
              ])
              .describe("Filter comparison operator"),
            value: z
              .union([z.string(), z.number()])
              .describe("Value to compare against"),
          })
        )
        .optional()
        .describe("Optional filters to limit schedule rows"),
      sortBy: z
        .array(
          z.object({
            field: z.string().describe("Field name to sort by"),
            ascending: z.boolean().default(true).describe("Sort direction"),
          })
        )
        .optional()
        .describe("Sort order for schedule rows"),
      groupBy: z
        .string()
        .optional()
        .describe("Field name to group rows by (e.g., 'Level', 'Family')"),
      showTotals: z
        .boolean()
        .optional()
        .default(false)
        .describe(
          "Show grand totals row for numeric fields (e.g., total area, count)"
        ),
      isItemized: z
        .boolean()
        .optional()
        .default(true)
        .describe(
          "If false, identical rows are merged (like a material takeoff). If true, each element is a separate row."
        ),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_schedule", args);
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
              text: `Create schedule failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
