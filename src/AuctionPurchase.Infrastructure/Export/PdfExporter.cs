using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AuctionPurchase.Core.Reports;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuctionPurchase.Infrastructure.Export
{
    /// <summary>
    /// Renders a report as a PDF document driven entirely by the report template.
    /// No hard-coded layouts exist here — all structure comes from
    /// <see cref="ReportTemplateLayout"/> and <see cref="ReportRenderContext"/>.
    /// </summary>
    public class PdfExporter
    {
        public PdfExporter()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseSystemFonts = true;
            QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
        }

        // ─────────────────────────────────────────────────────────────
        // Primary export  (used by all callers)
        // ─────────────────────────────────────────────────────────────

        public byte[] ExportTemplate(
            ReportTemplateLayout layout,
            ReportRenderContext context,
            List<ReportEntry> entries)
        {
            var columns  = layout.Columns.Where(c => c.IsVisible).ToList();
            var page     = layout.Page;
            var body     = layout.Body;

            bool isBrandWise = string.Equals(context.ReportType, "BrandWise", StringComparison.OrdinalIgnoreCase)
                || (context.ReportTitle?.Contains("Brand", StringComparison.OrdinalIgnoreCase) == true
                    && !context.ReportTitle.Contains("Broker", StringComparison.OrdinalIgnoreCase));

            bool isBrokerWise = string.Equals(context.ReportType, "BrokerWise", StringComparison.OrdinalIgnoreCase)
                || context.ReportTitle?.Contains("Broker", StringComparison.OrdinalIgnoreCase) == true;

            bool isGrouped = isBrandWise || isBrokerWise;

            var document = Document.Create(container =>
            {
                if (isGrouped)
                {
                    string groupField = isBrandWise ? "Brand" : "Broker";
                    var groups = isBrandWise
                        ? entries.GroupBy(e => string.IsNullOrWhiteSpace(e.BrandName) ? "(Unbranded)" : e.BrandName.Trim())
                                 .OrderBy(g => g.Key).ToList()
                        : entries.GroupBy(e => string.IsNullOrWhiteSpace(e.BrokerName) ? "(Unassigned)" : e.BrokerName.Trim())
                                 .OrderBy(g => g.Key).ToList();

                    // Omit the grouped column from the data table if present, since it's displayed in the page title
                    var effectiveColumns = isBrandWise
                        ? columns.Where(c => !string.Equals(c.Field, "BrandName", StringComparison.OrdinalIgnoreCase)
                                          && !string.Equals(c.Field, "Brand", StringComparison.OrdinalIgnoreCase)).ToList()
                        : columns.Where(c => !string.Equals(c.Field, "BrokerName", StringComparison.OrdinalIgnoreCase)
                                          && !string.Equals(c.Field, "Broker", StringComparison.OrdinalIgnoreCase)).ToList();

                    if (effectiveColumns.Count == 0) effectiveColumns = columns;

                    if (groups.Count == 0)
                    {
                        container.Page(p =>
                        {
                            ConfigurePageSetup(p, page);
                            p.Header().Column(header => RenderElements(header, layout.HeaderElements, context));
                            p.Content().PaddingTop(24).AlignCenter().Text("No entries found for the selected criteria.")
                                     .FontSize(11).Italic().FontColor(Colors.Grey.Medium);
                            p.Footer().Column(footer => RenderFooterElements(footer, layout.FooterElements, context));
                        });
                    }
                    else
                    {
                        foreach (var group in groups)
                        {
                            var groupEntries = group.ToList();
                            var groupTitle = $"{groupField}: {group.Key}";

                            container.Page(p =>
                            {
                                ConfigurePageSetup(p, page);

                                // ── Header ───────────────────────────────────────────
                                p.Header().Column(header =>
                                {
                                    var grpCtx = context.Clone();
                                    grpCtx.GroupName = group.Key;
                                    grpCtx.GroupTitle = groupTitle;

                                    RenderElements(header, layout.HeaderElements, grpCtx, groupTitle);
                                });

                                // ── Body (Data table for this brand / broker) ────────
                                p.Content().PaddingTop(6).Table(table =>
                                {
                                    table.ColumnsDefinition(def =>
                                    {
                                        var total = effectiveColumns.Sum(c => c.Width);
                                        foreach (var col in effectiveColumns)
                                            def.RelativeColumn((float)col.Width / (total > 0 ? total : 1));
                                    });

                                    // Header row
                                    table.Header(h =>
                                    {
                                        foreach (var col in effectiveColumns)
                                        {
                                            var bg = HexToColor(body.TableHeaderBackground, Colors.Blue.Darken2);
                                            var fg = HexToColor(body.TableHeaderForeground, Colors.White);
                                            h.Cell()
                                             .Background(bg)
                                             .Padding(3)
                                             .AlignmentFromString(col.Alignment)
                                             .Text(col.Title)
                                             .FontColor(fg)
                                             .Bold();
                                        }
                                    });

                                    // Data rows
                                    bool alt = false;
                                    foreach (var entry in groupEntries)
                                    {
                                        foreach (var col in effectiveColumns)
                                        {
                                            var cellBg = (!string.IsNullOrEmpty(col.BackgroundColor))
                                                ? HexToColor(col.BackgroundColor, Colors.White)
                                                : (alt && body.AlternateRowShading && !string.IsNullOrEmpty(body.AlternateRowBackground))
                                                    ? HexToColor(body.AlternateRowBackground, Colors.White)
                                                    : Colors.White;

                                            var cellContainer = table.Cell()
                                                            .Border(0.5f)
                                                            .BorderColor(Colors.Grey.Lighten2)
                                                            .Background(cellBg)
                                                            .Padding(2);

                                            var txtContainer = cellContainer.AlignmentFromString(col.Alignment);
                                            ApplyColumnTextStyle(txtContainer, col).Text(ReportTemplateValueResolver.Display(entry, col));
                                        }
                                        alt = !alt;
                                    }

                                    // Group Sub-Total row
                                    if (body.ShowTotals)
                                    {
                                        var subTotalBg = HexToColor(body.TotalsBackground, Colors.Blue.Lighten4);
                                        var subTotalFg = HexToColor(body.TotalsForeground, Colors.Black);
                                        bool firstSubTotalCol = true;
                                        foreach (var col in effectiveColumns)
                                        {
                                            string value;
                                            if (col.IncludeInTotal || string.Equals(col.Field, "PricePerKg", StringComparison.OrdinalIgnoreCase))
                                            {
                                                value = ReportTemplateValueResolver.Total(groupEntries, col.Field)
                                                    .ToString(string.IsNullOrWhiteSpace(col.Format) ? "N2" : col.Format);
                                            }
                                            else if (firstSubTotalCol)
                                            {
                                                value = $"Total - {group.Key}";
                                                firstSubTotalCol = false;
                                            }
                                            else
                                            {
                                                value = "";
                                            }

                                            table.Cell()
                                                 .Background(subTotalBg)
                                                 .Padding(3)
                                                 .AlignmentFromString(col.Alignment)
                                                 .Text(value)
                                                 .FontColor(subTotalFg)
                                                 .Bold();
                                        }
                                    }
                                });

                                // ── Footer ───────────────────────────────────────────
                                p.Footer().Column(footer =>
                                {
                                    var grpCtx = context.Clone();
                                    grpCtx.GroupName = group.Key;
                                    grpCtx.GroupTitle = groupTitle;
                                    RenderFooterElements(footer, layout.FooterElements, grpCtx);
                                });
                            });
                        }
                    }
                }
                else
                {
                    // ── Standard flat report (AllEntries) ────────────────────
                    container.Page(p =>
                    {
                        ConfigurePageSetup(p, page);

                        // Header
                        p.Header().Column(header =>
                        {
                            RenderElements(header, layout.HeaderElements, context);
                        });

                        // Body
                        p.Content().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(def =>
                            {
                                var total = columns.Sum(c => c.Width);
                                foreach (var col in columns)
                                    def.RelativeColumn((float)col.Width / (total > 0 ? total : 1));
                            });

                            // Header row
                            table.Header(h =>
                            {
                                foreach (var col in columns)
                                {
                                    var bg = HexToColor(body.TableHeaderBackground, Colors.Blue.Darken2);
                                    var fg = HexToColor(body.TableHeaderForeground, Colors.White);
                                    h.Cell()
                                     .Background(bg)
                                     .Padding(3)
                                     .AlignmentFromString(col.Alignment)
                                     .Text(col.Title)
                                     .FontColor(fg)
                                     .Bold();
                                }
                            });

                            bool alt = false;
                            foreach (var entry in entries)
                            {
                                foreach (var col in columns)
                                {
                                    var cellBg = (!string.IsNullOrEmpty(col.BackgroundColor))
                                        ? HexToColor(col.BackgroundColor, Colors.White)
                                        : (alt && body.AlternateRowShading && !string.IsNullOrEmpty(body.AlternateRowBackground))
                                            ? HexToColor(body.AlternateRowBackground, Colors.White)
                                            : Colors.White;

                                    var cell = table.Cell()
                                                    .Border(0.5f)
                                                    .BorderColor(Colors.Grey.Lighten2)
                                                    .Background(cellBg)
                                                    .Padding(2);

                                    var container = cell.AlignmentFromString(col.Alignment);
                                    ApplyColumnTextStyle(container, col).Text(ReportTemplateValueResolver.Display(entry, col));
                                }
                                alt = !alt;
                            }

                            // Totals row
                            if (body.ShowTotals)
                            {
                                var totalBg = HexToColor(body.TotalsBackground, Colors.Blue.Lighten4);
                                var totalFg = HexToColor(body.TotalsForeground, Colors.Black);
                                foreach (var col in columns)
                                {
                                    string value = (col.IncludeInTotal || string.Equals(col.Field, "PricePerKg", StringComparison.OrdinalIgnoreCase))
                                        ? ReportTemplateValueResolver.Total(entries, col.Field)
                                            .ToString(string.IsNullOrWhiteSpace(col.Format) ? "N2" : col.Format)
                                        : (col == columns.First() ? "Total" : "");

                                    table.Cell()
                                         .Background(totalBg)
                                         .Padding(3)
                                         .AlignmentFromString(col.Alignment)
                                         .Text(value)
                                         .FontColor(totalFg)
                                         .Bold();
                                }
                            }
                        });

                        // Footer
                        p.Footer().Column(footer =>
                        {
                            RenderFooterElements(footer, layout.FooterElements, context);
                        });
                    });
                }
            });

            return document.GeneratePdf();
        }

        private static void ConfigurePageSetup(PageDescriptor p, PageSetup page)
        {
            p.Size(ResolvePageSize(page.PageSize, page.Orientation));
            p.MarginTop   ((float)page.MarginTopMm,    Unit.Millimetre);
            p.MarginBottom((float)page.MarginBottomMm, Unit.Millimetre);
            p.MarginLeft  ((float)page.MarginLeftMm,   Unit.Millimetre);
            p.MarginRight ((float)page.MarginRightMm,  Unit.Millimetre);
            p.DefaultTextStyle(x => x.FontSize(8).FontFamily("Segoe UI"));
        }

        // ─────────────────────────────────────────────────────────────
        // Header / footer element rendering
        // ─────────────────────────────────────────────────────────────

        private static void RenderElements(
            ColumnDescriptor col,
            List<ReportSectionElement> elements,
            ReportRenderContext ctx,
            string? groupTitle = null)
        {
            bool hasGroupInElements = elements.Any(el =>
                el.Content.Contains("{{GroupTitle}}") ||
                el.Content.Contains("{{GroupName}}") ||
                (!string.IsNullOrEmpty(ctx.GroupName) && el.Content.Contains(ctx.GroupName, StringComparison.OrdinalIgnoreCase)));

            bool groupTitleRendered = false;

            foreach (var el in elements)
            {
                // In demo image: Company -> Report Title -> Brand: RPL -> Sale No / Date
                if (!string.IsNullOrEmpty(groupTitle) && !hasGroupInElements && !groupTitleRendered)
                {
                    if (el.Content.Contains("{{SaleNo}}") || el.Content.Contains("Sale No"))
                    {
                        col.Item().PaddingTop(3).PaddingBottom(3).AlignCenter()
                           .Text(groupTitle)
                           .FontSize(13.5f)
                           .Bold()
                           .FontColor(Colors.Black);
                        groupTitleRendered = true;
                    }
                }

                RenderElement(col, el, ctx, pageNum: null, totalPages: null);
            }

            if (!string.IsNullOrEmpty(groupTitle) && !hasGroupInElements && !groupTitleRendered)
            {
                col.Item().PaddingTop(3).PaddingBottom(3).AlignCenter()
                   .Text(groupTitle)
                   .FontSize(13.5f)
                   .Bold()
                   .FontColor(Colors.Black);
            }
        }

        private static void RenderFooterElements(
            ColumnDescriptor col,
            List<ReportSectionElement> elements,
            ReportRenderContext ctx)
        {
            foreach (var el in elements)
            {
                // Saved templates can still contain the old approval row.
                // Omit it at PDF rendering time so existing reports update too.
                var content = el.Content ?? string.Empty;
                if (new[] { "{{PreparedBy}}", "{{CheckedBy}}", "{{ApprovedBy}}", "Prepared By:", "Checked By:", "Approved By:" }
                    .Any(label => content.Contains(label, StringComparison.OrdinalIgnoreCase)))
                    continue;
                RenderElement(col, el, ctx, pageNum: 0, totalPages: 0);
            }
        }

        private static void RenderElement(
            ColumnDescriptor col,
            ReportSectionElement el,
            ReportRenderContext ctx,
            int? pageNum,
            int? totalPages)
        {
            switch (el.ElementType)
            {
                case "Line":
                case "Separator":
                    col.Item().PaddingTop(2).PaddingBottom(2).LineHorizontal(0.5f);
                    break;

                case "CompanyLogo":
                    if (!string.IsNullOrWhiteSpace(ctx.CompanyLogoBase64))
                    {
                        try
                        {
                            var bytes = Convert.FromBase64String(ctx.CompanyLogoBase64);
                            float h = el.HeightMm > 0 ? el.HeightMm : 14f;
                            col.Item()
                               .AlignmentFromString(el.Alignment)
                               .Height(h, Unit.Millimetre)
                               .Image(bytes)
                               .FitHeight();
                        }
                        catch { /* skip broken logo */ }
                    }
                    break;

                case "Text":
                case "Field":
                default:
                    {
                        var content = ReportTemplateValueResolver.Element(el, ctx);
                        if (el.Content.Contains("{{PageNumber}}") || el.Content.Contains("{{TotalPages}}"))
                        {
                            // Footer page number — split around tokens
                            RenderPageNumberText(col, el, ctx);
                        }
                        else if (el.Content.Contains("{{SaleNo}}") && (el.Content.Contains("{{SaleDate}}") || el.Content.Contains("Sale Date") || el.Content.Contains("Date")))
                        {
                            var saleNoStr = !string.IsNullOrWhiteSpace(ctx.SaleNo) ? $"Sale No: {ctx.SaleNo}" : "";
                            var saleDateVal = ctx.SaleDate?.ToString(ctx.DateFormat) ?? (ctx.FromDate?.ToString(ctx.DateFormat) ?? "");
                            var saleDateStr = !string.IsNullOrWhiteSpace(saleDateVal) ? $"Date: {saleDateVal}" : "";

                            col.Item().PaddingTop(2).PaddingBottom(2).Row(r =>
                            {
                                r.RelativeItem().AlignLeft().Text(saleNoStr).FontSize(el.FontSize > 0 ? el.FontSize : 9.5f).Bold();
                                r.RelativeItem().AlignRight().Text(saleDateStr).FontSize(el.FontSize > 0 ? el.FontSize : 9.5f).Bold();
                            });
                        }
                        else
                        {
                            var container = col.Item().AlignmentFromString(el.Alignment);
                            ApplyElementTextStyle(container, el).Text(content);
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// Handles a footer line that contains {{PageNumber}} and/or {{TotalPages}}
        /// by splitting the text and injecting QuestPDF page-number spans.
        /// </summary>
        private static void RenderPageNumberText(
            ColumnDescriptor col,
            ReportSectionElement el,
            ReportRenderContext ctx)
        {
            var container = col.Item().AlignmentFromString(el.Alignment);
            ApplyElementTextStyle(container, el).Text(text =>
               {

                   // Split on {{PageNumber}} first
                   var parts = el.Content.Split("{{PageNumber}}", StringSplitOptions.None);
                   for (var i = 0; i < parts.Length; i++)
                   {
                       // Each part may contain {{TotalPages}}
                       var subparts = parts[i].Split("{{TotalPages}}", StringSplitOptions.None);
                       for (var j = 0; j < subparts.Length; j++)
                       {
                           var resolved = ReportTemplateValueResolver.Header(subparts[j], ctx);
                           if (!string.IsNullOrEmpty(resolved))
                               text.Span(resolved);
                           if (j < subparts.Length - 1)
                               text.TotalPages();
                       }
                       if (i < parts.Length - 1)
                           text.CurrentPageNumber();
                   }
               });
        }

        // ─────────────────────────────────────────────────────────────
        // Style helpers
        // ─────────────────────────────────────────────────────────────

        private static IContainer ApplyElementTextStyle(IContainer txt, ReportSectionElement el)
        {
            return txt.DefaultTextStyle(x =>
            {
                x = x.FontSize(el.FontSize > 0 ? el.FontSize : 9f);
                if (!string.IsNullOrWhiteSpace(el.FontFamily)) x = x.FontFamily(el.FontFamily);
                if (el.Bold)   x = x.Bold();
                if (el.Italic) x = x.Italic();
                if (!string.IsNullOrWhiteSpace(el.TextColor))
                    x = x.FontColor(el.TextColor);
                return x;
            });
        }


        private static IContainer ApplyColumnTextStyle(IContainer txt, ReportColumnDefinition col)
        {
            return txt.DefaultTextStyle(x =>
            {
                if (col.FontSize > 0) x = x.FontSize(col.FontSize);
                if (!string.IsNullOrWhiteSpace(col.FontFamily)) x = x.FontFamily(col.FontFamily);
                if (col.Bold) x = x.Bold();
                if (!string.IsNullOrWhiteSpace(col.TextColor)) x = x.FontColor(col.TextColor);
                return x;
            });
        }

        // ─────────────────────────────────────────────────────────────
        // Colour / page-size helpers
        // ─────────────────────────────────────────────────────────────

        private static string HexToColor(string hex, string fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            // QuestPDF accepts "#RRGGBB" strings directly
            return hex.StartsWith('#') ? hex : $"#{hex}";
        }

        private static PageSize ResolvePageSize(string size, string orientation)
        {
            var base_ = size switch
            {
                "Letter" => PageSizes.Letter,
                "Legal"  => PageSizes.Legal,
                _        => PageSizes.A4
            };
            return string.Equals(orientation, "Landscape", StringComparison.OrdinalIgnoreCase) 
                ? base_.Landscape() 
                : base_;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // QuestPDF extension helpers
    // ─────────────────────────────────────────────────────────────────

    internal static class QuestPdfExtensions
    {
        public static IContainer AlignmentFromString(this IContainer c, string alignment) =>
            alignment?.ToUpperInvariant() switch
            {
                "RIGHT"  => c.AlignRight(),
                "CENTER" => c.AlignCenter(),
                _        => c.AlignLeft()
            };
    }
}
