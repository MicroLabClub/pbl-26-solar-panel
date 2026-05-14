using Quartz;
using SolarMonitoring.API.Data;
using SolarMonitoring.API.Jobs;
using SolarMonitoring.API.Models;
using SolarMonitoring.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDb"));
builder.Services.Configure<MqttSettings>(
    builder.Configuration.GetSection("Mqtt"));

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<InMemoryStore>();
builder.Services.AddSingleton<AlertEvaluator>();
builder.Services.AddSingleton<EnergyPredictor>();
builder.Services.AddSingleton<MqttMessageQueue>();
builder.Services.AddSingleton<TelemetryIngestionService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddSingleton<GeocodingService>();
builder.Services.AddSingleton<PhysicsModel>();
builder.Services.AddSingleton<MlCorrectionModel>();
builder.Services.AddSingleton<HybridPredictor>();
builder.Services.AddTransient<ModelTrainingJob>();

// One-shot startup task: seed default installation + backfill telemetry. Runs before MQTT.
builder.Services.AddHostedService<InstallationsSeeder>();

// MQTT subscriber runs as a long-lived background service.
builder.Services.AddHostedService<MqttSubscriberService>();

// Quartz scheduler — registers TelemetryPersistJob to drain the MQTT queue
// to MongoDB on a cron schedule (default: every minute on :00).
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey(nameof(TelemetryPersistJob));
    q.AddJob<TelemetryPersistJob>(opts => opts.WithIdentity(jobKey));

    var cron = builder.Configuration.GetValue<string>("Mqtt:PersistCron") ?? "0 * * * * ?";
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity($"{nameof(TelemetryPersistJob)}-trigger")
        .WithCronSchedule(cron));

    // Nightly ML training at 02:00 UTC.
    var trainKey = new JobKey(nameof(ModelTrainingJob));
    q.AddJob<ModelTrainingJob>(opts => opts.WithIdentity(trainKey));
    q.AddTrigger(opts => opts
        .ForJob(trainKey)
        .WithIdentity($"{nameof(ModelTrainingJob)}-trigger")
        .WithCronSchedule("0 0 2 * * ?"));
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

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
