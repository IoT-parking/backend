using backend.Configuration;
using backend.Repositories;
using backend.Services;
using backend.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Configuration from environment variables and appsettings.json
// Priority: Environment Variables > appsettings.json
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Bind configuration with ability to override from env variables
builder.Services.Configure<MongoDbSettings>(options =>
{
    builder.Configuration.GetSection(MongoDbSettings.SectionName).Bind(options);
    
    // Override from env variables if they exist
    var connString = builder.Configuration["MONGODB_CONNECTION_STRING"];
    if (!string.IsNullOrEmpty(connString))
        options.ConnectionString = connString;
    
    var dbName = builder.Configuration["MONGODB_DATABASE_NAME"];
    if (!string.IsNullOrEmpty(dbName))
        options.DatabaseName = dbName;
    
    var collectionName = builder.Configuration["MONGODB_COLLECTION_NAME"];
    if (!string.IsNullOrEmpty(collectionName))
        options.SensorReadingsCollectionName = collectionName;
});

builder.Services.Configure<MqttSettings>(options =>
{
    builder.Configuration.GetSection(MqttSettings.SectionName).Bind(options);
    
    // Override from env variables if they exist
    var brokerHost = builder.Configuration["MQTT_BROKER_HOST"];
    if (!string.IsNullOrEmpty(brokerHost))
        options.BrokerHost = brokerHost;
    
    var brokerPort = builder.Configuration["MQTT_BROKER_PORT"];
    if (!string.IsNullOrEmpty(brokerPort) && int.TryParse(brokerPort, out var port))
        options.BrokerPort = port;
    
    var username = builder.Configuration["MQTT_USERNAME"];
    if (!string.IsNullOrEmpty(username))
        options.Username = username;
    
    var password = builder.Configuration["MQTT_PASSWORD"];
    if (!string.IsNullOrEmpty(password))
        options.Password = password;
    
    var clientId = builder.Configuration["MQTT_CLIENT_ID"];
    if (!string.IsNullOrEmpty(clientId))
        options.ClientId = clientId;
    
    var topic = builder.Configuration["MQTT_TOPIC"];
    if (!string.IsNullOrEmpty(topic))
        options.Topic = topic;
    
    var useTls = builder.Configuration["MQTT_USE_TLS"];
    if (!string.IsNullOrEmpty(useTls) && bool.TryParse(useTls, out var tls))
        options.UseTls = tls;
});

builder.Services.AddSingleton<ISensorReadingRepository, SensorReadingRepository>();
builder.Services.AddSingleton<IExportService, ExportService>();
builder.Services.AddHostedService<MqttClientService>();

builder.Services.AddControllers();

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IoT Parking Sensor API",
        Version = "v1",
        Description = "API for managing IoT sensor readings in parking system",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Bidul team",
            Email = "stanislaw.grochowski@bidul.pl"
        },
        License = new Microsoft.OpenApi.Models.OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Enable XML comments for Swagger (optional)
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "IoT Parking Sensor API v1");
        options.RoutePrefix = string.Empty; // Swagger UI on root URL
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();
app.MapHub<SensorHub>("/sensorHub");

app.MapGet("/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}))
.WithName("HealthCheck")
.WithTags("Health");

app.Run();
