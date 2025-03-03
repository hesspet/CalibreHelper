using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Net;
using QuickEPUB;
using System.Text.Json;

internal class Program
{
    private static string BuildList(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "<p>...</p>"
            : "<ul>" + string.Join("", value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                                   .Select(line => $"<li>{WebUtility.HtmlEncode(line)}</li>")) + "</ul>";
    }

    private static void ErstelleEpubDatei(List<(string Title, string Creator, string Description, string Highlight, string Tricks, string FilePath)> books, string epubFile)
    {
        var epub = new Epub("Collected Metadata", "Various");

        foreach (var book in books)
        {
            if (string.IsNullOrWhiteSpace(book.Tricks)) continue;

            string fileLink = string.IsNullOrEmpty(book.FilePath)
                ? "No file available"
                : $"<a href=\"file:///{WebUtility.UrlEncode(book.FilePath.Replace("\\", "/"))}\">{book.FilePath}</a>";

            string tricksList = BuildList(book.Tricks);
            string highlightList = BuildList(book.Highlight);

            string content = $"""
        <h1>{book.Title}</h1>
        <h2>by {book.Creator}</h2>
        <p><strong>Description:</strong> {book.Description}</p>
        <p><strong>Tricks:</strong> {tricksList}</p>
        <p><strong>Highlight:</strong> {highlightList}</p>
        <p><strong>File:</strong> {fileLink}</p>
        """;
            epub.AddSection(book.Title, content);
        }

        using (FileStream fs = new FileStream(epubFile, FileMode.Create, FileAccess.Write))
        {
            epub.Export(fs);
        }

        Console.WriteLine($"EPUB file created at {epubFile}");
    }

    private static List<(string Title, string Creator, string Description, string Highlight, string Tricks, string FilePath)> ExctrahiereMetadataAusCaliberOpfDateien(string opfDir)
    {
        var books = new List<(string Title, string Creator, string Description, string Highlight, string Tricks, string FilePath)>();

        foreach (var file in Directory.GetFiles(opfDir, "metadata.opf", SearchOption.AllDirectories))
        {
            try
            {
                XDocument doc = XDocument.Load(file);
                XNamespace dc = "http://purl.org/dc/elements/1.1/";
                XNamespace opfNs = doc.Root.GetDefaultNamespace();

                string title = doc.Descendants(dc + "title").FirstOrDefault()?.Value ?? "Untitled";
                string creator = doc.Descendants(dc + "creator").FirstOrDefault()?.Value ?? "Unknown";
                string description = doc.Descendants(dc + "description").FirstOrDefault()?.Value ?? "No description";

                string highlight = doc.Descendants(opfNs + "meta")
                      .Where(e => (string)e.Attribute("name") == "calibre:user_metadata:#highlight")
                      .Select(e => (string)e.Attribute("content"))
                      .FirstOrDefault();
                highlight = ExtractValueFromJson(highlight);

                string tricks = doc.Descendants(opfNs + "meta")
                     .Where(e => (string)e.Attribute("name") == "calibre:user_metadata:#tricks")
                     .Select(e => (string)e.Attribute("content"))
                     .FirstOrDefault();
                tricks = ExtractValueFromJson(tricks);

                string directory = Path.GetDirectoryName(file);
                string linkedFile = Directory.GetFiles(directory, "*.pdf")
                                      .Concat(Directory.GetFiles(directory, "*.epub"))
                                      .Concat(Directory.GetFiles(directory, "*.doc*"))
                                      .FirstOrDefault();

                books.Add((title, creator, description, highlight, tricks, linkedFile));
                Console.WriteLine($"Processed: {title} by {creator}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {file}: {ex.Message}");
            }
        }

        return books.OrderBy(b => b.Creator).ThenBy(b => b.Title).ToList();
    }

    private static string ExtractValueFromJson(string highlight)
    {
        if (!string.IsNullOrEmpty(highlight))
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(highlight);
                if (jsonDoc.RootElement.TryGetProperty("#value#", out JsonElement valueElement))
                {
                    highlight = valueElement.GetString();
                }
            }
            catch (JsonException)
            {
                Console.WriteLine("Error parsing highlight JSON");
            }
        }

        return highlight;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="args">
    ///     * args[0] root Verzeichnis der Caliber Biblothek
    ///     * args[1] zieldatei. Dateiendng sollte epub sein
    /// </param>
    private static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: CalibreGetData <input_path> <output_path>");
            return;
        }

        string opfDir = args[0];
        string epubFile = args[1];

        List<(string Title, string Creator, string Description, string Highlight, string Tricks, string FilePath)> books = ExctrahiereMetadataAusCaliberOpfDateien(opfDir);
        ErstelleEpubDatei(books, epubFile);

        Console.WriteLine($"\nZusammenfassung:\nTotal books processed: {books.Count}\nEPUB file created: {epubFile}\n");
    }
}