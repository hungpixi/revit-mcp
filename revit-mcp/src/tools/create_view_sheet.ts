import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateViewSheetTool(server: McpServer) {
  server.tool(
    "create_view_sheet",
    "Create one or more drawing sheets in Revit and optionally place views on them. Supports batch sheet creation from a drawing index (e.g., A-000 to A-801). Title block family type must exist in the project. Use for automated documentation production and batch PDF export preparation.",
    {
      sheets: z
        .array(
          z.object({
            sheetNumber: z
              .string()
              .describe(
                "Sheet number code, e.g. 'A-101', 'S-201', 'M-301'"
              ),
            sheetName: z
              .string()
              .describe("Sheet title, e.g. 'FLOOR PLAN - LEVEL 1'"),
            titleBlockTypeId: z
              .number()
              .optional()
              .describe(
                "Element ID of the title block family type to use. If omitted, uses the first available."
              ),
            views: z
              .array(
                z.object({
                  viewId: z.number().describe("Element ID of the view to place"),
                  centerX: z
                    .number()
                    .optional()
                    .describe(
                      "X position of viewport center on sheet in mm (from sheet origin). If omitted, auto-centered."
                    ),
                  centerY: z
                    .number()
                    .optional()
                    .describe("Y position of viewport center on sheet in mm"),
                  scale: z
                    .number()
                    .optional()
                    .describe(
                      "Override view scale denominator, e.g. 100 for 1:100"
                    ),
                })
              )
              .optional()
              .describe("Views to place on this sheet"),
          })
        )
        .describe("List of sheets to create"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_view_sheet", args);
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
              text: `Create view sheet failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
