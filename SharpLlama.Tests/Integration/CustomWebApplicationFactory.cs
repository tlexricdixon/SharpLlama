using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SharpLlama.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(cfg =>
        {
            var dict = new Dictionary<string, string?>
            {
                ["ModelPath"] = "test-model.bin" // placeholder so service construction does not throw early
            };
            cfg.AddInMemoryCollection(dict!);
        });

        builder.ConfigureServices(services =>
        {
            // Optionally replace heavy dependencies here
        });
        return base.CreateHost(builder);
    }
}
