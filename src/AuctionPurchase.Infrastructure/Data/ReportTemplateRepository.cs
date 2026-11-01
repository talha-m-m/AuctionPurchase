using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AuctionPurchase.Core.Reports;
using Dapper;

namespace AuctionPurchase.Infrastructure.Data
{
    public class ReportTemplateRepository
    {
        private readonly IDatabaseFactory _dbFactory;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public ReportTemplateRepository(IDatabaseFactory dbFactory) => _dbFactory = dbFactory;

        // ─────────────────────────────────────────────────────────────
        // Query
        // ─────────────────────────────────────────────────────────────

        public async Task<List<ReportTemplate>> GetByTypeAsync(string reportType)
        {
            await EnsureDefaultAsync(reportType);
            using var connection = _dbFactory.CreateConnection();
            return (await connection.QueryAsync<ReportTemplate>(
                "SELECT * FROM ReportTemplates WHERE ReportType=@ReportType ORDER BY IsDefault DESC, TemplateName",
                new { ReportType = reportType })).ToList();
        }

        public async Task<List<ReportTemplate>> GetAllAsync()
        {
            using var connection = _dbFactory.CreateConnection();
            return (await connection.QueryAsync<ReportTemplate>(
                "SELECT * FROM ReportTemplates ORDER BY ReportType, IsDefault DESC, TemplateName")).ToList();
        }

        public async Task<ReportTemplate?> GetAsync(int templateId)
        {
            using var connection = _dbFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<ReportTemplate>(
                "SELECT * FROM ReportTemplates WHERE TemplateId=@TemplateId",
                new { TemplateId = templateId });
        }

        // ─────────────────────────────────────────────────────────────
        // Layout deserialisation + migration
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Deserialises the stored JSON and, if it is a v1 layout, upgrades it to v2.
        /// </summary>
        public ReportTemplateLayout GetLayout(ReportTemplate template)
        {
            if (string.IsNullOrWhiteSpace(template.TemplateDefinition))
                return CreateDefaultLayout(template.ReportType);

            var layout = JsonSerializer.Deserialize<ReportTemplateLayout>(
                template.TemplateDefinition, JsonOptions);

            if (layout is null) throw new InvalidOperationException(
                "This report template definition could not be read.");

            // Migrate v1 → v2
            if (layout.SchemaVersion < 2)
                layout = LayoutMigrator.MigrateV1ToV2(layout);

            if (layout.Columns.Count == 0)
                throw new InvalidOperationException(
                    "This report template is not compatible with this application version.");

            // Ensure templates use Portrait A4 and Sale Date
            if (layout.Page != null)
            {
                layout.Page.Orientation = "Portrait";
                layout.Page.PageSize = "A4";
            }
            if (layout.HeaderElements != null)
            {
                foreach (var el in layout.HeaderElements)
                {
                    if (el.Content != null)
                    {
                        if (el.Content.Contains("Generated: {{GeneratedDate}}") || el.Content.Contains("{{GeneratedTime}}"))
                        {
                            el.Content = "Sale No: {{SaleNo}}        Sale Date: {{SaleDate}}";
                        }
                        else if (el.Content.Contains("{{GeneratedDate}}"))
                        {
                            el.Content = el.Content.Replace("{{GeneratedDate}}", "{{SaleDate}}");
                        }
                    }
                }
            }

            return layout;
        }

        // ─────────────────────────────────────────────────────────────
        // Save / duplicate / delete
        // ─────────────────────────────────────────────────────────────

