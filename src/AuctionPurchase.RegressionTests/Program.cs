using AuctionPurchase.Core.Models;
using AuctionPurchase.Infrastructure.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using AuctionPurchase.Core.Reports;
using AuctionPurchase.Infrastructure.Export;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using AuctionPurchase.Infrastructure;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine($"PASS: {message}");
}

if (args.Contains("--portable"))
{
    var portableData = Path.Combine(AppContext.BaseDirectory, "Data");
    if (Directory.Exists(portableData)) throw new Exception("Portable test requires an isolated output directory.");
    Directory.CreateDirectory(portableData);
    try
    {
        File.WriteAllText(Path.Combine(portableData, "settings.json"), "{}");
        SettingsManager.Reload();
        Check(SettingsManager.DefaultDatabaseLocation == portableData &&
              SettingsManager.DefaultBackupLocation == Path.Combine(AppContext.BaseDirectory, "Backups") &&
              SettingsManager.DefaultExportLocation == Path.Combine(AppContext.BaseDirectory, "Exports"),
              "Portable data, backups and exports resolve beside the application");
        var config = new DatabaseConfig();
        Check(config.DatabasePath == Path.Combine(portableData, "Auction.db"),
            "First portable launch creates its own database without a machine-specific path");
        var factory = new DatabaseFactory(config);
        await new MigrationRunner(factory).RunMigrationsAsync();
        await new DatabaseSeeder(factory).SeedAsync();
        using var connection = factory.CreateConnection();
        Check(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Sales") == 0 &&
              await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Gardens") > 0 &&
              await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Brokers") > 0 &&
              await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Grades") > 0,
              "Fresh portable copy initializes master lists with no sales history");
    }
    finally
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(portableData, true);
    }
    return;
}

