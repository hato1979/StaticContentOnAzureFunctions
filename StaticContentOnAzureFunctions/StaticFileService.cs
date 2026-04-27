using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;

namespace StaticContentOnAzureFunctions;

/// <summary>
/// Represents a file loaded into memory, including optional gzip-compressed version.
/// </summary>
public sealed class CachedFile
{
    public byte[] Content { get; init; } = [];
    public byte[]? GzipContent { get; init; }
    public string ETag { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
}

/// <summary>
/// Loads all files from the Content directory into memory at startup and provides
/// fast in-memory access to static file content.
/// </summary>
public sealed class StaticFileService
{
    private static readonly HashSet<string> GzipExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".js", ".css", ".json", ".txt"
    };

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".html", "text/html; charset=utf-8" },
        { ".htm",  "text/html; charset=utf-8" },
        { ".js",   "application/javascript; charset=utf-8" },
        { ".mjs",  "application/javascript; charset=utf-8" },
        { ".css",  "text/css; charset=utf-8" },
        { ".json", "application/json; charset=utf-8" },
        { ".txt",  "text/plain; charset=utf-8" },
        { ".xml",  "application/xml; charset=utf-8" },
        { ".svg",  "image/svg+xml; charset=utf-8" },
        { ".ico",  "image/x-icon" },
        { ".png",  "image/png" },
        { ".jpg",  "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif",  "image/gif" },
        { ".webp", "image/webp" },
        { ".woff", "font/woff" },
        { ".woff2","font/woff2" },
        { ".ttf",  "font/ttf" },
        { ".eot",  "application/vnd.ms-fontobject" },
        { ".mp4",  "video/mp4" },
        { ".webm", "video/webm" },
        { ".pdf",  "application/pdf" },
        { ".map",  "application/json; charset=utf-8" },
    };

    private readonly Dictionary<string, CachedFile> _cache;

    public StaticFileService(IHostEnvironment env)
        : this(Path.Combine(env.ContentRootPath, "Content"))
    {
    }

    internal StaticFileService(string contentDirectory)
    {
        _cache = LoadFiles(contentDirectory);
    }

    private static Dictionary<string, CachedFile> LoadFiles(string contentDirectory)
    {
        var cache = new Dictionary<string, CachedFile>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(contentDirectory))
        {
            return cache;
        }

        foreach (var filePath in Directory.EnumerateFiles(contentDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(contentDirectory, filePath)
                .Replace('\\', '/');
            var key = "/" + relativePath;

            var content = File.ReadAllBytes(filePath);
            var etag = ComputeETag(content);
            var contentType = GetContentType(filePath);
            var extension = Path.GetExtension(filePath);

            byte[]? gzipContent = null;
            if (GzipExtensions.Contains(extension))
            {
                gzipContent = CompressGzip(content);
            }

            cache[key] = new CachedFile
            {
                Content = content,
                GzipContent = gzipContent,
                ETag = etag,
                ContentType = contentType,
            };
        }

        return cache;
    }

    /// <summary>
    /// Attempts to get a cached file by its URL path.
    /// </summary>
    public bool TryGetFile(string path, out CachedFile? file)
        => _cache.TryGetValue(path, out file);

    /// <summary>
    /// Returns the cached index.html entry, or null if not present.
    /// </summary>
    public CachedFile? GetIndexFile()
        => _cache.TryGetValue("/index.html", out var f) ? f : null;

    /// <summary>
    /// Computes a quoted ETag string from the SHA-256 hash of the content.
    /// </summary>
    public static string ComputeETag(byte[] content)
    {
        var hash = SHA256.HashData(content);
        return "\"" + Convert.ToHexString(hash).ToLowerInvariant() + "\"";
    }

    /// <summary>
    /// Returns the MIME type for the given file path based on its extension.
    /// Falls back to application/octet-stream for unknown extensions.
    /// </summary>
    public static string GetContentType(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return MimeTypes.TryGetValue(ext, out var mime) ? mime : "application/octet-stream";
    }

    /// <summary>
    /// Compresses the given bytes using gzip.
    /// </summary>
    public static byte[] CompressGzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.Optimal))
        {
            gz.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }
}
