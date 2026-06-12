using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Document;

namespace MPS.Infrastructure.Services;

public sealed class DocxMergeService : IDocumentRenderingService
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

    public Task<Stream> RenderAsync(Stream templateDocx, PlaceholderContext context, CancellationToken cancellationToken = default)
    {
        var values = context.ToDictionary();

        // Copy to writable MemoryStream — the original stream from MinIO may be read-only.
        var ms = new MemoryStream();
        templateDocx.CopyTo(ms);
        ms.Seek(0, SeekOrigin.Begin);

        using (var doc = WordprocessingDocument.Open(ms, isEditable: true))
        {
            var body = doc.MainDocumentPart?.Document?.Body
                ?? throw new InvalidOperationException("DOCX_CORRUPT: Document body is missing.");

            foreach (var para in body.Descendants<Paragraph>())
            {
                NormalizePlaceholderRuns(para);
                ReplacePlaceholdersInParagraph(para, values);
            }

            doc.MainDocumentPart!.Document.Save();
        }

        ms.Seek(0, SeekOrigin.Begin);
        return Task.FromResult<Stream>(ms);
    }

    // Merge all runs in a paragraph into a single run when they contain a split placeholder.
    // This handles Word's habit of splitting {{key}} across multiple runs due to spell-check.
    private static void NormalizePlaceholderRuns(Paragraph para)
    {
        var runs = para.Elements<Run>().ToList();
        if (runs.Count < 2) return;

        var fullText = string.Concat(runs.Select(r => r.InnerText));
        if (!fullText.Contains("{{")) return;

        // Only merge if a placeholder is actually split (spans more than one run).
        var isSplit = false;
        foreach (var run in runs)
        {
            var t = run.InnerText;
            if ((t.Contains("{{") && !t.Contains("}}")) ||
                (t.Contains("}}") && !t.Contains("{{")))
            {
                isSplit = true;
                break;
            }
        }
        if (!isSplit) return;

        var firstRun = runs[0];
        foreach (var el in firstRun.Elements<Text>().ToList())
            el.Remove();
        firstRun.AppendChild(new Text(fullText) { Space = SpaceProcessingModeValues.Preserve });

        for (var i = 1; i < runs.Count; i++)
            runs[i].Remove();
    }

    private static void ReplacePlaceholdersInParagraph(Paragraph para, IReadOnlyDictionary<string, string> values)
    {
        foreach (var run in para.Elements<Run>())
        {
            foreach (var textEl in run.Elements<Text>())
            {
                var replaced = PlaceholderRegex.Replace(textEl.Text, m =>
                {
                    var key = m.Groups[1].Value.Trim();
                    return values.TryGetValue(key, out var val) ? val : m.Value;
                });

                if (replaced != textEl.Text)
                {
                    textEl.Text = replaced;
                    textEl.Space = SpaceProcessingModeValues.Preserve;
                }
            }
        }
    }
}
