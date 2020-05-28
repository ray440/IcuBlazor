namespace IcuBlazor.Controllers

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open IcuBlazor

[<ApiController>]
[<Route("api/[controller]")>]
type IcuTestController() =
    inherit Controller()

    let sess sid = IcuSessions.Find(sid)

    [<HttpGet("[action]")>]
    member __.Ping() = // @ https://localhost:44322/api/IcuTest/Ping
        let t = DateTime.Now.ToString("hh:mm:ss ")
        let r = Guid.NewGuid().ToString()
        Task.FromResult(SF "Pong t = %s   r = %s" t r)

    [<HttpPost("[action]")>]
    member __.SetConfig([<FromBody>] config:IcuConfig) =
        let ss = IcuSessions.Get config.Name config
        Task.FromResult(ss.ID)

    [<HttpGet("[action]")>]
    member __.ReadTest(sid, tname) =
        IcuRpc.ReadTest (sess sid) tname
    
    [<HttpPost("[action]")>]
    member __.SaveTest(sid, [<FromBody>] dres:DiffAssert) =
        IcuRpc.SaveTest (sess sid) dres
    
    [<HttpGet("[action]")>]
    member __.RunServerTests(sid) =
        IcuRpc.RunServerTests (sess sid)

    [<HttpGet("[action]")>]
    member __.InitBrowserCapture(sid, title) =
        IcuRpc.InitImageCapture (sess sid) title

    [<HttpPost("[action]")>]
    member __.CheckRect(sid, [<FromBody>] args:SnapshotArgs) =
        IcuRpc.CheckRect (sess sid) args

