import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

const materialSpecSchema = z.object({
  code: z.string().describe("Material code, e.g. 'FT-01', 'GT1', 'P4'"),
  name: z.string().describe("Display name, e.g. 'Gạch Ceramic 600x600'"),
  category: z
    .enum(["floor", "wall", "stair", "other"])
    .describe("Element category this material applies to"),
  widthMm: z
    .number()
    .describe("Tile/brick width in mm. 0 = solid (paint, plaster)"),
  heightMm: z
    .number()
    .describe("Tile/brick height in mm. 0 = solid"),
  thicknessMm: z
    .number()
    .describe("Tile/brick thickness in mm (used for floor type compound layer)"),
  jointWidthMm: z
    .number()
    .default(3)
    .describe("Grout joint width in mm"),
  patternType: z
    .enum(["stack", "running_bond", "herringbone", "basket_weave", "diagonal"])
    .default("stack")
    .describe(
      "Tile layout pattern: stack=grid, running_bond=brick offset, herringbone=45° interlocking"
    ),
  colorHex: z
    .string()
    .default("#CCCCCC")
    .describe("Approximate tile color as hex, e.g. '#E8E0D0'"),
  finish: z
    .string()
    .default("")
    .describe("Surface finish description, e.g. 'Bóng', 'Mờ', 'Chống trượt'"),
  locations: z
    .array(z.string())
    .default([])
    .describe("Areas where this material is applied, e.g. ['Phòng khách', 'Sảnh']"),
  createFloorType: z
    .boolean()
    .optional()
    .default(true)
    .describe(
      "Create a Revit FloorType (compound floor) using this material as the finish layer"
    ),
  createWallType: z
    .boolean()
    .optional()
    .default(false)
    .describe(
      "Create a Revit WallType with this material as finish coat (for paint/plaster/cladding)"
    ),
  saveFamilyPath: z
    .string()
    .optional()
    .describe(
      "Optional: path to save the material as an external .rfa generic model family file. E.g. 'C:\\\\Families\\\\Materials\\\\FT-01.rfa'"
    ),
});

export function registerCreateBrickComponentTool(server: McpServer) {
  server.tool(
    "create_brick_component",
    "Create complete Revit material components from brick/tile specifications: (1) a FillPattern element with the correct tile grid, (2) a Material with the fill pattern and color applied, (3) optionally a FloorType or WallType compound structure using the material as the finish layer. Works for ceramic tiles, wood floors, stone, paint, plaster, and any patterned surface. Feed results from extract_materials_from_document directly into this tool.",
    {
      materials: z
        .array(materialSpecSchema)
        .min(1)
        .describe("List of material specifications to create in Revit"),
      overwriteExisting: z
        .boolean()
        .optional()
        .default(false)
        .describe(
          "If a material/pattern with the same code already exists, overwrite it"
        ),
      baseFloorTypeName: z
        .string()
        .optional()
        .describe(
          "Name of an existing FloorType to duplicate as base for new floor types. Defaults to the first available."
        ),
    },
    async (args) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_brick_component", args);
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
              text: `Create brick component failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
          isError: true,
        };
      }
    }
  );
}
