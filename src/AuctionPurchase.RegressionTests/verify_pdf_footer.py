from pathlib import Path
from pypdf import PdfReader
import pypdfium2 as pdfium

files = list(Path("tmp/pdfs").glob("Footer-*.pdf"))
assert len(files) == 3
labels = ["Prepared By:", "Checked By:", "Approved By:", "PREPARER", "CHECKER", "APPROVER"]
for file in files:
    text = "\n".join(page.extract_text() for page in PdfReader(file).pages)
    assert not any(label in text for label in labels), (file, text)
    assert "Keep this footer note" in text and "Page 1 of 1" in text, (file, text)
    print(f"PASS: {file.name}: approval labels removed; footer note and pagination retained")

pdfium.PdfDocument(str(files[0]))[0].render(scale=1).to_pil().save("tmp/pdfs/Footer-preview.png")
