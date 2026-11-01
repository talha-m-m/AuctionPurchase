"""Check the clean, portable user distribution without launching the GUI."""
import hashlib
import json
from pathlib import Path
from zipfile import ZipFile

archive = Path("dist/AuctionPurchase.zip")
with ZipFile(archive) as bundle:
    assert bundle.testzip() is None, "ZIP integrity check failed"
    names = bundle.namelist()
    assert all(name.startswith("AuctionPurchase/") and ".." not in name.split("/") for name in names)
    exes = [name for name in names if name.lower().endswith(".exe")]
    assert exes == ["AuctionPurchase/AuctionPurchase.exe"], exes
    assert not any(name.lower().endswith((".db", ".pdb", ".xlsx")) for name in names), names
    settings = json.loads(bundle.read("AuctionPurchase/Data/settings.json"))
    assert all(settings.get(key) == "" for key in ("DatabaseLocation", "BackupLocation", "ExportLocation"))
    app = bundle.read(exes[0])
    assert app[:2] == b"MZ", "Not a Windows executable"
    assert hashlib.sha256(app).digest() == hashlib.sha256(Path("dist/AuctionPurchase/AuctionPurchase.exe").read_bytes()).digest()
    assert "AuctionPurchase/README.txt" in names
    print("PASS: ZIP integrity, executable name, portable settings, clean data and executable hash")
    for name in names:
        print(name)
print(f"Archive size: {archive.stat().st_size / 1024 / 1024:.1f} MB")