var databasePath = Path.Combine(Path.GetTempPath(), $"AuctionUploadTests-{Guid.NewGuid():N}.db");
try
{
    var factory = new TestDatabaseFactory(databasePath);
    await new MigrationRunner(factory).RunMigrationsAsync();
    using (var connection = factory.CreateConnection())
    {
        await connection.ExecuteAsync(@"
            INSERT INTO Brands (BrandId, Name) VALUES (10000, 'Test Brand');
            INSERT INTO Brokers (BrokerId, Name) VALUES (10000, 'Test Broker'), (10001, 'Other Broker');
            INSERT INTO Gardens (GardenId, Name) VALUES (10000, 'Test Garden'), (10001, 'Account Garden'), (10002, 'Third Garden'), (10003, 'Unrelated Garden');
            INSERT INTO Grades (GradeId, Name) VALUES (10000, 'Test Grade');
            INSERT INTO Sheds (ShedId, Name) VALUES (10000, 'Test Shed');
            INSERT INTO Blends (BlendId, Name) VALUES (10000, 'Test Blend');");
    }
    var repo = new SaleRepository(factory);
    AuctionEntry Lot(string lotNo) => new()
    {
        BrandId = 10000, BrokerId = 10000, GardenId = 10000, GradeId = 10000,
        LotNo = lotNo, InvoiceNo = "", NumberOfBags = 10, TotalKgs = 500, PricePerKg = 0
    };
    Sale Upload(params AuctionEntry[] entries) => new()
    {
        SaleNo = " Test-20 ", SaleDate = new DateTime(2026, 9, 14), Entries = entries.ToList()
    };

    var saleId = await repo.AddAsync(Upload(Lot("L001"), Lot("L001")));
    var saved = (await repo.GetWithEntriesAsync(saleId))!;
    Check(saved.Entries.Count == 1, "Duplicate rows in one upload create one lot");
    var originalId = saved.Entries.Single().EntryId;
    await repo.AddAsync(Upload(Lot(" l001 ")));
    saved = (await repo.GetWithEntriesAsync(saleId))!;
    Check(saved.Entries.Count == 1, "Repeat upload ignores case and surrounding whitespace");

    var completed = Lot("L001");
    completed.InvoiceNo = "INV-1";
    completed.PricePerKg = 250;
    completed.ShedId = 10000;
    completed.BlendId = 10000;
    completed.NumberOfBags = 99;
    completed.TotalKgs = 900;
    await repo.AddAsync(Upload(completed, Lot("L002")));
    saved = (await repo.GetWithEntriesAsync(saleId))!;
    var filled = saved.Entries.Single(x => x.EntryId == originalId);
    Check(saved.Entries.Count == 2, "Mixed upload adds only the new lot");
    Check(filled.InvoiceNo == "INV-1" && filled.PricePerKg == 250 && filled.ShedId == 10000 && filled.BlendId == 10000,
        "Repeat upload fills missing text, numeric and lookup fields");
    Check(filled.NumberOfBags == 10 && filled.TotalKgs == 500 && filled.Value == 125000,
        "Existing values are preserved and total is recalculated");
    await repo.AddAsync(Upload(completed, Lot("L002")));
    Check((await repo.GetWithEntriesAsync(saleId))!.Entries.Count == 2, "Completed repeat upload remains idempotent");

    var otherBroker = Lot("L001");
    otherBroker.BrokerId = 10001;
    await repo.AddAsync(Upload(otherBroker));
    Check((await repo.GetWithEntriesAsync(saleId))!.Entries.Count == 3, "Same lot number from a different broker stays separate");

    var secondSale = Upload(Lot("L001"));
    secondSale.SaleNo = "Test-21";
    var secondId = await repo.AddAsync(secondSale);
    Check(secondId != saleId && (await repo.GetWithEntriesAsync(secondId))!.Entries.Count == 1,
        "Different sales keep their own lots");

    var incomplete = Lot("");
    Check(AuctionEntryMerge.FindMatch(new[] { incomplete }, Lot("")) == incomplete,
        "Identical incomplete rows are skipped");
    var missingBroker = Lot("L100");
    missingBroker.BrokerId = null;
    missingBroker.BrandId = null;
    var resolved = Lot("L100");
    Check(AuctionEntryMerge.FindMatch(new[] { missingBroker }, resolved) == missingBroker &&
          AuctionEntryMerge.FillMissing(missingBroker, resolved) && missingBroker.BrokerId == 10000 && missingBroker.BrandId == 10000,
        "Unique lot can fill missing broker and brand");

    var first = Lot("L200");
    var second = Lot("L200");
    second.BrokerId = 10001;
    var ambiguous = Lot("L200");
    ambiguous.BrokerId = null;
    Check(AuctionEntryMerge.FindMatch(new[] { first, second }, ambiguous) == null,
        "Ambiguous lot does not modify an arbitrary broker's entry");

    var duplicateCompletion = Lot("L300");
    duplicateCompletion.InvoiceNo = "NEW-INVOICE";
    var duplicateUpload = Upload(Lot("L300"), duplicateCompletion);
    duplicateUpload.SaleNo = "Test-22";
    var duplicateId = await repo.AddAsync(duplicateUpload);
    var duplicateSaved = (await repo.GetWithEntriesAsync(duplicateId))!;
    Check(duplicateSaved.Entries.Count == 1 && duplicateSaved.Entries[0].InvoiceNo == "NEW-INVOICE",
        "Later duplicate in the same upload fills the newly inserted row");

    var companyRepo = new CompanyRepository(factory);
    var company = new Company { Name = " Test Company ", GardenIds = new() { 10000, 10001, 10002 } };
    company.CompanyId = await companyRepo.SaveAsync(company);
    var companySaved = (await companyRepo.GetAllAsync()).Single();
    Check(companySaved.Name == "Test Company" && companySaved.GardenIds.Count == 3 && companySaved.GardenNames.Contains("Account Garden"),
        "Company stores and lists multiple linked gardens");
    bool overlapRejected = false;
    try { await companyRepo.SaveAsync(new Company { Name = "Other Company", GardenIds = new() { 10001 } }); }
    catch (InvalidOperationException) { overlapRejected = true; }
    Check(overlapRejected && (await companyRepo.GetAllAsync()).Count == 1,
        "Garden cannot silently move to a second company");
    bool duplicateCompanyRejected = false;
    try { await companyRepo.SaveAsync(new Company { Name = " test company ", GardenIds = new() { 10003 } }); }
    catch (InvalidOperationException) { duplicateCompanyRejected = true; }
    Check(duplicateCompanyRejected, "Duplicate company names are rejected ignoring case and whitespace");
    Check((await repo.GetWithEntriesAsync(saleId))!.Entries.All(x => x.AccountGardenId == x.GardenId),
        "Entries without an explicit account default to their garden");
    var crossAccount = Lot("ACCOUNT-1");
    crossAccount.AccountGardenId = 10001;
    var accountUpload = Upload(crossAccount);
    accountUpload.SaleNo = "Account-Sale";
    var accountSaleId = await repo.AddAsync(accountUpload);
    var accountSale = (await repo.GetWithEntriesAsync(accountSaleId))!;
    Check(accountSale.Entries.Single().AccountGardenId == 10001, "Different selling account survives saving and loading");
    var reportRepo = new ReportRepository(factory);
    var report = (await reportRepo.GetReportEntriesAsync(new ReportFilter { SaleId = accountSaleId })).Single();
    Check(report.GardenName == "Test Garden" && report.AccountName == "Account Garden" &&
        report.GardenDisplay == "Test Garden acc Account Garden", "Reports combine garden and different account in the required order");
    Check((string)ReportTemplateValueResolver.Value(report, "GardenName") == "Test Garden acc Account Garden",
        "Existing export templates resolve the combined garden column");
    var layout = new ReportTemplateLayout
    {
        Columns = new() { new ReportColumnDefinition { Field = "GardenName", Title = "Garden", Width = 220, IsVisible = true } }
    };
    var renderContext = new ReportRenderContext { ReportType = "AllEntries", ReportTitle = "Account test" };
    using (var workbook = new XLWorkbook(new MemoryStream(new ExcelExporter().ExportTemplate(layout, renderContext, new() { report }))))
        Check(workbook.Worksheets.First().CellsUsed().Any(x => x.GetString() == "Test Garden acc Account Garden"),
            "Excel report writes the combined garden and account into one column");
    using (var document = WordprocessingDocument.Open(new MemoryStream(new WordExporter().ExportTemplate(layout, renderContext, new() { report })), false))
        Check(document.MainDocumentPart!.Document!.InnerText.Contains("Test Garden acc Account Garden"),
            "Word report writes the combined garden and account");
    var pdf = new PdfExporter().ExportTemplate(layout, renderContext, new() { report });
    Check(System.Text.Encoding.ASCII.GetString(pdf, 0, 4) == "%PDF", "PDF export supports the account-aware report data");
    if (args.Contains("--pdf-footer"))
    {
        layout.FooterElements = new()
        {
            new() { ElementType = "Line" },
            new() { Content = "Prepared By: {{PreparedBy}}     Checked By: {{CheckedBy}}     Approved By: {{ApprovedBy}}" },
            new() { Content = "Keep this footer note" },
            new() { Content = "Page {{PageNumber}} of {{TotalPages}}", Alignment = "Center" }
        };
        renderContext.PreparedBy = "PREPARER";
        renderContext.CheckedBy = "CHECKER";
        renderContext.ApprovedBy = "APPROVER";
        string directory = Path.Combine(Environment.CurrentDirectory, "tmp", "pdfs");
        Directory.CreateDirectory(directory);
        foreach (string reportType in new[] { "AllEntries", "BrandWise", "BrokerWise" })
        {
            renderContext.ReportType = reportType;
            File.WriteAllBytes(Path.Combine(directory, $"Footer-{reportType}.pdf"),
                new PdfExporter().ExportTemplate(layout, renderContext, new() { report }));
        }
    }
    var sameReport = (await reportRepo.GetReportEntriesAsync(new ReportFilter { SaleId = secondId })).Single();
    Check(sameReport.GardenDisplay == "Test Garden", "Reports show only garden when garden and account match");
    var dateContext = new ReportRenderContext { SaleDate = new DateTime(2026, 11, 1), DateFormat = "yyyy-MM-dd" };
    Check(ReportTemplateValueResolver.Header("{{SaleDate}}", dateContext) == "2026-11-01",
        "Report header dates use the preferred date format");
    var accountEntry = accountSale.Entries.Single();
    accountEntry.AccountGardenId = 10002;
    await repo.UpdateLotAsync(accountEntry, accountSale.SaleNo, accountSale.SaleDate);
    Check((await repo.GetWithEntriesAsync(accountSaleId))!.Entries.Single().AccountGardenId == 10002,
        "Lot editing updates the account");
    accountSale.Entries[0].AccountGardenId = 10001;
    await repo.UpdateAsync(accountSale);
    Check((await repo.GetWithEntriesAsync(accountSaleId))!.Entries.Single().AccountGardenId == 10001,
        "Editing a whole sale preserves explicit accounts");
    company.GardenIds.Remove(10002);
    await companyRepo.SaveAsync(company);
    Check((await companyRepo.GetAllAsync()).Single().GardenIds.Count == 2, "Company garden assignments can be edited");
    await companyRepo.DeleteAsync(company.CompanyId);
    Check((await companyRepo.GetAllAsync()).Count == 0 &&
        (await repo.GetWithEntriesAsync(accountSaleId))!.Entries.Single().AccountGardenId == 10001,
        "Deleting a company preserves historical sale accounts");

    using (var connection = factory.CreateConnection())
        await connection.ExecuteAsync(@"
            DROP TABLE CompanyGardens;
            DROP TABLE Companies;
            ALTER TABLE AuctionEntries DROP COLUMN AccountGardenId;
            DELETE FROM SchemaMigrations WHERE Version = 10;");
    await new MigrationRunner(factory).RunMigrationsAsync();
    var upgraded = (await repo.GetWithEntriesAsync(saleId))!;
    Check(upgraded.Entries.Count == 3 && upgraded.Entries.All(x => x.AccountGardenId == x.GardenId),
        "Upgrading existing data keeps entries and defaults account to garden");
    await new MigrationRunner(factory).RunMigrationsAsync();
    Check((await repo.GetWithEntriesAsync(saleId))!.Entries.Count == 3, "Migration safely runs again without changing existing entries");
}
finally
{
    SqliteConnection.ClearAllPools();
    if (File.Exists(databasePath)) File.Delete(databasePath);
}

sealed class TestDatabaseFactory(string path) : IDatabaseFactory
{
    public SqliteConnection CreateConnection() => new($"Data Source={path};Foreign Keys=True");
}
