// @ts-nocheck
import { server } from "./index";
import axios from "axios";

// Đặt môi trường
process.env.NODE_ENV = "test";

jest.mock("axios");
const mockedAxios = axios as jest.Mocked<typeof axios>;

describe("Revit MCP Node.js Server - TDD Boilerplate", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe("Suite 1: Server Config (Nên PASS)", () => {
    it("Khởi tạo đúng server MCP", () => {
      expect(server).toBeDefined();
    });
  });

  describe("Suite 2: Thêm tính năng delete_element (TDD - ĐANG FAIL)", () => {
    /*
      ======================================================
      NHIỆM VỤ TDD CỦA BẠN:
      Yêu cầu: Viết một function hoặc logic handle việc xóa đối tượng.
      Bạn cần implement function `handleDeleteElement` trong index.ts 
      hoặc tạo file mới `handlers.ts` và import vào đây.
      ======================================================
    */

    it("FAIL TEST 1: Phải có hàm handleDeleteElement xử lý xóa", async () => {
      // Hàm này chưa được tạo, việc gọi nó sẽ báo lỗi đỏ (Fail test)
      // Hãy tạo hàm này trong hệ thống của bạn và export ra
      const { handleDeleteElement } = require("./index.js");
      
      expect(typeof handleDeleteElement).toBe("function");
    });

    it("FAIL TEST 2: handleDeleteElement phải gọi axios tới bridge C# với action = 'delete_element'", async () => {
      const { handleDeleteElement } = require("./index.js");
      mockedAxios.post.mockResolvedValueOnce({ data: { success: true } });

      // Gọi thử
      if(handleDeleteElement) {
        await handleDeleteElement("123456");
      }

      // Kiểm tra có gọi Axios dến /api/ai-command không
      expect(mockedAxios.post).toHaveBeenCalledWith(
        "http://localhost:5050/api/ai-command/",
        {
          action: "delete_element",
          payload: { elementId: "123456" }
        }
      );
    });
  });
});
