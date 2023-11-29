namespace Server
{
    public record ValidateOriginMiddleware
    {
        private readonly RequestDelegate _next;

        public ValidateOriginMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!context.Request.Headers.ContainsKey("Origin"))
            {
                context.Response.StatusCode = 401;
                return;
            }

            var origin = context.Request.Headers.Origin;

            if (origin != "https://vision-client.azurewebsites.net")
            {
                context.Response.StatusCode = 403;
                return;
            }

            await _next(context);
        }
    }
}