import * as fs from "fs";
import * as path from "path";

// Lấy thư mục gốc hiện tại (cùng cấp với thư mục src/)
const LOG_DIR = path.join(__dirname, "..", "logs");

if (!fs.existsSync(LOG_DIR)) {
  fs.mkdirSync(LOG_DIR);
}

const LOG_FILE = path.join(LOG_DIR, "macro_execution.log");

export function appendLog(message: string): void {
  const timestamp = new Date().toISOString();
  const logLine = `[${timestamp}] ${message}\n`;
  
  // Appends to the file asynchronously
  fs.appendFile(LOG_FILE, logLine, (err) => {
    if (err) {
      console.error("Failed to write to log file:", err);
    }
  });

  // Cũng in ra Standard Error để MCP client có thể ghi lại nếu bị fail
  console.error(logLine.trim());
}
