/**
 * solve_electrical_full.js
 * Tự động vẽ TOÀN BỘ hệ thống điện Nhà Máy NML từ bản vẽ 1.BaiTapMau_Dien.png
 * 
 * Nhà máy: ~60m x 25m (≈197ft x 82ft)
 * Cao trần: ~5m (≈16.4ft)
 * Cable tray: chạy ở độ cao ~3.7m (≈12ft)
 * Đèn: treo ở ~4.5m (≈15ft)
 */

const axios = require('axios');
const API = 'http://localhost:5050/api/ai-command/';

async function send(action, payload = {}) {
    try {
        const res = await axios.post(API, { action, payload }, { timeout: 60000 });
        const data = res.data;
        // Parse if string
        const result = typeof data === 'string' ? JSON.parse(data) : data;
        console.log(`  ✅ ${action}: ${result.msg || result.status || JSON.stringify(result).substring(0, 100)}`);
        return result;
    } catch (err) {
        console.error(`  ❌ ${action}: ${err.message}`);
        return { status: 'Error', msg: err.message };
    }
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

// ========== KÍCH THƯỚC NHÀ MÁY (feet) ==========
// 60m ≈ 197ft, 25m ≈ 82ft, 5m ceiling ≈ 16.4ft
const W = 197;  // Chiều rộng (X)
const L = 82;   // Chiều dài (Y) 
const H = 16.4; // Chiều cao trần

// Grid cột: 6m ≈ 20ft spacing
const GRID_SPACING = 20;
const COLS_X = Math.floor(W / GRID_SPACING) + 1; // 11 cột theo X
const COLS_Y = Math.floor(L / GRID_SPACING) + 1; // 5 cột theo Y

// MEP elevations
const TRAY_ELEV = 12;  // Cable tray ở 12ft ≈ 3.7m
const LIGHT_ELEV = 15; // Đèn ở 15ft ≈ 4.5m

// Lighting grid: 3m ≈ 10ft spacing
const LIGHT_SPACING = 10;

async function main() {
    console.log('='.repeat(60));
    console.log('  NHÀ MÁY NML — HỆ THỐNG ĐIỆN — AUTOMATION');
    console.log('  Kích thước: ' + W + 'ft x ' + L + 'ft x ' + H + 'ft');
    console.log('='.repeat(60));

    // ============================================================
    // PHASE A: KHUNG NHÀ (Structure)
    // ============================================================
    console.log('\n📐 Phase A: Khung nhà xưởng');

    // A1: Tạo dự án MEP mới
    console.log('\n  [A1] Tạo dự án MEP...');
    await send('create_project_from_template', {
        templatePath: 'C:\\ProgramData\\Autodesk\\RVT 2020\\Templates\\US Imperial\\Electrical-Default.rte'
    });
    await sleep(3000);

    // A2: Tạo Grids - lưới cột
    console.log('\n  [A2] Tạo lưới cột (Grids)...');
    // Grid dọc (theo X) — dùng số 1-11
    for (let i = 0; i < COLS_X; i++) {
        const x = i * GRID_SPACING;
        await send('create_wall', { // Dùng tạm create_grid nếu có, create_wall fallback
            startPt: [x, -5, 0], 
            endPt: [x, L + 5, 0]
        });
    }
    // Grid ngang (theo Y) — dùng chữ A-E
    for (let j = 0; j < COLS_Y; j++) {
        const y = j * GRID_SPACING;
        await send('create_wall', {
            startPt: [-5, y, 0],
            endPt: [W + 5, y, 0]
        });
    }
    await sleep(1000);

    // A3: Tạo 4 tường bao chính
    console.log('\n  [A3] Tạo 4 tường bao nhà xưởng...');
    await send('create_wall', { startPt: [0, 0, 0], endPt: [W, 0, 0], levelId: "1" });
    await sleep(500);
    await send('create_wall', { startPt: [W, 0, 0], endPt: [W, L, 0], levelId: "1" });
    await sleep(500);
    await send('create_wall', { startPt: [W, L, 0], endPt: [0, L, 0], levelId: "1" });
    await sleep(500);
    await send('create_wall', { startPt: [0, L, 0], endPt: [0, 0, 0], levelId: "1" });
    await sleep(1000);

    // A4: Cột kết cấu tại giao điểm grid
    console.log('\n  [A4] Đặt cột kết cấu...');
    const colPoints = [];
    for (let ix = 0; ix < COLS_X; ix++) {
        for (let iy = 0; iy < COLS_Y; iy++) {
            colPoints.push([ix * GRID_SPACING, iy * GRID_SPACING, 0]);
        }
    }
    await send('create_structural_column', { points: colPoints });
    await sleep(2000);

    // ============================================================
    // PHASE B: HỆ THỐNG MÁNG CÁP (Cable Tray)
    // ============================================================
    console.log('\n🔌 Phase B: Hệ thống máng cáp (Cable Tray)');

    // B1: 2 tuyến chính chạy dọc nhà (song song theo X)
    console.log('\n  [B1] Máng cáp chính dọc nhà...');
    // Tuyến 1: y = 20ft (cách tường ~6m)
    await send('create_cable_tray', {
        width: 300, height: 100,
        points: [[5, 20, TRAY_ELEV], [W - 5, 20, TRAY_ELEV]]
    });
    await sleep(1000);

    // Tuyến 2: y = 60ft
    await send('create_cable_tray', {
        width: 300, height: 100,
        points: [[5, 60, TRAY_ELEV], [W - 5, 60, TRAY_ELEV]]
    });
    await sleep(1000);

    // B2: Tuyến ngang kết nối 2 tuyến dọc (đầu nhà, giữa nhà, cuối nhà)
    console.log('\n  [B2] Máng cáp ngang kết nối...');
    const crossX = [5, 40, 80, 120, 160, W - 5]; // 6 tuyến ngang
    for (const x of crossX) {
        await send('create_cable_tray', {
            width: 200, height: 100,
            points: [[x, 20, TRAY_ELEV], [x, 60, TRAY_ELEV]]
        });
        await sleep(500);
    }

    // B3: Nhánh phụ (vuông góc - thả xuống khu vực đèn)
    console.log('\n  [B3] Máng cáp nhánh phụ...');
    const branchX = [20, 60, 100, 140, 180];
    for (const x of branchX) {
        // Nhánh trên: y = 20 -> y = 5
        await send('create_cable_tray', {
            width: 150, height: 100,
            points: [[x, 20, TRAY_ELEV], [x, 5, TRAY_ELEV]]
        });
        // Nhánh dưới: y = 60 -> y = 77
        await send('create_cable_tray', {
            width: 150, height: 100,
            points: [[x, 60, TRAY_ELEV], [x, L - 5, TRAY_ELEV]]
        });
        await sleep(300);
    }
    await sleep(1000);

    // ============================================================
    // PHASE C: TỦ ĐIỆN & ỐNG LUỒN DÂY (Conduit)
    // ============================================================
    console.log('\n⚡ Phase C: Tủ điện & Conduit');

    // C1: 2 Tủ điện tổng (đặt ở 2 đầu nhà)
    console.log('\n  [C1] Đặt tủ điện tổng...');
    await send('place_electrical_equipment', {
        points: [
            [10, L / 2, 0],   // Tủ điện trái (gần tường trái)
            [W - 10, L / 2, 0] // Tủ điện phải (gần tường phải)
        ]
    });
    await sleep(1000);

    // C2: Conduit thả từ cable tray xuống tủ điện
    console.log('\n  [C2] Conduit chính (tủ điện)...');
    // Conduit từ cable tray xuống tủ trái
    await send('create_conduit', {
        points: [[10, L / 2, TRAY_ELEV], [10, L / 2, 4]]
    });
    // Conduit từ cable tray xuống tủ phải
    await send('create_conduit', {
        points: [[W - 10, L / 2, TRAY_ELEV], [W - 10, L / 2, 4]]
    });
    await sleep(500);

    // C3: Conduit drops từ nhánh cable tray (mỗi nhánh 2 drop)
    console.log('\n  [C3] Conduit drops từ nhánh...');
    for (const x of branchX) {
        // Drop trên
        await send('create_conduit', {
            points: [[x, 10, TRAY_ELEV], [x, 10, 4]]
        });
        // Drop dưới
        await send('create_conduit', {
            points: [[x, L - 10, TRAY_ELEV], [x, L - 10, 4]]
        });
        await sleep(200);
    }
    await sleep(1000);

    // ============================================================
    // PHASE D: ĐÈN CHIẾU SÁNG (Lighting Fixtures)
    // ============================================================
    console.log('\n💡 Phase D: Đèn chiếu sáng (Grid Pattern)');

    // Đặt đèn theo grid pattern trên toàn bộ nhà máy
    // Offset 10ft từ tường, spacing 10ft (~3m)
    const lightOriginX = 10;
    const lightOriginY = 5;
    const lightCountX = Math.floor((W - 20) / LIGHT_SPACING) + 1; // ≈ 18
    const lightCountY = Math.floor((L - 10) / LIGHT_SPACING) + 1; // ≈ 8
    const totalLights = lightCountX * lightCountY;

    console.log(`  Grid: ${lightCountX} x ${lightCountY} = ${totalLights} đèn`);
    console.log(`  Spacing: ${LIGHT_SPACING}ft, Origin: (${lightOriginX}, ${lightOriginY}), Elevation: ${LIGHT_ELEV}ft`);

    await send('place_lighting_fixture', {
        grid: {
            originX: lightOriginX,
            originY: lightOriginY,
            spacingX: LIGHT_SPACING,
            spacingY: LIGHT_SPACING,
            countX: lightCountX,
            countY: lightCountY,
            elevation: LIGHT_ELEV,
        }
    });
    await sleep(3000);

    // ============================================================
    // PHASE E: KIỂM TRA KẾT QUẢ
    // ============================================================
    console.log('\n📊 Phase E: Kiểm tra kết quả');
    const info = await send('get_project_info', {});
    console.log('  Project Info:', JSON.stringify(info, null, 2));

    // ============================================================
    // SAVE
    // ============================================================
    console.log('\n💾 Lưu dự án...');
    await send('save_project', {
        savePath: 'd:\\CODE\\revit-mcp\\Output_NML_Electrical.rvt'
    });

    console.log('\n' + '='.repeat(60));
    console.log('  ✅ HOÀN TẤT! Nhà Máy NML — Hệ Thống Điện');
    console.log('='.repeat(60));
}

main().catch(console.error);
