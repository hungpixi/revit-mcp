import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

const layerSchema = z.object({
  function: z
    .enum([
      "Finish1",
      "Finish2",
      "Substrate",
      "Thermal",
      "Core",
      "Membrane",
      "StructuralDeck",
    ])
    .describe("Layer function in Revit wall layer ordering"),
  materialName: z
    .string()
    .describe(
      "Material name as it exists (or will be created) in the project, e.g. 'Brick - Baked', 'Cement Plaster Finish', 'Paint - Interior White'"
    ),
  thicknessMm: z.number().describe("Layer thickness in millimeters"),
  wraps: z
    .boolean()
    .optional()
    .default(false)
    .describe("Whether this layer wraps at wall ends (Revit layer wrapping)"),
});

export function registerCreateWallTypeTool(server: McpServer) {
  server.tool(
    "create_wall_type",
    "Create custom wall types with multi-layer compositions in Revit. Each layer has a function (Finish, Substrate, Core), material, and thickness. Use to create project-specific wall types such as P1 (white paint exterior), P3 (grey paint), P4 (stone cladding), P5 (grooved finish), or any brick/plaster/paint layered wall assembly required by the architectural drawings.",
    {
      wallTypes: z
        .array(
          z.object({
            typeName: z
              .string()
              .describe(
                "Name for this wall type, e.g. 'Tuong Gach 200mm - P1', 'Tuong Gach 80x80x180 - P3'"
              ),
            baseTypeName: z
              .string()
              .optional()
              .describe(
                "Existing wall type to duplicate as base. If omitted, duplicates the default Basic Wall."
              ),
            layers: z
              .array(layerSchema)
              .min(1)
              .describe(
                "Wall layers from exterior to interior (outside to inside). Must include at least one Core layer."
              ),
            function: z
              .enum(["Interior", "Exterior", "Retaining", "Soffit", "Foundation"])
              .optional()
              .default("Exterior")
              .describe("Wall function classification"),
            wrapsAtInserts: z
              .enum(["DoNotWrap", "Exterior", "Interior", "Both"])
              .optional()
              .default("DoNotWrap")
              .describe("Layer wrapping behavior at inserts (doors, windows)"),
          })
        )
        .describe("Wall type definitions to create"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_wall_type", args);
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
              text: `Create wall type failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
