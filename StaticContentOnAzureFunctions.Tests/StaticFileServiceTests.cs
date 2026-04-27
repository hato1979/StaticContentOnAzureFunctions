using System.IO.Compression;
using System.Security.Cryptography;
using Xunit;

namespace StaticContentOnAzureFunctions.Tests;

public sealed class StaticFileServiceTests : IDisposable
{
    private readonly string _tempDir;

    public StaticFileServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void TryGetFile_ReturnsFile_WhenFileExists()
    {
        WriteFile("index.html", "<html></html>");
        var svc = new StaticFileService(_tempDir);

        var found = svc.TryGetFile("/index.html", out var file);

        Assert.True(found);
        Assert.NotNull(file);
        Assert.Equal("text/html; charset=utf-8", file!.ContentType);
    }

    [Fact]
    public void TryGetFile_ReturnsFalse_WhenFileDoesNotExist()
    {
        var svc = new StaticFileService(_tempDir);

        var found = svc.TryGetFile("/missing.html", out var file);

        Assert.False(found);
        Assert.Null(file);
    }

    [Fact]
    public void GetIndexFile_ReturnsIndexHtml_WhenPresent()
    {
        WriteFile("index.html", "<html></html>");
        var svc = new StaticFileService(_tempDir);

        var file = svc.GetIndexFile();

        Assert.NotNull(file);
        Assert.Equal("text/html; charset=utf-8", file!.ContentType);
    }

    [Fact]
    public void GetIndexFile_ReturnsNull_WhenAbsent()
    {
        var svc = new StaticFileService(_tempDir);

        var file = svc.GetIndexFile();

        Assert.Null(file);
    }

    [Theory]
    [InlineData(".html", "text/html; charset=utf-8")]
    [InlineData(".js", "application/javascript; charset=utf-8")]
    [InlineData(".css", "text/css; charset=utf-8")]
    [InlineData(".json", "application/json; charset=utf-8")]
    [InlineData(".txt", "text/plain; charset=utf-8")]
    [InlineData(".png", "image/png")]
    [InlineData(".ico", "image/x-icon")]
    [InlineData(".woff2", "font/woff2")]
    [InlineData(".xyz", "application/octet-stream")]
    public void GetContentType_ReturnsExpectedMimeType(string extension, string expected)
    {
        var result = StaticFileService.GetContentType("file" + extension);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ComputeETag_ReturnsQuotedHexSha256()
    {
        var data = "hello"u8.ToArray();
        var expectedHash = SHA256.HashData(data);
        var expectedETag = "\"" + Convert.ToHexString(expectedHash).ToLowerInvariant() + "\"";

        var etag = StaticFileService.ComputeETag(data);

        Assert.Equal(expectedETag, etag);
    }

    [Fact]
    public void ComputeETag_IsDeterministic()
    {
        var data = "test content"u8.ToArray();
        Assert.Equal(StaticFileService.ComputeETag(data), StaticFileService.ComputeETag(data));
    }

    [Fact]
    public void GzipFiles_AreGeneratedForCompressibleExtensions()
    {
        WriteFile("app.js", "console.log('hello');");
        WriteFile("style.css", "body { margin: 0; }");
        WriteFile("page.html", "<html></html>");
        WriteFile("data.json", "{\"key\":\"value\"}");
        WriteFile("readme.txt", "Hello world");
        var svc = new StaticFileService(_tempDir);

        foreach (var path in new[] { "/app.js", "/style.css", "/page.html", "/data.json", "/readme.txt" })
        {
            svc.TryGetFile(path, out var file);
            Assert.NotNull(file!.GzipContent);
        }
    }

    [Fact]
    public void GzipFiles_AreNotGeneratedForNonCompressibleExtensions()
    {
        WriteFile("image.png", "fake png data");
        var svc = new StaticFileService(_tempDir);

        svc.TryGetFile("/image.png", out var file);
        Assert.NotNull(file);
        Assert.Null(file!.GzipContent);
    }

    [Fact]
    public void CompressGzip_ProducesValidGzipData()
    {
        var original = "Hello, world! This is some test content."u8.ToArray();
        var compressed = StaticFileService.CompressGzip(original);

        using var input = new MemoryStream(compressed);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gz.CopyTo(output);

        Assert.Equal(original, output.ToArray());
    }

    [Fact]
    public void LoadsFilesFromSubdirectories()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "assets"));
        WriteFile("assets/logo.png", "fake png");
        var svc = new StaticFileService(_tempDir);

        var found = svc.TryGetFile("/assets/logo.png", out var file);
        Assert.True(found);
        Assert.NotNull(file);
    }

    [Fact]
    public void ETag_DiffersForDifferentContent()
    {
        var etag1 = StaticFileService.ComputeETag("content1"u8.ToArray());
        var etag2 = StaticFileService.ComputeETag("content2"u8.ToArray());
        Assert.NotEqual(etag1, etag2);
    }

    [Fact]
    public void ServiceInitializesWithoutError_WhenContentDirectoryAbsent()
    {
        var nonExistent = Path.Combine(_tempDir, "nonexistent");
        var svc = new StaticFileService(nonExistent);

        Assert.Null(svc.GetIndexFile());
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
