import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

// ─────────────────────────────────────────────────────────────────────────────
// Phase 2A: Level & Grid Tools
// ─────────────────────────────────────────────────────────────────────────────

// Schema: Create Level
const CreateLevelSchema = z.object({
  levelName: z.string().describe("Name for the level (e.g., 'Level 1', 'Roof')"),
  elevation: z.number().describe("Elevation in millimeters relative to project base"),
  description: z.string().optional().describe("Optional description or comments"),
  structuralLevel: z.boolean().optional().describe("Mark as structural level")
});

export async function registerCreateLevelTool(server: McpServer) {
  await server.tool(
    "create_level",
    "Create a new building level in the Revit project",
    {
      levelName: { type: "string" },
      elevation: { type: "number" },
      description: { type: "string" },
      structuralLevel: { type: "boolean" }
    },
    async (args) => {
      const params = CreateLevelSchema.parse(args);
      
      return await withRevitConnection(async (client) => {
        const response = await client.request({
          method: "create_level",
          params: {
            levelName: params.levelName,
            elevation: params.elevation,
            description: params.description,
            structuralLevel: params.structuralLevel ?? false
          }
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2)
            }
          ]
        };
      });
    }
  );
}

// Schema: Create Grid
const CreateGridSchema = z.object({
  gridName: z.string().describe("Grid name (e.g., 'A', 'B', '1', '2')"),
  startPoint: z.object({
    x: z.number().describe("X coordinate in mm"),
    y: z.number().describe("Y coordinate in mm"),
    z: z.number().optional().describe("Z coordinate in mm")
  }),
  endPoint: z.object({
    x: z.number().describe("X coordinate in mm"),
    y: z.number().describe("Y coordinate in mm"),
    z: z.number().optional().describe("Z coordinate in mm")
  }),
  isHorizontal: z.boolean().optional().describe("Grid direction (true=horizontal)")
});

export async function registerCreateGridTool(server: McpServer) {
  await server.tool(
    "create_grid",
    "Create a structural grid line in the Revit project",
    {
      gridName: { type: "string" },
      startPoint: { type: "object" },
      endPoint: { type: "object" },
      isHorizontal: { type: "boolean" }
    },
    async (args) => {
      const params = CreateGridSchema.parse(args);

      return await withRevitConnection(async (client) => {
        const response = await client.request({
          method: "create_grid",
          params: {
            gridName: params.gridName,
            startPoint: params.startPoint,
            endPoint: params.endPoint,
            isHorizontal: params.isHorizontal ?? false
          }
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2)
            }
          ]
        };
      });
    }
  );
}

// Schema: Create Grid Intersections
const CreateGridIntersectionsSchema = z.object({
  gridNames: z.array(z.string()).describe("Grid names to find intersections (e.g., ['A', 'B', '1', '2'])"),
  markerType: z.enum(["POINT", "REFERENCE_POINT", "GRID_INTERSECTION"]).optional(),
  level: z.string().optional().describe("Target level for markers")
});

export async function registerCreateGridIntersectionsTool(server: McpServer) {
  await server.tool(
    "create_grid_intersections",
    "Create reference points at grid intersections",
    {
      gridNames: { type: "array" },
      markerType: { type: "string" },
      level: { type: "string" }
    },
    async (args) => {
      const params = CreateGridIntersectionsSchema.parse(args);

      return await withRevitConnection(async (client) => {
        const response = await client.request({
          method: "create_grid_intersections",
          params: {
            gridNames: params.gridNames,
            markerType: params.markerType ?? "POINT",
            level: params.level
          }
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2)
            }
          ]
        };
      });
    }
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Phase 2B: Element Query Tools
// ─────────────────────────────────────────────────────────────────────────────

// Schema: Get Current View Elements
const GetCurrentViewElementsSchema = z.object({
  elementCategories: z.array(z.string()).optional().describe("Filter by category (Walls, Doors, Windows, etc.)"),
  includeProperties: z.boolean().optional().describe("Include element properties"),
  limit: z.number().optional().describe("Maximum elements to return (default 1000)")
});

export async function registerGetCurrentViewElementsTool(server: McpServer) {
  await server.tool(
    "get_current_view_elements",
    "Get all elements in the current Revit view",
    {
      elementCategories: { type: "array" },
      includeProperties: { type: "boolean" },
      limit: { type: "number" }
    },
    async (args) => {
      const params = GetCurrentViewElementsSchema.parse(args);

      return await withRevitConnection(async (client) => {
        const response = await client.request({
          method: "get_current_view_elements",
          params: {
            elementCategories: params.elementCategories,
            includeProperties: params.includeProperties,
            limit: params.limit
          }
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2)
            }
          ]
        };
      });
    }
  );
}

// Schema: Get Selected Elements
const GetSelectedElementsSchema = z.object({
  includeGeometry: z.boolean().optional().describe("Include geometry bounding box"),
  includeParameters: z.boolean().optional().describe("Include all parameters")
});

export async function registerGetSelectedElementsTool(server: McpServer) {
  await server.tool(
    "get_selected_elements",
    "Get information about currently selected elements",
    {
      includeGeometry: { type: "boolean" },
      includeParameters: { type: "boolean" }
    },
    async (args) => {
      const params = GetSelectedElementsSchema.parse(args);

      return await withRevitConnection(async (client) => {
        const response = await client.request({
          method: "get_selected_elements",
          params: {
            includeGeometry: params.includeGeometry,
            includeParameters: params.includeParameters
          }
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2)
            }
          ]
        };
      });
    }
  );
}

// Schema: Get Element Properties
const GetElementPropertiesSchema = z.object({
  elementId: z.number().describe("Element ID to query"),
  includeSharedParameters: z.boolean().optional().describe("Include shared parameters"),
  propertyNames: z.array(z.string()).optional().describe("Specific properties to retrieve")
});

export async function registerGetElementPropertiesTool(server: McpServer) {
  await server.tool(
    "get_element_properties",
    "Get detailed properties of a specific element",
    {
      elementId: { type: "number" },
      includeSharedParameters: { type: "boolean" },
      propertyNames: { type: "array" }
    },
    async (args) => {
      const params = GetElementPropertiesSchema.parse(args);

      return await withRevitConnection(async (client) => {
        const response = await client.request({
          method: "get_element_properties",
          params: {
            elementId: params.elementId,
            includeSharedParameters: params.includeSharedParameters,
            propertyNames: params.propertyNames
          }
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2)
            }
          ]
        };
      });
    }
  );
}

// Schema: Analyze Model Statistics
const AnalyzeModelStatisticsSchema = z.object({
  includeByCategory: z.boolean().optional().describe("Break down by category"),
  includeByLevel: z.boolean().optional().describe("Break down by level"),
  includeVolumes: z.boolean().optional().describe("Calculate total volumes")
});

export async function registerAnalyzeModelStatisticsTool(server: McpServer) {
  await server.tool(
    "analyze_model_statistics",
    "Analyze comprehensive BIM model statistics and breakdown",
    {
      includeByCategory: { type: "boolean" },
      includeByLevel: { type: "boolean" },
      includeVolumes: { type: "boolean" }
    },
    async (args) => {
      const params = AnalyzeModelStatisticsSchema.parse(args);

      return await withRevitConnection(async (client) => {
        const response = await client.request({
          method: "analyze_model_statistics",
          params: {
            includeByCategory: params.includeByCategory ?? true,
            includeByLevel: params.includeByLevel ?? true,
            includeVolumes: params.includeVolumes ?? false
          }
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2)
            }
          ]
        };
      });
    }
  );
}
