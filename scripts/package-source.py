"""Create a GitHub-ready source archive, excluding local data and build output."""
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

root = Path(__file__).resolve().parent.parent
archive = root / "dist/AuctionPurchase-source.zip"
archive.parent.mkdir(exist_ok=True)
roots = [root / name for name in ("src", "scripts", "docs", ".github")]
files = [root / name for name in ("README.md", ".gitignore", ".gitattributes", ".editorconfig", "global.json")]
for folder in roots:
    files.extend(path for path in folder.rglob("*") if path.is_file()
                 and not {"bin", "obj", "__pycache__"}.intersection(path.relative_to(root).parts)
                 and not path.name.endswith("_wpftmp.csproj"))
with ZipFile(archive, "w", compression=ZIP_DEFLATED, compresslevel=9) as bundle:
    for path in sorted(files):
        bundle.write(path, "AuctionPurchase/" + path.relative_to(root).as_posix())
with ZipFile(archive) as bundle:
    assert bundle.testzip() is None
    assert not any(set(name.split("/")).intersection({".local", "bin", "obj", "dist"}) for name in bundle.namelist())
    assert not any(name.startswith(("AuctionPurchase/Data/", "AuctionPurchase/Exports/", "AuctionPurchase/Backups/")) for name in bundle.namelist())
    assert not any(name.lower().endswith((".exe", ".db", ".pdb", ".bak", ".tkbackup")) for name in bundle.namelist())
print(f"Ready: {archive.relative_to(root)} ({archive.stat().st_size / 1024:.0f} KB)")
