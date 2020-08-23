namespace MyWebApi

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.HttpsPolicy;
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.ResponseCompression
open Microsoft.AspNetCore.Cors.Infrastructure
open IcuBlazor

// Note: This is only used for starting IcuBlazor.Server manually, which is rare.

type Startup (configuration: IConfiguration) =
    //new (configuration: IConfiguration) as this =
    //    Startup() then
    //    this.Configuration <- configuration

    //member val Configuration : IConfiguration = null with get, set


    member __.ConfigureServices(services: IServiceCollection) =
        services.AddMvc() |> ignore
        services.AddResponseCompression(fun opts ->
            opts.MimeTypes <- ResponseCompressionDefaults.MimeTypes.Concat([|"application/octet-stream"|])
        ) |> ignore

        let cb(builder:CorsPolicyBuilder) =
            builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader() |> ignore
        services.AddCors(fun options -> options.AddPolicy("AllowAny", cb)) |> ignore
        


    member __.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        app
            .UseCors("AllowAny") 
            .UseResponseCompression()
            |> ignore

        if (env.IsDevelopment()) then
            app
                .UseDeveloperExceptionPage()
                .UseBlazorDebugging()
                |> ignore
        app
            .UseStaticFiles()
            .UseHttpsRedirection()
            .UseRouting()
            .UseAuthorization()
            .UseEndpoints(fun endpoints -> 
                endpoints.MapDefaultControllerRoute() |> ignore
            )
            |> ignore

        app.UseIcuServer(env) |> ignore // IcuBlazor!

