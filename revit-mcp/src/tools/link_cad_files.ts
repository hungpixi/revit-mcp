import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerLinkCadFilesTool(server: McpServer) {
  server.tool(
    "link_cad_files",
    "Link one or more DWG files into the active Revit document (Origin placement). Returns created ImportInstance ids and names.",
    {
      filePaths: z
        .array(z.string())
        .min(1)
        .describe("Absolute paths to DWG files to link into Revit."),
      placement: z
        .enum(["origin"])
        .optional()
        .default("origin")
        .describe("Placement mode (currently only 'origin')."),
      skipIfAlreadyLinked: z
        .boolean()
        .optional()
        .default(true)
        .describe("If true, skip files already linked (best-effort name match)."),
    },
    async (args) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("link_cad_files", args);
        });

        return {
          content: [{ type: "text", text: JSON.stringify(response, null, 2) }],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `link_cad_files failed: ${
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

