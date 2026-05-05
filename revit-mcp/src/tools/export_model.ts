import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerExportModelTool(server: McpServer) {
  server.tool(
    "export_model",
    "Export the Revit model or specific sheets/views to various formats: IFC (for BIM interoperability), DWG/DXF (for CAD handoff), PDF (for documentation), NWC (for Navisworks clash detection), or schedule data to CSV/Excel. Supports batch sheet printing to PDF with standard naming convention.",
    {
      exportType: z
        .enum(["IFC", "DWG", "DXF", "PDF", "NWC", "CSV", "XLSX"])
        .describe("Output file format"),
      outputPath: z
        .string()
        .describe(
          "Full folder path (or file path for single-file exports) where the output will be saved, e.g. 'C:\\\\Projects\\\\MauNhaSo5\\\\Export'"
        ),
      scope: z
        .enum(["entire_model", "current_view", "sheets", "view_ids", "schedule"])
        .default("entire_model")
        .describe("What to export"),
      sheetNumbers: z
        .array(z.string())
        .optional()
        .describe(
          "For PDF batch export: list of sheet numbers, e.g. ['A-000', 'A-101', 'A-201']. If omitted with scope='sheets', exports all sheets."
        ),
      viewIds: z
        .array(z.number())
        .optional()
        .describe("For scope='view_ids': specific view element IDs to export"),
      scheduleId: z
        .number()
        .optional()
        .describe("For CSV/XLSX export: element ID of the schedule view to export"),
      namingConvention: z
        .string()
        .optional()
        .describe(
          "For PDF batch: file naming pattern using placeholders {ProjectNumber}, {SheetNumber}, {SheetName}, {Date}. E.g. '{ProjectNumber}_{SheetNumber}_{SheetName}_{Date}.pdf'"
        ),
      ifcOptions: z
        .object({
          version: z
            .enum(["IFC2x3", "IFC4", "IFC4x3"])
            .default("IFC4")
            .describe("IFC schema version"),
          exportBaseQuantities: z
            .boolean()
            .default(true)
            .describe("Include computed quantities in IFC export"),
          splitByLevel: z
            .boolean()
            .default(false)
            .describe("Split IFC into one file per level"),
        })
        .optional()
        .describe("IFC-specific export options"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("export_model", args);
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
              text: `Export failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
