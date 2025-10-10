using SharpLlama.ChatUi.Service;
using SharpLlama.ChatUI;
using SharpLlama.ChatUI.Components;
using SharpLlama.ChatUI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Chat service + HttpClient (base address supplied per user input later)
builder.Services.AddHttpClient<ChatApiClient>();

builder.Services.AddScoped<ChatState>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddSingleton<ComponentRegistry>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
