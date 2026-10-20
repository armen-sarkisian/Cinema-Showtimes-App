using CinemaShowtimes.Api;
using CinemaShowtimes.Api.Errors;
using CinemaShowtimes.Application;
using CinemaShowtimes.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

            var problem = ApiProblemFactory.Create(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                "Request validation failed",
                "One or more request fields are invalid.",
                "validation_error");
            problem.Extensions["errors"] = errors;
            return new BadRequestObjectResult(problem);
        };
    });
builder.Services.AddSwaggerDocumentation();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<MovieApplicationService>();
builder.Services.AddScoped<ShowtimeApplicationService>();
builder.Services.AddScoped<ReservationApplicationService>();
builder.Services.AddDbContext<CinemaDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("Cinema")
        ?? throw new InvalidOperationException("Connection string 'Cinema' is not configured.")));

var app = builder.Build();
app.UseMiddleware<CinemaExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.UseHttpsRedirection();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();

public partial class Program { }
