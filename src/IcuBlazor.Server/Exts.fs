namespace IcuBlazor

open System.Runtime.CompilerServices
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting

[<Extension>]
type Extensions = 

    [<Extension>]
    static member inline UseIcuServer(app:IApplicationBuilder, env:IWebHostEnvironment) =
        let c = app.ApplicationServices.GetService<IcuConfig>()
        //ENV.wwwroot <- env.WebRootPath // usually empty!
        WWWRoot.FromConfig env c
        ENV.IsIcuEnabled <- true
        DBG.Verbosity <- c.Verbosity
        DBG.SetSystem("Srv")
