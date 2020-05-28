namespace MyWebApi

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration

module Program =
    let exitCode = 0

    let BuildWebHost(args:string[]) =
        WebHost.CreateDefaultBuilder()
            .UseConfiguration(
                ConfigurationBuilder().AddCommandLine(args).Build())
            .UseUrls("https://*:44322", "http://*:22222")
            .UseStartup<Startup>()
            .Build()

    [<EntryPoint>]
    let main args =
        BuildWebHost(args).Run()
        exitCode
