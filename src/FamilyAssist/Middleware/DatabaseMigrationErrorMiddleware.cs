using FamilyAssist.Data;

namespace FamilyAssist.Middleware;

/// <summary>
/// Short-circuits all requests with a static HTML error page when
/// the database migration failed at startup. This works without
/// Blazor/EF since those depend on a working database.
/// </summary>
public sealed class DatabaseMigrationErrorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DatabaseHealthState _state;

    public DatabaseMigrationErrorMiddleware(RequestDelegate next, DatabaseHealthState state)
    {
        _next = next;
        _state = state;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_state.HasMigrationError)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = 503;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(BuildErrorPage(_state.ErrorMessage ?? "Unknown error", _state.ErrorDetails));
    }

    private static string BuildErrorPage(string message, string? details)
    {
        var escapedMessage = System.Net.WebUtility.HtmlEncode(message);
        var escapedDetails = System.Net.WebUtility.HtmlEncode(details ?? "");

        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8"/>
            <meta name="viewport" content="width=device-width, initial-scale=1"/>
            <title>FamilyAssist — Database Error</title>
            <style>
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                    background: #121212;
                    color: #e0e0e0;
                    display: flex;
                    justify-content: center;
                    align-items: center;
                    min-height: 100vh;
                    padding: 24px;
                }
                .card {
                    background: #1c1c1c;
                    border: 1px solid rgba(255,255,255,0.12);
                    border-radius: 12px;
                    max-width: 600px;
                    width: 100%;
                    padding: 32px;
                }
                .icon {
                    font-size: 48px;
                    margin-bottom: 16px;
                }
                h1 {
                    font-size: 1.5rem;
                    color: #ff5252;
                    margin-bottom: 8px;
                }
                .subtitle {
                    color: #bbb;
                    margin-bottom: 24px;
                    line-height: 1.5;
                }
                .error-box {
                    background: #2c2c2c;
                    border: 1px solid rgba(255,82,82,0.3);
                    border-radius: 8px;
                    padding: 16px;
                    margin-bottom: 24px;
                    font-family: 'Cascadia Code', 'Fira Code', monospace;
                    font-size: 0.85rem;
                    white-space: pre-wrap;
                    word-break: break-word;
                    max-height: 200px;
                    overflow-y: auto;
                    color: #ff8a80;
                }
                .steps {
                    list-style: none;
                    margin-bottom: 24px;
                }
                .steps li {
                    padding: 8px 0;
                    padding-left: 24px;
                    position: relative;
                    line-height: 1.4;
                }
                .steps li::before {
                    content: attr(data-step);
                    position: absolute;
                    left: 0;
                    color: #03a9f4;
                    font-weight: bold;
                }
                .btn {
                    display: inline-block;
                    background: #03a9f4;
                    color: #fff;
                    border: none;
                    border-radius: 9999px;
                    padding: 10px 24px;
                    font-size: 0.9rem;
                    font-weight: 500;
                    cursor: pointer;
                    text-decoration: none;
                }
                .btn:hover { background: #0288d1; }
                details { margin-bottom: 24px; }
                details summary {
                    cursor: pointer;
                    color: #03a9f4;
                    font-size: 0.85rem;
                    margin-bottom: 8px;
                }
            </style>
        </head>
        <body>
            <div class="card">
                <div class="icon">⚠️</div>
                <h1>Database Migration Failed</h1>
                <p class="subtitle">
                    FamilyAssist could not update its database to the latest version.
                    The application cannot start until this is resolved.
                </p>

                <div class="error-box">{{escapedMessage}}</div>

                <details>
                    <summary>Show full error details</summary>
                    <div class="error-box">{{escapedDetails}}</div>
                </details>

                <ul class="steps">
                    <li data-step="1.">Check the Add-on logs for more details</li>
                    <li data-step="2.">If you recently downgraded, restore a database backup</li>
                    <li data-step="3.">Restart the Add-on to retry the migration</li>
                    <li data-step="4.">If the problem persists, file an issue on GitHub</li>
                </ul>

                <a class="btn" href="/" onclick="location.reload(); return false;">Retry</a>
            </div>
        </body>
        </html>
        """;
    }
}