        public async Task<int> SaveAsync(ReportTemplate template, ReportTemplateLayout layout)
        {
            ValidateLayout(layout);
            template.TemplateName = template.TemplateName.Trim();
            template.TemplateDefinition = JsonSerializer.Serialize(layout, JsonOptions);
            template.UpdatedAt = DateTime.Now;

            using var connection = _dbFactory.CreateConnection();
            if (template.TemplateId == 0)
            {
                template.CreatedAt = template.UpdatedAt;
                return await connection.QuerySingleAsync<int>(@"
                    INSERT INTO ReportTemplates
                        (TemplateName, ReportType, Version, TemplateDefinition, IsDefault, CreatedAt, UpdatedAt)
                    VALUES
                        (@TemplateName,@ReportType,@Version,@TemplateDefinition,@IsDefault,@CreatedAt,@UpdatedAt)
                    RETURNING TemplateId;", template);
            }

            await connection.ExecuteAsync(@"
                UPDATE ReportTemplates
                SET TemplateName=@TemplateName, Version=@Version,
                    TemplateDefinition=@TemplateDefinition, UpdatedAt=@UpdatedAt
                WHERE TemplateId=@TemplateId", template);
            return template.TemplateId;
        }

        public async Task DeleteAsync(int templateId)
        {
            using var connection = _dbFactory.CreateConnection();
            var isDefault = await connection.ExecuteScalarAsync<int>(
                "SELECT IsDefault FROM ReportTemplates WHERE TemplateId=@TemplateId",
                new { TemplateId = templateId });
            if (isDefault == 1)
                throw new InvalidOperationException("The default template cannot be deleted.");
            await connection.ExecuteAsync(
                "DELETE FROM ReportTemplates WHERE TemplateId=@TemplateId",
                new { TemplateId = templateId });
        }

        public async Task<ReportTemplate> DuplicateAsync(int templateId, string name)
        {
            var source = await GetAsync(templateId)
                ?? throw new InvalidOperationException("Template not found.");
            var layout = GetLayout(source);
            source.TemplateId = 0;
            source.TemplateName = name;
            source.IsDefault = false;
            source.CreatedAt = DateTime.Now;
            source.UpdatedAt = source.CreatedAt;
            var newId = await SaveAsync(source, layout);
            source.TemplateId = newId;
            return source;
        }

        // ─────────────────────────────────────────────────────────────
        // Import / Export  (.tktpl)
        // ─────────────────────────────────────────────────────────────

        public async Task<string> ExportAsync(int templateId)
        {
            var template = await GetAsync(templateId)
                ?? throw new InvalidOperationException("Template not found.");
            var layout = GetLayout(template);   // validates + migrates
            return JsonSerializer.Serialize(new
            {
                Format = "tktpl",
                Compatibility = 2,
                Template = template,
                Layout = layout
            }, JsonOptions);
        }

        public async Task<int> ImportAsync(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Format", out var format)
                || format.GetString() != "tktpl"
                || !doc.RootElement.TryGetProperty("Compatibility", out var compat)
                || compat.GetInt32() > 2
                || !doc.RootElement.TryGetProperty("Template", out var templateElement))
                throw new InvalidOperationException(
                    "This is not a compatible .tktpl template file (expected Compatibility ≤ 2).");

            var template = templateElement.Deserialize<ReportTemplate>(JsonOptions)
                ?? throw new InvalidOperationException("Template data is missing.");
            template.TemplateId = 0;
            template.IsDefault = false;

            var layout = GetLayout(template);   // migrate if v1
            await SaveAsync(template, layout);
            return template.TemplateId;
        }

        // ─────────────────────────────────────────────────────────────
        // Default layout factory
        // ─────────────────────────────────────────────────────────────

        public static ReportTemplateLayout CreateDefaultLayout(string reportType = "AllEntries")
        {
            var columns = new List<ReportColumnDefinition>();
            
            if (reportType == "BrandWise")
            {
                columns = new List<ReportColumnDefinition>
                {
                    new() { Field="BrokerName",   Title="Brokers Name",   Width=90 },
                    new() { Field="SaleNo",       Title="SL. NO.",        Width=50, Alignment="Center" },
                    new() { Field="LotNo",        Title="Lot. NO.",       Width=55, Alignment="Center" },
                    new() { Field="GardenName",   Title="Garden",         Width=110 },
                    new() { Field="InvoiceNo",    Title="Inv. NO.",       Width=70 },
                    new() { Field="GradeName",    Title="Grade",          Width=70 },
                    new() { Field="NumberOfBags", Title="NO. of Bags",    Width=60, Alignment="Right",
                            Format="N0", IncludeInTotal=true },
                    new() { Field="TotalKgs",     Title="Total KGS",      Width=75, Alignment="Right",
                            Format="N2", IncludeInTotal=true },
                    new() { Field="PricePerKg",   Title="Price per KGs",  Width=75, Alignment="Right",
                            Format="N2", IncludeInTotal=true },
                    new() { Field="Value",        Title="Value",          Width=95, Alignment="Right",
                            Format="N2", IncludeInTotal=true },
                    new() { Field="BlendName",    Title="Blend",          Width=65 }
                };
            }
            else if (reportType == "BrokerWise")
            {
                columns = new List<ReportColumnDefinition>
                {
                    new() { Field="BrandName",    Title="Brand",          Width=90 },
                    new() { Field="SaleNo",       Title="SL. NO.",        Width=50, Alignment="Center" },
                    new() { Field="LotNo",        Title="Lot. NO.",       Width=55, Alignment="Center" },
                    new() { Field="GardenName",   Title="Garden",         Width=110 },
                    new() { Field="InvoiceNo",    Title="Inv. NO.",       Width=70 },
                    new() { Field="GradeName",    Title="Grade",          Width=70 },
                    new() { Field="NumberOfBags", Title="NO. of Bags",    Width=60, Alignment="Right",
                            Format="N0", IncludeInTotal=true },
                    new() { Field="TotalKgs",     Title="Total KGS",      Width=75, Alignment="Right",
                            Format="N2", IncludeInTotal=true },
                    new() { Field="PricePerKg",   Title="Price per KGs",  Width=75, Alignment="Right",
                            Format="N2", IncludeInTotal=true },
                    new() { Field="Value",        Title="Value",          Width=95, Alignment="Right",
                            Format="N2", IncludeInTotal=true },
                    new() { Field="BlendName",    Title="Blend",          Width=65 }
                };
            }
            else if (reportType == "BrokerWiseSummary")
            {
                columns = new List<ReportColumnDefinition>
                {
                    new() { Field="BrokerName",   Title="Brokers Name",   Width=110 },
                    new() { Field="LotNo",        Title="Lot. NO.",       Width=150 },
                    new() { Field="InvoiceNo",    Title="Inv. NO.",       Width=150 },
                    new() { Field="NumberOfBags", Title="NO. of Bags",    Width=75, Alignment="Right",
                            Format="N0", IncludeInTotal=true },
                    new() { Field="TotalKgs",     Title="Total KGS",      Width=90, Alignment="Right",
                            Format="N2", IncludeInTotal=true },
                    new() { Field="PricePerKg",   Title="Price per KGs",  Width=85, Alignment="Right",
                            Format="N2", IncludeInTotal=true },
                    new() { Field="Value",        Title="Value",          Width=110, Alignment="Right",
                            Format="N2", IncludeInTotal=true }
                };
            }
            else if (reportType == "BrandWiseSummary")
            {
                columns = new List<ReportColumnDefinition>
                {
                    new() { Field="BrandName",    Title="Brand",          Width=120 },
                    new() { Field="BrokerName",   Title="Brokers Name",   Width=250 },
                    new() { Field="NumberOfBags", Title="NO. of Bags",    Width=75, Alignment="Right",
                            Format="N0", IncludeInTotal=true },
                    new() { Field="TotalKgs",     Title="Total KGS",      Width=90, Alignment="Right",
                            Format="N2", IncludeInTotal=true },
                    new() { Field="PricePerKg",   Title="Price per KGs",  Width=85, Alignment="Right",
                            Format="N2", IncludeInTotal=true },
                    new() { Field="Value",        Title="Value",          Width=110, Alignment="Right",
                            Format="N2", IncludeInTotal=true }
                };
            }
            else
            {
                columns = new List<ReportColumnDefinition>
                {
                    new() { Field="BrokerName", Title="Broker",   Width=100 },
                    new() { Field="BrandName",  Title="Brand",    Width=100 },
                    new() { Field="LotNo",      Title="Lot No",   Width=70  },
                    new() { Field="GardenName", Title="Garden",   Width=130 },
                    new() { Field="InvoiceNo",  Title="Invoice",  Width=90  },
                    new() { Field="GradeName",  Title="Grade",    Width=70  },
                    new() { Field="NumberOfBags", Title="Bags",   Width=65, Alignment="Right",
                            Format="N0", IncludeInTotal=true },
                    new() { Field="TotalKgs",   Title="KGs",      Width=85, Alignment="Right",
                            Format="N2", IncludeInTotal=true },
                    new() { Field="PricePerKg", Title="Price/KG", Width=85, Alignment="Right",
                            Format="N2", IncludeInTotal=true },
                    new() { Field="Value",      Title="Value",    Width=100, Alignment="Right",
                            Format="N2", IncludeInTotal=true }
                };
            }

            return new ReportTemplateLayout
            {
                SchemaVersion = 2,
                Page = new PageSetup
                {
                    PageSize    = "A4",
                    Orientation = "Portrait",
                    MarginTopMm    = 10,
                    MarginBottomMm = 10,
                    MarginLeftMm   = 10,
                    MarginRightMm  = 10
                },
                HeaderElements = new List<ReportSectionElement>
                {
                    new() { ElementType = "CompanyLogo",  Alignment = "Center", HeightMm = 14f },
                    new() { ElementType = "Text", Content = "{{CompanyName}}",
                            FontSize = 14f, Bold = true, Alignment = "Center" },
                    new() { ElementType = "Text", Content = "{{CompanyAddress}}",
                            FontSize = 9f,  Alignment = "Center" },
                    new() { ElementType = "Line" },
                    new() { ElementType = "Text", Content = "{{ReportTitle}}",
                            FontSize = 12f, Bold = true, Alignment = "Center" },
                    new() { ElementType = "Text",
                            Content = "Sale No: {{SaleNo}}        Sale Date: {{SaleDate}}",
                            FontSize = 9.5f, Alignment = "Center" },
                },
                Body = new BodyOptions
                {
                    ShowTotals            = true,
                    AlternateRowShading   = true,
                    TableHeaderBackground = "#1A3A6A",
                    TableHeaderForeground = "#FFFFFF",
                    TotalsBackground      = "#BDD7EE",
                    TotalsForeground      = "#000000",
                    AlternateRowBackground = "#EEF2FF"
                },
                Columns = columns,
                FooterElements = new List<ReportSectionElement>
                {
                    new() { ElementType = "Line" },
                    new() { ElementType = "Text",
                            Content = "Prepared By: {{PreparedBy}}     Checked By: {{CheckedBy}}     Approved By: {{ApprovedBy}}",
                            FontSize = 9f, Alignment = "Left" },
                    new() { ElementType = "Text",
                            Content = "Page {{PageNumber}} of {{TotalPages}}",
                            FontSize = 9f, Alignment = "Center" }
                }
            };
        }

        // ─────────────────────────────────────────────────────────────
        // Ensure default exists
        // ─────────────────────────────────────────────────────────────

        private async Task EnsureDefaultAsync(string reportType)
        {
            using var connection = _dbFactory.CreateConnection();
            var exists = await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM ReportTemplates WHERE ReportType=@ReportType",
                new { ReportType = reportType });
            if (exists > 0) return;

            var template = new ReportTemplate
            {
                TemplateName = $"{GetTitle(reportType)} — Standard",
                ReportType   = reportType,
                IsDefault    = true
            };
            await SaveAsync(template, CreateDefaultLayout(reportType));
        }

