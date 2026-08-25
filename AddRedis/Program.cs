// FILE KHUNG - xem AddRedis.csproj.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => "AddRedis");
app.Run();
