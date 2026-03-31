import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";
import axios from "axios";
import { appendLog } from "./logger.js";
import fs from "fs";
import path from "path";

const REVIT_BRIDGE_URL = "http://localhost:5050/api/ai-command/";

// 1. Setup một instance Model_Context_Protocol Server
const server = new Server(
  {
    name: "revit-bim-automation",
    version: "1.0.0",
  },
  {
    capabilities: {
      tools: {},
    },
  }
);

// 2. Định nghĩa các hành vi Tool cho Antigravity (Antigravity coi đây là bộ API có sẵn)
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: [
      {
        name: "open_new_project",
        description: "Mở một bảng hộp thoại tạo Project mới trong Revit (Chỉ dùng khi Revit đang ở Home Screen hoặc cần văn bản mới)",
        inputSchema: { type: "object", properties: {} },
      },
      {
        name: "create_project_from_template",
        description: "Tự động tạo dự án mới từ đường dẫn Template tĩnh",
        inputSchema: {
          type: "object",
          properties: {
            templatePath: { type: "string", description: "Đường dẫn tuyệt đối của file .rte" }
          },
          required: ["templatePath"],
        },
      },
      {
        name: "save_project",
        description: "Lưu dự án hiện hành xuống ổ đĩa",
        inputSchema: {
          type: "object",
          properties: {
            savePath: { type: "string", description: "Đường dẫn thư mục lưu file (bao gồm tên .rvt)" }
          },
          required: ["savePath"],
        },
      },
      {
        name: "close_project",
        description: "Đóng file dự án hiện tại (không lưu)",
        inputSchema: { type: "object", properties: {} },
      },
      {
        name: "get_levels",
        description: "Lấy danh sách ID của các tầng (Levels) trong file Revit 2020 hiện tại.",
        inputSchema: { type: "object", properties: {} },
      },
      {
        name: "create_wall",
        description: "Vẽ một bức tường mới ở Revit theo điểm A và điểm B. Đơn vị luôn là feet.",
        inputSchema: {
          type: "object",
          properties: {
            startPt: { type: "array", items: { type: "number" }, description: "[X, Y, Z] tọa độ bắt đầu" },
            endPt: { type: "array", items: { type: "number" }, description: "[X, Y, Z] tọa độ kết thúc" },
          },
          required: ["startPt", "endPt"],
        },
      },
      {
        name: "create_duct_routing",
        description: "Vẽ một tuyến ống gió MEP kết nối các điểm",
        inputSchema: {
          type: "object",
          properties: {
            points: { type: "array", items: { type: "array", items: { type: "number" } }, description: "Mảng chứa các Vector3 điểm đi của ống gió. Ví dụ: [[0,0,0], [10,0,0], [10,10,0]]" }
          },
          required: ["points"],
        },
      },
      {
        name: "place_family_instance",
        description: "Đặt một Model/Family (Cửa hoặc Cửa số) tại một điểm trên First Level",
        inputSchema: {
          type: "object",
          properties: {
            point: { type: "array", items: { type: "number" }, description: "[X, Y, Z] Tọa độ tại điểm kích chuột" }
          },
          required: ["point"],
        },
      },
      {
        name: "extract_5d_quantities",
        description: "Bóc tách khối lượng (QTO - 5D) hạn chế Token. Tối đa trả về N phần tử.",
        inputSchema: {
          type: "object",
          properties: {
            categoryName: { type: "string", description: "Tên Category để lọc (VD: 'Walls', 'Doors', để rỗng là lấy tất cả)" },
            limit: { type: "number", description: "Giới hạn số phần tử trả về để tránh tràn AI Token (Mặc định 50)" }
          },
        },
      },
      {
        name: "update_4d_schedule",
        description: "Cập nhật thông tin tiến độ / bảo trì (4D/5D) vào Parameter của cấu kiện.",
        inputSchema: {
          type: "object",
          properties: {
            elementId: { type: "number", description: "ID của cấu kiện cần cập nhật" },
            parameterName: { type: "string", description: "Tên Parameter (VD: 'Comments', 'Mark', 'Install Date')" },
            value: { type: "string", description: "Giá trị Cập nhật" }
          },
          required: ["elementId", "parameterName", "value"],
        },
      },
      {
        name: "execute_revit_command",
        description: "Chạy một lệnh mặc định (phím tắt) của Revit thông qua Command ID. Tương đương việc bấm trên Ribbon UI.",
        inputSchema: {
          type: "object",
          properties: {
            commandId: { type: "string", description: "Chuỗi ID lệnh của Revit (VD: 'ID_OBJECTS_WALL', 'ID_VIEW_3D', 'ID_FILE_SAVE', 'ID_FILE_SAVE_AS')" }
          },
          required: ["commandId"],
        },
      },
      {
        name: "execute_pipeline",
        description: "Thực thi một chuỗi các tác vụ (Macro Pipeline) liên hoàn. AI có thể suy luận các bước và truyền vào đây để nó tự động hóa quy trình mà không cần gọi nhiều tool.",
        inputSchema: {
          type: "object",
          properties: {
            macroName: { type: "string", description: "Tên cùa macro để ghi log (VD: Khởi_tạo_dự_án)" },
            steps: { 
              type: "array", 
              description: "Danh sách các tác vụ con",
              items: {
                type: "object",
                properties: {
                  stage: { type: "number", description: "Thứ tự của bước" },
                  type: { type: "string", description: "'api' (truyền thông số hình học ngầm) hoặc 'shortcut' (kích hoạt công cụ vẽ vật lý qua Command ID)" },
                  action: { type: "string", description: "Tên Action API (VD: 'create_wall'). Dùng nếu type = 'api'" },
                  shortcutCode: { type: "string", description: "Code phím tắt (VD: 'WA', 'AL'). Dùng nếu type = 'shortcut'" },
                  payload: { type: "object", description: "Thông số hình học hoặc thuộc tính cho lệnh api" }
                },
                required: ["stage", "type"]
              }
            }
          },
          required: ["macroName", "steps"]
        }
      },
      {
        name: "place_lighting_fixture",
        description: "Đặt đèn chiếu sáng vào model. Hỗ trợ 2 mode: (1) grid pattern - đặt hàng loạt theo lưới, (2) points - đặt từng cái theo tọa độ. Đơn vị: feet.",
        inputSchema: {
          type: "object",
          properties: {
            grid: {
              type: "object",
              description: "Mode lưới: đặt đèn đều theo hàng/cột",
              properties: {
                originX: { type: "number", description: "X gốc lưới (feet)" },
                originY: { type: "number", description: "Y gốc lưới (feet)" },
                spacingX: { type: "number", description: "Khoảng cách ngang giữa các đèn (feet)" },
                spacingY: { type: "number", description: "Khoảng cách dọc giữa các đèn (feet)" },
                countX: { type: "number", description: "Số đèn theo hàng X" },
                countY: { type: "number", description: "Số đèn theo hàng Y" },
                elevation: { type: "number", description: "Cao độ đèn (feet), mặc định 15ft ≈ 4.5m" },
              }
            },
            points: { type: "array", items: { type: "array", items: { type: "number" } }, description: "Danh sách tọa độ [X,Y,Z] cho từng đèn (feet)" },
            elevation: { type: "number", description: "Cao độ mặc định nếu Z không được chỉ định (feet)" },
          },
        },
      },
      {
        name: "create_electrical_circuit",
        description: "Tạo mạch điện (Electrical Circuit) liên kết các thiết bị điện vào tủ phân phối.",
        inputSchema: {
          type: "object",
          properties: {
            fixtureIds: { type: "array", items: { type: "number" }, description: "Danh sách Element IDs của đèn/thiết bị cần nối vào mạch" },
            panelId: { type: "number", description: "Element ID của tủ điện (Electrical Panel)" },
            systemType: { type: "string", description: "Loại mạch: 'Lighting' hoặc 'Power'" },
          },
          required: ["fixtureIds"],
        },
      },
      {
        name: "get_element_ids_by_category",
        description: "Lấy danh sách Element IDs theo category (VD: 'lighting', 'panels', 'cabletray', 'conduit', 'walls', 'columns'). Dùng để tham chiếu khi tạo circuits.",
        inputSchema: {
          type: "object",
          properties: {
            category: { type: "string", description: "Tên category: 'lighting', 'panels', 'cabletray', 'conduit', 'walls', 'columns'" },
            limit: { type: "number", description: "Giới hạn số lượng trả về (mặc định 100)" },
          },
          required: ["category"],
        },
      },
      {
        name: "get_project_info",
        description: "Lấy thông tin tổng quan dự án: levels, số lượng element theo category. Hữu ích để kiểm tra tiến độ modeling.",
        inputSchema: { type: "object", properties: {} },
      },
      {
        name: "compile_blueprint_to_revit",
        description: "Gửi 1 file JSON blueprint hoàn chỉnh (toàn bộ toà nhà/nhà máy) vào Revit để biên dịch mẻ (batch) trong 1 Transaction khổng lồ. Cách mạng so với từng lệnh lẻ.",
        inputSchema: {
          type: "object",
          properties: {
            jsonPath: { type: "string", description: "Đường dẫn tuyệt đối đến file blueprint.json" }
          },
          required: ["jsonPath"]
        }
      }
    ],

  };
});

