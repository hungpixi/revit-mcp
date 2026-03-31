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
            console.error(`[FAIL] Lỗi kết nối Add-in tại cảng 5050. Revit đã bật chưa?`);
            reject(e);
        });

        req.write(data);
        req.end();
    });
}

async function runTestPhase3() {
    console.log("=== BẮT ĐẦU TEST PHASE 3: NHÀ THÉP & TUYẾN ĐIỆN MÁNG CÁP ===");

    try {
        // 1. Tạo 4 Cột Thép (Khung nhà xưởng Móng - Cột)
        console.log("\n1. Tạo Khung Thép Công Nghiệp (Cột)...");
        await sendCommand("create_structural_column", { 
            points: [
                [0, 0, 0],   
                [20, 0, 0],
                [0, 15, 0],
                [20, 15, 0]
            ]
        });

        // 2. Kẻ dầm thép mái xung quanh 4 cột (Z = 12)
        console.log("\n2. Rải Dầm Thép (Structural Framing)...");
        await sendCommand("create_structural_framing", { 
            points: [
                [0, 0, 12],   
                [20, 0, 12],
                [20, 15, 12],
                [0, 15, 12],
                [0, 0, 12]     // Kép kín vòng
            ]
        });

        // 3. Rải Máng Cáp cấp điện chiếu sáng cho xưởng (Cable Tray width 300, Z=10)
        console.log("\n3. Thi công Máng Cáp Mạng Điện (Cable Tray)...");
        await sendCommand("create_cable_tray", { 
            width: 300, 
            height: 100, 
            points: [
                [-5, 5, 10],   // Kéo từ ngoài tủ điện ngoài trời vào
                [5, 5, 10],
                [5, 12, 10],
                [15, 12, 10]
            ]
        });

        console.log("\n=== HOÀN THÀNH TEST PHASE 3 ===");
    } catch (err) {
        console.log("Dừng quy trình do lỗi Exception.");
    }
}

runTestPhase3();
