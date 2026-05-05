import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { extractFromDocument } from "../utils/PDFExtractor.js";
import path from "path";
import fs from "fs";

export function registerExtractMaterialsFromDocumentTool(server: McpServer) {
  server.tool(
    "extract_materials_from_document",
    "Read a PDF or DWG/DXF file and automatically extract material/brick/tile specifications (code, dimensions, pattern type, color), door schedules (D01-D11), and window schedules (W1-W10). Supports the project's PDF binder and individual CAD drawing files. Use this as the first step before create_brick_component to populate the material database.",
    {
      filePath: z
        .string()
        .describe(
          "Full path to the PDF or DWG file. E.g. 'D:\\\\CODE\\\\revit-mcp\\\\131203-MAU NHA SO 5_BINDER.pdf' or 'D:\\\\CODE\\\\revit-mcp\\\\CAD\\\\A-501-MẶTBẰNGHOÀNTHIỆNSÀNTẦNGHẦM&TẦNG1.dwg'"
        ),
      includeKnownProjectMaterials: z
        .boolean()
        .optional()
        .default(true)
        .describe(
          "Always include the project's known material set (FT-01 to FT-04, GT1, P1-P5) even if not detected in the document text"
        ),
      targetFiles: z
        .array(z.string())
        .optional()
        .describe(
          "Convenience: scan multiple files at once (PDF + multiple DWG). If provided, filePath is used as base directory."
        ),
    },
    async (args) => {
      try {
        const allResults = [];

        if (args.targetFiles && args.targetFiles.length > 0) {
          // Multi-file mode
          const basePath = args.filePath;
          for (const fileName of args.targetFiles) {
            const fullPath = path.isAbsolute(fileName)
              ? fileName
              : path.join(basePath, fileName);

            if (!fs.existsSync(fullPath)) {
              allResults.push({ file: fullPath, error: "File not found" });
              continue;
            }

            try {
              const result = await extractFromDocument(fullPath);
              allResults.push({
                file: path.basename(fullPath),
                ...result,
                rawText: undefined, // omit raw text in multi-file mode
              });
            } catch (e) {
              allResults.push({
                file: fileName,
                error: e instanceof Error ? e.message : String(e),
              });
            }
          }

          return {
            content: [
              {
                type: "text",
                text: JSON.stringify(
                  {
                    status: "success",
                    filesProcessed: allResults.length,
                    results: allResults,
                  },
                  null,
                  2
                ),
              },
            ],
          };
        }

        // Single file mode
        const result = await extractFromDocument(args.filePath);

        const summary = {
          status: "success",
          sourceFile: path.basename(result.sourceFile),
          pageCount: result.pageCount,
          warnings: result.warnings,
          summary: {
            materialsFound: result.materials.length,
            doorsFound: result.doors.length,
            windowsFound: result.windows.length,
          },
          materials: result.materials,
          doors: result.doors,
          windows: result.windows,
          nextStep:
            "Use create_brick_component with these material specs to create Revit fill patterns, materials, and floor/wall types.",
        };

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(summary, null, 2),
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `Extract materials failed: ${
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
