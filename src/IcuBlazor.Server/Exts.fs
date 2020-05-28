namespace IcuBlazor

open System
open System.Runtime.CompilerServices
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection

[<Extension>]
type Extensions = 

    [<Extension>]
    static member inline AddIcuServer(services:IServiceCollection) =
        services.AddHttpClient("ICUapi")

    [<Extension>]
    static member inline UseIcuServer(app:IApplicationBuilder, wwwroot) =
        let c = app.ApplicationServices.GetService<IcuConfig>()
        if not(String.IsNullOrEmpty(wwwroot)) then
            c.WWWroot <- wwwroot

        ENV.IsIcuEnabled <- true
        DBG.Verbosity <- c.Verbosity
        DBG.SetSystem("Srv")
        ServerRpc.Init()

