import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerSearchModulesTool(server: McpServer) {
  server.tool(
    "search_modules",
    "Search for Revit elements using rich criteria: by category, family name, type name, parameter values, spatial bounding box, or level. Returns matching element IDs with summary data. This is the primary discovery tool — use it before modify_element, operate_element, get_element_parameters, or delete_element to find the target elements.",
    {
      query: z
        .object({
          categories: z
            .array(z.string())
            .optional()
            .describe(
              "Filter by Revit categories, e.g. ['Walls', 'Structural Columns', 'Doors']"
            ),
          familyName: z
            .string()
            .optional()
            .describe("Partial match on family name (case-insensitive)"),
          typeName: z
            .string()
            .optional()
            .describe("Partial match on type name (case-insensitive)"),
          levelName: z
            .string()
            .optional()
            .describe(
              "Only return elements on this level, e.g. 'Level 1' or 'Tang 1'"
            ),
          parameterFilters: z
            .array(
              z.object({
                name: z.string().describe("Parameter name"),
                operator: z
                  .enum(["equals", "not_equals", "contains", "greater", "less"])
                  .describe("Comparison operator"),
                value: z
                  .union([z.string(), z.number()])
                  .describe("Value to compare"),
              })
            )
            .optional()
            .describe(
              "Filter by parameter values, e.g. [{name:'Mark', operator:'contains', value:'D'}] to find doors D01-D11"
            ),
          boundingBox: z
            .object({
              minX: z.number(),
              minY: z.number(),
              minZ: z.number(),
              maxX: z.number(),
              maxY: z.number(),
              maxZ: z.number(),
            })
            .optional()
            .describe("Only return elements within this 3D bounding box (mm)"),
          textSearch: z
            .string()
            .optional()
            .describe(
              "Free text search across all parameter values (slower but comprehensive)"
            ),
        })
        .describe("Search criteria — at least one filter should be provided"),
      includeDetails: z
        .boolean()
        .optional()
        .default(false)
        .describe(
          "If true, return full parameter summary per result. If false (default), return only IDs, category, family, type, and level."
        ),
      limit: z
        .number()
        .optional()
        .default(200)
        .describe("Maximum number of results to return"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("search_modules", args);
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
              text: `Search modules failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
