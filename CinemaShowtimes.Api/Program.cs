using CinemaShowtimes.Api;
using CinemaShowtimes.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddDbContext<CinemaDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("Cinema")
        ?? throw new InvalidOperationException("Connection string 'Cinema' is not configured.")));

var app = builder.Build();

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
