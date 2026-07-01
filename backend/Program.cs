using StayFlow.Api.Extensions;
using StayFlow.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplicationDatabase(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
