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
                try {
                    const parsed = JSON.parse(resData);
                    if (parsed.status === 'OK') {
                        console.log(`  ✅ [${action}]: ${parsed.msg || 'OK'}`);
                    } else {
                        console.log(`  ❌ [${action}]: ${parsed.msg}`);
                    }
                    resolve(parsed);
                } catch(e) {
                    console.log(`  ❌ [${action}] Parse error: ${resData}`);
                    resolve({status: "Error", msg: resData});
                }
            });
        });
        req.on('error', (e) => {
            console.error(`  🔌 Lỗi kết nối cổng 5050.`);
            reject(e);
        });
        req.write(data);
        req.end();
    });
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

async function runAutoAssignment() {
    console.log("==================================================");
    console.log(" TỰ ĐỘNG GIẢI BÀI TẬP ĐIỆN MEP (AUTO 100%)");
    console.log("==================================================\n");

    console.log("📌 Step 1: Khởi tạo Project ngầm (Background)...");
    let res = await sendCommand("open_new_project", {});
    if(res.status !== 'OK') { return; }
    
    // Đợi chút cho Revit kích hoạt view
    console.log("  ⏳ Chờ Revit dựng UI...");
    await sleep(4000); 

    console.log("\n📌 Step 1.5: Import bản vẽ PDF làm Underlay tham chiếu...");
    let imgRes = await sendCommand("import_image", { path: "d:\\CODE\\revit-mcp\\1.BaiTapMau_Dien.png", point: [0, 0, 0] });
    if (imgRes.status !== 'OK') {
        console.log("  ⚠️ Bỏ qua bước import ảnh do lỗi:", imgRes.msg);
    }
    await sleep(1000);

    console.log("\n📌 Step 2: Dựng Khung Xưởng (Struct)...");
    await sendCommand("create_structural_column", { points: [[0,0,0], [30,0,0], [0,20,0], [30,20,0]] });
    await sendCommand("create_structural_framing", { points: [[0,0,15], [30,0,15], [30,20,15], [0,20,15], [0,0,15]] });
    await sleep(500);

    console.log("\n📌 Step 3: Đặt 2 Tủ Điện Tổng (Panel Board)...");
    await sendCommand("place_electrical_equipment", { points: [[2, 2, 0], [28, 2, 0]] });
    await sleep(500);

    console.log("\n📌 Step 4: Đi Tuyến Máng Cáp Chính (Cable Tray) nối 2 Tủ (Cao độ 12ft)...");
    await sendCommand("create_cable_tray", { 
        width: 300, height: 100, 
        points: [
            [2, 2, 12],   // Thả trên tủ 1
            [2, 10, 12],  // Chạy ngang xưởng
            [28, 10, 12], 
            [28, 2, 12]   // Thả trên tủ 2
        ] 
    });
    await sleep(500);

    console.log("\n📌 Step 5: Thả Ống Luồn Cáp (Conduit) từ Máng xuống Tủ...");
    await sendCommand("create_conduit", { points: [[2, 2, 12], [2, 2, 4]] });
    await sendCommand("create_conduit", { points: [[28, 2, 12], [28, 2, 4]] });

    console.log("\n==================================================");
    console.log(" 🎉 HOÀN THÀNH AUTO BÀI TẬP ĐIỆN 100%!");
    console.log("==================================================\n");
}

runAutoAssignment();
