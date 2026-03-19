using AppScaffolding;
using LibationFileManager;
using LibationWeb.Hubs;

// Initialize Libation configuration (must happen before anything else)
var config = LibationScaffolding.RunPreConfigMigrations();
LibationScaffolding.RunPostConfigMigrations(config);
LibationScaffolding.RunPostMigrationScaffolding(Variety.Chardonnay, config);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseRouting();
app.MapControllers();
app.MapHub<ProgressHub>("/hubs/progress");

// Fallback: serve index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Run();
