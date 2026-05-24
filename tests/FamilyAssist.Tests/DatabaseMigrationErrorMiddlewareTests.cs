using FamilyAssist.Data;
using FamilyAssist.Middleware;
using Microsoft.AspNetCore.Http;

namespace FamilyAssist.Tests;

public class DatabaseMigrationErrorMiddlewareTests
{
    [Fact]
    public async Task WhenNoError_PassesThrough()
    {
        var state = new DatabaseHealthState();
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        var middleware = new DatabaseMigrationErrorMiddleware(next, state);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task WhenMigrationError_Returns503WithHtml()
    {
        var state = new DatabaseHealthState();
        state.SetError(new InvalidOperationException("Test migration failure"));
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        var middleware = new DatabaseMigrationErrorMiddleware(next, state);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Contains("text/html", context.Response.ContentType);

        // Read response body
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("Database Migration Failed", body);
        Assert.Contains("Test migration failure", body);
    }

    [Fact]
    public async Task ErrorPage_EscapesHtmlInMessage()
    {
        var state = new DatabaseHealthState();
        state.SetError(new Exception("Error with <script>alert('xss')</script>"));
        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new DatabaseMigrationErrorMiddleware(next, state);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("<script>", body);
        Assert.Contains("&lt;script&gt;", body);
    }

    [Fact]
    public void DatabaseHealthState_InitialState_NoError()
    {
        var state = new DatabaseHealthState();

        Assert.False(state.HasMigrationError);
        Assert.Null(state.ErrorMessage);
        Assert.Null(state.ErrorDetails);
    }

    [Fact]
    public void DatabaseHealthState_SetError_StoresDetails()
    {
        var state = new DatabaseHealthState();
        var inner = new Exception("inner");
        var ex = new InvalidOperationException("outer", inner);

        state.SetError(ex);

        Assert.True(state.HasMigrationError);
        Assert.Equal("outer", state.ErrorMessage);
        Assert.Contains("inner", state.ErrorDetails);
        Assert.Contains("InvalidOperationException", state.ErrorDetails);
    }
}
