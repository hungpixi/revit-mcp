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
            console.error(`[FAIL] Lỗi kết nối Add-in tại cảng 5050. Revit đã mở Add-in lên chưa?`);
            reject(e);
        });

        req.write(data);
        req.end();
    });
}

async function runTestPhase2() {
    console.log("=== BẮT ĐẦU TEST PHASE 2: CƠ ĐIỆN NẶNG (HVAC & NƯỚC) ===");

    try {
        // 1. Tạo tuyến ống gió HVAC (Duct Routing) vạch sẵn
        // Kích thước 400x300, 3 điểm nối (sẽ tự phát sinh 1 góc vuông Elbow)
        console.log("\n1. Tạo mạng lưới Ống Gió (HVAC)...");
        await sendCommand("create_duct", { 
            width: 400, 
            height: 300, 
            points: [
                [5, 5, 10],   // Z = 10 feet cao độ ống
                [15, 5, 10],
                [15, 15, 10]
            ]
        });

        // 2. Tạo một tuyến ống nước sạch 
        // Đường kính D90, đi kèm fitting
        console.log("\n2. Tạo mạng lưới Cấp Thoát Nước (Plumbing)...");
        await sendCommand("create_pipe", { 
            diameter: 90, 
            points: [
                [20, 5, -2],   // Z = -2 feet (Dưới mặt đất)
                [30, 5, -2],
                [30, 15, -2],
                [30, 15, 8]    // Trục đứng Water Riser
            ]
        });

        console.log("\n=== HOÀN THÀNH TEST PHASE 2 ===");
    } catch (err) {
        console.log("Dừng quy trình do lỗi quá trình phát sinh (Exception).");
    }
}

runTestPhase2();
