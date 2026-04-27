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
    private const string CorsAllowOriginValue = "*";
    private const string CorsAllowMethodsValue = "GET, OPTIONS";
    private const string CorsAllowHeadersValue = "Content-Type, Accept, Accept-Encoding, If-None-Match";
    private const string CorsMaxAgeValue = "86400";

    private readonly StaticFileService _staticFileService;

    public StaticFileFunction(StaticFileService staticFileService)
    {
        _staticFileService = staticFileService;
    }

    [Function("StaticFile")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "{*path}")] HttpRequest request,
        string? path)
    {
        var response = request.HttpContext.Response;

        // Add CORS header to every response
        response.Headers[HeaderNames.AccessControlAllowOrigin] = CorsAllowOriginValue;

        // Handle CORS preflight request
        if (HttpMethods.IsOptions(request.Method))
        {
            response.Headers[HeaderNames.AccessControlAllowMethods] = CorsAllowMethodsValue;
            response.Headers[HeaderNames.AccessControlAllowHeaders] = CorsAllowHeadersValue;
            response.Headers[HeaderNames.AccessControlMaxAge] = CorsMaxAgeValue;
            return new StatusCodeResult(StatusCodes.Status204NoContent);
        }

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

        response.Headers[HeaderNames.CacheControl] = CacheControlValue;

        if (useGzip)
        {
            response.Headers[HeaderNames.ContentEncoding] = "gzip";
        }

        return result;
    }
}
