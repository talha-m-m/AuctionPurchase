using ClosedXML.Excel;
using AuctionPurchase.Core.Reports;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuctionPurchase.Infrastructure.Export
{
    /// <summary>
    /// Renders a report as an editable XLSX workbook driven entirely by the
    /// report template.  No hard-coded layouts exist here.
    /// </summary>
    public class ExcelExporter
    {
        public byte[] ExportTemplate(
            ReportTemplateLayout layout,
            ReportRenderContext context,
            List<ReportEntry> entries)
        {
            using var workbook = new XLWorkbook();
            var ws      = workbook.Worksheets.Add("Report");
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            var columns = layout.Columns.Where(c => c.IsVisible).ToList();
            var body    = layout.Body;
            int row     = 1;

            bool isBrandWise = string.Equals(context.ReportType, "BrandWise", StringComparison.OrdinalIgnoreCase)
                || (context.ReportTitle?.Contains("Brand", StringComparison.OrdinalIgnoreCase) == true
                    && !context.ReportTitle.Contains("Broker", StringComparison.OrdinalIgnoreCase));

            bool isBrokerWise = string.Equals(context.ReportType, "BrokerWise", StringComparison.OrdinalIgnoreCase)
                || context.ReportTitle?.Contains("Broker", StringComparison.OrdinalIgnoreCase) == true;

            bool isGrouped = isBrandWise || isBrokerWise;

            // Company logo
            if (!string.IsNullOrWhiteSpace(context.CompanyLogoBase64))
            {
                try
                {
                    var logoBytes = Convert.FromBase64String(context.CompanyLogoBase64);
                    var img = ws.AddPicture(new MemoryStream(logoBytes))
                               .MoveTo(ws.Cell(row, 1))
                               .WithSize(160, 50);
                    row += 4;
                }
                catch { }
            }

            // Header elements
            foreach (var el in layout.HeaderElements)
            {
                if (el.ElementType is "CompanyLogo") continue;
                if (el.ElementType is "Line" or "Separator")
                {
                    ws.Range(row, 1, row, columns.Count > 0 ? columns.Count : 1)
                      .Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    row++;
                    continue;
                }

                var text   = ReportTemplateValueResolver.Element(el, context);
                var cell   = ws.Cell(row, 1);
                cell.Value = text;

                if (columns.Count > 1)
                    ws.Range(row, 1, row, columns.Count).Merge();

                var style = cell.Style;
                style.Alignment.Horizontal = AlignFromString(el.Alignment);
                style.Font.Bold            = el.Bold;
                style.Font.Italic          = el.Italic;
                if (el.FontSize > 0) style.Font.FontSize = el.FontSize;
                if (!string.IsNullOrWhiteSpace(el.FontFamily)) style.Font.FontName = el.FontFamily;
                if (!string.IsNullOrWhiteSpace(el.TextColor))
                    style.Font.FontColor = XLColor.FromHtml(el.TextColor);
                if (!string.IsNullOrWhiteSpace(el.BackgroundColor))
                    style.Fill.BackgroundColor = XLColor.FromHtml(el.BackgroundColor);

                ws.Row(row).Height = el.FontSize > 12 ? el.FontSize * 1.5 : 15;
                row++;
            }

            row++; // blank row before table

            // Column header row
            var hdrBg = ParseColor(body.TableHeaderBackground, XLColor.FromArgb(26, 58, 106));
            var hdrFg = ParseColor(body.TableHeaderForeground, XLColor.White);

            for (var i = 0; i < columns.Count; i++)
            {
                var col  = columns[i];
                var cell = ws.Cell(row, i + 1);
                cell.Value = col.Title;
                cell.Style.Font.Bold             = true;
                cell.Style.Fill.BackgroundColor  = hdrBg;
                cell.Style.Font.FontColor        = hdrFg;
                cell.Style.Alignment.Horizontal  = AlignFromString(col.Alignment);
                cell.Style.Border.OutsideBorder  = XLBorderStyleValues.Thin;
            }
            row++;

            // Data rows
            var altBg = ParseColor(body.AlternateRowBackground, XLColor.FromArgb(238, 242, 255));

            if (isGrouped)
            {
                string groupField = isBrandWise ? "Brand" : "Broker";
                var groups = isBrandWise
                    ? entries.GroupBy(e => string.IsNullOrWhiteSpace(e.BrandName) ? "(Unbranded)" : e.BrandName.Trim())
                             .OrderBy(g => g.Key).ToList()
                    : entries.GroupBy(e => string.IsNullOrWhiteSpace(e.BrokerName) ? "(Unassigned)" : e.BrokerName.Trim())
                             .OrderBy(g => g.Key).ToList();

                foreach (var group in groups)
                {
                    // Group header row
                    var grpCell = ws.Cell(row, 1);
                    grpCell.Value = $"{groupField}: {group.Key}";
                    if (columns.Count > 1) ws.Range(row, 1, row, columns.Count).Merge();
                    grpCell.Style.Font.Bold = true;
                    grpCell.Style.Font.FontColor = XLColor.White;
                    grpCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A3A6A");
                    grpCell.Style.Font.FontSize = 11;
                    row++;

                    var groupEntries = group.ToList();
                    bool alt = false;
                    foreach (var entry in groupEntries)
                    {
                        for (var i = 0; i < columns.Count; i++)
                        {
                            var col  = columns[i];
                            var cell = ws.Cell(row, i + 1);
                            var raw  = ReportTemplateValueResolver.Value(entry, col.Field);
                            cell.Value = raw switch
                            {
                                decimal d    => XLCellValue.FromObject(d),
                                int     n    => XLCellValue.FromObject(n),
                                DateTime dt  => XLCellValue.FromObject(dt),
                                _            => (XLCellValue)ReportTemplateValueResolver.Display(entry, col)
                            };
                            if (!string.IsNullOrWhiteSpace(col.Format)) cell.Style.NumberFormat.Format = ToExcelFormat(col);
                            cell.Style.Alignment.Horizontal = AlignFromString(col.Alignment);
                            if (!string.IsNullOrWhiteSpace(col.TextColor)) cell.Style.Font.FontColor = XLColor.FromHtml(col.TextColor);
                            if (!string.IsNullOrWhiteSpace(col.BackgroundColor)) cell.Style.Fill.BackgroundColor = XLColor.FromHtml(col.BackgroundColor);
                            else if (alt && body.AlternateRowShading) cell.Style.Fill.BackgroundColor = altBg;
                            if (col.Bold) cell.Style.Font.Bold = true;
                            if (col.FontSize > 0) cell.Style.Font.FontSize = col.FontSize;
                        }
                        alt = !alt;
                        row++;
                    }

                    // Sub-total
                    if (body.ShowTotals)
                    {
                        var subTotBg = ParseColor(body.TotalsBackground, XLColor.FromArgb(189, 215, 238));
                        ws.Row(row).Style.Font.Bold = true;
                        for (var i = 0; i < columns.Count; i++)
                        {
                            var col  = columns[i];
                            var cell = ws.Cell(row, i + 1);
                            if (col.IncludeInTotal || string.Equals(col.Field, "PricePerKg", StringComparison.OrdinalIgnoreCase))
                            {
                                cell.Value = ReportTemplateValueResolver.Total(groupEntries, col.Field);
                                if (!string.IsNullOrWhiteSpace(col.Format)) cell.Style.NumberFormat.Format = ToExcelFormat(col);
                            }
                            else if (i == 0) cell.Value = $"Total - {group.Key}";
                            cell.Style.Fill.BackgroundColor = subTotBg;
                            cell.Style.Alignment.Horizontal = AlignFromString(col.Alignment);
                        }
                        row++;
                    }

                    // Page break after each brand / broker
                    ws.PageSetup.AddHorizontalPageBreak(row - 1);
                    row++;
                }

                // Grand total
                if (body.ShowTotals && groups.Count > 1)
                {
                    ws.Row(row).Style.Font.Bold = true;
                    ws.Row(row).Style.Font.FontColor = XLColor.White;
                    for (var i = 0; i < columns.Count; i++)
                    {
                        var col  = columns[i];
                        var cell = ws.Cell(row, i + 1);
                        if (col.IncludeInTotal || string.Equals(col.Field, "PricePerKg", StringComparison.OrdinalIgnoreCase))
                        {
                            cell.Value = ReportTemplateValueResolver.Total(entries, col.Field);
                            if (!string.IsNullOrWhiteSpace(col.Format)) cell.Style.NumberFormat.Format = ToExcelFormat(col);
                        }
                        else if (i == 0) cell.Value = "Grand Total";
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A3A6A");
                        cell.Style.Alignment.Horizontal = AlignFromString(col.Alignment);
                    }
                    row++;
                }
            }
            else
            {
                bool alt = false;
                foreach (var entry in entries)
                {
                    for (var i = 0; i < columns.Count; i++)
                    {
                        var col  = columns[i];
                        var cell = ws.Cell(row, i + 1);
                        var raw  = ReportTemplateValueResolver.Value(entry, col.Field);

                        cell.Value = raw switch
                        {
                            decimal d    => XLCellValue.FromObject(d),
                            int     n    => XLCellValue.FromObject(n),
                            DateTime dt  => XLCellValue.FromObject(dt),
                            _            => (XLCellValue)ReportTemplateValueResolver.Display(entry, col)
                        };

                        if (!string.IsNullOrWhiteSpace(col.Format))
                            cell.Style.NumberFormat.Format = ToExcelFormat(col);

                        cell.Style.Alignment.Horizontal = AlignFromString(col.Alignment);

                        if (!string.IsNullOrWhiteSpace(col.TextColor))
                            cell.Style.Font.FontColor = XLColor.FromHtml(col.TextColor);
                        if (!string.IsNullOrWhiteSpace(col.BackgroundColor))
                            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(col.BackgroundColor);
                        else if (alt && body.AlternateRowShading)
                            cell.Style.Fill.BackgroundColor = altBg;

                        if (col.Bold) cell.Style.Font.Bold = true;
                        if (col.FontSize > 0) cell.Style.Font.FontSize = col.FontSize;
                    }
                    alt = !alt;
                    row++;
                }

                // Totals row
                if (body.ShowTotals)
                {
                    var totBg = ParseColor(body.TotalsBackground, XLColor.FromArgb(189, 215, 238));
                    ws.Row(row).Style.Font.Bold = true;

                    for (var i = 0; i < columns.Count; i++)
                    {
                        var col  = columns[i];
                        var cell = ws.Cell(row, i + 1);

                        if (col.IncludeInTotal)
                        {
                            var total = ReportTemplateValueResolver.Total(entries, col.Field);
                            cell.Value = total;
                            if (!string.IsNullOrWhiteSpace(col.Format))
                                cell.Style.NumberFormat.Format = ToExcelFormat(col);
                        }
                        else if (i == 0)
                        {
                            cell.Value = "Total";
                        }

                        cell.Style.Fill.BackgroundColor = totBg;
                        cell.Style.Alignment.Horizontal = AlignFromString(col.Alignment);
                    }
                    row++;
                }
            }

            // Footer elements
            row++;
            foreach (var el in layout.FooterElements)
            {
                if (el.ElementType is "Line" or "Separator")
                {
                    ws.Range(row, 1, row, columns.Count > 0 ? columns.Count : 1)
                      .Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    continue;
                }
                var text   = ReportTemplateValueResolver.Element(el, context);
                var cell   = ws.Cell(row, 1);
                cell.Value = text;
                if (columns.Count > 1)
                    ws.Range(row, 1, row, columns.Count).Merge();
                var style = cell.Style;
                style.Alignment.Horizontal = AlignFromString(el.Alignment);
                style.Font.Italic          = el.Italic;
                if (el.FontSize > 0) style.Font.FontSize = el.FontSize;
                row++;
            }

            // Column widths
            ws.Columns().AdjustToContents(minWidth: 8, maxWidth: 60);

            using var result = new MemoryStream();
            workbook.SaveAs(result);
            return result.ToArray();
        }

        // Helpers

        private static XLAlignmentHorizontalValues AlignFromString(string s) =>
            s?.ToUpperInvariant() switch
            {
                "RIGHT"  => XLAlignmentHorizontalValues.Right,
                "CENTER" => XLAlignmentHorizontalValues.Center,
                _        => XLAlignmentHorizontalValues.Left
            };

        private static XLColor ParseColor(string hex, XLColor fallback)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                    return XLColor.FromHtml(hex.StartsWith('#') ? hex : $"#{hex}");
            }
            catch { }
            return fallback;
        }

        private static string ToExcelFormat(ReportColumnDefinition col)
        {
            if (string.IsNullOrWhiteSpace(col.Format)) return string.Empty;
            return col.Format switch
            {
                "N0" => "#,##0",
                "N1" => "#,##0.0",
                "N2" => "#,##0.00",
                "N3" => "#,##0.000",
                "C2" => $"#,##0.00",
                _    => col.Format
            };
        }
    }
}
