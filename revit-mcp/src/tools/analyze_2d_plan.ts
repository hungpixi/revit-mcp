import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import * as fs from "fs";
import * as path from "path";

export function registerAnalyze2dPlanTool(server: McpServer) {
  server.tool(
    "analyze_2d_plan",
    "Analyze a 2D architectural floor plan image (PNG/JPG/PDF screenshot) and extract detected building elements with their coordinates — similar to WiseBIM AI detection. Returns walls (start/end points, thickness), doors (position, size, swing direction), windows (position, size, sill height), columns, and room boundaries. Coordinates are scaled using the provided reference scale. After analysis, you can pass the result to create_line_based_element, create_point_based_element, and create_room to build the 3D Revit model.",
    {
      imagePath: z
        .string()
        .describe(
          "Full local file path to the floor plan image (PNG, JPG, or BMP), e.g. 'C:\\\\Projects\\\\MauNhaSo5\\\\A-101.png'"
        ),
      scaleMmPerPixel: z
        .number()
        .optional()
        .describe(
          "Real-world scale: how many mm equals 1 pixel in the image. Use a known dimension to calibrate, e.g. if a 5000mm wall measures 200px, scaleMmPerPixel=25. If omitted, returns normalized 0-1 coordinates."
        ),
      referenceLength: z
        .object({
          pixelLength: z
            .number()
            .describe("Length of the known dimension in pixels on the image"),
          realLengthMm: z
            .number()
            .describe("Real length of that dimension in mm"),
        })
        .optional()
        .describe(
          "Alternative to scaleMmPerPixel: provide a known dimension for auto-calibration"
        ),
      detectElements: z
        .array(
          z.enum([
            "walls",
            "doors",
            "windows",
            "columns",
            "rooms",
            "stairs",
            "grids",
          ])
        )
        .default(["walls", "doors", "windows", "rooms"])
        .describe("Element types to detect from the plan"),
      wallThicknessDefaultMm: z
        .number()
        .optional()
        .default(200)
        .describe(
          "Default wall thickness in mm when it cannot be determined from the image"
        ),
      originX: z
        .number()
        .optional()
        .default(0)
        .describe("X coordinate (mm) of the plan's origin point in Revit model space"),
      originY: z
        .number()
        .optional()
        .default(0)
        .describe("Y coordinate (mm) of the plan's origin point in Revit model space"),
      levelElevationMm: z
        .number()
        .optional()
        .default(0)
        .describe("Elevation of the floor level these elements belong to (mm)"),
    },
    async (args, extra) => {
      try {
        if (!fs.existsSync(args.imagePath)) {
          return {
            content: [
              {
                type: "text",
                text: `Image file not found: ${args.imagePath}`,
              },
            ],
          };
        }

        const ext = path.extname(args.imagePath).toLowerCase();
        const supportedFormats = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"];
        if (!supportedFormats.includes(ext)) {
          return {
            content: [
              {
                type: "text",
                text: `Unsupported image format: ${ext}. Supported: ${supportedFormats.join(", ")}`,
              },
            ],
          };
        }

        const imageBuffer = fs.readFileSync(args.imagePath);
        const base64Image = imageBuffer.toString("base64");
        const mimeType = ext === ".jpg" || ext === ".jpeg" ? "image/jpeg" : "image/png";

        let scaleMmPerPixel = args.scaleMmPerPixel;
        if (!scaleMmPerPixel && args.referenceLength) {
          scaleMmPerPixel =
            args.referenceLength.realLengthMm / args.referenceLength.pixelLength;
        }

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(
                {
                  status: "ready_for_vision_analysis",
                  message:
                    "Image loaded successfully. Pass this result to Claude's vision capability by including the image in your next message along with the detected parameters.",
                  imageBase64: base64Image,
                  mimeType,
                  scaleMmPerPixel: scaleMmPerPixel ?? null,
                  originX: args.originX,
                  originY: args.originY,
                  levelElevationMm: args.levelElevationMm,
                  detectElements: args.detectElements,
                  wallThicknessDefaultMm: args.wallThicknessDefaultMm,
                  instructions:
                    "Analyze this floor plan image. For each detected element return: {type, startX, startY, endX, endY, thicknessMm} for walls; {type, x, y, widthMm, heightMm, swingAngle} for doors/windows; {type, x, y, widthMm, depthMm} for columns. Apply scaleMmPerPixel to convert pixel coords to mm, then offset by originX/originY.",
                },
                null,
                2
              ),
            },
            {
              type: "image",
              data: base64Image,
              mimeType,
            } as any,
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `Analyze 2D plan failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
