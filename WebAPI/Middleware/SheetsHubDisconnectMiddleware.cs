namespace WebAPI.Middleware;

/// <summary>
/// Lets the Sheets client end long polling with an HTTP POST while retaining
/// SignalR's authenticated connection cleanup and the normal CORS pipeline.
/// </summary>
public sealed class SheetsHubDisconnectMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsPost(request.Method) ||
            !request.Path.Equals(new PathString("/api/sheets-hub/disconnect"), StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var originalPath = request.Path;
        var originalMethod = request.Method;
        try
        {
            // This translation is internal: IIS/proxies receive POST. Routing,
            // CORS and authorization then process the existing hub endpoint.
            request.Path = "/api/sheets-hub";
            request.Method = HttpMethods.Delete;
            await next(context);
        }
        finally
        {
            request.Path = originalPath;
            request.Method = originalMethod;
        }
    }
}
