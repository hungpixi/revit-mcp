import * as net from "net";

export class RevitClientConnection {
  host: string;
  port: number;
  socket: net.Socket;
  isConnected: boolean = false;
  responseCallbacks: Map<string, (response: string) => void> = new Map();
  buffer: string = "";

  constructor(host: string, port: number) {
    this.host = host;
    this.port = port;
    this.socket = new net.Socket();
    this.setupSocketListeners();
  }

  private setupSocketListeners(): void {
    this.socket.on("connect", () => {
      this.isConnected = true;
    });

    this.socket.on("data", (data) => {
      // 将接收到的数据添加到缓冲区
      const dataString = data.toString();
      this.buffer += dataString;

      // 尝试解析完整的JSON响应
      this.processBuffer();
    });

    this.socket.on("close", () => {
      this.isConnected = false;
    });

    this.socket.on("error", (error) => {
      console.error("RevitClientConnection error:", error);
      this.isConnected = false;
    });
  }

  private processBuffer(): void {
    const { messages, remaining } = extractJsonMessages(this.buffer);
    this.buffer = remaining;

    for (const message of messages) {
      this.handleResponse(message);
    }
  }

  public connect(): boolean {
    if (this.isConnected) {
      return true;
    }

    try {
      this.socket.connect(this.port, this.host);
      return true;
    } catch (error) {
      console.error("Failed to connect:", error);
      return false;
    }
  }

  public disconnect(): void {
    this.socket.end();
    this.isConnected = false;
  }

  private generateRequestId(): string {
    return Date.now().toString() + Math.random().toString().substring(2, 8);
  }

  private handleResponse(responseData: string): void {
    try {
      const response = JSON.parse(responseData);
      // 从响应中获取ID
      const requestId = response.id || "default";

      const callback = this.responseCallbacks.get(requestId);
      if (callback) {
        callback(responseData);
        this.responseCallbacks.delete(requestId);
      }
    } catch (error) {
      console.error("Error parsing response:", error);
    }
  }

  public sendCommand(command: string, params: any = {}): Promise<any> {
    return new Promise((resolve, reject) => {
      try {
        if (!this.isConnected) {
          this.connect();
        }

        // 生成请求ID
        const requestId = this.generateRequestId();

        // 创建符合JSON-RPC标准的请求对象
        const commandObj = {
          jsonrpc: "2.0",
          method: command,
          params: params,
          id: requestId,
        };

        // 存储回调函数
        this.responseCallbacks.set(requestId, (responseData) => {
          try {
            const response = JSON.parse(responseData);
            if (response.error) {
              reject(
                new Error(response.error.message || "Unknown error from Revit")
              );
            } else {
              resolve(response.result);
            }
          } catch (error) {
            if (error instanceof Error) {
              reject(new Error(`Failed to parse response: ${error.message}`));
            } else {
              reject(new Error(`Failed to parse response: ${String(error)}`));
            }
          }
        });

        // 发送命令
        const commandString = JSON.stringify(commandObj);
        this.socket.write(commandString);

        // 设置超时
        setTimeout(() => {
          if (this.responseCallbacks.has(requestId)) {
            this.responseCallbacks.delete(requestId);
            reject(new Error(`Command timed out after 2 minutes: ${command}`));
          }
        }, 120000); // 2分钟超时
      } catch (error) {
        reject(error);
      }
    });
  }
}

export function extractJsonMessages(buffer: string): {
  messages: string[];
  remaining: string;
} {
  const messages: string[] = [];
  let cursor = 0;

  while (cursor < buffer.length) {
    while (cursor < buffer.length && /\s/.test(buffer[cursor])) {
      cursor++;
    }

    if (cursor >= buffer.length) {
      return { messages, remaining: "" };
    }

    if (buffer[cursor] !== "{") {
      const nextObjectStart = buffer.indexOf("{", cursor + 1);
      if (nextObjectStart === -1) {
        return { messages, remaining: "" };
      }
      cursor = nextObjectStart;
    }

    const messageEnd = findJsonObjectEnd(buffer, cursor);
    if (messageEnd === -1) {
      return { messages, remaining: buffer.slice(cursor) };
    }

    const candidate = buffer.slice(cursor, messageEnd + 1);
    try {
      JSON.parse(candidate);
      messages.push(candidate);
      cursor = messageEnd + 1;
    } catch {
      cursor = messageEnd + 1;
    }
  }

  return { messages, remaining: "" };
}

function findJsonObjectEnd(buffer: string, start: number): number {
  let depth = 0;
  let inString = false;
  let escaped = false;

  for (let i = start; i < buffer.length; i++) {
    const char = buffer[i];

    if (escaped) {
      escaped = false;
      continue;
    }

    if (char === "\\" && inString) {
      escaped = true;
      continue;
    }

    if (char === '"') {
      inString = !inString;
      continue;
    }

    if (inString) {
      continue;
    }

    if (char === "{") {
      depth++;
    } else if (char === "}") {
      depth--;
      if (depth === 0) {
        return i;
      }
    }
  }

  return -1;
}
