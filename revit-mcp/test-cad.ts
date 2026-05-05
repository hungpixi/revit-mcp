#!/usr/bin/env node

import path from "path";
import { fileURLToPath } from "url";
import {
  analyzeCADFiles,
  generateCADAnalysisReport,
  verifyCADStructure,
} from "../src/utils/CADAnalyzer.js";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

async function runCADTest() {
  console.log("🔍 CAD Structure Test - Mẫu Nhà Số 5 Project\n");

  // Đường dẫn đến thư mục CAD
  const cadDirectory = path.join(__dirname, "..", "CAD");

  try {
    // Phân tích CAD files
    console.log(`📂 Scanning CAD directory: ${cadDirectory}\n`);
    const cadFiles = await analyzeCADFiles(cadDirectory);

    // In báo cáo
    const report = generateCADAnalysisReport(cadFiles);
    console.log(report);

    // Kiểm tra cấu trúc
    console.log("\n=== STRUCTURE VERIFICATION ===\n");
    const verification = verifyCADStructure(cadFiles);

    console.log(`✅ Found: ${verification.found}/${verification.total} files`);
    console.log(`📊 Completeness: ${verification.percentageComplete.toFixed(1)}%\n`);

    if (verification.missing.length > 0) {
      console.log("❌ Missing files:");
      verification.missing.forEach((f) => console.log(`   - ${f}`));
    }

    // Summary by type
    console.log("\n=== FILES BY TYPE ===\n");

    const floorPlans = cadFiles.filter(
      (f) => f.expectedStructure?.description.includes("Mặt bằng")
    );
    const details = cadFiles.filter(
      (f) => f.expectedStructure?.description.includes("Chi tiết")
    );
    const elevations = cadFiles.filter(
      (f) => f.expectedStructure?.description.includes("Mặt đứng")
    );
    const schedules = cadFiles.filter((f) => f.fileName.includes("A-701") || f.fileName.includes("A-702") || f.fileName.includes("A-703"));

    console.log(`📐 Floor Plans: ${floorPlans.length} files`);
    console.log(`🔧 Detail Drawings: ${details.length} files`);
    console.log(`📋 Elevations & Sections: ${elevations.length} files`);
    console.log(`📊 Schedules: ${schedules.length} files`);

    // Blocks summary
    console.log("\n=== EXPECTED BLOCKS SUMMARY ===\n");

    const allBlocks = new Set<string>();
    cadFiles.forEach((f) => {
      f.expectedStructure?.expectedBlocks?.forEach((b) => allBlocks.add(b));
    });

    const doors = Array.from(allBlocks)
      .filter((b) => b.startsWith("D"))
      .sort();
    const windows = Array.from(allBlocks)
      .filter((b) => b.startsWith("W"))
      .sort();
    const louvers = Array.from(allBlocks)
      .filter((b) => b.startsWith("LV"))
      .sort();

    console.log(`🚪 Door Blocks: ${doors.join(", ") || "None"}`);
    console.log(`🪟 Window Blocks: ${windows.join(", ") || "None"}`);
    console.log(`☀️  Louver Blocks: ${louvers.join(", ") || "None"}`);

    // Levels summary
    console.log("\n=== FLOOR LEVELS ===\n");
    const levels = cadFiles
      .filter((f) => f.expectedStructure?.elevation !== undefined)
      .sort((a, b) => (b.expectedStructure?.elevation ?? 0) - (a.expectedStructure?.elevation ?? 0));

    levels.forEach((f) => {
      const struct = f.expectedStructure!;
      console.log(`📍 ${struct.level}: ${struct.elevation}mm - ${struct.fileName}`);
    });

    console.log("\n✨ CAD structure analysis completed!\n");
  } catch (error) {
    console.error("❌ Error during CAD analysis:", error);
    process.exit(1);
  }
}

runCADTest();
