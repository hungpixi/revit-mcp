import axios from "axios";
import fs from "fs";
import path from "path";

// Copy the logging function exactly as in index.ts for testing
const LOG_DIR = path.join(__dirname, "..", "logs");
if (!fs.existsSync(LOG_DIR)) fs.mkdirSync(LOG_DIR);
const LOG_FILE = path.join(LOG_DIR, "macro_execution.log");

function appendLog(message: string): void {
  const timestamp = new Date().toISOString();
  const logLine = `[${timestamp}] ${message}\n`;
  fs.appendFileSync(LOG_FILE, logLine);
  console.log(logLine.trim());
}

async function runTestPipeline() {
    const macroName = "Autobuild_House_Demo";
    const steps = [
        // 1. Dùng API ngầm tạo tường
        { stage: 1, type: "api", action: "create_wall", payload: { startPt: [0,0,0], endPt: [20,20,0] } },
        // 2. Kích hoạt lệnh vẽ Tường (WA) trên UI
        { stage: 2, type: "shortcut", shortcutCode: "WA" },
        // 3. Đổi sang View 3D nhanh sau khi vẽ xong
        { stage: 3, type: "shortcut", shortcutCode: "{3D}" }
    ];

    appendLog(`\n=== BẮT ĐẦU MACRO: ${macroName} ===`);
    let successCount = 0;
    
    const shortcutsPath = path.join(__dirname, "shortcuts.json");
    const shortcuts = fs.existsSync(shortcutsPath) ? JSON.parse(fs.readFileSync(shortcutsPath, "utf-8")) : {};

    for (const step of steps) {
        appendLog(`[Stage ${step.stage}] Thực thi lệnh type: ${step.type}...`);
        try {
            if (step.type === "api") {
                const res = await axios.post("http://localhost:5050/api/ai-command/", { action: step.action, payload: step.payload || {} });
                appendLog(`    -> Chạm API C# thành công (Response): ${JSON.stringify(res.data)}`);
            } else if (step.type === "shortcut" && step.shortcutCode) {
                const config = shortcuts[step.shortcutCode];
                if (!config) throw new Error(`Không tìm thấy phím tắt ${step.shortcutCode}`);
                
                const res = await axios.post("http://localhost:5050/api/ai-command/", { action: "execute_revit_command", payload: { commandId: config.commandId } });
                appendLog(`    -> Kích hoạt UI Revit (CommandId: ${config.commandId}): ${JSON.stringify(res.data)}`);
            }
            successCount++;
        } catch(stepErr: any) {
            appendLog(`    -> THẤT BẠI: ${stepErr.message}`);
        }
        
        // Nghỉ 1s giữa mỗi lệnh cho giống thật
        await new Promise(r => setTimeout(r, 1000));
    }
    
    appendLog(`=== KẾT THÚC MACRO: ${macroName}. Thành công: ${successCount}/${steps.length} ===\n`);
}

runTestPipeline();
