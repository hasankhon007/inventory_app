using Microsoft.AspNetCore.Builder;

namespace Course.Services.Middleware;

public static class ApplicationMiddlewareExtensions
{
    public static IApplicationBuilder UseApplicationMiddlewares(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        return app;
    }
}
