using System;
using System.Linq;
using AuctionPurchase.Core.Reports;

namespace AuctionPurchase.Infrastructure.Export
{
    /// <summary>
    /// Resolves template variables and formats report data values.
    /// All renderers (PDF, DOCX, XLSX) use this class — no renderer owns
    /// a hard-coded field mapping.
    /// </summary>
    public static class ReportTemplateValueResolver
    {
        // ─────────────────────────────────────────────────────────────
        // Header / footer text variable replacement
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Replaces all {{Variable}} tokens in <paramref name="text"/>
        /// with values from the render context.
        /// </summary>
        public static string Header(string text, ReportRenderContext context,
                                    int? page = null, int? pages = null) => text
            .Replace("{{CompanyName}}",    context.CompanyName)
            .Replace("{{CompanyAddress}}", context.CompanyAddress)
            .Replace("{{CompanyPhone}}",   context.CompanyPhone)
            .Replace("{{CompanyEmail}}",   context.CompanyEmail)
            .Replace("{{CompanyWebsite}}", context.CompanyWebsite)
            .Replace("{{ReportTitle}}",    context.ReportTitle)
            .Replace("{{ReportSubtitle}}", context.ReportSubtitle)
            .Replace("{{ReportType}}",     context.ReportType)
            .Replace("{{GroupTitle}}",     context.GroupTitle)
            .Replace("{{GroupName}}",      context.GroupName)
            .Replace("{{BrandName}}",      context.GroupName)
            .Replace("{{BrokerName}}",     context.GroupName)
            .Replace("{{SaleNo}}",         context.SaleNo)
            .Replace("{{SaleDate}}",       context.SaleDate?.ToString(context.DateFormat) ?? (context.FromDate?.ToString(context.DateFormat) ?? ""))
            .Replace("{{FromDate}}",       context.FromDate?.ToString(context.DateFormat) ?? "")
            .Replace("{{ToDate}}",         context.ToDate?.ToString(context.DateFormat) ?? "")
            .Replace("{{GeneratedDate}}",  context.SaleDate?.ToString(context.DateFormat) ?? (context.FromDate?.ToString(context.DateFormat) ?? ""))
            .Replace("{{GeneratedTime}}",  "")
            .Replace("{{PreparedBy}}",     context.PreparedBy)
            .Replace("{{CheckedBy}}",      context.CheckedBy)
            .Replace("{{ApprovedBy}}",     context.ApprovedBy)
            .Replace("{{PageNumber}}",     page?.ToString() ?? "")
            .Replace("{{TotalPages}}",     pages?.ToString() ?? "");

        // ─────────────────────────────────────────────────────────────
        // Structured element content resolver
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the display text for a <see cref="ReportSectionElement"/>.
        /// Returns an empty string for non-text elements (Line, Separator, CompanyLogo).
        /// </summary>
        public static string Element(ReportSectionElement el, ReportRenderContext ctx)
        {
            if (el.ElementType is "Line" or "Separator" or "CompanyLogo") return string.Empty;
            return Header(el.Content, ctx);
        }

        // ─────────────────────────────────────────────────────────────
        // Data row field resolution
        // ─────────────────────────────────────────────────────────────

        public static object Value(ReportEntry entry, string field) => field switch
        {
            "SaleNo"        => entry.SaleNo,
            "SaleDate"      => entry.SaleDate,
            "BrandName" or "Brand"   => entry.BrandName,
            "BrokerName" or "Broker" => entry.BrokerName,
            "LotNo"         => entry.LotNo,
            "GardenName" or "Garden" => entry.GardenDisplay,
            "InvoiceNo"     => entry.InvoiceNo,
            "GradeName" or "Grade"   => entry.GradeName,
            "NumberOfBags"  => entry.NumberOfBags,
            "TotalKgs"      => entry.TotalKgs,
            "PricePerKg"    => entry.PricePerKg,
            "Value"         => entry.Value,
            "ShedName" or "Shed"   => (object)(entry.ShedName ?? string.Empty),
            "BlendName" or "Blend" => (object)(entry.BlendName ?? string.Empty),
            _               => string.Empty
        };

        /// <summary>
        /// Returns a formatted display string for a data cell.
        /// Uses the column's Format if provided, otherwise falls back
        /// to DecimalPrecision or DateFormat if set.
        /// </summary>
        public static string Display(ReportEntry entry, ReportColumnDefinition column)
        {
            var value = Value(entry, column.Field);

            // Use explicit Format if present
            if (value is IFormattable formattable)
            {
                if (!string.IsNullOrWhiteSpace(column.Format))
                    return formattable.ToString(column.Format, null);

                // Use DateFormat for date values
                if (value is DateTime dt && !string.IsNullOrWhiteSpace(column.DateFormat))
                    return dt.ToString(column.DateFormat);

                // Use DecimalPrecision for numeric values (if no Format set)
                if (value is decimal or double or float or int or long)
                    return formattable.ToString($"N{column.DecimalPrecision}", null);
            }

            return value?.ToString() ?? string.Empty;
        }

        // ─────────────────────────────────────────────────────────────
        // Column totals
        // ─────────────────────────────────────────────────────────────

        public static decimal Total(System.Collections.Generic.IEnumerable<ReportEntry> entries,
                                    string field) => field switch
        {
            "NumberOfBags" => entries.Sum(e => e.NumberOfBags),
            "TotalKgs"     => entries.Sum(e => e.TotalKgs),
            "PricePerKg"   => entries.Any() && entries.Sum(e => e.TotalKgs) > 0
                                ? entries.Sum(e => e.Value) / entries.Sum(e => e.TotalKgs)
                                : 0,
            "Value"        => entries.Sum(e => e.Value),
            _              => 0
        };
    }
}
