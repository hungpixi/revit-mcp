const http = require('http');

const revitPort = 5050;

async function sendCommand(action, payload) {
    const data = JSON.stringify({ action, payload });

    const options = {
        hostname: 'localhost',
        port: revitPort,
        path: '/api/ai-command/',
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Content-Length': Buffer.byteLength(data)
        }
    };

    return new Promise((resolve, reject) => {
        const req = http.request(options, (res) => {
            let resData = '';
            res.on('data', (chunk) => resData += chunk);
            res.on('end', () => {
                if (res.statusCode === 200) {
                    console.log(`[SUCCESS] ${action}: ${resData}`);
                    resolve(resData);
                } else {
                    console.error(`[ERROR] ${action} HTTP ${res.statusCode}: ${resData}`);
                    reject(new Error(`HTTP ${res.statusCode}`));
                }
            });
        });

        req.on('error', (e) => {
            console.error(`[FAIL] Không thể kết nối Revit ở port ${revitPort}. Revit đã bật Add-in chưa?`);
            reject(e);
        });

        req.write(data);
        req.end();
    });
}

async function runTestPhase1() {
    console.log("=== BẮT ĐẦU TEST PHASE 1: MÔ HÌNH KIẾN TRÚC ===");

    try {
        // 1. Tạo lưới (Grid) 3x3 (Khoảng cách 4000mm ~ 13.12 ft)
        console.log("\n1. Tạo lưới trục (Grids)...");
        await sendCommand("create_grid", { name: "X1", startPt: [0, 0, 0], endPt: [0, 26.24, 0] });
        await sendCommand("create_grid", { name: "X2", startPt: [13.12, 0, 0], endPt: [13.12, 26.24, 0] });
        await sendCommand("create_grid", { name: "X3", startPt: [26.24, 0, 0], endPt: [26.24, 26.24, 0] });
        await sendCommand("create_grid", { name: "Y1", startPt: [0, 0, 0], endPt: [26.24, 0, 0] });
        await sendCommand("create_grid", { name: "Y2", startPt: [0, 13.12, 0], endPt: [26.24, 13.12, 0] });
        await sendCommand("create_grid", { name: "Y3", startPt: [0, 26.24, 0], endPt: [26.24, 26.24, 0] });

        // // 2. Tạo Levels (Tầng 1 và Mái) (13.12 ft ~ 4m)
        console.log("\n2. Tạo Cao độ (Levels)...");
        await sendCommand("create_level", { elevation: 0.0, name: "Tầng 1 (Phase 1)" });
        await sendCommand("create_level", { elevation: 13.12, name: "Tầng Mái (Phase 1)" });

        // 3. Tạo Tường bao (Walls)
        console.log("\n3. Xây tường (Walls)...");
        await sendCommand("create_wall", { startPt: [0, 0, 0], endPt: [0, 26.24, 0] });
        await sendCommand("create_wall", { startPt: [0, 26.24, 0], endPt: [26.24, 26.24, 0] });
        await sendCommand("create_wall", { startPt: [26.24, 26.24, 0], endPt: [26.24, 0, 0] });
        await sendCommand("create_wall", { startPt: [26.24, 0, 0], endPt: [0, 0, 0] });

        // 4. Đặt Cửa (Door) và Cửa sổ (Window) (Dynamic Family Fallback)
        console.log("\n4. Đặt khối Family (Windows & Doors) - Tự động nội suy từ Library...");
        await sendCommand("place_family_instance", { category: "Doors", point: [13.12, 0, 0] });
        await sendCommand("place_family_instance", { category: "Windows", point: [0, 13.12, 4.0] }); 

        console.log("\n=== HOÀN THÀNH TEST PHASE 1 ===");
    } catch (err) {
        console.log("Quy trình dừng lại vì lỗi:", err.message);
    }
}

runTestPhase1();
