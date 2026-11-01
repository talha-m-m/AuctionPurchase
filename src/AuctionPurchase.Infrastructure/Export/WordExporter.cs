using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using AuctionPurchase.Core.Reports;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuctionPurchase.Infrastructure.Export
{
    /// <summary>
    /// Renders a report as an editable DOCX document driven entirely by the
    /// report template.  No hard-coded layouts exist here.
    /// </summary>
    public class WordExporter
    {
        public byte[] ExportTemplate(
            ReportTemplateLayout layout,
            ReportRenderContext context,
            List<ReportEntry> entries)
        {
            using var ms       = new MemoryStream();
            using var document = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document);

            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            var columns = layout.Columns.Where(c => c.IsVisible).ToList();
            var page    = layout.Page;
            var bodyOpt = layout.Body;

            // ── Page orientation / size ───────────────────────────────
            var (pgW, pgH) = ResolvePageSize(page.PageSize, page.Orientation);
            var marginDxa  = (uint)(page.MarginTopMm    * 567);   // 1 mm ≈ 56.7 dxa
            var marginBDxa = (uint)(page.MarginBottomMm * 567);
            var marginLDxa = (uint)(page.MarginLeftMm   * 567);
            var marginRDxa = (uint)(page.MarginRightMm  * 567);

            // ── Company logo in document header (if available) ────────
            if (!string.IsNullOrWhiteSpace(context.CompanyLogoBase64))
            {
                try
                {
                    var logoBytes = Convert.FromBase64String(context.CompanyLogoBase64);
                    var imagePart = mainPart.AddImagePart(ImagePartType.Png);
                    using var logoStream = new MemoryStream(logoBytes);
                    imagePart.FeedData(logoStream);
                    body.AppendChild(CreateLogoParagraph(mainPart.GetIdOfPart(imagePart)));
                }
                catch { /* skip broken logo */ }
            }

            // ── Header elements ───────────────────────────────────────
            foreach (var el in layout.HeaderElements)
            {
                if (el.ElementType is "CompanyLogo") continue;   // handled above
                if (el.ElementType is "Line" or "Separator")
                {
                    body.AppendChild(CreateHorizontalRule());
                    continue;
                }
                var text = ReportTemplateValueResolver.Element(el, context);
                body.AppendChild(CreateStyledParagraph(text, el));
            }

            // ── Data table ────────────────────────────────────────────
            var hdrBg = CleanHex(bodyOpt.TableHeaderBackground, "1A3A6A");
            var hdrFg = CleanHex(bodyOpt.TableHeaderForeground, "FFFFFF");
            var totBg = CleanHex(bodyOpt.TotalsBackground,      "BDD7EE");
            var altBg = CleanHex(bodyOpt.AlternateRowBackground, "EEF2FF");

            bool isBrandWise = string.Equals(context.ReportType, "BrandWise", StringComparison.OrdinalIgnoreCase)
                || (context.ReportTitle?.Contains("Brand", StringComparison.OrdinalIgnoreCase) == true
                    && !context.ReportTitle.Contains("Broker", StringComparison.OrdinalIgnoreCase));

            bool isBrokerWise = string.Equals(context.ReportType, "BrokerWise", StringComparison.OrdinalIgnoreCase)
                || context.ReportTitle?.Contains("Broker", StringComparison.OrdinalIgnoreCase) == true;

            bool isGrouped = isBrandWise || isBrokerWise;

            if (isGrouped)
            {
                string groupField = isBrandWise ? "Brand" : "Broker";
                var groups = isBrandWise
                    ? entries.GroupBy(e => string.IsNullOrWhiteSpace(e.BrandName) ? "(Unbranded)" : e.BrandName.Trim())
                             .OrderBy(g => g.Key).ToList()
                    : entries.GroupBy(e => string.IsNullOrWhiteSpace(e.BrokerName) ? "(Unassigned)" : e.BrokerName.Trim())
                             .OrderBy(g => g.Key).ToList();

                var effectiveColumns = isBrandWise
                    ? columns.Where(c => !string.Equals(c.Field, "BrandName", StringComparison.OrdinalIgnoreCase)
                                      && !string.Equals(c.Field, "Brand", StringComparison.OrdinalIgnoreCase)).ToList()
                    : columns.Where(c => !string.Equals(c.Field, "BrokerName", StringComparison.OrdinalIgnoreCase)
                                      && !string.Equals(c.Field, "Broker", StringComparison.OrdinalIgnoreCase)).ToList();

                if (effectiveColumns.Count == 0) effectiveColumns = columns;

                for (int gIdx = 0; gIdx < groups.Count; gIdx++)
                {
                    var group = groups[gIdx];
                    var groupEntries = group.ToList();

                    // Group Title Heading
                    body.AppendChild(new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }, new SpacingBetweenLines { Before = "160", After = "120" }),
                        new Run(
                            new RunProperties(new Bold(), new FontSize { Val = "26" }, new Color { Val = hdrBg }),
                            new Text($"{groupField}: {group.Key}"))));

                    var grpTable = new Table(new TableProperties(
                        new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                        new TableBorders(
                            new TopBorder    { Val = BorderValues.Single, Size = 4 },
                            new BottomBorder { Val = BorderValues.Single, Size = 4 },
                            new LeftBorder   { Val = BorderValues.Single, Size = 4 },
                            new RightBorder  { Val = BorderValues.Single, Size = 4 },
                            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 2 },
                            new InsideVerticalBorder   { Val = BorderValues.Single, Size = 2 })));

                    // Header row
                    var headerRow = new TableRow();
                    foreach (var col in effectiveColumns)
                        headerRow.AppendChild(CreateCell(col.Title, bold: true,
                            bgColor: hdrBg, fontColor: hdrFg, alignment: col.Alignment));
                    grpTable.AppendChild(headerRow);

                    // Data rows
                    bool alt = false;
                    foreach (var entry in groupEntries)
                    {
                        var rowBg = alt && bodyOpt.AlternateRowShading ? altBg : "FFFFFF";
                        var row = new TableRow();
                        foreach (var col in effectiveColumns)
                        {
                            var bg = !string.IsNullOrEmpty(col.BackgroundColor)
                                ? CleanHex(col.BackgroundColor, "FFFFFF") : rowBg;
                            row.AppendChild(CreateCell(
                                ReportTemplateValueResolver.Display(entry, col),
                                bold: col.Bold,
                                bgColor: bg,
                                fontColor: CleanHex(col.TextColor, "000000"),
                                alignment: col.Alignment,
                                fontSizeHalfPts: col.FontSize > 0 ? ((int)(col.FontSize * 2)).ToString() : "18"));
                        }
                        grpTable.AppendChild(row);
                        alt = !alt;
                    }

                    // Group Sub-total row
                    if (bodyOpt.ShowTotals)
                    {
                        var totRow = new TableRow();
                        bool firstCol = true;
                        foreach (var col in effectiveColumns)
                        {
                            var val = col.IncludeInTotal
                                ? ReportTemplateValueResolver.Total(groupEntries, col.Field)
                                    .ToString(string.IsNullOrWhiteSpace(col.Format) ? "N2" : col.Format)
                                : (firstCol ? $"Sub Total-{group.Key}" : "");
                            firstCol = false;

                            totRow.AppendChild(CreateCell(val, bold: true,
                                bgColor: totBg, fontColor: "000000",
                                alignment: col.Alignment));
                        }
                        grpTable.AppendChild(totRow);
                    }

                    body.AppendChild(grpTable);

                    // Page break after group (except the last group)
                    if (gIdx < groups.Count - 1)
                    {
                        body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                    }
                }
            }
            else
            {
                var table = new Table(new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableBorders(
                        new TopBorder    { Val = BorderValues.Single, Size = 4 },
                        new BottomBorder { Val = BorderValues.Single, Size = 4 },
                        new LeftBorder   { Val = BorderValues.Single, Size = 4 },
                        new RightBorder  { Val = BorderValues.Single, Size = 4 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 2 },
                        new InsideVerticalBorder   { Val = BorderValues.Single, Size = 2 })));

                // Header row
                var headerRow = new TableRow();
                foreach (var col in columns)
                    headerRow.AppendChild(CreateCell(col.Title, bold: true,
                        bgColor: hdrBg, fontColor: hdrFg, alignment: col.Alignment));
                table.AppendChild(headerRow);

                // Data rows
                bool alt = false;
                foreach (var entry in entries)
                {
                    var rowBg = alt && bodyOpt.AlternateRowShading ? altBg : "FFFFFF";
                    var row   = new TableRow();
                    foreach (var col in columns)
                    {
                        var bg = !string.IsNullOrEmpty(col.BackgroundColor)
                            ? CleanHex(col.BackgroundColor, "FFFFFF") : rowBg;
                        row.AppendChild(CreateCell(
                            ReportTemplateValueResolver.Display(entry, col),
                            bold: col.Bold,
                            bgColor: bg,
                            fontColor: CleanHex(col.TextColor, "000000"),
                            alignment: col.Alignment,
                            fontSizeHalfPts: col.FontSize > 0 ? ((int)(col.FontSize * 2)).ToString() : "18"));
                    }
                    table.AppendChild(row);
                    alt = !alt;
                }

                // Totals row
                if (bodyOpt.ShowTotals)
                {
                    var totRow = new TableRow();
                    foreach (var col in columns)
                    {
                        var val = col.IncludeInTotal
                            ? ReportTemplateValueResolver.Total(entries, col.Field)
                                .ToString(string.IsNullOrWhiteSpace(col.Format) ? "N2" : col.Format)
                            : (col == columns.First() ? "Total" : "");
                        totRow.AppendChild(CreateCell(val, bold: true,
                            bgColor: totBg, fontColor: "000000",
                            alignment: col.Alignment));
                    }
                    table.AppendChild(totRow);
                }

                body.AppendChild(table);
            }

            // ── Footer elements ───────────────────────────────────────
            foreach (var el in layout.FooterElements)
            {
                if (el.ElementType is "Line" or "Separator")
                {
                    body.AppendChild(CreateHorizontalRule());
                    continue;
                }
                var text = ReportTemplateValueResolver.Element(el, context);
                body.AppendChild(CreateStyledParagraph(text, el));
            }

            // ── Section properties (page size / margin) ───────────────
            body.AppendChild(new SectionProperties(
                new PageSize { Width = pgW, Height = pgH,
                    Orient = page.Orientation == "Landscape"
                        ? PageOrientationValues.Landscape
                        : PageOrientationValues.Portrait },
                new PageMargin
                {
                    Top    = (int)marginDxa,
                    Bottom = (int)marginBDxa,
                    Left   = marginLDxa,
                    Right  = marginRDxa
                }));

            mainPart.Document.Save();
            document.Dispose();
            return ms.ToArray();
        }

        // ─────────────────────────────────────────────────────────────
        // Paragraph builders
        // ─────────────────────────────────────────────────────────────

        private static Paragraph CreateStyledParagraph(string text, ReportSectionElement el)
        {
            var justification = JustFromString(el.Alignment);
            var fontSize = el.FontSize > 0
                ? ((int)(el.FontSize * 2)).ToString()
                : "18";

            var runProps = new RunProperties(new FontSize { Val = fontSize });
            if (el.Bold)   runProps.AppendChild(new Bold());
            if (el.Italic) runProps.AppendChild(new Italic());
            if (el.Underline) runProps.AppendChild(new Underline { Val = UnderlineValues.Single });
            if (!string.IsNullOrWhiteSpace(el.FontFamily))
                runProps.AppendChild(new RunFonts { Ascii = el.FontFamily, HighAnsi = el.FontFamily });
            if (!string.IsNullOrWhiteSpace(el.TextColor))
                runProps.AppendChild(new Color { Val = CleanHex(el.TextColor, "000000") });

            return new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = justification },
                    new SpacingBetweenLines { After = "60" }),
                new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        }

        private static Paragraph CreateLogoParagraph(string imageRelId)
        {
            // Approximate size: 50mm wide × 15mm tall in EMUs
            const long widthEmu  = 1800000L;  // ~50 mm
            const long heightEmu = 540000L;   // ~15 mm

            var nvPicPr = new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties(
                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties { Id = 0U, Name = "Logo" },
                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties());

            var blipFill = new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill(
                new DocumentFormat.OpenXml.Drawing.Blip { Embed = imageRelId },
                new DocumentFormat.OpenXml.Drawing.Stretch(
                    new DocumentFormat.OpenXml.Drawing.FillRectangle()));

            var xfrm = new DocumentFormat.OpenXml.Drawing.Transform2D(
                new DocumentFormat.OpenXml.Drawing.Offset { X = 0L, Y = 0L },
                new DocumentFormat.OpenXml.Drawing.Extents  { Cx = widthEmu, Cy = heightEmu });

            var prstGeom = new DocumentFormat.OpenXml.Drawing.PresetGeometry(
                new DocumentFormat.OpenXml.Drawing.AdjustValueList())
            { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle };

            var spPr = new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties(xfrm, prstGeom);
            var pic  = new DocumentFormat.OpenXml.Drawing.Pictures.Picture(nvPicPr, blipFill, spPr);

            var graphicData = new DocumentFormat.OpenXml.Drawing.GraphicData(pic)
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };

            var graphic = new DocumentFormat.OpenXml.Drawing.Graphic(graphicData);

            var inline = new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline(
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent { Cx = widthEmu, Cy = heightEmu },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties { Id = 1U, Name = "CompanyLogo" },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.NonVisualGraphicFrameDrawingProperties(),
                graphic);

            var drawing = new Drawing(inline);

            return new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(drawing));
        }

        private static Paragraph CreateHorizontalRule()
        {
            return new Paragraph(
                new ParagraphProperties(
                    new ParagraphBorders(
                        new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "888888" })));
        }

        // ─────────────────────────────────────────────────────────────
        // Table cell builder
        // ─────────────────────────────────────────────────────────────

        private static TableCell CreateCell(
            string text,
            bool bold              = false,
            string bgColor         = "FFFFFF",
            string fontColor       = "000000",
            string alignment       = "Left",
            int colSpan            = 1,
            string fontSizeHalfPts = "18")
        {
            var justification = JustFromString(alignment);
            var runProps = new RunProperties(new FontSize { Val = fontSizeHalfPts });
            if (bold) runProps.AppendChild(new Bold());
            if (fontColor != "000000")
                runProps.AppendChild(new Color { Val = fontColor });

            var cellProps = new TableCellProperties(
                new Shading { Fill = CleanHex(bgColor, "FFFFFF"), Val = ShadingPatternValues.Clear });
            if (colSpan > 1) cellProps.AppendChild(new GridSpan { Val = colSpan });

            return new TableCell(
                cellProps,
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = justification },
                        new SpacingBetweenLines { After = "0" }),
                    new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────

        private static (uint w, uint h) ResolvePageSize(string size, string orientation)
        {
            // OOXML page size in twentieths of a point.
            // A4 portrait: 11906 × 16838; Landscape: swap.
            // Letter portrait: 12240 × 15840.
            var (pw, ph) = size switch
            {
                "Letter" => (12240u, 15840u),
                "Legal"  => (12240u, 20160u),
                _        => (11906u, 16838u)   // A4
            };
            return orientation == "Landscape" ? (ph, pw) : (pw, ph);
        }

        private static JustificationValues JustFromString(string s) =>
            s?.ToUpperInvariant() switch
            {
                "RIGHT"  => JustificationValues.Right,
                "CENTER" => JustificationValues.Center,
                _        => JustificationValues.Left
            };

        private static string CleanHex(string hex, string fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            return hex.TrimStart('#');
        }
    }
}
