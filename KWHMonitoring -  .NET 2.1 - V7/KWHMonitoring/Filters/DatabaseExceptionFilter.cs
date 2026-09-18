using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KWHMonitoring.Filters
{
    /// <summary>
    /// Global exception filter that catches database-related exceptions (SqlException,
    /// DbUpdateException, timeout) and returns a user-friendly error instead of the
    /// developer exception page. API requests get JSON 503; browser requests get a
    /// DatabaseError view.
    /// </summary>
    public class DatabaseExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<DatabaseExceptionFilter> _logger;

        public DatabaseExceptionFilter(ILogger<DatabaseExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            if (!IsDatabaseException(context.Exception))
                return;

            _logger.LogError(context.Exception,
                "Database error while processing {Method} {Path}",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path);

            context.ExceptionHandled = true;

            if (IsApiRequest(context))
            {
                context.Result = new JsonResult(new
                {
                    error = "Database sedang tidak tersedia. Silakan coba lagi nanti.",
                    status = 503
                })
                { StatusCode = 503 };
            }
            else
            {
                context.Result = new ViewResult
                {
                    ViewName = "DatabaseError"
                };
            }
        }

        private static bool IsApiRequest(ExceptionContext context)
        {
            var path = context.HttpContext.Request.Path.Value;
            if (path != null && path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
                return true;

            var accept = context.HttpContext.Request.Headers["Accept"].ToString();
            if (accept != null && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static bool IsDatabaseException(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is System.Data.SqlClient.SqlException)
                    return true;
                if (current is TimeoutException)
                    return true;
            }

            if (ex is DbUpdateException)
                return true;

            return false;
        }
    }
}
