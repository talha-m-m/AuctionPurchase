<p align="center"><img src="docs/assets/app-icon.png" width="88" alt="Auction Purchase icon"></p>

# Auction Purchase

A Windows desktop workspace for tea auction purchases, garden accounts, and purchasing reports.

**Created by TMM** · [LinkedIn](https://www.linkedin.com/in/talhamm/) · [GitHub](https://github.com/talha-m-m)

## Features

- Sale entry with searchable master data and Excel uploads that skip duplicate lots and fill missing fields.
- Companies with multiple gardens and separate selling accounts for each lot.
- Purchasing dashboard with a Bangladesh-centered tea garden map.
- PDF, Excel, and Word reports with editable templates.
- Light/dark themes, date preferences, and database backup/restore.
- Self-contained Windows executable; end users do not need to install .NET.

## Run the application

Extract `AuctionPurchase.zip` and open `AuctionPurchase/AuctionPurchase.exe`.
The portable copy creates its database on first launch, with built-in garden,
broker, and grade lists. It contains no private sales history.

Data, backups, and exported reports stay beside the executable. Keep the whole
application folder together when moving it.

## Build from source

Requires Windows 10/11 (64-bit), the **.NET 8 SDK**, and PowerShell.

```powershell
dotnet build src/AuctionPurchase.sln -c Release -m:1
dotnet run --project src/AuctionPurchase.UI -c Release --no-build
```

## Verification

```powershell
dotnet run --project src/AuctionPurchase.RegressionTests -c Release --no-build
dotnet run --project src/AuctionPurchase.RegressionTests -c Release --no-build -- --portable
```

Interactive WPF checks require a Windows desktop session:

```powershell
dotnet run --project src/AuctionPurchase.SearchTests -c Release --no-build
```

## Create a user-ready ZIP

```powershell
./scripts/package.ps1
```

Outputs:

- `dist/AuctionPurchase/AuctionPurchase.exe` — runnable app; existing local data is preserved.
- `dist/AuctionPurchase.zip` — fresh portable package, ready to share.

The GitHub Actions workflow builds, checks, and packages the application on Windows.
Its ZIP is available as a workflow artifact and can be attached to a GitHub release.

## Repository layout

```text
src/
  AuctionPurchase.Core/             Models and interfaces
  AuctionPurchase.Infrastructure/   SQLite, imports, exports, and backups
  AuctionPurchase.UI/               WPF application
  AuctionPurchase.RegressionTests/  Database and export checks
  AuctionPurchase.SearchTests/      Interactive UI checks
scripts/                           Packaging and verification tools
docs/assets/                       Documentation artwork
.github/workflows/                 Windows build and packaging
```

Private databases, settings, backups, exports, local artwork, generated builds,
and distribution files are excluded by `.gitignore`. On the development machine,
existing working files are preserved in `.local/`; this folder is never part of
the source distribution. Configure custom data locations in Settings when needed.

## Map data

Map tiles: [OpenStreetMap](https://www.openstreetmap.org/copyright).
Bangladesh outline: [Natural Earth](https://www.naturalearthdata.com/), public-domain boundary data.
The app's dependencies retain their respective licenses.
