import fitz
import os

pdf_path = r'd:\CODE\revit-mcp\1.BaiTapMau_Dien.pdf'
out_dir = r'd:\CODE\revit-mcp\thumbnails'
if not os.path.exists(out_dir):
    os.makedirs(out_dir)

doc = fitz.open(pdf_path)
for i in range(len(doc)):
    page = doc.load_page(i)
    pix = page.get_pixmap(dpi=72)
    pix.save(os.path.join(out_dir, f"page_{i}.png"))
print(f"Exported {len(doc)} pages to {out_dir}")
