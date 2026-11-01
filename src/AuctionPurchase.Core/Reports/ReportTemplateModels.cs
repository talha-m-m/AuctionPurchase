using System;
using System.Collections.Generic;

namespace AuctionPurchase.Core.Reports
{
    public class ReportTemplate
    {
        public int TemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string ReportType { get; set; } = "AllEntries";
        public string Version { get; set; } = "1.0";
        public string TemplateDefinition { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    // ─────────────────────────────────────────────────────────────────
    // Schema v2: structured element model.
    // Schema v1 (HeaderLines / FooterLines string lists) is migrated
    // automatically by ReportTemplateRepository.GetLayout().
    // ─────────────────────────────────────────────────────────────────
    public class ReportTemplateLayout
    {
        public int SchemaVersion { get; set; } = 2;

        // ── Page setup ────────────────────────────────────────────────
        public PageSetup Page { get; set; } = new();

        // ── Header elements (rendered top-to-bottom) ──────────────────
        public List<ReportSectionElement> HeaderElements { get; set; } = new();

        // ── Body / table options ──────────────────────────────────────
        public List<ReportColumnDefinition> Columns { get; set; } = new();
        public BodyOptions Body { get; set; } = new();

        // ── Footer elements (rendered top-to-bottom) ──────────────────
        public List<ReportSectionElement> FooterElements { get; set; } = new();

        // ── Legacy v1 fields kept for migration only (not written by v2) ──
        public List<string>? HeaderLines { get; set; }
        public List<string>? FooterLines { get; set; }
        public bool? ShowTotals { get; set; }

        // ── Backwards-compat page fields kept for migration ───────────
        public string? PageSize { get; set; }
        public string? Orientation { get; set; }
        public decimal? MarginTop { get; set; }
        public decimal? MarginBottom { get; set; }
        public decimal? MarginLeft { get; set; }
        public decimal? MarginRight { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────
    // Page setup
    // ─────────────────────────────────────────────────────────────────
    public class PageSetup
    {
        /// <summary>A4 | Letter | Legal</summary>
        public string PageSize { get; set; } = "A4";
        /// <summary>Portrait | Landscape</summary>
        public string Orientation { get; set; } = "Portrait";
        public decimal MarginTopMm { get; set; } = 10;
        public decimal MarginBottomMm { get; set; } = 10;
        public decimal MarginLeftMm { get; set; } = 10;
        public decimal MarginRightMm { get; set; } = 10;
    }

    // ─────────────────────────────────────────────────────────────────
    // Body / table options
    // ─────────────────────────────────────────────────────────────────
    public class BodyOptions
    {
        public bool ShowTotals { get; set; } = true;
        public bool ShowGroupHeaders { get; set; } = false;
        public bool AlternateRowShading { get; set; } = true;
        /// <summary>Hex colour for table header row background, e.g. "#1A3A6A".</summary>
        public string TableHeaderBackground { get; set; } = "#1A3A6A";
        /// <summary>Hex colour for table header text.</summary>
        public string TableHeaderForeground { get; set; } = "#FFFFFF";
        /// <summary>Hex colour for totals row background.</summary>
        public string TotalsBackground { get; set; } = "#BDD7EE";
        /// <summary>Hex colour for totals row text.</summary>
        public string TotalsForeground { get; set; } = "#000000";
        /// <summary>Hex colour for alternate data row shading (empty = no shading).</summary>
        public string AlternateRowBackground { get; set; } = "#F0F4FF";
    }

    // ─────────────────────────────────────────────────────────────────
    // Section element  (one visual element in header or footer)
    // ─────────────────────────────────────────────────────────────────
    public class ReportSectionElement
    {
        // ── Identity ──────────────────────────────────────────────────
        /// <summary>
        /// Text | Field | Image | Line | Separator | CompanyLogo
        /// </summary>
        public string ElementType { get; set; } = "Text";

        /// <summary>
        /// Static text, a template variable such as {{CompanyName}},
        /// or empty for Line/Separator/CompanyLogo.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        // ── Font ──────────────────────────────────────────────────────
        public string FontFamily { get; set; } = "Segoe UI";
        public float FontSize { get; set; } = 10f;
        public bool Bold { get; set; } = false;
        public bool Italic { get; set; } = false;
        public bool Underline { get; set; } = false;

        // ── Layout ────────────────────────────────────────────────────
        /// <summary>Left | Center | Right</summary>
        public string Alignment { get; set; } = "Center";

        // ── Colour ────────────────────────────────────────────────────
        /// <summary>Hex, e.g. "#FFFFFF". Empty = default.</summary>
        public string TextColor { get; set; } = string.Empty;
        /// <summary>Hex, e.g. "#000000". Empty = transparent.</summary>
        public string BackgroundColor { get; set; } = string.Empty;

        // ── Size hints (for logo / image) ─────────────────────────────
        /// <summary>Width in mm (0 = auto / full-width).</summary>
        public float WidthMm { get; set; } = 0f;
        /// <summary>Height in mm (0 = auto).</summary>
        public float HeightMm { get; set; } = 15f;
    }

    // ─────────────────────────────────────────────────────────────────
    // Table column definition (extended in v2)
    // ─────────────────────────────────────────────────────────────────
    public class ReportColumnDefinition
    {
        // ── Data ──────────────────────────────────────────────────────
        public string Field { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        // ── Layout ────────────────────────────────────────────────────
        /// <summary>Relative width unit (same scale as QuestPDF RelativeColumn).</summary>
        public int Width { get; set; } = 100;
        /// <summary>Left | Center | Right</summary>
        public string Alignment { get; set; } = "Left";

        // ── Format ────────────────────────────────────────────────────
        /// <summary>Standard .NET format string: N0, N2, C2, dd/MM/yyyy, etc.</summary>
        public string Format { get; set; } = string.Empty;
        /// <summary>Decimal places override (used when Format is empty).</summary>
        public int DecimalPrecision { get; set; } = 2;
        /// <summary>Currency symbol prepended when Format starts with C.</summary>
        public string CurrencySymbol { get; set; } = string.Empty;
        /// <summary>Date format string override, e.g. "dd/MM/yyyy".</summary>
        public string DateFormat { get; set; } = string.Empty;

        // ── Visibility / totals ───────────────────────────────────────
        public bool IsVisible { get; set; } = true;
        public bool IncludeInTotal { get; set; } = false;

        // ── Style ─────────────────────────────────────────────────────
        public string FontFamily { get; set; } = string.Empty;
        public float FontSize { get; set; } = 0f;
        public bool Bold { get; set; } = false;
        /// <summary>Hex colour for data cell text. Empty = default.</summary>
        public string TextColor { get; set; } = string.Empty;
        /// <summary>Hex colour for data cell background. Empty = default.</summary>
        public string BackgroundColor { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────
    // Render context  (populated from settings + report filter at run-time)
    // ─────────────────────────────────────────────────────────────────
    public class ReportRenderContext
    {
        public string DateFormat { get; set; } = "dd/MM/yyyy";
        public string ReportTitle { get; set; } = string.Empty;
        public string ReportSubtitle { get; set; } = string.Empty;
        public string SaleNo { get; set; } = string.Empty;
        public DateTime? SaleDate { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        // Company profile
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string CompanyPhone { get; set; } = string.Empty;
        public string CompanyEmail { get; set; } = string.Empty;
        public string CompanyWebsite { get; set; } = string.Empty;
        /// <summary>Company logo as a Base64-encoded PNG/JPG string.</summary>
        public string CompanyLogoBase64 { get; set; } = string.Empty;

        // Signatories
        public string PreparedBy { get; set; } = string.Empty;
        public string CheckedBy { get; set; } = string.Empty;
        public string ApprovedBy { get; set; } = string.Empty;

        // Grouping
        public string ReportType { get; set; } = "AllEntries";
        public string GroupName { get; set; } = string.Empty;
        public string GroupTitle { get; set; } = string.Empty;

        public ReportRenderContext Clone() => (ReportRenderContext)MemberwiseClone();
    }
}
