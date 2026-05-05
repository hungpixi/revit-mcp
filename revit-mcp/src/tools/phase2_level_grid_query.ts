import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

// NOTE:
// This file originally duplicated several existing tools (create_level, create_grid,
// get_selected_elements, etc.) and used an outdated MCP tool schema shape.
// We keep only the Phase 2 additions that are not provided elsewhere:
// - create_grid_intersections
// - get_element_properties

const CreateGridIntersectionsSchema = z.object({
  gridNames: z
    .array(z.string())
    .min(2)
    .describe("Grid names to find intersections, e.g. ['A','B','1','2']"),
  markerType: z
    .enum(["POINT", "REFERENCE_POINT", "GRID_INTERSECTION"])
    .optional()
    .default("POINT")
    .describe("Type of marker to create at intersections"),
  level: z.string().optional().describe("Optional target level name for markers"),
});

export function registerCreateGridIntersectionsTool(server: McpServer) {
  server.tool(
    "create_grid_intersections",
    "Create reference markers at grid intersections for layout control and coordination.",
    {
      gridNames: CreateGridIntersectionsSchema.shape.gridNames,
      markerType: CreateGridIntersectionsSchema.shape.markerType,
      level: CreateGridIntersectionsSchema.shape.level,
    },
    async (args) => {
      const params = CreateGridIntersectionsSchema.parse(args);
      const response = await withRevitConnection(async (client) => {
        return await client.sendCommand("create_grid_intersections", params);
      });

      return {
        content: [{ type: "text", text: JSON.stringify(response, null, 2) }],
      };
    }
  );
}

const GetElementPropertiesSchema = z.object({
  elementId: z.number().int().describe("ElementId to query"),
  includeSharedParameters: z
    .boolean()
    .optional()
    .default(false)
    .describe("If true, also returns shared parameters (if available)"),
  propertyNames: z
    .array(z.string())
    .optional()
    .describe("Optional whitelist of property/parameter names to return"),
});

export function registerGetElementPropertiesTool(server: McpServer) {
  server.tool(
    "get_element_properties",
    "Get detailed properties/parameters for a specific element by ElementId.",
    {
      elementId: GetElementPropertiesSchema.shape.elementId,
      includeSharedParameters: GetElementPropertiesSchema.shape.includeSharedParameters,
      propertyNames: GetElementPropertiesSchema.shape.propertyNames,
    },
    async (args) => {
      const params = GetElementPropertiesSchema.parse(args);
      const response = await withRevitConnection(async (client) => {
        return await client.sendCommand("get_element_properties", params);
      });

      return {
        content: [{ type: "text", text: JSON.stringify(response, null, 2) }],
      };
    }
  );
}
