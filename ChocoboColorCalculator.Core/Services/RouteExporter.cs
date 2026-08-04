using System.Globalization;
using System.Net;
using System.Text;
using ChocoboColorCalculator.Core.Data;
using ChocoboColorCalculator.Core.Models;

namespace ChocoboColorCalculator.Core.Services;

public enum RouteExportFormat
{
    Pdf,
    Text,
    Html,
}

public static class RouteExporter
{
    private const double PdfWidth = 595;
    private const double PdfHeight = 842;
    private static readonly PdfColor Navy = new(0.035, 0.055, 0.105);
    private static readonly PdfColor Violet = new(0.39, 0.24, 0.78);
    private static readonly PdfColor Blue = new(0.15, 0.48, 0.82);
    private static readonly PdfColor Gold = new(0.94, 0.62, 0.13);
    private static readonly PdfColor Green = new(0.12, 0.62, 0.39);
    private static readonly PdfColor Coral = new(0.90, 0.27, 0.31);
    private static readonly PdfColor Ink = new(0.09, 0.12, 0.19);
    private static readonly PdfColor Muted = new(0.38, 0.43, 0.52);
    private static readonly PdfColor Pale = new(0.955, 0.965, 0.985);
    private static readonly PdfColor White = new(1, 1, 1);

    public static string Export(RouteExportDocument document, RouteExportFormat format, string directory)
    {
        ArgumentNullException.ThrowIfNull(document);
        Directory.CreateDirectory(directory);
        var extension = format switch
        {
            RouteExportFormat.Pdf => ".pdf",
            RouteExportFormat.Text => ".txt",
            RouteExportFormat.Html => ".html",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        var baseName = $"Chocobo-Route-{SafeFilePart(document.StartName)}-to-{SafeFilePart(document.TargetName)}-" +
                       $"{DateTime.Now:yyyyMMdd-HHmmss}";
        var path = AvailablePath(directory, baseName, extension);

        switch (format)
        {
            case RouteExportFormat.Pdf:
                File.WriteAllBytes(path, CreatePdf(document));
                break;
            case RouteExportFormat.Text:
                File.WriteAllText(path, CreateText(document), new UTF8Encoding(false));
                break;
            case RouteExportFormat.Html:
                File.WriteAllText(path, CreateHtml(document), new UTF8Encoding(false));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }

        return path;
    }

    public static string CreateText(RouteExportDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("CHOCOBO COLOR CALCULATOR - FEEDING ROUTE");
        builder.AppendLine(new string('=', 72));
        builder.AppendLine($"Route:      {document.StartName} -> {document.TargetName}");
        builder.AppendLine($"Calculated: {document.CalculatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
        builder.AppendLine($"Progress:   {document.CompletedCount} / {document.Steps.Count} steps complete");
        builder.AppendLine();
        builder.AppendLine("ROUTE OVERVIEW");
        builder.AppendLine($"  Starting color:   {document.StartName} ({RgbText(document.StartRgb)})");
        builder.AppendLine($"  Desired color:    {document.TargetName} ({RgbText(document.TargetRgb)})");
        builder.AppendLine($"  Predicted result: {document.PredictedColorName} ({RgbText(document.EndpointRgb)})");
        builder.AppendLine($"  Reliable aim:     {RgbText(document.AimRgb)}");
        builder.AppendLine($"  Reliability:      {document.ClassificationMargin:F2} from the nearest rival");
        builder.AppendLine();
        builder.AppendLine("SHOPPING LIST");
        AppendShoppingText(builder, document);
        builder.AppendLine();
        builder.AppendLine("HOW TO USE THIS ROUTE");
        builder.AppendLine("  1. Confirm your chocobo currently shows the starting color above.");
        builder.AppendLine("  2. Stable the chocobo and have every fruit in the shopping list ready.");
        builder.AppendLine("  3. Feed exactly one fruit per numbered step, from top to bottom.");
        builder.AppendLine("  4. Mark each step in the plugin or let automatic detection track it.");
        builder.AppendLine("  5. Do not add fruit because a feather message did not appear.");
        builder.AppendLine("  6. After the final step, leave the chocobo stabled for six Earth hours.");
        builder.AppendLine();
        builder.AppendLine("ORDERED FEEDING ROUTE");
        builder.AppendLine(new string('-', 72));
        builder.AppendLine($"{"STEP",-6}{"FRUIT",-24}{"RGB AFTER",-16}{"STATUS",-18}EFFECT");
        builder.AppendLine(new string('-', 72));
        foreach (var step in document.Steps)
        {
            var fruit = ChocoboData.Fruit(step.Fruit);
            builder.AppendLine(
                $"{step.Number,-6}{Truncate(step.FruitName, 22),-24}{RgbChannels(step.RgbAfter),-16}" +
                $"{StepStatus(document, step),-18}{EffectText(fruit.Delta)}");
        }
        if (document.Steps.Count == 0)
            builder.AppendLine("No fruit is required because the starting and desired colors match.");
        builder.AppendLine(new string('-', 72));
        builder.AppendLine();
        builder.AppendLine("IMPORTANT");
        builder.AppendLine("The feather-growth message only means a named-color boundary was crossed.");
        builder.AppendLine("Its absence does not mean a fruit failed. Follow only the ordered list above.");
        if (!string.IsNullOrWhiteSpace(document.Warning))
        {
            builder.AppendLine();
            builder.AppendLine("ACCURACY NOTE");
            builder.AppendLine(document.Warning);
        }
        return builder.ToString();
    }

    public static string CreateHtml(RouteExportDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        builder.AppendLine($"<title>{Html(document.StartName)} to {Html(document.TargetName)} - Chocobo Route</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("*{box-sizing:border-box}body{margin:0;background:#0b1020;color:#eaf0ff;font:15px/1.55 system-ui,-apple-system,Segoe UI,sans-serif}");
        builder.AppendLine("main{max-width:1040px;margin:auto;padding:32px 22px 60px}.hero{padding:38px;border-radius:22px;background:linear-gradient(135deg,#35206f,#123e70);box-shadow:0 24px 70px #0008}");
        builder.AppendLine(".eyebrow{color:#ffc45d;font-size:12px;font-weight:800;letter-spacing:.16em}.hero h1{margin:7px 0;font-size:clamp(28px,5vw,48px)}.hero p{margin:0;color:#c6d5f5}");
        builder.AppendLine("section{margin-top:22px;padding:25px;border:1px solid #52618455;border-radius:18px;background:#151c2dcc;box-shadow:0 12px 35px #0003}h2{margin:0 0 16px;font-size:17px;letter-spacing:.08em}");
        builder.AppendLine(".grid{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}.card{padding:17px;border-radius:14px;background:#202a40}.label{color:#8fa1c4;font-size:11px;font-weight:800;letter-spacing:.09em}.value{display:flex;align-items:center;gap:10px;margin-top:6px;font-size:18px;font-weight:750}.swatch{width:28px;height:28px;border-radius:8px;border:2px solid #fff5;box-shadow:0 5px 12px #0006}");
        builder.AppendLine(".shopping{display:flex;gap:10px;flex-wrap:wrap}.fruit{display:flex;align-items:center;gap:9px;padding:9px 13px;border-radius:999px;background:#222c43}.dot{width:12px;height:12px;border-radius:50%}.count{color:#ffc45d;font-weight:800}");
        builder.AppendLine("ol{padding-left:24px;margin-bottom:0}li{padding:3px 0}table{width:100%;border-collapse:separate;border-spacing:0;overflow:hidden;border-radius:12px}th{position:sticky;top:0;background:#242f49;color:#aebfe2;text-align:left;font-size:11px;letter-spacing:.08em}th,td{padding:10px 12px;border-bottom:1px solid #52618433}tr:last-child td{border:0}tbody tr:nth-child(even){background:#1a2337}.next{background:#4b371b!important}.done{color:#72e5aa}.auto{color:#68b8ff}.queued{color:#93a0b7}.rgb{display:inline-flex;align-items:center;gap:8px}.tiny{width:16px;height:16px;border-radius:5px;border:1px solid #fff4}");
        builder.AppendLine(".note{border-left:4px solid #ffc45d}.footer{text-align:center;color:#7584a3;margin-top:22px}@media(max-width:720px){.grid{grid-template-columns:1fr}.hero{padding:25px}section{padding:18px}.table-wrap{overflow:auto}th,td{white-space:nowrap}}");
        builder.AppendLine("@media print{body{background:#fff;color:#172033}main{max-width:none;padding:0}.hero,section{box-shadow:none;break-inside:avoid}.hero{color:#fff}.table-wrap{overflow:visible}thead{display:table-header-group}tr{break-inside:avoid}.footer{color:#555}}");
        builder.AppendLine("</style></head><body><main>");
        builder.AppendLine("<header class=\"hero\"><div class=\"eyebrow\">CHOCOBO COLOR CALCULATOR</div>");
        builder.AppendLine($"<h1>{Html(document.StartName)} <span aria-hidden=\"true\">&rarr;</span> {Html(document.TargetName)}</h1>");
        builder.AppendLine($"<p>Reliable ordered feeding route &middot; {document.Steps.Count} fruit steps &middot; Generated {document.CalculatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}</p></header>");

        builder.AppendLine("<section><h2>ROUTE OVERVIEW</h2><div class=\"grid\">");
        AppendHtmlColorCard(builder, "STARTING COLOR", document.StartName, document.StartRgb);
        AppendHtmlColorCard(builder, "DESIRED COLOR", document.TargetName, document.TargetRgb);
        builder.AppendLine($"<div class=\"card\"><div class=\"label\">PROGRESS</div><div class=\"value\">{document.CompletedCount} / {document.Steps.Count}</div><div>{Html(document.PredictedColorName)} predicted &middot; margin {document.ClassificationMargin:F2}</div></div>");
        builder.AppendLine("</div></section>");

        builder.AppendLine("<section><h2>SHOPPING LIST</h2><div class=\"shopping\">");
        var groups = document.Steps.GroupBy(step => step.Fruit).ToList();
        if (groups.Count == 0)
            builder.AppendLine("<div class=\"fruit\">No fruit required</div>");
        foreach (var group in groups)
        {
            var fruit = ChocoboData.Fruit(group.Key);
            builder.AppendLine($"<div class=\"fruit\"><span class=\"dot\" style=\"background:{FruitHex(group.Key)}\"></span>" +
                               $"{Html(fruit.Name)} <span class=\"count\">&times;{group.Count()}</span></div>");
        }
        builder.AppendLine("</div></section>");

        builder.AppendLine("<section><h2>HOW TO USE THIS ROUTE</h2><ol>");
        builder.AppendLine("<li>Confirm your chocobo currently shows the starting color above.</li>");
        builder.AppendLine("<li>Stable the chocobo and have every fruit in the shopping list ready.</li>");
        builder.AppendLine("<li>Feed exactly one fruit per numbered step, from top to bottom.</li>");
        builder.AppendLine("<li>Mark each step in the plugin or let automatic detection track it.</li>");
        builder.AppendLine("<li>Do not add fruit because a feather-growth message did not appear.</li>");
        builder.AppendLine("<li>After the final step, leave the chocobo stabled for six Earth hours.</li>");
        builder.AppendLine("</ol></section>");

        builder.AppendLine("<section><h2>ORDERED FEEDING ROUTE</h2><div class=\"table-wrap\"><table><thead><tr>");
        builder.AppendLine("<th>STEP</th><th>FRUIT</th><th>EFFECT</th><th>RGB AFTER</th><th>STATUS</th></tr></thead><tbody>");
        foreach (var step in document.Steps)
        {
            var status = StepStatus(document, step);
            var statusClass = step.Number == document.NextStepNumber ? "next" : string.Empty;
            var textClass = step.Completion switch
            {
                RouteStepCompletion.Automatic or RouteStepCompletion.ManualAndAutomatic => "auto",
                RouteStepCompletion.Manual => "done",
                _ => "queued",
            };
            builder.AppendLine($"<tr class=\"{statusClass}\"><td>{step.Number:00}</td><td><span class=\"rgb\"><span class=\"dot\" style=\"background:{FruitHex(step.Fruit)}\"></span>{Html(step.FruitName)}</span></td>" +
                               $"<td>{Html(EffectText(ChocoboData.Fruit(step.Fruit).Delta))}</td>" +
                               $"<td><span class=\"rgb\"><span class=\"tiny\" style=\"background:{step.RgbAfter.Hex}\"></span>{Html(RgbText(step.RgbAfter))}</span></td>" +
                               $"<td class=\"{textClass}\">{Html(status)}</td></tr>");
        }
        if (document.Steps.Count == 0)
            builder.AppendLine("<tr><td colspan=\"5\">No fruit is required because the starting and desired colors match.</td></tr>");
        builder.AppendLine("</tbody></table></div></section>");

        builder.AppendLine("<section class=\"note\"><h2>IMPORTANT</h2><p>The feather-growth message only means the pending color crossed a named-color boundary. Its absence does not mean a fruit failed. Follow only the ordered list above.</p>");
        if (!string.IsNullOrWhiteSpace(document.Warning))
            builder.AppendLine($"<p><strong>Accuracy note:</strong> {Html(document.Warning)}</p>");
        builder.AppendLine("</section><div class=\"footer\">Generated by Chocobo Color Calculator for Dalamud</div></main></body></html>");
        return builder.ToString();
    }

    public static byte[] CreatePdf(RouteExportDocument document)
    {
        var pages = new List<PdfPageBuilder>();
        pages.Add(BuildCoverPage(document));
        const int stepsPerPage = 25;
        for (var offset = 0; offset < document.Steps.Count; offset += stepsPerPage)
            pages.Add(BuildRoutePage(document, offset, Math.Min(stepsPerPage, document.Steps.Count - offset)));
        if (document.Steps.Count == 0)
            pages.Add(BuildRoutePage(document, 0, 0));
        return WritePdf(pages);
    }

    private static PdfPageBuilder BuildCoverPage(RouteExportDocument document)
    {
        var page = new PdfPageBuilder();
        page.FillRect(0, 0, PdfWidth, 112, Navy);
        page.FillRect(0, 0, 7, 112, Violet);
        page.Text("CHOCOBO COLOR CALCULATOR", 40, 31, 11, true, Gold);
        page.Text("FEEDING ROUTE", 40, 51, 26, true, White);
        page.Text($"{document.StartName}  ->  {document.TargetName}", 40, 84, 13, false, new PdfColor(0.78, 0.85, 1));
        page.Text($"Calculated {document.CalculatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}", 400, 88, 8.5, false, new PdfColor(0.68, 0.75, 0.88));

        page.Text("ROUTE OVERVIEW", 40, 133, 12, true, Violet);
        DrawColorCard(page, 40, 153, 162, "STARTING COLOR", document.StartName, document.StartRgb, Blue);
        DrawColorCard(page, 216, 153, 162, "DESIRED COLOR", document.TargetName, document.TargetRgb, Violet);
        DrawProgressCard(page, 392, 153, 163, document);

        page.Text("SHOPPING LIST", 40, 260, 12, true, Violet);
        var groups = document.Steps.GroupBy(step => step.Fruit).ToList();
        if (groups.Count == 0)
        {
            page.FillRect(40, 281, 515, 48, Pale);
            page.Text("No fruit required - the starting and desired colors already match.", 56, 298, 10, false, Ink);
        }
        else
        {
            var chipWidth = (515d - (groups.Count - 1) * 10) / groups.Count;
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var fruit = ChocoboData.Fruit(group.Key);
                var x = 40 + i * (chipWidth + 10);
                page.FillRect(x, 281, chipWidth, 48, Pale);
                page.FillRect(x, 281, 5, 48, FruitColor(group.Key));
                page.Text(FitText(fruit.Name, 22), x + 15, 294, 9, true, Ink);
                page.Text($"Quantity  x{group.Count()}", x + 15, 311, 9, false, Gold);
            }
        }

        page.Text("HOW TO USE THIS ROUTE", 40, 365, 12, true, Violet);
        var instructions = new[]
        {
            "Confirm your chocobo currently shows the starting color above.",
            "Stable the chocobo and have every fruit in the shopping list ready.",
            "Feed exactly one fruit per numbered step, from top to bottom.",
            "Track each step manually or let automatic detection advance it.",
            "Do not add fruit because a feather-growth message did not appear.",
            "After the final step, leave the chocobo stabled for six Earth hours.",
        };
        for (var i = 0; i < instructions.Length; i++)
        {
            var top = 390 + i * 31;
            page.FillRect(40, top, 22, 22, i == instructions.Length - 1 ? Gold : Blue);
            page.Text((i + 1).ToString(CultureInfo.InvariantCulture), 47, top + 5, 9, true, White);
            page.Text(instructions[i], 74, top + 5, 9.5, false, Ink);
        }

        page.FillRect(40, 604, 515, 92, new PdfColor(1.0, 0.965, 0.86));
        page.FillRect(40, 604, 5, 92, Gold);
        page.Text("IMPORTANT", 57, 620, 10, true, new PdfColor(0.52, 0.34, 0.04));
        page.DrawWrappedText(
            "The feather-growth message only means a named-color boundary was crossed. Its absence does not mean a fruit failed. Follow only the ordered route, then wait the full six Earth hours.",
            57, 641, 478, 9.5, 14, Ink);
        page.Text($"Predicted result: {document.PredictedColorName}  |  Endpoint {RgbText(document.EndpointRgb)}", 57, 678, 9, true, Green);

        page.FillRect(40, 715, 515, 77, Pale);
        if (!string.IsNullOrWhiteSpace(document.Warning))
        {
            page.Text("ACCURACY NOTE", 55, 728, 8.5, true, Violet);
            page.DrawWrappedText(document.Warning, 55, 743, 480, 7.5, 10, Ink);
        }
        else
        {
            page.Text("DOCUMENT GUIDE", 55, 733, 9, true, Violet);
            page.Text("The following pages contain every numbered fruit, its RGB result, and saved completion status.", 55, 752, 9, false, Muted);
        }
        DrawPdfFooter(page, 1, 1 + Math.Max(1, (int)Math.Ceiling(document.Steps.Count / 25d)));
        return page;
    }

    private static PdfPageBuilder BuildRoutePage(RouteExportDocument document, int offset, int count)
    {
        var page = new PdfPageBuilder();
        page.FillRect(0, 0, PdfWidth, 78, Navy);
        page.FillRect(0, 0, 7, 78, Blue);
        page.Text("ORDERED FEEDING ROUTE", 40, 26, 18, true, White);
        var rangeText = count == 0 ? "No fruit required" : $"Steps {offset + 1}-{offset + count} of {document.Steps.Count}";
        page.Text(rangeText, 40, 52, 9, false, new PdfColor(0.70, 0.79, 0.94));
        page.Text($"{document.StartName} -> {document.TargetName}", 390, 37, 9, true, Gold);

        var columns = new[] { 40d, 82d, 101d, 258d, 350d, 455d };
        page.FillRect(40, 98, 515, 28, new PdfColor(0.13, 0.17, 0.27));
        page.Text("STEP", columns[0] + 7, 107, 8, true, White);
        page.Text("", columns[1], 107, 8, true, White);
        page.Text("FRUIT", columns[2], 107, 8, true, White);
        page.Text("EFFECT", columns[3], 107, 8, true, White);
        page.Text("RGB AFTER", columns[4], 107, 8, true, White);
        page.Text("STATUS", columns[5], 107, 8, true, White);

        if (count == 0)
        {
            page.FillRect(40, 126, 515, 60, Pale);
            page.Text("No fruit is required because the starting and desired colors match.", 58, 148, 10, false, Ink);
        }

        const double rowHeight = 25;
        for (var row = 0; row < count; row++)
        {
            var step = document.Steps[offset + row];
            var top = 126 + row * rowHeight;
            var isNext = step.Number == document.NextStepNumber;
            var background = isNext
                ? new PdfColor(1.0, 0.95, 0.80)
                : row % 2 == 0 ? White : Pale;
            page.FillRect(40, top, 515, rowHeight, background);
            if (isNext)
                page.FillRect(40, top, 4, rowHeight, Gold);
            page.Text(step.Number.ToString("00", CultureInfo.InvariantCulture), columns[0] + 8, top + 8, 8.5, true, isNext ? Gold : Ink);
            page.FillRect(columns[1], top + 7, 10, 10, FruitColor(step.Fruit));
            page.Text(FitText(PdfFruitName(step), 25), columns[2], top + 8, 8.2, false, Ink);
            page.Text(EffectText(ChocoboData.Fruit(step.Fruit).Delta), columns[3], top + 8, 8.2, false, Muted);
            page.FillRect(columns[4], top + 7, 10, 10, RgbColor(step.RgbAfter));
            page.Text($"{step.RgbAfter.R}/{step.RgbAfter.G}/{step.RgbAfter.B}", columns[4] + 16, top + 8, 8.2, false, Ink);
            var statusColor = step.Completion switch
            {
                RouteStepCompletion.Automatic or RouteStepCompletion.ManualAndAutomatic => Blue,
                RouteStepCompletion.Manual => Green,
                _ when isNext => Gold,
                _ => Muted,
            };
            page.Text(StepStatus(document, step), columns[5], top + 8, 7.8, true, statusColor);
        }

        var pageNumber = 2 + offset / 25;
        var pageCount = 1 + Math.Max(1, (int)Math.Ceiling(document.Steps.Count / 25d));
        DrawPdfFooter(page, pageNumber, pageCount);
        return page;
    }

    private static void DrawColorCard(
        PdfPageBuilder page,
        double x,
        double top,
        double width,
        string label,
        string name,
        RgbColor rgb,
        PdfColor accent)
    {
        page.FillRect(x, top, width, 78, Pale);
        page.FillRect(x, top, width, 4, accent);
        page.Text(label, x + 14, top + 17, 7.5, true, Muted);
        page.FillRect(x + 14, top + 37, 25, 25, RgbColor(rgb));
        page.Text(FitText(name, 20), x + 49, top + 37, 10, true, Ink);
        page.Text(RgbText(rgb), x + 49, top + 54, 8, false, Muted);
    }

    private static void DrawProgressCard(PdfPageBuilder page, double x, double top, double width, RouteExportDocument document)
    {
        page.FillRect(x, top, width, 78, Pale);
        page.FillRect(x, top, width, 4, Green);
        page.Text("ROUTE PROGRESS", x + 14, top + 17, 7.5, true, Muted);
        page.Text($"{document.CompletedCount} / {document.Steps.Count}", x + 14, top + 36, 16, true, Ink);
        page.Text(FitText($"{document.PredictedColorName} predicted", 27), x + 14, top + 58, 8, false, Green);
    }

    private static void DrawPdfFooter(PdfPageBuilder page, int pageNumber, int pageCount)
    {
        page.Line(40, 808, 555, 808, new PdfColor(0.82, 0.85, 0.91), 0.7);
        page.Text("Generated by Chocobo Color Calculator for Dalamud", 40, 818, 7.5, false, Muted);
        page.Text($"Page {pageNumber} of {pageCount}", 500, 818, 7.5, false, Muted);
    }

    private static byte[] WritePdf(IReadOnlyList<PdfPageBuilder> pages)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            string.Empty,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
        };
        var kids = new List<string>();
        for (var i = 0; i < pages.Count; i++)
        {
            var pageObject = 5 + i * 2;
            var contentObject = pageObject + 1;
            kids.Add($"{pageObject} 0 R");
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PdfWidth} {PdfHeight}] " +
                        $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObject} 0 R >>");
            var content = pages[i].Content;
            var contentLength = Encoding.ASCII.GetByteCount(content);
            objects.Add($"<< /Length {contentLength} >>\nstream\n{content}\nendstream");
        }
        objects[1] = $"<< /Type /Pages /Kids [{string.Join(' ', kids)}] /Count {pages.Count} >>";

        using var stream = new MemoryStream();
        WriteAscii(stream, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = stream.Position;
        WriteAscii(stream, $"xref\n0 {objects.Count + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
            WriteAscii(stream, $"{offsets[i]:0000000000} 00000 n \n");
        WriteAscii(stream, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return stream.ToArray();
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void AppendShoppingText(StringBuilder builder, RouteExportDocument document)
    {
        var groups = document.Steps.GroupBy(step => step.Fruit).ToList();
        if (groups.Count == 0)
        {
            builder.AppendLine("  No fruit required.");
            return;
        }
        foreach (var group in groups)
            builder.AppendLine($"  {ChocoboData.Fruit(group.Key).Name,-24} x{group.Count()}");
    }

    private static void AppendHtmlColorCard(StringBuilder builder, string label, string name, RgbColor rgb) =>
        builder.AppendLine($"<div class=\"card\"><div class=\"label\">{Html(label)}</div><div class=\"value\"><span class=\"swatch\" style=\"background:{rgb.Hex}\"></span>{Html(name)}</div><div>{Html(RgbText(rgb))}</div></div>");

    private static string StepStatus(RouteExportDocument document, RouteExportStep step) => step.Completion switch
    {
        RouteStepCompletion.ManualAndAutomatic => "AUTO + MANUAL",
        RouteStepCompletion.Automatic => "AUTO-DETECTED",
        RouteStepCompletion.Manual => "MANUAL",
        _ when step.Number == document.NextStepNumber => "NEXT",
        _ => "QUEUED",
    };

    private static string EffectText(RgbColor delta) =>
        $"R{Signed(delta.R)} G{Signed(delta.G)} B{Signed(delta.B)}";

    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);

    private static string RgbText(RgbColor rgb) => $"RGB {rgb.R}/{rgb.G}/{rgb.B}  {rgb.Hex}";

    private static string RgbChannels(RgbColor rgb) => $"{rgb.R}/{rgb.G}/{rgb.B}";

    private static PdfColor RgbColor(RgbColor rgb) => new(rgb.R / 255d, rgb.G / 255d, rgb.B / 255d);

    private static PdfColor FruitColor(FruitKind fruit) => fruit switch
    {
        FruitKind.XelphatolApple => new PdfColor(0.90, 0.22, 0.25),
        FruitKind.MamookPear => new PdfColor(0.25, 0.70, 0.34),
        FruitKind.OGhomoroBerries => new PdfColor(0.22, 0.46, 0.90),
        FruitKind.DomanPlum => new PdfColor(0.24, 0.72, 0.78),
        FruitKind.Valfruit => new PdfColor(0.76, 0.30, 0.66),
        FruitKind.CieldalaesPineapple => new PdfColor(0.94, 0.64, 0.12),
        _ => Muted,
    };

    private static string FruitHex(FruitKind fruit)
    {
        var color = FruitColor(fruit);
        return $"#{(int)(color.R * 255):X2}{(int)(color.G * 255):X2}{(int)(color.B * 255):X2}";
    }

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..Math.Max(1, maxLength - 1)] + "~";

    private static string FitText(string value, int maxLength) => Truncate(Ascii(value), maxLength);

    private static string PdfFruitName(RouteExportStep step)
    {
        var ascii = Ascii(step.FruitName);
        return ascii.Count(character => character == '?') > Math.Max(1, ascii.Length / 3)
            ? ChocoboData.Fruit(step.Fruit).Name
            : ascii;
    }

    private static string SafeFilePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.Join('-', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string AvailablePath(string directory, string baseName, string extension)
    {
        var path = Path.Combine(directory, baseName + extension);
        for (var suffix = 2; File.Exists(path); suffix++)
            path = Path.Combine(directory, $"{baseName}-{suffix}{extension}");
        return path;
    }

    private static string Ascii(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            builder.Append(character is >= ' ' and <= '~' ? character : '?');
        }
        return builder.ToString();
    }

    private readonly record struct PdfColor(double R, double G, double B);

    private sealed class PdfPageBuilder
    {
        private readonly StringBuilder content = new();

        public string Content => content.ToString();

        public void FillRect(double x, double top, double width, double height, PdfColor color) =>
            content.AppendLine(FormattableString.Invariant(
                $"{color.R:0.###} {color.G:0.###} {color.B:0.###} rg {x:0.##} {PdfHeight - top - height:0.##} {width:0.##} {height:0.##} re f"));

        public void Line(double x1, double top1, double x2, double top2, PdfColor color, double width) =>
            content.AppendLine(FormattableString.Invariant(
                $"{color.R:0.###} {color.G:0.###} {color.B:0.###} RG {width:0.##} w {x1:0.##} {PdfHeight - top1:0.##} m {x2:0.##} {PdfHeight - top2:0.##} l S"));

        public void Text(string value, double x, double top, double size, bool bold, PdfColor color)
        {
            var escaped = Ascii(value).Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
            content.AppendLine(FormattableString.Invariant(
                $"BT /{(bold ? "F2" : "F1")} {size:0.##} Tf {color.R:0.###} {color.G:0.###} {color.B:0.###} rg {x:0.##} {PdfHeight - top - size:0.##} Td ({escaped}) Tj ET"));
        }

        public void DrawWrappedText(
            string value,
            double x,
            double top,
            double width,
            double size,
            double lineHeight,
            PdfColor color)
        {
            var maxCharacters = Math.Max(8, (int)(width / (size * 0.52)));
            var words = Ascii(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            var current = new StringBuilder();
            foreach (var word in words)
            {
                if (current.Length > 0 && current.Length + 1 + word.Length > maxCharacters)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }
                if (current.Length > 0)
                    current.Append(' ');
                current.Append(word);
            }
            if (current.Length > 0)
                lines.Add(current.ToString());
            for (var i = 0; i < lines.Count; i++)
                Text(lines[i], x, top + i * lineHeight, size, false, color);
        }
    }
}
