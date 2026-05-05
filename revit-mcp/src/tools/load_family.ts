import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerLoadFamilyTool(server: McpServer) {
  server.tool(
    "load_family",
    "Load one or more Revit family files (.rfa) into the current project. Optionally set type parameters after loading. Use this to load door families (D01-D11), window families (W1-W10), structural families, MEP equipment families, or any custom parametric families into the project before placing elements.",
    {
      families: z
        .array(
          z.object({
            rfaPath: z
              .string()
              .describe(
                "Full file path to the .rfa family file, e.g. 'C:\\\\Families\\\\Doors\\\\D01-Single.rfa'"
              ),
            overwriteExisting: z
              .boolean()
              .optional()
              .default(true)
              .describe("If family already exists in project, replace it"),
            typeParameters: z
              .array(
                z.object({
                  typeName: z
                    .string()
                    .describe("Name of the family type to set parameters on"),
                  parameters: z
                    .array(
                      z.object({
                        name: z.string().describe("Parameter name"),
                        value: z
                          .union([z.string(), z.number(), z.boolean()])
                          .describe("Parameter value"),
                      })
                    )
                    .describe("Parameters to set on this type after loading"),
                })
              )
              .optional()
              .describe(
                "After loading, set these type parameters (e.g. Fire Rating, U-Value, Manufacturer)"
              ),
          })
        )
        .describe("List of family files to load"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("load_family", args);
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
              text: `Load family failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
