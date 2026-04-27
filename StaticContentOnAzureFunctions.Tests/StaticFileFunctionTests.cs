using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace StaticContentOnAzureFunctions.Tests;

public sealed class StaticFileFunctionTests : IDisposable
{
    private readonly string _tempDir;

    public StaticFileFunctionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "index.html"), "<html></html>");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Run_Options_Returns204NoContent()
    {
        var func = CreateFunction();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "OPTIONS";

        var result = func.Run(httpContext.Request, null);

        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, statusResult.StatusCode);
    }

    [Fact]
    public void Run_Options_ReturnsCorsPreflightHeaders()
    {
        var func = CreateFunction();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "OPTIONS";

        func.Run(httpContext.Request, null);

        Assert.Equal("*", httpContext.Response.Headers[HeaderNames.AccessControlAllowOrigin].ToString());
        Assert.Contains("GET", httpContext.Response.Headers[HeaderNames.AccessControlAllowMethods].ToString());
        Assert.False(string.IsNullOrEmpty(httpContext.Response.Headers[HeaderNames.AccessControlAllowHeaders].ToString()));
        Assert.False(string.IsNullOrEmpty(httpContext.Response.Headers[HeaderNames.AccessControlMaxAge].ToString()));
    }

    [Fact]
    public void Run_Get_IncludesAccessControlAllowOriginHeader()
    {
        var func = CreateFunction();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        func.Run(httpContext.Request, "index.html");

        Assert.Equal("*", httpContext.Response.Headers[HeaderNames.AccessControlAllowOrigin].ToString());
    }

    [Fact]
    public void Run_Get_NotFound_IncludesAccessControlAllowOriginHeader()
    {
        var svc = new StaticFileService(_tempDir);  // has index.html, so 404 never fires; use empty dir
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);
        var emptySvc = new StaticFileService(emptyDir);
        var func = new StaticFileFunction(emptySvc);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        func.Run(httpContext.Request, "missing.html");

        Assert.Equal("*", httpContext.Response.Headers[HeaderNames.AccessControlAllowOrigin].ToString());
    }

    [Fact]
    public void Run_Get_304_IncludesAccessControlAllowOriginHeader()
    {
        var func = CreateFunction();
        var svc = new StaticFileService(_tempDir);
        svc.TryGetFile("/index.html", out var file);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Headers[HeaderNames.IfNoneMatch] = file!.ETag;

        func.Run(httpContext.Request, "index.html");

        Assert.Equal("*", httpContext.Response.Headers[HeaderNames.AccessControlAllowOrigin].ToString());
    }

    private StaticFileFunction CreateFunction()
    {
        var svc = new StaticFileService(_tempDir);
        return new StaticFileFunction(svc);
    }
}