// 3. Sự kiện bắt request từ AI gõ lệnh Tool
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  try {
    if (name === "execute_pipeline") {
       const argsPipe = args as any;
       appendLog(`=== BẮT ĐẦU MACRO: ${argsPipe.macroName} ===`);
       let successCount = 0;
       
       const shortcutsPath = path.join(__dirname, "..", "src", "shortcuts.json");
       const shortcuts = fs.existsSync(shortcutsPath) ? JSON.parse(fs.readFileSync(shortcutsPath, "utf-8")) : {};

       const steps = argsPipe.steps as any[];
       steps.sort((a,b) => a.stage - b.stage);

       for (const step of steps) {
         appendLog(`[Stage ${step.stage}] Type: ${step.type}...`);
         try {
            if (step.type === "api") {
                const res = await axios.post(REVIT_BRIDGE_URL, { action: step.action, payload: step.payload || {} });
                appendLog(`    -> Success: ${JSON.stringify(res.data)}`);
            } else if (step.type === "shortcut") {
                const config = shortcuts[step.shortcutCode];
                if (!config) throw new Error(`Shortcut ${step.shortcutCode} không được hỗ trợ trong thư viện.`);
                const res = await axios.post(REVIT_BRIDGE_URL, { action: "execute_revit_command", payload: { commandId: config.commandId } });
                appendLog(`    -> Success Shortcut (${step.shortcutCode}): ${JSON.stringify(res.data)}`);
            }
            successCount++;
         } catch(stepErr: any) {
             appendLog(`    -> FAILED: ${stepErr.message}`);
             throw new Error(`Macro bị hỏng ở Stage ${step.stage}: ${stepErr.message}`);
         }
       }
       appendLog(`=== KẾT THÚC MACRO: ${argsPipe.macroName}. ${successCount}/${steps.length} steps run ===\n`);
       return { content: [{ type: "text", text: `Pipeline '${argsPipe.macroName}' hoàn thành. Checked log for details.` }] };
    }
    else if (name === "compile_blueprint_to_revit") {
        const { jsonPath } = args as any;
        const res = await axios.post(REVIT_BRIDGE_URL, { action: "build_from_blueprint", payload: { path: jsonPath } }, { timeout: 300000 }); // 5 minutes timeout for compiling entire blueprint
        return { content: [{ type: "text", text: JSON.stringify(res.data) }] };
    }

    // Các lệnh đơn lẻ thông thường
    const response = await axios.post(REVIT_BRIDGE_URL, {
      action: name,
      payload: args,
    });

    return {
      content: [
        {
          type: "text",
          text: `Đã Send lệnh cho Revit thành công.\nKết quả: ${JSON.stringify(response.data)}`,
        },
      ],
    };
  } catch (error: any) {
    return {
      content: [
        {
          type: "text",
          text: `Tuyệt vọng không kết nối được Revit! Nhớ bật tính năng Plugin trong máy tính!\nMã lỗi: ${error.message}`,
        },
      ],
      isError: true,
    };
  }
});

// 4. Khởi chạy luồng Standard I/O để bắt tay với IDE
async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("Antigravity Revit MCP Server is hooked!");
}

if (process.env.NODE_ENV !== "test") {
  main().catch(console.error);
}

export { server };
