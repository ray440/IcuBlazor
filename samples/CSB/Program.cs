using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IcuBlazor;

namespace CSB
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");

            builder.Services.AddTransient(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            builder.Services.AddIcuBlazor(new IcuConfig {
                IcuServer = "https://localhost:44322/",
                WWWroot = "C:\\change-to-actual-path\\CSB\\wwwroot\\",
                //TestDir = @"TestFiles",
                //EnableServerTests = false,
                //Verbosity = LogLevel.Error,
            });


            await builder.Build().RunAsync();
        }
    }
}
