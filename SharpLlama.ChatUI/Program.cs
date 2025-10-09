using SharpLlama.ChatUI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Chat service + HttpClient (base address supplied per user input later)
builder.Services.AddHttpClient<ChatApiClient>();

builder.Services.AddScoped<ChatState>();
builder.Services.AddScoped<ChatService>();

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
// IMPORTANT: Map the document component defined in Components/App.razor
app.MapRazorComponents<SharpLlama.ChatUI.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
