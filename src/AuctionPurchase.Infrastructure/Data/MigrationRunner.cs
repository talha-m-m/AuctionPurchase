using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dapper;

namespace AuctionPurchase.Infrastructure.Data
{
    public class MigrationRunner
    {
        private readonly IDatabaseFactory _dbFactory;

        public MigrationRunner(IDatabaseFactory dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task RunMigrationsAsync()
        {
            using var connection = _dbFactory.CreateConnection();
            await connection.OpenAsync();

            // Create Migrations table if not exists
            var createMigrationsTable = @"
                CREATE TABLE IF NOT EXISTS SchemaMigrations (
                    Version INTEGER PRIMARY KEY,
                    AppliedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                );";
            await connection.ExecuteAsync(createMigrationsTable);

            var currentVersion = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT MAX(Version) FROM SchemaMigrations") ?? 0;

            var migrations = GetMigrations();

            foreach (var migration in migrations)
            {
                if (migration.Key > currentVersion)
                {
                    using var transaction = connection.BeginTransaction();
                    try
                    {
                        await connection.ExecuteAsync(migration.Value, transaction: transaction);
                        await connection.ExecuteAsync(
                            "INSERT INTO SchemaMigrations (Version) VALUES (@Version)",
                            new { Version = migration.Key },
                            transaction: transaction);
                        
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception($"Failed to apply migration {migration.Key}", ex);
                    }
                }
            }
        }

        private SortedDictionary<int, string> GetMigrations()
        {
            return new SortedDictionary<int, string>
            {
                { 1, @"
                    CREATE TABLE IF NOT EXISTS Brands (
                        BrandId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        ShortName TEXT,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        SortOrder INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS Brokers (
                        BrokerId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        ShortName TEXT,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        SortOrder INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS Gardens (
                        GardenId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        ShortName TEXT,
                        Address TEXT,
                        Division TEXT,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        SortOrder INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS Grades (
                        GradeId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        SortOrder INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS Sheds (
                        ShedId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Location TEXT,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        SortOrder INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS Blends (
                        BlendId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        SortOrder INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS Sales (
                        SaleId INTEGER PRIMARY KEY AUTOINCREMENT,
                        SaleNo TEXT NOT NULL,
                        SaleDate DATETIME NOT NULL,
                        Notes TEXT,
                        CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );
                    CREATE INDEX IX_Sales_SaleNo ON Sales(SaleNo);
                    CREATE INDEX IX_Sales_SaleDate ON Sales(SaleDate);

                    CREATE TABLE IF NOT EXISTS AuctionEntries (
                        EntryId INTEGER PRIMARY KEY AUTOINCREMENT,
                        SaleId INTEGER NOT NULL,
                        BrandId INTEGER NOT NULL,
                        BrokerId INTEGER NOT NULL,
                        LotNo TEXT NOT NULL,
                        GardenId INTEGER NOT NULL,
                        InvoiceNo TEXT NOT NULL,
                        GradeId INTEGER NOT NULL,
                        NumberOfBags INTEGER NOT NULL,
                        TotalKgs DECIMAL(18,2) NOT NULL,
                        PricePerKg DECIMAL(18,2) NOT NULL,
                        Value DECIMAL(18,2) NOT NULL,
                        ShedId INTEGER,
                        BlendId INTEGER,
                        CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (SaleId) REFERENCES Sales(SaleId) ON DELETE CASCADE,
                        FOREIGN KEY (BrandId) REFERENCES Brands(BrandId),
                        FOREIGN KEY (BrokerId) REFERENCES Brokers(BrokerId),
                        FOREIGN KEY (GardenId) REFERENCES Gardens(GardenId),
                        FOREIGN KEY (GradeId) REFERENCES Grades(GradeId),
                        FOREIGN KEY (ShedId) REFERENCES Sheds(ShedId),
                        FOREIGN KEY (BlendId) REFERENCES Blends(BlendId)
                    );
                    CREATE INDEX IX_AuctionEntries_SaleId ON AuctionEntries(SaleId);
                    CREATE INDEX IX_AuctionEntries_BrokerId ON AuctionEntries(BrokerId);
                    CREATE INDEX IX_AuctionEntries_BrandId ON AuctionEntries(BrandId);
                    CREATE INDEX IX_AuctionEntries_GardenId ON AuctionEntries(GardenId);
                    CREATE INDEX IX_AuctionEntries_LotNo ON AuctionEntries(LotNo);
                    CREATE INDEX IX_AuctionEntries_InvoiceNo ON AuctionEntries(InvoiceNo);
                " },
                { 2, @"
                    WITH supplied(Name, SortOrder) AS (
                        VALUES
                        ('Monipur', 1001), ('Saif', 1002), ('Sagurnal', 1003), ('Maitri', 1004),
                        ('Baraoora', 1005), ('Popular', 1006), ('Sallylunn', 1007), ('Hamidia', 1008),
                        ('M.M Tea', 1009), ('Rema', 1010), ('Bangla', 1011), ('Mr Khan A/c dak', 1012),
                        ('Haldavalley', 1013), ('Champarai', 1014), ('Etah', 1015), ('Newsamanbagh', 1016),
                        ('Chatlapore', 1017), ('ZH Tea', 1018), ('Far-East', 1019), ('Chundeecherra', 1020),
                        ('Elite Global', 1021), ('Talma', 1022), ('Madabpore', 1023), ('Burjan', 1024),
                        ('Naheed', 1025), ('Sazeda', 1026), ('Bismillah', 1027), ('Parkul', 1028),
                        ('Jagadishpur', 1029), ('Brindabon', 1030), ('Fabiha', 1031), ('Surma & Purnim', 1032),
                        ('Khan', 1033), ('Chhotalekha', 1034), ('Oodaleha', 1035), ('Chandpur Belgaon', 1036),
                        ('Balisera', 1037), ('Marina', 1038), ('Moulvi', 1039), ('Rajghat', 1040),
                        ('Allynugger', 1041), ('Luskerpore', 1042), ('Shumshernugger', 1043), ('Luayuni', 1044),
                        ('Lungla', 1045), ('TripleZed', 1046), ('New Samnabagh', 1047), ('Baramasia', 1048),
                        ('Oodaleah', 1049), ('Euroasia', 1050), ('Supreme', 1051), ('Mega', 1052),
                        ('Surma & Purnima', 1053), ('Faz-east', 1054), ('Royal', 1055), ('Ranga A/c Eln', 1056),
                        ('Ranagapani', 1057), ('Kornajora', 1058), ('Mazdehee', 1059), ('Kapnapahar', 1060),
                        ('Clonal', 1061), ('Nahar', 1062), ('Kodala', 1063), ('Julekhanagar', 1064),
                        ('Nalua', 1065), ('Srigobindpur', 1066), ('Kurmah', 1067), ('Panchaboti', 1068),
                        ('Karnafuli', 1069), ('Rashidpur', 1070), ('Surma', 1071), ('Chandpore', 1072),
                        ('Junglebari', 1073), ('Silloah', 1074), ('Deanston', 1075), ('Mertinga', 1076),
                        ('Habibnagar', 1077), ('Dilkusha', 1078), ('Karimpore', 1079), ('New samanbagh', 1080),
                        ('Moominchera', 1081), ('Baikunthapur', 1082), ('Sonarupa', 1083), ('Chandbagh', 1084),
                        ('Phulbari', 1085), ('Daragaon', 1086), ('Baha A/c Atia', 1087), ('Noyapara', 1088)
                    )
                    INSERT INTO Gardens (Name, IsActive, SortOrder)
                    SELECT Name, 1, SortOrder
                    FROM supplied
                    WHERE NOT EXISTS (
                        SELECT 1 FROM Gardens WHERE lower(trim(Gardens.Name)) = lower(trim(supplied.Name))
                    );
                " },
                { 3, @"
                    -- Earlier versions allowed a sale number to be saved more than once.
                    -- Move all lots onto the oldest matching sale before enforcing one sale per number.
                    WITH canonical AS (
                        SELECT MIN(SaleId) AS KeepSaleId, lower(trim(SaleNo)) AS NormalizedSaleNo
                        FROM Sales
                        GROUP BY lower(trim(SaleNo))
                    )
                    UPDATE AuctionEntries
                    SET SaleId = (
                        SELECT canonical.KeepSaleId
                        FROM Sales duplicateSale
                        JOIN canonical ON canonical.NormalizedSaleNo = lower(trim(duplicateSale.SaleNo))
                        WHERE duplicateSale.SaleId = AuctionEntries.SaleId
                    )
                    WHERE SaleId NOT IN (
                        SELECT KeepSaleId FROM canonical
                    );

                    DELETE FROM Sales
                    WHERE SaleId NOT IN (
                        SELECT MIN(SaleId)
                        FROM Sales
                        GROUP BY lower(trim(SaleNo))
                    );

                    CREATE UNIQUE INDEX IF NOT EXISTS IX_Sales_NormalizedSaleNo
                    ON Sales(lower(trim(SaleNo)));
                " },
                { 4, @"
                    CREATE TABLE IF NOT EXISTS ReportTemplates (
                        TemplateId INTEGER PRIMARY KEY AUTOINCREMENT,
                        TemplateName TEXT NOT NULL,
                        ReportType TEXT NOT NULL,
                        Version TEXT NOT NULL DEFAULT '1.0',
                        TemplateDefinition TEXT NOT NULL,
                        IsDefault INTEGER NOT NULL DEFAULT 0,
                        CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );
                    CREATE INDEX IF NOT EXISTS IX_ReportTemplates_Type ON ReportTemplates(ReportType);
                " },
                { 5, @"
                    ALTER TABLE Gardens ADD COLUMN FullName TEXT;
                    ALTER TABLE Gardens ADD COLUMN District TEXT;
                    ALTER TABLE Gardens ADD COLUMN Acre REAL;
                    ALTER TABLE Gardens ADD COLUMN GoogleAddressLink TEXT;
                    UPDATE Gardens SET FullName = ShortName, District = Division;
                    
                    ALTER TABLE Brokers ADD COLUMN FullName TEXT;
                    ALTER TABLE Brokers ADD COLUMN Address TEXT;
                    UPDATE Brokers SET FullName = ShortName;
                    
                    ALTER TABLE Sheds ADD COLUMN Address TEXT;
                    UPDATE Sheds SET Address = Location;
                " },
                { 6, @"
ALTER TABLE Gardens ADD COLUMN GardenType TEXT;
ALTER TABLE Gardens RENAME COLUMN GoogleAddressLink TO MapLink;

CREATE TEMP TABLE SeedGardens (Name TEXT, FullName TEXT, Address TEXT, District TEXT, Acre REAL, GardenType TEXT);
INSERT INTO SeedGardens (Name, FullName, Address, District, Acre, GardenType) VALUES
('Allynugger', 'আলীনগর চা বাগান', 'ডাকঘরঃ শমশেরনগর | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার।', 'মৌলভীবাজার', 4462.88, 'Tea Estate'),
('Ameenabad', 'আমীনাবাদ চা বাগান', 'ডাকঘরঃ বরমচাল | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার।', 'মৌলভীবাজার', 745.15, 'Tea Estate'),
('Amrail', 'আমরাইল চা বাগান', 'ডাককঘরঃ সাতগাঁও | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার।', 'মৌলভীবাজার', 3853.16, 'Tea Estate'),
('Atiabagh', 'আতিয়াবাগ চা বাগান', 'ডাকঘরঃ জুরী | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 732.15, 'Tea Estate'),
('Balisera', 'বালিশেরা চা বাগান', 'ডাকঘরঃ কালীঘাট | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 8652.69, 'Tea Estate'),
('Baraoora', 'ভাড়াউড়া চা বাগান', 'ডাকঘরঃ শ্রীমঙ্গল | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2601.84, 'Tea Estate'),
('BTRI', 'বিটিআরআই চা বাগান', 'ডাকঘরঃ শ্রীমঙ্গল | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 476.15, 'Tea Estate'),
('Chandbagh', 'চান্দভাগ চা বাগান', 'ডাকঘরঃ ইন্দেশর | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1043.7, 'Tea Estate'),
('Chatlapore', 'চাতলাপুর চা বাগান', 'ডাকঘরঃ শমশের নগর | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3150.85, 'Tea Estate'),
('Clevdon', 'ক্লিভডন চা বাগান', 'ডাকঘরঃ রাঙ্গিরকুল | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1503.79, 'Tea Estate'),
('Clonal', 'ক্লোনাল চা বাগান', 'ডাকঘরঃ ভূুনবীর | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 399.05, 'Tea Estate'),
('Dewrachora', 'দেওড়াছড়া চা বাগান', 'ডাকঘরঃ মুন্সিবাজার | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1868.35, 'Tea Estate'),
('Deanston', 'ডিনষ্টোন টি এস্টেট', 'ডাকঘরঃ খেজুরীছড়া | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 8013.95, 'Tea Estate'),
('Dhamai', 'ধামাই চা বাগান', 'ডাকঘরঃ জুরী | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজর', 'মৌলভীবাজার', 2582.07, 'Tea Estate'),
('Dildarpur', 'দিলদারপুর চা বাগান', 'ডাকঘরঃ রাঙ্গিরকুল | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজর', 'মৌলভীবাজার', 765.27, 'Tea Estate'),
('Dinarpur', 'দিনারপুর চা বাগান', 'ডাকঘরঃ শমসের নগর | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 999.62, 'Tea Estate'),
('Etah', 'ইটা চা বাগান', 'ডাকঘরঃ করিমপুর | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3188.03, 'Tea Estate'),
('Ghazipore', 'গাজীপুর চা বাগান', 'ডাকঘরঃ কুলাউড়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2056.84, 'Tea Estate'),
('Hafiz', 'হাফিজ চা বাগান', 'ডাকঘরঃ পূর্বশাহবাজপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 132.88, 'Tea Estate'),
('Hajinagar', 'হাজীনগর চা বাগান', 'ডাকঘরঃ কাজলধারা | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 449.35, 'Tea Estate'),
('Hossainabad', 'হোসেনাবাদ চা বাগান', 'ডাকঘরঃ কালিঘাট | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 999.43, 'Tea Estate'),
('Jhemai', 'ঝিমাই চা বাগান', 'ডাকঘরঃ কুলাউড়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 661.55, 'Tea Estate'),
('Julekhanagar', 'জুলেখানগর চা বাগান', 'ডাকঘরঃ খেজুড়ীছড়া | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 861.26, 'Tea Estate'),
('Junglebari', 'জঙ্গলবাড়ী চা বাগান', 'ডাকঘরঃ কালিঘাট | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1000, 'Tea Estate'),
('Karimpore', 'করিমপুর চা বাগান', 'ডাকঘরঃ করিমপুর | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজর', 'মৌলভীবাজার', 3347.15, 'Tea Estate'),
('Kurmah', 'কুরমা চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3152.19, 'Tea Estate'),
('Luayuni-Holicherra', 'লুয়াইউনি-হলিছড়া চা বাগান', 'ডাকঘরঃ কাজলধারা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2039.02, 'Tea Estate'),
('Madanmohanpur', 'মদনমোহনপুর চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 996.7, 'Tea Estate'),
('Marina', 'মেরিনা চা বাগান', 'ডাকঘরঃ কুলাউড়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 872.24, 'Tea Estate'),
('Mazdehee', 'মাজদিহি চা বাগান', 'ডাকঘরঃ নারায়নছেড়া | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3022.63, 'Tea Estate'),
('Mertinga', 'মিরতিংগা চা বাগান', 'ডাকঘরঃ মুন্সিবাজার | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2478.91, 'Tea Estate'),
('Mirzapur', 'মির্জাপুর চা বাগান', 'ডাকঘরঃ শমশেরনগর | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2406.06, 'Tea Estate'),
('New Samanbagh', 'নিউ সমনবাগ চা বাগান', 'ডাকঘরঃ দক্ষিণবাগ | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3528.82, 'Tea Estate'),
('Nurjahan', 'নুরজাহান চা বাগান', 'ডাকঘরঃ শ্রীমঙ্গল | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 738.27, 'Tea Estate'),
('Pallakandi', 'পাল্লাকান্দি চা বাগান', 'ডাকঘরঃ লংলা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 123.68, 'Tea Estate'),
('Patrakhola', 'পাত্রখোলা চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 4587.28, 'Tea Estate'),
('Phultala', 'ফুলতলা চা বাগান', 'ডাকঘরঃ ফুলতলা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2636.55, 'Tea Estate'),
('Phulbari', 'ফুলবাড়ি চা বাগান', 'ডাকঘরঃ কেরামতনগর | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1514.76, 'Tea Estate'),
('Premnagar', 'প্রেমনগর চা বাগান', 'ডাকঘরঃ নরায়ণছেড়া | থানাঃ মৌলভীবাজার | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 971.07, 'Tea Estate'),
('Rajghat', 'রাজঘাট চা বাগান', 'ডাকঘরঃ খেজুরীছড়া | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 5816.77, 'Tea Estate'),
('Rajkie', 'রাজকী চা বাগান', 'ডাকঘরঃ ফুলতলা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2015.91, 'Tea Estate'),
('Rajnagar', 'রাজনগর টি এস্টেট', 'ডাকঘরঃ লংলা | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2412.8, 'Tea Estate'),
('Rashidabad', 'রশীদাবাদ চা বাগান', 'ডাকঘরঃ দক্ষিণবাগ | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 400.09, 'Tea Estate'),
('Rehana', 'রেহানা চা বাগান', 'ডাকঘরঃ কুলাউড়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 464.46, 'Tea Estate'),
('Sagornal', 'সাগরনাল চা বাগান', 'ডাকঘরঃ সাগরনাল | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1800.61, 'Tea Estate'),
('Saif', 'সাইফ টি এস্টেট', 'ডাকঘরঃ কুলাউড়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 937.09, 'Tea Estate'),
('Sathgao', 'সাতগাঁও চা বাগান', 'ডাকঘরঃ সাতগাঁও | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3078.85, 'Tea Estate'),
('Shamshernagar', 'শমশেরনগর চা বাগান', 'ডাকঘরঃ কমলগঞ্জ | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 4326.47, 'Tea Estate'),
('Silloah', 'সিলুয়া চা বাগান', 'ডাকঘরঃ জুরী | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2474.67, 'Tea Estate'),
('Srigobindapur', 'শ্রীগোবিন্দপুর চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 882.07, 'Tea Estate'),
('Amo', 'অহিদাবাদ বা আমু চা বাগান', 'ডাকঘরঃ শ্রীধরপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 742.8, 'Tea Estate'),
('Zareen', 'জেরিন চা বাগান', 'ডাকঘরঃ শ্রীমঙ্গল | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 681.86, 'Tea Estate'),
('Champarai', 'চম্পারায় চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1996.31, 'Tea Estate'),
('Dilkusha', 'দিলকুশা চা বাগান', 'ডাকঘরঃ সিলোয়াবাজার | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1039.6, 'Tea Estate'),
('Dhalai', 'দলই চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1200.9, 'Tea Estate'),
('Kapnapahar', 'কাপনাপাহাড় চা বাগান', 'ডাকঘরঃ জুরী | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1925.85, 'Tea Estate'),
('Lungla', 'লংলা চা বাগান', 'ডাকঘরঃ লংলা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3202.76, 'Tea Estate'),
('Utttorbagh & Indanagar', 'উত্তরবাগ-ইন্দোনগর', 'ইন্দোনগর | ডাকঘরঃ ইন্দেশর | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3577.88, 'Tea Estate'),
('Pallathal', 'পাল্লাথল চা বাগান', 'ডাকঘরঃ পূর্বশাহবাজপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 795.93, 'Tea Estate'),
('Ratna', 'রত্না চা বাগান', 'ডাকঘরঃ জুরী | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1703.66, 'Tea Estate'),
('Sonarupa', 'সোনারূপা চা বাগান', 'ডাকঘরঃ দক্ষিণবাগ | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2502.23, 'Tea Estate'),
('Bahadurpur', 'বাহাদুরপুর চা বাগান', 'ডাকঘরঃ বড়লেখা | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 631.02, 'Tea Estate'),
('Chotolekha', 'ছোটলেখা চা বাগান', 'ডাকঘরঃ বড়লেখা | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1964.5, 'Tea Estate'),
('Fatehbagh', 'ফতেহবাগ চা বাগান', 'ডাকঘরঃ পূর্বশাহবাজপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 479.99, 'Tea Estate'),
('Hamidia', 'হামিদিয়া চা বাগান', 'ডাকঘরঃ মাথারকাপন | থানাঃ মৌলভীবাজার | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 455.18, 'Tea Estate'),
('Hingazia', 'হিঙ্গাজিয়া চা বাগান', 'ডাকঘরঃ কাজলধারা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2354.19, 'Tea Estate'),
('Islamadad', 'ইসলামাবাদ চা বাগান', 'ডাকঘরঃ সিলোয়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 117.13, 'Tea Estate'),
('Kaliti', 'কালিটি চা বাগান', 'ডাকঘরঃ কুলাউড়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1163.03, 'Tea Estate'),
('M.R. Khan', 'এম আর খান চা বাগান', 'ডাকঘরঃ শ্রীমঙ্গল | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 600, 'Tea Estate'),
('Madabpur', 'মাধবপুর চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2825.03, 'Tea Estate'),
('Moulvi', 'মৌলভী টি এস্টেট', 'ডাকঘরঃ নারয়ণছড়া | থানাঃ মৌলভীবাজার | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1771.62, 'Tea Estate'),
('Nahar', 'নাহার চা বাগান', 'ডাকঘরঃ খেজুরীছড়া | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 864.85, 'Tea Estate'),
('Rungicherra', 'রাঙ্গীছড়া চা বাগান', 'ডাকঘরঃ পৃথিমপাশা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1086.9, 'Tea Estate'),
('Sabazpur', 'শাহবাজপুর চা বাগান', 'ডাকঘরঃ সুজাউল | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2886.11, 'Tea Estate'),
('Patharia', 'পাথারিয়া চা বাগান', 'ডাকঘরঃ দক্ষিণবাগ | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1682.64, 'Tea Estate'),
('Dakshin Gul', 'দক্ষিণগুল চা বাগান', 'ডাকঘরঃ বড়লেখা | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1131.66, 'Tea Estate'),
('Alladad', 'আল্লাদাদ চা বাগান', 'ডাকঃ পূর্বশাহবাজপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 945.48, 'Tea Garden'),
('Borhannagar', 'বোরহাননগর চা বাগান', 'ডাকঘরঃ পৃথিমপাশা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 177.54, 'Tea Garden'),
('Azgarabad', 'আজগরাবাদ চা বাগান', 'ডাকঘরঃ পৃথিমপাশা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1426, 'Tea Garden'),
('Muraichhara', 'মুরাইছড়া চা বাগান', 'ডাকঘরঃ পৃথিমপাশা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 978.34, 'Tea Garden'),
('Mathiura', 'মাথিউড়া চা বাগান', 'ডাকঘরঃ রাজনগর | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1656.55, 'Tea Garden'),
('Nandarani', 'নন্দরাণী চা বাগান', 'ডাকঘরঃ কালিঘাট | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 426.42, 'Tea Garden'),
('Shafinagar', 'শফিনগর চা বাগান', 'ডাকঘরঃ টেংরাবাজার | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 97.96, 'Tea Garden'),
('Kashipur', 'কাশিপুর চা বাগান', 'ডাকঘরঃ লংলা | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 141.58, 'Tea Garden'),
('Lakshichhara', 'লক্ষিছড়া চা বাগান', 'ডাকঘরঃ বড়লেখা | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1096.34, 'Tea Garden'),
('Sirajnagar', 'সিরাজনগর চা বাগান', 'ডাকঘরঃ কাজলধারা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 830.61, 'Tea Garden'),
('Kalikabari', 'কালীকাবাড়ী চা বাগান', 'ডাকঘরঃ পূর্বশাহবাজপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 344.62, 'Tea Garden'),
('Ayeshabagh', 'আয়েশাবাগ চা বাগান', 'ডাকঘরঃ শ্রীধরপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 526.61, 'Tea Garden'),
('Bijoya', 'বিজয়া চা বাগান', 'ডাকঘরঃ রাঙ্গিরকুল | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 755.76, 'Tea Garden'),
('Kumarshail', 'কুমারশাইল চা বাগান', 'ডাকঘরঃ শ্রীধরপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 985.79, 'Tea Garden'),
('Rahmania', 'রহমানিয়া চা বাগান', 'ডাকঘরঃ বড়লেখা | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 347.68, 'Tea Garden'),
('Baikunthapur', 'বৈকন্ঠপুর চা বাগান', 'ডাকঘরঃ ইটাখোলা | থানাঃ মাধবপুর | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 922.59, 'Tea Estate'),
('Parkul-Nasimabad', 'পারকুল-নাছিমাবাদ চা বাগান', 'ডাকঘরঃ রশিদপুর | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2531.75, 'Tea Estate'),
('Jagadishpur', 'জগদীশপুর চা বাগান', 'ডাকঘরঃ ইটাখোলা | থানাঃ মাধবপুর | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1489.04, 'Tea Estate'),
('Surma', 'সুরমা চা বাগান', 'ডাকঘরঃ ইটাখোলা | থানাঃ মাধবপুর | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 4454.39, 'Tea Estate'),
('Chaklapunji', 'চাকলাপুঞ্জি চা বাগান', 'ডাকঘরঃ চাঁদপুর | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1753.79, 'Tea Estate'),
('Teliapara', 'তেলিয়াপাড়া চা বাগান', 'ডাকঘরঃ ইটাখোলা | থানাঃ মাধবপুর | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2601.28, 'Tea Estate'),
('Rema', 'রেমা চা বাগান', 'ডাকঘরঃ গাজীপুর | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2114.61, 'Tea Estate'),
('Noyapara', 'নোয়াপাড়া চা বাগান', 'ডাকঘরঃ ইটাখোলা | থানাঃ মাধবপুর | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1701.07, 'Tea Estate'),
('Chundeecherra', 'চন্ডিছড়া চা বাগান', 'ডাকঘরঃ চাঁদপুরবাগান | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1960.43, 'Tea Estate'),
('Sreebari', 'শ্রীবাড়ী চা বাগান', 'ডাকঘরঃ রশিদপুর | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1649.12, 'Tea Estate'),
('Lalchand', 'লালচান্দ চা বাগান', 'ডাকঘরঃ শাহাজীবাজার | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2462.28, 'Tea Estate'),
('Brindabon', 'বৃন্দবন চা বাগান', 'ডাকঘরঃ পুটিজুরী | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1192.54, 'Tea Estate'),
('Chandpur', 'চাঁদপুর চা এস্টেট', 'ডাকঘরঃ চাঁদপুর | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 3949.49, 'Tea Estate'),
('Daragaon', 'দারাগাঁও চা বাগান', 'ডাকঘরঃ রশিদপুর | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2347.41, 'Tea Estate'),
('Imam', 'ইমাম চা বাগান', 'ডাকঘরঃ ফটিজুরী | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1148, 'Tea Estate'),
('Rashidpur', 'রশিদপুর চা বাগান', 'ডাকঘরঃ রশিদপুর | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 5574.3, 'Tea Estate'),
('Deundi', 'ডেউন্দি চা বাগান', 'ডাকঘরঃ চাঁদপুরবাগান | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2812.72, 'Tea Estate'),
('Luskerpore', 'লস্করপুর চা বাগান', 'ডাকঘরঃ চাঁদপুরবাগান | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2827.28, 'Tea Estate'),
('Amu', 'আমু চা বাগান', 'ডাকঘরঃ চাঁদপুর বাগান | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2505.27, 'Tea Estate'),
('Nalua', 'নলুয়া চা বাগান', 'ডাকঘরঃ চাঁদপুরবাগান | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 3063.03, 'Tea Estate'),
('Amtali', 'আমতলী চা বাগান', 'ডাকঘরঃ রশিদপুর | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 235.23, 'Tea Estate'),
('Madhupur', 'মধুপুর চা বাগান', 'ডাকঘরঃ বাহুবল | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1165.24, 'Tea Estate'),
('Bawani', 'বাওয়ানী চা বাগান', 'ডাকঘরঃ পুটিজুরী | থানাঃ হবিগঞ্জ সদর | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1179, 'Tea Garden'),
('Chaltachhara', 'চালতাছড়া চা বাগান', 'ডাকঘর: রশিদপুর | উপজেলা: বাহুবল | জেলা: হবিগঞ্জ।', 'হবিগঞ্জ', 63.56, 'Tea Garden'),
('Boban', 'ববান চা বাগান', 'পানিউমদা | উপজেলা: নবীগঞ্জ | জেলা: হবিগঞ্জ।', 'হবিগঞ্জ', 208.52, 'Tea Garden'),
('Alibahar', 'আলীবাহার চা বাগান', 'ডাকঘরঃ আখালিয়া | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 683.5, 'Tea Estate'),
('Lovachhara', 'লোভাছড়া চা বাগান', 'ডাকঘরঃ কানাইঘাট | থানাঃ কানাইঘাট | জেলাঃ সিলেট', 'সিলেট', 1839, 'Tea Estate'),
('Burjan', 'বুরজান চা বাগান', 'ডাকঘরঃ খাদিমনগর | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 3263.31, 'Tea Estate'),
('Khadim', 'খাদিম চা বাগান', 'ডাকঘরঃ খাদিমনগর | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 3629.6, 'Tea Estate'),
('Lakkatura', 'লাক্কাতুরা চা বাগান', 'ডাকঘরঃ সিলেট | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 3198.05, 'Tea Estate'),
('Lalakhal', 'লালাখাল চা বাগান', 'ডাকঘরঃ জৈন্তাপুর | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 1317.34, 'Tea Estate'),
('Monipur', 'মনিপুর চা বাগান', 'ডাকঘরঃ ফার্টিলাইজার ফ্যাক্টরী | থানাঃ ফেঞ্চুগঞ্জ | জেলাঃ সিলেট', 'সিলেট', 1694.69, 'Tea Estate'),
('Jaflong', 'জাফলং চা বাগান', 'ডাকঃ জাফলং | থানাঃ গোয়াইনঘাট | জেলাঃ সিলেট', 'সিলেট', 2164.14, 'Tea Estate'),
('Afifanagar', 'আফিফানগর চা বাগান', 'ডাকঘরঃ জৈন্তাপুর | থানাঃ জৈন্তাপুর | জেলাঃ সিলেট', 'সিলেট', 868.96, 'Tea Estate'),
('Habibnagar', 'হাবিবনগর চা বাগান', 'ডাকঘরঃ চিকনাগুল | থানাঃ গোয়াইনঘাট | জেলাঃ সিলেট', 'সিলেট', 1583.35, 'Tea Estate'),
('Moomincherra', 'মোমিনছড়া চা বাগান', 'ডাকঘরঃ বাটেরা | থানাঃ ফেঞ্চুগঞ্জ | জেলাঃ সিলেট', 'সিলেট', 986.12, 'Tea Estate'),
('Malnichhara', 'মালনীছড়া চা বাগান', 'ডাকঘরঃ সিলেট | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 2164.14, 'Tea Estate'),
('Dalia', 'ডালিয়া চা বাগান', 'ডাকঘরঃ সিলেট | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 174.77, 'Tea Garden'),
('Star', 'ষ্টার চা বাগান', 'ডাকঘরঃ সিলেট | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 392.96, 'Tea Garden'),
('Sreepur', 'শ্রীপুর চা বাগান', 'ডাকঘরঃ জৈন্তাপুর | থানাঃ জৈন্তাপুর | জেলাঃ সিলেট', 'সিলেট', 1017.52, 'Tea Garden'),
('Fatehpur', 'ফতেহপুর চা বাগান', 'ডাকঘরঃ বারানগর | থানাঃ গোয়াইনঘাট | জেলাঃ সিলেট', 'সিলেট', 521.28, 'Tea Garden'),
('Daluchhara', 'ডালুছড়া চা বাগান', 'ডাকঘরঃ ফেঞ্চুগঞ্জ | থানাঃ ফেঞ্চুগঞ্জ | জেলাঃ সিলেট', 'সিলেট', 325.52, 'Tea Garden'),
('Khan', 'খান চা বাগান', 'ডাকঘরঃ চিকনাগুল | থানাঃ গোয়াইনঘাট | জেলাঃ সিলেট', 'সিলেট', 1578.32, 'Tea Garden'),
('Meghalaya', 'মেঘালয় চা বাগান', 'ডাকঘর: দরবস্ত বাজার | উপজেলা: জৈন্তাপুর | জেলা: সিলেট', 'সিলেট', 1216.92, 'Tea Garden'),
('Naseha', 'নাছেহা চা বাগান', 'ডাকঘরঃ দাঁতমারা | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 528.37, 'Tea Estate'),
('Thandachhari', 'ঠান্ডাছড়ি চা বাগান', 'ডাকঘরঃ ঠান্ডাছড়ি | থানাঃ রাঙ্গুনিয়া | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 1045.03, 'Tea Estate'),
('Dantmara', 'দাঁতমারা চা বাগান', 'ডাকঘরঃ দাঁতমারা | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 452.54, 'Tea Estate'),
('Udalia', 'উদালিয়া চা বাগান', 'ডাকঘরঃ নাজিরহাট | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 2995.72, 'Tea Estate'),
('Elahinoor', 'এলাহীনুর চা বাগান', 'ডাকঘরঃ নাজিরহাট | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 1491.8, 'Tea Estate'),
('Ma Jaan', 'মা জান চা বাগান', 'ডাকঘরঃ দাঁতমারা | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 581.98, 'Tea Estate'),
('Aasia', 'আছিয়া চা বাগান', 'ডাকঘরঃ ভূঁজপুর | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 676.92, 'Tea Estate'),
('Rangapani', 'রাঙ্গাপানি চা বাগান', 'ডাকঘরঃ ফটিকছড়ি | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 2639.2, 'Tea Estate'),
('Chandpur Belgaon', 'চাঁদপুর-বেলগাঁও চা বাগান', 'ডাকঘরঃ মানিকগ্রাম | থানাঃ বাঁশখালী | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 3472.53, 'Tea Estate'),
('Panchaboti', 'পঞ্চবটি চা বাগান', 'ডাকঘরঃ মির্জাপুর | থানাঃ ভুঁজপুর | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 129.16, 'Tea Estate'),
('Baramasia', 'বারমাসিয়া চা বাগান', 'ডাকঘরঃ নাজিরহাট | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 2922.68, 'Tea Estate'),
('Kodala', 'কোদালা চা বাগান', 'ডাকঘরঃ কোদালা | থানাঃ রাঙ্গুনিয়া | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 2438.87, 'Tea Estate'),
('Neptune', 'নেপচুন চা বাগান', 'ডাকঘরঃ নারায়ণহাট | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 2631.66, 'Tea Estate'),
('Karnafuli', 'কর্ণফুলী চা বাগান', 'ডাকঘরঃ ফটিকছড়ি | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 6564.49, 'Tea Estate'),
('Ramgarh', 'রামগড় চা বাগান', 'ডাকঘরঃ নলুয়া ভায়া রামগাঁ | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 1399.82, 'Tea Estate'),
('Kaiyacherra', 'কৈয়াছড়া-ডলু চা বাগান', 'ডাকঘরঃ ভূজপুর | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 1700.25, 'Tea Estate'),
('New Dantmara', 'নিউ-দাঁতমারা চা বাগান', 'ডাকঘরঃ দাঁতমারা | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 505.81, 'Tea Estate'),
('Haldavalley', 'হালদাভ্যালি চা বাগান', 'ডাকঘরঃ নারায়ণহাট | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 1052.69, 'Tea Estate'),
('Adharmanik', 'আধাঁরমানিক চা বাগান', 'ডাকঘরঃ চিকনছেড়া | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 712.07, 'Tea Garden'),
('Mohammadnagar', 'মোহাম্মদনগর চা বাগান', 'ডাকঘরঃ মির্জাহাট | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 188.18, 'Tea Garden'),
('Agunia', 'আগুনিয়া চা বাগান', 'ডাকঘরঃ ধামিরহাট | থানাঃ রাঙ্গুনিয়া | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 310.6, 'Tea Garden'),
('Chowdhury', 'চৌধুরী চা বাগান', 'ডাকঘরঃ পশ্চিম ভুজপুর | থানাঃ ভুজপুর | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 560.79, 'Tea Garden'),
('Sallylunn', 'স্যালিলান চা বাগান', 'ভিতরগড় | উপজেলা: পঞ্চগড় | জেলাঃ পঞ্চগড়', 'পঞ্চগড়', 135.59, 'Tea Garden'),
('Dahuk', 'ডাহুক চা বাগান', 'ইউনিয়নঃ বুড়াবুড়ি | উপজেলাঃ তেতুলিয়া | জেলাঃ পঞ্চগড়', 'পঞ্চগড়', 47.97, 'Tea Garden'),
('Kazi & Kazi', 'কাজী এন্ড কাজী চা বাগান', 'ডাকঘরঃ বুড়াবুড়ি | থানাঃ তেতুলিয়া | জেলাঃ পঞ্চগড়', 'পঞ্চগড়', 4149.53, 'Tea Garden'),
('Haque', 'হক চা বাগান', 'ইউনিয়ন: শালবাহান | থানাঃ তেতুলিয়া | জেলাঃ পঞ্চগড়', 'পঞ্চগড়', 126.12, 'Tea Garden'),
('M.M. Tea', 'এম এম টি এ স্টেট লিমিটেড', 'পাহাড়বাড়ী, হাড়িভাষা, | পঞ্চগড় সদর, | পঞ্চগড়।', 'পঞ্চগড়', 107.63, 'Tea Garden'),
('Karatoa', 'করতোয়া চা বাগান', 'ডাকঘর: জগদল বাজার | থানাঃ পঞ্চগড় সদর | জেলাঃ পঞ্চগড়', 'পঞ্চগড়', 150.67, 'Tea Garden'),
('Triple Zed', 'ট্রিপল জেড চা বাগান', 'সদর উপজেলা, | পঞ্চগড়।', 'পঞ্চগড়', 73.18, 'Tea Garden'),
('Z & Z', 'জেড এন্ড জেড চা বাগান', 'সরকার পাড়া, | তেতুলিয়া, | পঞ্চগড়', 'পঞ্চগড়', 28.3, 'Tea Garden'),
('Organic Origin', 'অর্গানিক অরিজিন', 'ইউনিয়ন: আলোয়াখোয়া | উপজেলা: আটোয়ারী | জেলা: পঞ্চগড়', 'পঞ্চগড়', 45.64, 'Tea Garden'),
('Panchagarh Tea Company', 'পঞ্চগড় টি কোম্পানী', 'পঞ্চগড় টি কোম্পানী লি: মাঝপাড়া, শালবাহার, | তেতুলিয়া, পঞ্চগড়', 'পঞ্চগড়', 70.03, 'Tea Estate'),
('Waggachara', 'ওয়াজ্ঞাছড়া চা বাগান', 'ডাকঘরঃ বড়ইছড়ি | থানাঃ কাপ্তাই | জেলাঃ রাঙ্গামাটি', 'রাঙ্গামাটি', 760.99, 'Tea Estate'),
('Khedarmara', 'খেদারমারা চা বাগান', 'পো: বড়ইছড়ি | উপজেলা: বাঘাইছড়ি | জেলা: রাঙ্গামাটি।', 'রাঙ্গামাটি', 33.95, 'Tea Garden'),
('Green Field', 'গ্রীণ ফিল্ড', 'লাহিড়ী হাট | উপজেলা: বালিয়াডাঙ্গী | জেলা: ঠাকুরগাঁও।', 'ঠাকুরগাঁও', 40.77, 'Tea Garden');

UPDATE Gardens
SET FullName = s.FullName,
    Address = s.Address,
    District = s.District,
    Acre = s.Acre,
    GardenType = s.GardenType
FROM SeedGardens s
WHERE lower(trim(Gardens.Name)) = lower(trim(s.Name));

INSERT INTO Gardens (Name, ShortName, FullName, Address, District, Acre, GardenType, IsActive, SortOrder)
SELECT s.Name, s.Name, s.FullName, s.Address, s.District, s.Acre, s.GardenType, 1, 0
FROM SeedGardens s
WHERE NOT EXISTS (
    SELECT 1 FROM Gardens g WHERE lower(trim(g.Name)) = lower(trim(s.Name))
);

DROP TABLE SeedGardens;
                " },
                { 7, @"
                    -- Update broker records to match Broker List.xlsx
                    UPDATE Brokers SET FullName = 'National Brokers Ltd.' WHERE Name = 'NBL';
                    UPDATE Brokers SET FullName = 'K. S. Brokers Ltd.' WHERE Name = 'KSBL';
                    UPDATE Brokers SET FullName = 'Purba Bangla Brokers Ltd.' WHERE Name = 'PBBL';
                    UPDATE Brokers SET FullName = 'Produce Brokers Ltd.' WHERE Name = 'PBL';
                    UPDATE Brokers SET FullName = 'Planters Brokers Limited' WHERE Name = 'PLNT';
                    UPDATE Brokers SET FullName = 'Progressive Brokers Ltd.', Name = 'PROG' WHERE Name = 'Progressive' OR Name = 'PROG';
                    UPDATE Brokers SET FullName = 'Unity Brokers Ltd.' WHERE Name = 'UBL';

                    -- Drop redundant ShortName column from Brokers
                    ALTER TABLE Brokers DROP COLUMN ShortName;

                    -- Update Brands: add Description, copy existing ShortName, then drop ShortName
                    ALTER TABLE Brands ADD COLUMN Description TEXT;
                    UPDATE Brands SET Description = ShortName WHERE Description IS NULL;
                    ALTER TABLE Brands DROP COLUMN ShortName;

                    -- Drop redundant ShortName column from Gardens
                    ALTER TABLE Gardens DROP COLUMN ShortName;
                " },
                { 8, @"
                    -- Recreate AuctionEntries with nullable GardenId, GradeId, BrandId, BrokerId, InvoiceNo
                    CREATE TABLE AuctionEntries_new (
                        EntryId INTEGER PRIMARY KEY AUTOINCREMENT,
                        SaleId INTEGER NOT NULL,
                        BrandId INTEGER,
                        BrokerId INTEGER,
                        LotNo TEXT NOT NULL,
                        GardenId INTEGER,
                        InvoiceNo TEXT,
                        GradeId INTEGER,
                        NumberOfBags INTEGER NOT NULL,
                        TotalKgs DECIMAL(18,2) NOT NULL,
                        PricePerKg DECIMAL(18,2) NOT NULL,
                        Value DECIMAL(18,2) NOT NULL,
                        ShedId INTEGER,
                        BlendId INTEGER,
                        CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (SaleId) REFERENCES Sales(SaleId) ON DELETE CASCADE,
                        FOREIGN KEY (BrandId) REFERENCES Brands(BrandId),
                        FOREIGN KEY (BrokerId) REFERENCES Brokers(BrokerId),
                        FOREIGN KEY (GardenId) REFERENCES Gardens(GardenId),
                        FOREIGN KEY (GradeId) REFERENCES Grades(GradeId),
                        FOREIGN KEY (ShedId) REFERENCES Sheds(ShedId),
                        FOREIGN KEY (BlendId) REFERENCES Blends(BlendId)
                    );

                    INSERT INTO AuctionEntries_new SELECT * FROM AuctionEntries;
                    DROP TABLE AuctionEntries;
                    ALTER TABLE AuctionEntries_new RENAME TO AuctionEntries;

                    CREATE INDEX IX_AuctionEntries_SaleId ON AuctionEntries(SaleId);
                    CREATE INDEX IX_AuctionEntries_BrokerId ON AuctionEntries(BrokerId);
                    CREATE INDEX IX_AuctionEntries_BrandId ON AuctionEntries(BrandId);
                    CREATE INDEX IX_AuctionEntries_GardenId ON AuctionEntries(GardenId);
                    CREATE INDEX IX_AuctionEntries_LotNo ON AuctionEntries(LotNo);
                    CREATE INDEX IX_AuctionEntries_InvoiceNo ON AuctionEntries(InvoiceNo);
                " },
                { 10, @"
                    CREATE TABLE Companies (
                        CompanyId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        IsActive INTEGER NOT NULL DEFAULT 1
                    );
                    CREATE UNIQUE INDEX IX_Companies_Name ON Companies(lower(trim(Name)));
                    CREATE TABLE CompanyGardens (
                        CompanyId INTEGER NOT NULL REFERENCES Companies(CompanyId) ON DELETE CASCADE,
                        GardenId INTEGER PRIMARY KEY REFERENCES Gardens(GardenId) ON DELETE CASCADE
                    );
                    CREATE INDEX IX_CompanyGardens_CompanyId ON CompanyGardens(CompanyId);
                    ALTER TABLE AuctionEntries ADD COLUMN AccountGardenId INTEGER REFERENCES Gardens(GardenId);
                    UPDATE AuctionEntries SET AccountGardenId = GardenId;
                " },
                { 9, @"
                    -- Clean out duplicate/extraneous gardens so exactly the 170 official gardens exist
                    DELETE FROM Gardens;
                    DELETE FROM sqlite_sequence WHERE name = 'Gardens';

                    INSERT INTO Gardens (GardenId, Name, FullName, Address, District, Acre, GardenType, IsActive, SortOrder) VALUES
                    (1, 'Allynugger', 'আলীনগর চা বাগান', 'ডাকঘরঃ শমশেরনগর | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার।', 'মৌলভীবাজার', 4462.88, 'Tea Estate', 1, 1),
                    (2, 'Ameenabad', 'আমীনাবাদ চা বাগান', 'ডাকঘরঃ বরমচাল | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার।', 'মৌলভীবাজার', 745.15, 'Tea Estate', 1, 2),
                    (3, 'Amrail', 'আমরাইল চা বাগান', 'ডাককঘরঃ সাতগাঁও | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার।', 'মৌলভীবাজার', 3853.16, 'Tea Estate', 1, 3),
                    (4, 'Atiabagh', 'আতিয়াবাগ চা বাগান', 'ডাকঘরঃ জুরী | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 732.15, 'Tea Estate', 1, 4),
                    (5, 'Balisera', 'বালিশেরা চা বাগান', 'ডাকঘরঃ কালীঘাট | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 8652.69, 'Tea Estate', 1, 5),
                    (6, 'Baraoora', 'ভাড়াউড়া চা বাগান', 'ডাকঘরঃ শ্রীমঙ্গল | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2601.84, 'Tea Estate', 1, 6),
                    (7, 'BTRI', 'বিটিআরআই চা বাগান', 'ডাকঘরঃ শ্রীমঙ্গল | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 476.15, 'Tea Estate', 1, 7),
                    (8, 'Chandbagh', 'চান্দভাগ চা বাগান', 'ডাকঘরঃ ইন্দেশর | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1043.7, 'Tea Estate', 1, 8),
                    (9, 'Chatlapore', 'চাতলাপুর চা বাগান', 'ডাকঘরঃ শমশের নগর | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3150.85, 'Tea Estate', 1, 9),
                    (10, 'Clevdon', 'ক্লিভডন চা বাগান', 'ডাকঘরঃ রাঙ্গিরকুল | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1503.79, 'Tea Estate', 1, 10),
                    (11, 'Clonal', 'ক্লোনাল চা বাগান', 'ডাকঘরঃ ভূুনবীর | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 399.05, 'Tea Estate', 1, 11),
                    (12, 'Dewrachora', 'দেওড়াছড়া চা বাগান', 'ডাকঘরঃ মুন্সিবাজার | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1868.35, 'Tea Estate', 1, 12),
                    (13, 'Deanston', 'ডিনষ্টোন টি এস্টেট', 'ডাকঘরঃ খেজুরীছড়া | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 8013.95, 'Tea Estate', 1, 13),
                    (14, 'Dhamai', 'ধামাই চা বাগান', 'ডাকঘরঃ জুরী | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজর', 'মৌলভীবাজার', 2582.07, 'Tea Estate', 1, 14),
                    (15, 'Dildarpur', 'দিলদারপুর চা বাগান', 'ডাকঘরঃ রাঙ্গিরকুল | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজর', 'মৌলভীবাজার', 765.27, 'Tea Estate', 1, 15),
                    (16, 'Dinarpur', 'দিনারপুর চা বাগান', 'ডাকঘরঃ শমসের নগর | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 999.62, 'Tea Estate', 1, 16),
                    (17, 'Etah', 'ইটা চা বাগান', 'ডাকঘরঃ করিমপুর | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3188.03, 'Tea Estate', 1, 17),
                    (18, 'Ghazipore', 'গাজীপুর চা বাগান', 'ডাকঘরঃ কুলাউড়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2056.84, 'Tea Estate', 1, 18),
                    (19, 'Hafiz', 'হাফিজ চা বাগান', 'ডাকঘরঃ পূর্বশাহবাজপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 132.88, 'Tea Estate', 1, 19),
                    (20, 'Hajinagar', 'হাজীনগর চা বাগান', 'ডাকঘরঃ কাজলধারা | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 449.35, 'Tea Estate', 1, 20),
                    (21, 'Hossainabad', 'হোসেনাবাদ চা বাগান', 'ডাকঘরঃ কালিঘাট | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 999.43, 'Tea Estate', 1, 21),
                    (22, 'Jhemai', 'ঝিমাই চা বাগান', 'ডাকঘরঃ কুলাউড়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 661.55, 'Tea Estate', 1, 22),
                    (23, 'Julekhanagar', 'জুলেখানগর চা বাগান', 'ডাকঘরঃ খেজুড়ীছড়া | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 861.26, 'Tea Estate', 1, 23),
                    (24, 'Junglebari', 'জঙ্গলবাড়ী চা বাগান', 'ডাকঘরঃ কালিঘাট | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1000, 'Tea Estate', 1, 24),
                    (25, 'Karimpore', 'করিমপুর চা বাগান', 'ডাকঘরঃ করিমপুর | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজর', 'মৌলভীবাজার', 3347.15, 'Tea Estate', 1, 25),
                    (26, 'Kurmah', 'কুরমা চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3152.19, 'Tea Estate', 1, 26),
                    (27, 'Luayuni-Holicherra', 'লুয়াইউনি-হলিছড়া চা বাগান', 'ডাকঘরঃ কাজলধারা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2039.02, 'Tea Estate', 1, 27),
                    (28, 'Madanmohanpur', 'মদনমোহনপুর চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 996.7, 'Tea Estate', 1, 28),
                    (29, 'Marina', 'মেরিনা চা বাগান', 'ডাকঘরঃ কুলাউড়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 872.24, 'Tea Estate', 1, 29),
                    (30, 'Mazdehee', 'মাজদিহি চা বাগান', 'ডাকঘরঃ নারায়নছেড়া | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3022.63, 'Tea Estate', 1, 30),
                    (31, 'Mertinga', 'মিরতিংগা চা বাগান', 'ডাকঘরঃ মুন্সিবাজার | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2478.91, 'Tea Estate', 1, 31),
                    (32, 'Mirzapur', 'মির্জাপুর চা বাগান', 'ডাকঘরঃ শমশেরনগর | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2406.06, 'Tea Estate', 1, 32),
                    (33, 'New Samanbagh', 'নিউ সমনবাগ চা বাগান', 'ডাকঘরঃ দক্ষিণবাগ | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3528.82, 'Tea Estate', 1, 33),
                    (34, 'Nurjahan', 'নুরজাহান চা বাগান', 'ডাকঘরঃ শ্রীমঙ্গল | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 738.27, 'Tea Estate', 1, 34),
                    (35, 'Pallakandi', 'পাল্লাকান্দি চা বাগান', 'ডাকঘরঃ লংলা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 123.68, 'Tea Estate', 1, 35),
                    (36, 'Patrakhola', 'পাত্রখোলা চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 4587.28, 'Tea Estate', 1, 36),
                    (37, 'Phultala', 'ফুলতলা চা বাগান', 'ডাকঘরঃ ফুলতলা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2636.55, 'Tea Estate', 1, 37),
                    (38, 'Phulbari', 'ফুলবাড়ি চা বাগান', 'ডাকঘরঃ কেরামতনগর | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1514.76, 'Tea Estate', 1, 38),
                    (39, 'Premnagar', 'প্রেমনগর চা বাগান', 'ডাকঘরঃ নরায়ণছেড়া | থানাঃ মৌলভীবাজার | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 971.07, 'Tea Estate', 1, 39),
                    (40, 'Rajghat', 'রাজঘাট চা বাগান', 'ডাকঘরঃ খেজুরীছড়া | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 5816.77, 'Tea Estate', 1, 40),
                    (41, 'Rajkie', 'রাজকী চা বাগান', 'ডাকঘরঃ ফুলতলা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2015.91, 'Tea Estate', 1, 41),
                    (42, 'Rajnagar', 'রাজনগর টি এস্টেট', 'ডাকঘরঃ লংলা | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2412.8, 'Tea Estate', 1, 42),
                    (43, 'Rashidabad', 'রশীদাবাদ চা বাগান', 'ডাকঘরঃ দক্ষিণবাগ | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 400.09, 'Tea Estate', 1, 43),
                    (44, 'Rehana', 'রেহানা চা বাগান', 'ডাকঘরঃ কুলাউড়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 464.46, 'Tea Estate', 1, 44),
                    (45, 'Sagornal', 'সাগরনাল চা বাগান', 'ডাকঘরঃ সাগরনাল | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1800.61, 'Tea Estate', 1, 45),
                    (46, 'Saif', 'সাইফ টি এস্টেট', 'ডাকঘরঃ কুলাউড়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 937.09, 'Tea Estate', 1, 46),
                    (47, 'Sathgao', 'সাতগাঁও চা বাগান', 'ডাকঘরঃ সাতগাঁও | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3078.85, 'Tea Estate', 1, 47),
                    (48, 'Shamshernagar', 'শমশেরনগর চা বাগান', 'ডাকঘরঃ কমলগঞ্জ | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 4326.47, 'Tea Estate', 1, 48),
                    (49, 'Silloah', 'সিলুয়া চা বাগান', 'ডাকঘরঃ জুরী | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2474.67, 'Tea Estate', 1, 49),
                    (50, 'Srigobindapur', 'শ্রীগোবিন্দপুর চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 882.07, 'Tea Estate', 1, 50),
                    (51, 'Amo', 'অহিদাবাদ বা আমু চা বাগান', 'ডাকঘরঃ শ্রীধরপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 742.8, 'Tea Estate', 1, 51),
                    (52, 'Zareen', 'জেরিন চা বাগান', 'ডাকঘরঃ শ্রীমঙ্গল | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 681.86, 'Tea Estate', 1, 52),
                    (53, 'Champarai', 'চম্পারায় চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1996.31, 'Tea Estate', 1, 53),
                    (54, 'Dilkusha', 'দিলকুশা চা বাগান', 'ডাকঘরঃ সিলোয়াবাজার | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1039.6, 'Tea Estate', 1, 54),
                    (55, 'Dhalai', 'দলই চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1200.9, 'Tea Estate', 1, 55),
                    (56, 'Kapnapahar', 'কাপনাপাহাড় চা বাগান', 'ডাকঘরঃ জুরী | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1925.85, 'Tea Estate', 1, 56),
                    (57, 'Lungla', 'লংলা চা বাগান', 'ডাকঘরঃ লংলা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3202.76, 'Tea Estate', 1, 57),
                    (58, 'Utttorbagh & Indanagar', 'উত্তরবাগ-ইন্দোনগর', 'ইন্দোনগর | ডাকঘরঃ ইন্দেশর | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 3577.88, 'Tea Estate', 1, 58),
                    (59, 'Pallathal', 'পাল্লাথল চা বাগান', 'ডাকঘরঃ পূর্বশাহবাজপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 795.93, 'Tea Estate', 1, 59),
                    (60, 'Ratna', 'রত্না চা বাগান', 'ডাকঘরঃ জুরী | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1703.66, 'Tea Estate', 1, 60),
                    (61, 'Sonarupa', 'সোনারূপা চা বাগান', 'ডাকঘরঃ দক্ষিণবাগ | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2502.23, 'Tea Estate', 1, 61),
                    (62, 'Bahadurpur', 'বাহাদুরপুর চা বাগান', 'ডাকঘরঃ বড়লেখা | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 631.02, 'Tea Estate', 1, 62),
                    (63, 'Chotolekha', 'ছোটলেখা চা বাগান', 'ডাকঘরঃ বড়লেখা | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1964.5, 'Tea Estate', 1, 63),
                    (64, 'Fatehbagh', 'ফতেহবাগ চা বাগান', 'ডাকঘরঃ পূর্বশাহবাজপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 479.99, 'Tea Estate', 1, 64),
                    (65, 'Hamidia', 'হামিদিয়া চা বাগান', 'ডাকঘরঃ মাথারকাপন | থানাঃ মৌলভীবাজার | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 455.18, 'Tea Estate', 1, 65),
                    (66, 'Hingazia', 'হিঙ্গাজিয়া চা বাগান', 'ডাকঘরঃ কাজলধারা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2354.19, 'Tea Estate', 1, 66),
                    (67, 'Islamadad', 'ইসলামাবাদ চা বাগান', 'ডাকঘরঃ সিলোয়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 117.13, 'Tea Estate', 1, 67),
                    (68, 'Kaliti', 'কালিটি চা বাগান', 'ডাকঘরঃ কুলাউড়া | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1163.03, 'Tea Estate', 1, 68),
                    (69, 'M.R. Khan', 'এম আর খান চা বাগান', 'ডাকঘরঃ শ্রীমঙ্গল | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 600, 'Tea Estate', 1, 69),
                    (70, 'Madabpur', 'মাধবপুর চা বাগান', 'ডাকঘরঃ পাত্রখোলা | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2825.03, 'Tea Estate', 1, 70),
                    (71, 'Moulvi', 'মৌলভী টি এস্টেট', 'ডাকঘরঃ নারয়ণছড়া | থানাঃ মৌলভীবাজার | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1771.62, 'Tea Estate', 1, 71),
                    (72, 'Nahar', 'নাহার চা বাগান', 'ডাকঘরঃ খেজুরীছড়া | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 864.85, 'Tea Estate', 1, 72),
                    (73, 'Rungicherra', 'রাঙ্গীছড়া চা বাগান', 'ডাকঘরঃ পৃথিমপাশা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1086.9, 'Tea Estate', 1, 73),
                    (74, 'Sabazpur', 'শাহবাজপুর চা বাগান', 'ডাকঘরঃ সুজাউল | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 2886.11, 'Tea Estate', 1, 74),
                    (75, 'Patharia', 'পাথারিয়া চা বাগান', 'ডাকঘরঃ দক্ষিণবাগ | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1682.64, 'Tea Estate', 1, 75),
                    (76, 'Dakshin Gul', 'দক্ষিণগুল চা বাগান', 'ডাকঘরঃ বড়লেখা | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1131.66, 'Tea Estate', 1, 76),
                    (77, 'Alladad', 'আল্লাদাদ চা বাগান', 'ডাকঃ পূর্বশাহবাজপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 945.48, 'Tea Garden', 1, 77),
                    (78, 'Borhannagar', 'বোরহাননগর চা বাগান', 'ডাকঘরঃ পৃথিমপাশা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 177.54, 'Tea Garden', 1, 78),
                    (79, 'Azgarabad', 'আজগরাবাদ চা বাগান', 'ডাকঘরঃ পৃথিমপাশা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1426, 'Tea Garden', 1, 79),
                    (80, 'Muraichhara', 'মুরাইছড়া চা বাগান', 'ডাকঘরঃ পৃথিমপাশা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 978.34, 'Tea Garden', 1, 80),
                    (81, 'Mathiura', 'মাথিউড়া চা বাগান', 'ডাকঘরঃ রাজনগর | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1656.55, 'Tea Garden', 1, 81),
                    (82, 'Nandarani', 'নন্দরাণী চা বাগান', 'ডাকঘরঃ কালিঘাট | থানাঃ কমলগঞ্জ | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 426.42, 'Tea Garden', 1, 82),
                    (83, 'Shafinagar', 'শফিনগর চা বাগান', 'ডাকঘরঃ টেংরাবাজার | থানাঃ শ্রীমঙ্গল | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 97.96, 'Tea Garden', 1, 83),
                    (84, 'Kashipur', 'কাশিপুর চা বাগান', 'ডাকঘরঃ লংলা | থানাঃ রাজনগর | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 141.58, 'Tea Garden', 1, 84),
                    (85, 'Lakshichhara', 'লক্ষিছড়া চা বাগান', 'ডাকঘরঃ বড়লেখা | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 1096.34, 'Tea Garden', 1, 85),
                    (86, 'Sirajnagar', 'সিরাজনগর চা বাগান', 'ডাকঘরঃ কাজলধারা | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 830.61, 'Tea Garden', 1, 86),
                    (87, 'Kalikabari', 'কালীকাবাড়ী চা বাগান', 'ডাকঘরঃ পূর্বশাহবাজপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 344.62, 'Tea Garden', 1, 87),
                    (88, 'Ayeshabagh', 'আয়েশাবাগ চা বাগান', 'ডাকঘরঃ শ্রীধরপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 526.61, 'Tea Garden', 1, 88),
                    (89, 'Bijoya', 'বিজয়া চা বাগান', 'ডাকঘরঃ রাঙ্গিরকুল | থানাঃ কুলাউড়া | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 755.76, 'Tea Garden', 1, 89),
                    (90, 'Kumarshail', 'কুমারশাইল চা বাগান', 'ডাকঘরঃ শ্রীধরপুর | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 985.79, 'Tea Garden', 1, 90),
                    (91, 'Rahmania', 'রহমানিয়া চা বাগান', 'ডাকঘরঃ বড়লেখা | থানাঃ বড়লেখা | জেলাঃ মৌলভীবাজার', 'মৌলভীবাজার', 347.68, 'Tea Garden', 1, 91),
                    (92, 'Baikunthapur', 'বৈকন্ঠপুর চা বাগান', 'ডাকঘরঃ ইটাখোলা | থানাঃ মাধবপুর | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 922.59, 'Tea Estate', 1, 92),
                    (93, 'Parkul-Nasimabad', 'পারকুল-নাছিমাবাদ চা বাগান', 'ডাকঘরঃ রশিদপুর | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2531.75, 'Tea Estate', 1, 93),
                    (94, 'Jagadishpur', 'জগদীশপুর চা বাগান', 'ডাকঘরঃ ইটাখোলা | থানাঃ মাধবপুর | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1489.04, 'Tea Estate', 1, 94),
                    (95, 'Surma', 'সুরমা চা বাগান', 'ডাকঘরঃ ইটাখোলা | থানাঃ মাধবপুর | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 4454.39, 'Tea Estate', 1, 95),
                    (96, 'Chaklapunji', 'চাকলাপুঞ্জি চা বাগান', 'ডাকঘরঃ চাঁদপুর | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1753.79, 'Tea Estate', 1, 96),
                    (97, 'Teliapara', 'তেলিয়াপাড়া চা বাগান', 'ডাকঘরঃ ইটাখোলা | থানাঃ মাধবপুর | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2601.28, 'Tea Estate', 1, 97),
                    (98, 'Rema', 'রেমা চা বাগান', 'ডাকঘরঃ গাজীপুর | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2114.61, 'Tea Estate', 1, 98),
                    (99, 'Noyapara', 'নোয়াপাড়া চা বাগান', 'ডাকঘরঃ ইটাখোলা | থানাঃ মাধবপুর | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1701.07, 'Tea Estate', 1, 99),
                    (100, 'Chundeecherra', 'চন্ডিছড়া চা বাগান', 'ডাকঘরঃ চাঁদপুরবাগান | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1960.43, 'Tea Estate', 1, 100),
                    (101, 'Sreebari', 'শ্রীবাড়ী চা বাগান', 'ডাকঘরঃ রশিদপুর | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1649.12, 'Tea Estate', 1, 101),
                    (102, 'Lalchand', 'লালচান্দ চা বাগান', 'ডাকঘরঃ শাহাজীবাজার | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2462.28, 'Tea Estate', 1, 102),
                    (103, 'Brindabon', 'বৃন্দবন চা বাগান', 'ডাকঘরঃ পুটিজুরী | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1192.54, 'Tea Estate', 1, 103),
                    (104, 'Chandpur', 'চাঁদপুর চা এস্টেট', 'ডাকঘরঃ চাঁদপুর | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 3949.49, 'Tea Estate', 1, 104),
                    (105, 'Daragaon', 'দারাগাঁও চা বাগান', 'ডাকঘরঃ রশিদপুর | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2347.41, 'Tea Estate', 1, 105),
                    (106, 'Imam', 'ইমাম চা বাগান', 'ডাকঘরঃ ফটিজুরী | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1148, 'Tea Estate', 1, 106),
                    (107, 'Rashidpur', 'রশিদপুর চা বাগান', 'ডাকঘরঃ রশিদপুর | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 5574.3, 'Tea Estate', 1, 107),
                    (108, 'Deundi', 'ডেউন্দি চা বাগান', 'ডাকঘরঃ চাঁদপুরবাগান | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2812.72, 'Tea Estate', 1, 108),
                    (109, 'Luskerpore', 'লস্করপুর চা বাগান', 'ডাকঘরঃ চাঁদপুরবাগান | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2827.28, 'Tea Estate', 1, 109),
                    (110, 'Amu', 'আমু চা বাগান', 'ডাকঘরঃ চাঁদপুর বাগান | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 2505.27, 'Tea Estate', 1, 110),
                    (111, 'Nalua', 'নলুয়া চা বাগান', 'ডাকঘরঃ চাঁদপুরবাগান | থানাঃ চুনারুঘাট | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 3063.03, 'Tea Estate', 1, 111),
                    (112, 'Amtali', 'আমতলী চা বাগান', 'ডাকঘরঃ রশিদপুর | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 235.23, 'Tea Estate', 1, 112),
                    (113, 'Madhupur', 'মধুপুর চা বাগান', 'ডাকঘরঃ বাহুবল | থানাঃ বাহুবল | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1165.24, 'Tea Estate', 1, 113),
                    (114, 'Bawani', 'বাওয়ানী চা বাগান', 'ডাকঘরঃ পুটিজুরী | থানাঃ হবিগঞ্জ সদর | জেলাঃ হবিগঞ্জ', 'হবিগঞ্জ', 1179, 'Tea Garden', 1, 114),
                    (115, 'Chaltachhara', 'চালতাছড়া চা বাগান', 'ডাকঘর: রশিদপুর | উপজেলা: বাহুবল | জেলা: হবিগঞ্জ।', 'হবিগঞ্জ', 63.56, 'Tea Garden', 1, 115),
                    (116, 'Boban', 'ববান চা বাগান', 'পানিউমদা | উপজেলা: নবীগঞ্জ | জেলা: হবিগঞ্জ।', 'হবিগঞ্জ', 208.52, 'Tea Garden', 1, 116),
                    (117, 'Alibahar', 'আলীবাহার চা বাগান', 'ডাকঘরঃ আখালিয়া | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 683.5, 'Tea Estate', 1, 117),
                    (118, 'Lovachhara', 'লোভাছড়া চা বাগান', 'ডাকঘরঃ কানাইঘাট | থানাঃ কানাইঘাট | জেলাঃ সিলেট', 'সিলেট', 1839, 'Tea Estate', 1, 118),
                    (119, 'Burjan', 'বুরজান চা বাগান', 'ডাকঘরঃ খাদিমনগর | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 3263.31, 'Tea Estate', 1, 119),
                    (120, 'Khadim', 'খাদিম চা বাগান', 'ডাকঘরঃ খাদিমনগর | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 3629.6, 'Tea Estate', 1, 120),
                    (121, 'Lakkatura', 'লাক্কাতুরা চা বাগান', 'ডাকঘরঃ সিলেট | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 3198.05, 'Tea Estate', 1, 121),
                    (122, 'Lalakhal', 'লালাখাল চা বাগান', 'ডাকঘরঃ জৈন্তাপুর | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 1317.34, 'Tea Estate', 1, 122),
                    (123, 'Monipur', 'মনিপুর চা বাগান', 'ডাকঘরঃ ফার্টিলাইজার ফ্যাক্টরী | থানাঃ ফেঞ্চুগঞ্জ | জেলাঃ সিলেট', 'সিলেট', 1694.69, 'Tea Estate', 1, 123),
                    (124, 'Jaflong', 'জাফলং চা বাগান', 'ডাকঃ জাফলং | থানাঃ গোয়াইনঘাট | জেলাঃ সিলেট', 'সিলেট', 2164.14, 'Tea Estate', 1, 124),
                    (125, 'Afifanagar', 'আফিফানগর চা বাগান', 'ডাকঘরঃ জৈন্তাপুর | থানাঃ জৈন্তাপুর | জেলাঃ সিলেট', 'সিলেট', 868.96, 'Tea Estate', 1, 125),
                    (126, 'Habibnagar', 'হাবিবনগর চা বাগান', 'ডাকঘরঃ চিকনাগুল | থানাঃ গোয়াইনঘাট | জেলাঃ সিলেট', 'সিলেট', 1583.35, 'Tea Estate', 1, 126),
                    (127, 'Moomincherra', 'মোমিনছড়া চা বাগান', 'ডাকঘরঃ বাটেরা | থানাঃ ফেঞ্চুগঞ্জ | জেলাঃ সিলেট', 'সিলেট', 986.12, 'Tea Estate', 1, 127),
                    (128, 'Malnichhara', 'মালনীছড়া চা বাগান', 'ডাকঘরঃ সিলেট | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 2164.14, 'Tea Estate', 1, 128),
                    (129, 'Dalia', 'ডালিয়া চা বাগান', 'ডাকঘরঃ সিলেট | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 174.77, 'Tea Garden', 1, 129),
                    (130, 'Star', 'ষ্টার চা বাগান', 'ডাকঘরঃ সিলেট | থানাঃ সিলেট | জেলাঃ সিলেট', 'সিলেট', 392.96, 'Tea Garden', 1, 130),
                    (131, 'Sreepur', 'শ্রীপুর চা বাগান', 'ডাকঘরঃ জৈন্তাপুর | থানাঃ জৈন্তাপুর | জেলাঃ সিলেট', 'সিলেট', 1017.52, 'Tea Garden', 1, 131),
                    (132, 'Fatehpur', 'ফতেহপুর চা বাগান', 'ডাকঘরঃ বারানগর | থানাঃ গোয়াইনঘাট | জেলাঃ সিলেট', 'সিলেট', 521.28, 'Tea Garden', 1, 132),
                    (133, 'Daluchhara', 'ডালুছড়া চা বাগান', 'ডাকঘরঃ ফেঞ্চুগঞ্জ | থানাঃ ফেঞ্চুগঞ্জ | জেলাঃ সিলেট', 'সিলেট', 325.52, 'Tea Garden', 1, 133),
                    (134, 'Khan', 'খান চা বাগান', 'ডাকঘরঃ চিকনাগুল | থানাঃ গোয়াইনঘাট | জেলাঃ সিলেট', 'সিলেট', 1578.32, 'Tea Garden', 1, 134),
                    (135, 'Meghalaya', 'মেঘালয় চা বাগান', 'ডাকঘর: দরবস্ত বাজার | উপজেলা: জৈন্তাপুর | জেলা: সিলেট', 'সিলেট', 1216.92, 'Tea Garden', 1, 135),
                    (136, 'Naseha', 'নাছেহা চা বাগান', 'ডাকঘরঃ দাঁতমারা | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 528.37, 'Tea Estate', 1, 136),
                    (137, 'Thandachhari', 'ঠান্ডাছড়ি চা বাগান', 'ডাকঘরঃ ঠান্ডাছড়ি | থানাঃ রাঙ্গুনিয়া | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 1045.03, 'Tea Estate', 1, 137),
                    (138, 'Dantmara', 'দাঁতমারা চা বাগান', 'ডাকঘরঃ দাঁতমারা | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 452.54, 'Tea Estate', 1, 138),
                    (139, 'Udalia', 'উদালিয়া চা বাগান', 'ডাকঘরঃ নাজিরহাট | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 2995.72, 'Tea Estate', 1, 139),
                    (140, 'Elahinoor', 'এলাহীনুর চা বাগান', 'ডাকঘরঃ নাজিরহাট | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 1491.8, 'Tea Estate', 1, 140),
                    (141, 'Ma Jaan', 'মা জান চা বাগান', 'ডাকঘরঃ দাঁতমারা | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 581.98, 'Tea Estate', 1, 141),
                    (142, 'Aasia', 'আছিয়া চা বাগান', 'ডাকঘরঃ ভূঁজপুর | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 676.92, 'Tea Estate', 1, 142),
                    (143, 'Rangapani', 'রাঙ্গাপানি চা বাগান', 'ডাকঘরঃ ফটিকছড়ি | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 2639.2, 'Tea Estate', 1, 143),
                    (144, 'Chandpur Belgaon', 'চাঁদপুর-বেলগাঁও চা বাগান', 'ডাকঘরঃ মানিকগ্রাম | থানাঃ বাঁশখালী | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 3472.53, 'Tea Estate', 1, 144),
                    (145, 'Panchaboti', 'পঞ্চবটি চা বাগান', 'ডাকঘরঃ মির্জাপুর | থানাঃ ভুঁজপুর | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 129.16, 'Tea Estate', 1, 145),
                    (146, 'Baramasia', 'বারমাসিয়া চা বাগান', 'ডাকঘরঃ নাজিরহাট | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 2922.68, 'Tea Estate', 1, 146),
                    (147, 'Kodala', 'কোদালা চা বাগান', 'ডাকঘরঃ কোদালা | থানাঃ রাঙ্গুনিয়া | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 2438.87, 'Tea Estate', 1, 147),
                    (148, 'Neptune', 'নেপচুন চা বাগান', 'ডাকঘরঃ নারায়ণহাট | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 2631.66, 'Tea Estate', 1, 148),
                    (149, 'Karnafuli', 'কর্ণফুলী চা বাগান', 'ডাকঘরঃ ফটিকছড়ি | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 6564.49, 'Tea Estate', 1, 149),
                    (150, 'Ramgarh', 'রামগড় চা বাগান', 'ডাকঘরঃ নলুয়া ভায়া রামগাঁ | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 1399.82, 'Tea Estate', 1, 150),
                    (151, 'Kaiyacherra', 'কৈয়াছড়া-ডলু চা বাগান', 'ডাকঘরঃ ভূজপুর | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 1700.25, 'Tea Estate', 1, 151),
                    (152, 'New Dantmara', 'নিউ-দাঁতমারা চা বাগান', 'ডাকঘরঃ দাঁতমারা | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 505.81, 'Tea Estate', 1, 152),
                    (153, 'Haldavalley', 'হালদাভ্যালি চা বাগান', 'ডাকঘরঃ নারায়ণহাট | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 1052.69, 'Tea Estate', 1, 153),
                    (154, 'Adharmanik', 'আধাঁরমানিক চা বাগান', 'ডাকঘরঃ চিকনছেড়া | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 712.07, 'Tea Garden', 1, 154),
                    (155, 'Mohammadnagar', 'মোহাম্মদনগর চা বাগান', 'ডাকঘরঃ মির্জাহাট | থানাঃ ফটিকছড়ি | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 188.18, 'Tea Garden', 1, 155),
                    (156, 'Agunia', 'আগুনিয়া চা বাগান', 'ডাকঘরঃ ধামিরহাট | থানাঃ রাঙ্গুনিয়া | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 310.6, 'Tea Garden', 1, 156),
                    (157, 'Chowdhury', 'চৌধুরী চা বাগান', 'ডাকঘরঃ পশ্চিম ভুজপুর | থানাঃ ভুজপুর | জেলাঃ চট্টগ্রাম', 'চট্টগ্রাম', 560.79, 'Tea Garden', 1, 157),
                    (158, 'Sallylunn', 'স্যালিলান চা বাগান', 'ভিতরগড় | উপজেলা: পঞ্চগড় | জেলাঃ পঞ্চগড়', 'পঞ্চগড়', 135.59, 'Tea Garden', 1, 158),
                    (159, 'Dahuk', 'ডাহুক চা বাগান', 'ইউনিয়নঃ বুড়াবুড়ি | উপজেলাঃ তেতুলিয়া | জেলাঃ পঞ্চগড়', 'পঞ্চগড়', 47.97, 'Tea Garden', 1, 159),
                    (160, 'Kazi & Kazi', 'কাজী এন্ড কাজী চা বাগান', 'ডাকঘরঃ বুড়াবুড়ি | থানাঃ তেতুলিয়া | জেলাঃ পঞ্চগড়', 'পঞ্চগড়', 4149.53, 'Tea Garden', 1, 160),
                    (161, 'Haque', 'হক চা বাগান', 'ইউনিয়ন: শালবাহান | থানাঃ তেতুলিয়া | জেলাঃ পঞ্চগড়', 'পঞ্চগড়', 126.12, 'Tea Garden', 1, 161),
                    (162, 'M.M. Tea', 'এম এম টি এ স্টেট লিমিটেড', 'পাহাড়বাড়ী, হাড়িভাষা, | পঞ্চগড় সদর, | পঞ্চগড়।', 'পঞ্চগড়', 107.63, 'Tea Garden', 1, 162),
                    (163, 'Karatoa', 'করতোয়া চা বাগান', 'ডাকঘর: জগদল বাজার | থানাঃ পঞ্চগড় সদর | জেলাঃ পঞ্চগড়', 'পঞ্চগড়', 150.67, 'Tea Garden', 1, 163),
                    (164, 'Triple Zed', 'ট্রিপল জেড চা বাগান', 'সদর উপজেলা, | পঞ্চগড়।', 'পঞ্চগড়', 73.18, 'Tea Garden', 1, 164),
                    (165, 'Z & Z', 'জেড এন্ড জেড চা বাগান', 'সরকার পাড়া, | তেতুলিয়া, | পঞ্চগড়', 'পঞ্চগড়', 28.3, 'Tea Garden', 1, 165),
                    (166, 'Organic Origin', 'অর্গানিক অরিজিন', 'ইউনিয়ন: আলোয়াখোয়া | উপজেলা: আটোয়ারী | জেলা: পঞ্চগড়', 'পঞ্চগড়', 45.64, 'Tea Garden', 1, 166),
                    (167, 'Panchagarh Tea Company', 'পঞ্চগড় টি কোম্পানী', 'পঞ্চগড় টি কোম্পানী লি: মাঝপাড়া, শালবাহার, | তেতুলিয়া, পঞ্চগড়', 'পঞ্চগড়', 70.03, 'Tea Estate', 1, 167),
                    (168, 'Waggachara', 'ওয়াজ্ঞাছড়া চা বাগান', 'ডাকঘরঃ বড়ইছড়ি | থানাঃ কাপ্তাই | জেলাঃ রাঙ্গামাটি', 'রাঙ্গামাটি', 760.99, 'Tea Estate', 1, 168),
                    (169, 'Khedarmara', 'খেদারমারা চা বাগান', 'পো: বড়ইছড়ি | উপজেলা: বাঘাইছড়ি | জেলা: রাঙ্গামাটি।', 'রাঙ্গামাটি', 33.95, 'Tea Garden', 1, 169),
                    (170, 'Green Field', 'গ্রীণ ফিল্ড', 'লাহিড়ী হাট | উপজেলা: বালিয়াডাঙ্গী | জেলা: ঠাকুরগাঁও।', 'ঠাকুরগাঁও', 40.77, 'Tea Garden', 1, 170);
                " }
            };
        }
    }
}
