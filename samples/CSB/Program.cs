using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IcuBlazor;
namespace CSB
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            var baseAddr = builder.HostEnvironment.BaseAddress;
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddr) });
            builder.Services.AddIcuBlazor(new IcuConfig {
                IcuServer = baseAddr,
                //TestDir = @"TestFiles",
                //EnableServer = false,
                //Verbosity = LogLevel.Error,
            });


            await builder.Build().RunAsync();
        }
    }
}
