namespace IcuBlazor.Controllers

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Hosting
open IcuBlazor

[<ApiController>]
[<Route("api/[controller]")>]
type IcuTestController(env:IWebHostEnvironment) =
    inherit Controller()

    let sess sid = IcuSessions.Find(sid)

    [<HttpGet("[action]")>]
    member __.Ping() = // @ https://localhost:44322/api/IcuTest/Ping
        if (Str.isEmpty ENV.wwwroot) then
            WWWRoot.FromFile env "index.html"

        let t = DateTime.Now.ToString("hh:mm:ss ")
        let r = Guid.NewGuid().ToString()
        Task.FromResult(SF "Pong t = %s   r = %s" t r)

    [<HttpPost("[action]")>]
    member __.SetConfig([<FromBody>] config:IcuConfig) =
        WWWRoot.FromConfig env config

        let rpc = ServerRpc.CreateSrvProxy(config)
        let ss = IcuSessions.Register rpc
        Task.FromResult(ss.ID)

    [<HttpGet("[action]")>]
    member __.ReadTest(sid, tname) =
        IcuRpc.ReadTest (sess sid) tname
    
    [<HttpPost("[action]")>]
    member __.SaveFileTest(sid, [<FromBody>] dres:Models.DiffFileAssert) =
        IcuRpc.SaveFileTest (sess sid) dres
    
    [<HttpPost("[action]")>]
    member __.SaveImageTest(sid, [<FromBody>] dres:Models.DiffImageAssert) =
        IcuRpc.SaveImageTest (sess sid) dres
    
    [<HttpGet("[action]")>]
    member __.RunServerTests(sid) =
        IcuRpc.RunServerTests (sess sid)

    [<HttpGet("[action]")>]
    member __.InitBrowserCapture(sid, title) =
        IcuRpc.InitImageCapture (sess sid) title

    [<HttpPost("[action]")>]
    member __.CheckRect(sid, [<FromBody>] args:Models.SnapshotArgs) =
        IcuRpc.CheckRect (sess sid) args

