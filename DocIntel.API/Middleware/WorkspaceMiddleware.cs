namespace DocIntel.API.Middleware;

public class WorkspaceMiddleware
{
    private readonly RequestDelegate _next;

    public WorkspaceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var workspaceIdClaim = context.User
                .FindFirst("WorkspaceId")?.Value;

            if (!string.IsNullOrEmpty(workspaceIdClaim) &&
                Guid.TryParse(workspaceIdClaim, out var workspaceId))
            {
                
                context.Items["WorkspaceId"] = workspaceId;
            }
        }

        await _next(context);
    }
}