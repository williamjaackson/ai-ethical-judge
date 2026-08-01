using AiEthicalJudge.Hubs;
using AiEthicalJudge.Services;
using AiEthicalJudge.Services.Llm;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Carries each new judgement to the results graph as it is produced.
builder.Services.AddSignalR();

builder.Services.AddSingleton(TimeProvider.System);

// The session is shared by the whole app and lives only in memory.
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddSingleton<ISessionService, SessionService>();

// Judging is stateless, so one instance serves every request.
builder.Services.AddSingleton<IAIJudgeService, AIJudgeService>();

// OpenAI is the only provider the app knows about, and only here.
builder.Services.Configure<OpenAIOptions>(
    builder.Configuration.GetSection(OpenAIOptions.SectionName));
builder.Services.AddSingleton<IChatCompletionClient, OpenAIChatCompletionClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseWebSockets();

var appFiles = new PhysicalFileProvider(
    Path.Combine(app.Environment.WebRootPath, "app"));
app.UseDefaultFiles(new DefaultFilesOptions
{
    RequestPath = "/app",
    FileProvider = appFiles
});
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/app",
    FileProvider = appFiles
});
app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ResultsHub>(ResultsHub.Path);

app.Run();