        private static string GetTitle(string reportType) => reportType switch
        {
            "BrandWise"         => "Brand Report",
            "BrokerWise"        => "Broker Report",
            "BrokerWiseSummary" => "Broker Wise Summary Report",
            "BrandWiseSummary"  => "Brand Wise Summary Report",
            _                   => "Tea Purchase Report"
        };

        // ─────────────────────────────────────────────────────────────
        // Validation
        // ─────────────────────────────────────────────────────────────

        private static void ValidateLayout(ReportTemplateLayout layout)
        {
            if (layout.Columns.Count == 0 ||
                layout.Columns.Any(c => string.IsNullOrWhiteSpace(c.Field) ||
                                        string.IsNullOrWhiteSpace(c.Title)))
                throw new InvalidOperationException(
                    "The template needs at least one complete table column.");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // V1 → V2 layout migrator
    // ─────────────────────────────────────────────────────────────────

    internal static class LayoutMigrator
    {
        /// <summary>
        /// Up-migrates a schema v1 layout (HeaderLines / FooterLines string lists)
        /// to a schema v2 layout (HeaderElements / FooterElements).
        /// </summary>
        public static ReportTemplateLayout MigrateV1ToV2(ReportTemplateLayout v1)
        {
            var layout = new ReportTemplateLayout
            {
                SchemaVersion = 2,
                Page = new PageSetup
                {
                    PageSize       = v1.PageSize ?? "A4",
                    Orientation    = v1.Orientation ?? "Landscape",
                    MarginTopMm    = v1.MarginTop    ?? 10,
                    MarginBottomMm = v1.MarginBottom ?? 10,
                    MarginLeftMm   = v1.MarginLeft   ?? 10,
                    MarginRightMm  = v1.MarginRight  ?? 10
                },
                Body = new BodyOptions
                {
                    ShowTotals = v1.ShowTotals ?? true
                },
                Columns = v1.Columns ?? new List<ReportColumnDefinition>()
            };

            // Convert header string lines → elements
            foreach (var line in v1.HeaderLines ?? new List<string>())
            {
                layout.HeaderElements.Add(new ReportSectionElement
                {
                    ElementType = "Text",
                    Content     = line,
                    Alignment   = "Center",
                    FontSize    = 10f
                });
            }

            // Convert footer string lines → elements
            foreach (var line in v1.FooterLines ?? new List<string>())
            {
                layout.FooterElements.Add(new ReportSectionElement
                {
                    ElementType = "Text",
                    Content     = line,
                    Alignment   = "Center",
                    FontSize    = 9f
                });
            }

            return layout;
        }
    }
}
