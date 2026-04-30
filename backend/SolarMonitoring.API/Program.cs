using SolarMonitoring.API.Data;
using SolarMonitoring.API.Models;
using SolarMonitoring.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDb"));

// MongoContext is registered for future use; the in-memory store is
// the active data source for this scaffold so the API runs without Mongo.
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<InMemoryStore>();
builder.Services.AddSingleton<AlertEvaluator>();
builder.Services.AddSingleton<EnergyPredictor>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("frontend");
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
