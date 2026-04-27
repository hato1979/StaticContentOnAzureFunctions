using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Net.Http.Headers;

namespace StaticContentOnAzureFunctions;

/// <summary>
/// Azure Function that serves bundled SPA static files from memory.
/// </summary>
public sealed class StaticFileFunction
{
    private const string CacheControlValue = "public, max-age=3600";

    private readonly StaticFileService _staticFileService;

    public StaticFileFunction(StaticFileService staticFileService)
    {
        _staticFileService = staticFileService;
    }

    [Function("StaticFile")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{*path}")] HttpRequest request,
        string? path)
    {
        var urlPath = "/" + (path ?? string.Empty).TrimStart('/');

        if (!_staticFileService.TryGetFile(urlPath, out var file) || file is null)
        {
            file = _staticFileService.GetIndexFile();
            if (file is null)
            {
                return new NotFoundResult();
            }
        }

        // Handle If-None-Match (conditional GET)
        var ifNoneMatch = request.Headers[HeaderNames.IfNoneMatch].ToString();
        if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == file.ETag)
        {
            return new StatusCodeResult(StatusCodes.Status304NotModified);
        }

        // Determine whether to serve gzip-compressed content
        var acceptEncoding = request.Headers[HeaderNames.AcceptEncoding].ToString();
        var useGzip = file.GzipContent is not null
            && acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase);

        var body = useGzip ? file.GzipContent! : file.Content;

        var result = new FileContentResult(body, file.ContentType)
        {
            EntityTag = EntityTagHeaderValue.Parse(file.ETag),
        };

        request.HttpContext.Response.Headers[HeaderNames.CacheControl] = CacheControlValue;

        if (useGzip)
        {
            request.HttpContext.Response.Headers[HeaderNames.ContentEncoding] = "gzip";
        }

        return result;
    }
}
