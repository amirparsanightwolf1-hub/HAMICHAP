using System.Buffers.Binary;
using System.IO.Compression;
using Docnet.Core;
using Docnet.Core.Models;

namespace HamiChap_MES_ERP.Services;

/// <summary>
/// Renders uploaded PDF artwork to PNG on the application server.
/// No CDN, third-party conversion API or external network access is involved.
/// </summary>
public static class LocalPdfPreviewEndpoints
{
    private const int MaxPages = 64;
    private const long MaxPdfBytes = 100L * 1024 * 1024;
    private const int RenderDimension = 1600;

    public static void MapLocalPdfPreview(this WebApplication app)
    {
        var group = app.MapGroup("/api/pdf-preview").RequireAuthorization();

        group.MapGet("/info", (string file, IWebHostEnvironment env) =>
        {
            if (!TryFindPdf(file, env, out var path))
                return Results.NotFound();

            try
            {
                using var reader = DocLib.Instance.GetDocReader(path, new PageDimensions(RenderDimension, RenderDimension));
                var count = reader.GetPageCount();
                if (count < 1 || count > MaxPages)
                    return Results.BadRequest($"PDF previews support 1 to {MaxPages} pages.");

                var pages = new List<object>(count);
                for (var i = 0; i < count; i++)
                {
                    using var page = reader.GetPageReader(i);
                    pages.Add(new { width = page.GetPageWidth(), height = page.GetPageHeight() });
                }

                return Results.Ok(new { pages });
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
            {
                return Results.BadRequest("Unable to open this PDF for preview.");
            }
        });

        group.MapGet("/page", (string file, int page, IWebHostEnvironment env) =>
        {
            if (!TryFindPdf(file, env, out var path))
                return Results.NotFound();

            if (page < 1 || page > MaxPages)
                return Results.BadRequest("Invalid PDF page number.");

            try
            {
                using var reader = DocLib.Instance.GetDocReader(path, new PageDimensions(RenderDimension, RenderDimension));
                if (page > reader.GetPageCount())
                    return Results.NotFound();

                using var pageReader = reader.GetPageReader(page - 1);
                var png = EncodePng(pageReader.GetImage(), pageReader.GetPageWidth(), pageReader.GetPageHeight());
                return Results.File(png, "image/png");
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
            {
                return Results.BadRequest("Unable to render this PDF page.");
            }
        });
    }

    // Only local PDFs previously uploaded into wwwroot/uploads may be rendered.
    // Resolving and checking the canonical path prevents traversal and SSRF.
    private static bool TryFindPdf(string file, IWebHostEnvironment env, out string path)
    {
        path = string.Empty;
        const string prefix = "/uploads/";
        if (string.IsNullOrWhiteSpace(file) ||
            !file.StartsWith(prefix, StringComparison.Ordinal) ||
            file.Contains('?') || file.Contains('#') || file.Contains('\\'))
            return false;

        try
        {
            var relative = Uri.UnescapeDataString(file[prefix.Length..]);
            if (relative.Contains('\\') || string.IsNullOrWhiteSpace(relative))
                return false;

            var uploadRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, "wwwroot", "uploads"));
            var resolved = Path.GetFullPath(Path.Combine(uploadRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!resolved.StartsWith(uploadRoot + Path.DirectorySeparatorChar, comparison) ||
                !resolved.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(resolved) ||
                new FileInfo(resolved).Length > MaxPdfBytes)
                return false;

            path = resolved;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UriFormatException)
        {
            return false;
        }
    }

    // A small dependency-free PNG encoder. Docnet.Core returns BGRA pixels;
    // PNG requires RGBA scanlines and zlib-compressed IDAT data.
    private static byte[] EncodePng(byte[] bgra, int width, int height)
    {
        if (width <= 0 || height <= 0 ||
            bgra.Length < checked(width * height * 4))
            throw new InvalidOperationException("PDF renderer returned an invalid image.");

        var rowBytes = checked(width * 4);
        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = new byte[rowBytes + 1]; // PNG filter 0 (None)
            for (var y = 0; y < height; y++)
            {
                var sourceOffset = y * rowBytes;
                for (var x = 0; x < width; x++)
                {
                    var src = sourceOffset + x * 4;
                    var dst = 1 + x * 4;
                    row[dst] = bgra[src + 2];     // red
                    row[dst + 1] = bgra[src + 1]; // green
                    row[dst + 2] = bgra[src];     // blue
                    row[dst + 3] = bgra[src + 3]; // alpha
                }
                deflate.Write(row);
            }
        }

        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8; // 8 bits per channel
        header[9] = 6; // RGBA
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var name = System.Text.Encoding.ASCII.GetBytes(type);
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        stream.Write(number);
        stream.Write(name);
        stream.Write(data);

        var crc = uint.MaxValue;
        foreach (var b in name)
            crc = UpdateCrc(crc, b);
        foreach (var b in data)
            crc = UpdateCrc(crc, b);
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc);
        stream.Write(number);
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
            crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0u);
        return crc;
    }
}
