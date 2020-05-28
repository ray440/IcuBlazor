
# Running Samples

The fastest way to get started with IcuBlazor is to just copy one of the `sample` projects and add your code.  If you need more details however, [configuring IcuBlazor](#configuration) is very simple.

To run the samples load the project in Visual Studio, set a startup project like `Server.SSBLinked` and run it. Note: `CSB.csproj` contains all the app and test code but shouldn't be used as the startup project.

You can run the tests from the commandline.  See `samples\*.bat` for examples.


# Installation



## Hosting Architectures

IcuBlazor supports both *Client-Side Blazor* (CSB) and *Server-Side Blazor* (SSB).  SSBs can run standalone but standalone CSBs may require an IcuBlazor server.  We can maintain both code-bases with two thin server projects: `Server.CSBLinked` and `Server.SSBLinked`.  These projects use a CSB app as if it was an external library. With this setup we can easily switch between server and wasm execution by changing Visual Studio's *Startup Project*.  Here is a layout of the sample projects:

![](sample-projs.svg)

## Configuration

Like other Blazor Libraries, IcuBlazor requires a little configuration.
1. Add IcuBlazor libs to your project thru the nuget packages.
    ```
    dotnet add package IcuBlazor
    dotnet add package IcuBlazor.Server
    ```
    
1. Add javascript files in your `index.html` or `_Hosts.cshtml` page: 

    ```html
    <head>
        <!-- Note: no extra .css file needed! -->
    </head>
    <body>
        :
        <script src="_content/IcuBlazor/interop.js"></script>
        <script src="_content/IcuBlazor/panzoom.min.js"></script>
        <script src="_framework/blazor.server.js"></script>
    </body>
    ```


1. We recommend adding  `@using IcuBlazor`  in `_Imports.razor`
   

CSB & SSBs are virtually the same in terms of app code but there are some setup differences that you need to be aware of.


### SSB Standalone

For SSB you will need to modify Startup.cs.

```cs
public void ConfigureServices(IServiceCollection services)
{
    ...
    services.AddIcuBlazor(new IcuConfig {
        TestDir = "icu_test_data", // dir under wwwroot
    });
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    ...
    app.UseIcuServer(env.WebRootPath); // IcuBlazor!
}

```

`TestDir` is a directory below `wwwroot/` where IcuBlazor stores testing data.  And...that's it.  You are ready to [start making tests](../Readme.md#Usage)! 

### CSB Standalone

You can run a CSB as a standalone web app by settting `IcuConfig.EnableServerTests=false`.  But IcuBlazor will ignore some checks such as `Checker.Log()` & `CompareDiv()`.  These checks need an IcuBlazor server running locally on your machine.  An easy way to add a server is to just use a `Server.CSBLinked` project.

The CSB is typically unaware of the IcuBlazor server. So you must specify the `IcuConfig.IcuServer` url and file path to the CSBs `wwwroot`.  For example in Program.cs:


```cs
using IcuBlazor;
...
    public static async Task Main(string[] args) {
        ...
        builder.Services.AddIcuBlazor(new IcuConfig {
            IcuServer = "https://localhost:44322/",
            WWWroot = @"C:\{project path}\wwwroot\",
            TestDir = @"MyTestData",
            // EnableServerTests=false,
        });

        await builder.Build().RunAsync();
    }
```

### SSBLinked

SSBLinked projects start from the simple SSB template project. Then you need to:
1. Cleanup the `*.razor` files that clash with the CSB files:
    - Remove `App.razor`
    - Remove `Shared\*.razor`. 
    - Remove `Pages\*.razor` except `Pages\Error.razor`.  <br/>
1. In `_Hosts.cshtml` point to your linked library (in this case `CSB.App`) instead of simply `App`.
    ```html
    <component type="typeof(CSB.App)" render-mode="Server" />
    ```

1. You *may* need to setup your HttpClient in `Startup.ConfigureServices()`
    ```cs
    services.AddTransient(sp => new HttpClient {
        // get port from launchSettings.json
        BaseAddress = new Uri("https://localhost:44376/") 
    });
    ```

### CSBLinked
A CSBLinked project starts as an *ASP hosted CSB* project. Additionally,

1. Make it a thin server. We only need the Server project so remove the Shared & Client sub-projects. 
1. Both client & server `launchSettings.json` should have the same `sslPort=443xx`. Also set `IcuConfig.IcuServer="https://localhost:443xx/"`.


