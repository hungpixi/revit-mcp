import fitz
doc = fitz.open(r'd:\CODE\revit-mcp\1.BaiTapMau_Dien.pdf')
page = doc.load_page(0)
pix = page.get_pixmap(dpi=150)
pix.save(r'd:\CODE\revit-mcp\1.BaiTapMau_Dien.png')
print("Saved PNG!")
