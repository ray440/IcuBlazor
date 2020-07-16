namespace IcuBlazor

open System
open IcuBlazor.Models

// Forward RPC requests from Browser App to Server.
// Server App requests go directly to handler on server.
//
//  [BrowserApp]                                           [ServerApp]
//       ↓                                                      ↓
// IcuSession.Rpc -> RPC.WebProxy -> IcuTestController -> IcuSession.Rpc
//                                                              ↓
//                                                       ServerRpc.Handler

module RPC = 
    open System.Net
    open System.Net.Http

    type private Connection(config:IcuConfig, webapi:Web.Api) = 

        let APIhost = config.IcuServer
        let mutable isOnline = false;
        let mutable doCheck = true

        let connect_failed() = 
           if ENV.IsWasm then
            "1) Check client calls builder.Services.AddIcuBlazor(config...).\n" +
            "2) Check server calls app.UseIcuServer() & services.AddIcuBlazor().\n" +
            "3) Ensure server project has started.\n"
           else
            "1) Check server calls app.UseIcuServer() & services.AddIcuBlazor().\n"

        let is_online() = async {
            try 
                isOnline <- false
                let! resp = webapi.GetAsync "Ping" |> Async.AwaitTask
                let! body = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
                if not(body.StartsWith("Pong")) then
                    let msg = SF "Can't access Web API at %A" APIhost
                    let h = SF "1) Try webBuilder.UseUrls(%A)." APIhost 
                    return raise (IcuException(msg, h))
                    
                isOnline <- (resp.StatusCode = HttpStatusCode.OK)
            with
            | :? IcuException as ie -> 
                return raise ie
            | e ->
                DBG.Err(SF "ICU Connection Error: %s" e.Message)
                let msg = SF "Can't connect to server %A\n" APIhost
                return raise (IcuException(msg, connect_failed()))
        }

        let init_session_async() = 
            async {
                DBG.Info(SF "IcuBlazor server: %A" config.IcuServer)
                DBG.Info(SF "IcuBlazor test dir: %A" config.TestDir)
                do! is_online()
                let! resp = webapi.PostJson "SetConfig" config
                if isOnline 
                then DBG.Info("IcuBlazor connected.")
                else DBG.Err("Error: IcuBlazor is not connected.")
            } // |> DBG.IndentA("IcuBlazor Init Session")

        member __.check_session() = async {
            if doCheck then
                do! init_session_async()
                doCheck <- false
        }



    type IProxy =
        abstract member Config : IcuConfig
        abstract member ReadTest: string -> Async<string>
        abstract member SaveTest: DiffAssert -> Async<unit>
        abstract member RunServerTests: unit -> Async<string>
        abstract member InitBrowserCapture: string -> Async<string>
        abstract member CheckRect: string -> SnapshotArgs -> Async<int>

    type NullProxy(config:IcuConfig) =
        let err() = failwith "Server tests disabled"
        interface IProxy with
            member val Config = config
            member __.ReadTest tname = err()
            member __.SaveTest diff = err()
            member __.RunServerTests() = err()
            member __.InitBrowserCapture(title) = err()
            member __.CheckRect _sid args = err()

    type WebProxy(config:IcuConfig, httpFactory:IHttpClientFactory) =

        let sid = config.SessionID
        let apiRoot = config.IcuServer+"api/IcuTest/"
        let webapi = new Web.Api(httpFactory, apiRoot)
        let conn = Connection(config, webapi)

        let fetch uri = 
            async {
                do! conn.check_session()
                return! webapi.GetString uri
            } // |> DBG.IndentA ("fetch "+uri)
        let post uri arg = 
            async {
                do! conn.check_session()
                return! webapi.PostJson uri arg
            } // |> DBG.IndentA ("post "+uri)

        interface IProxy with
            member val Config = config
            member __.ReadTest tname = 
                fetch (SF "ReadTest?sid=%s&tname=%s" sid tname)
            member __.SaveTest diff =
                post (SF "SaveTest?sid=%s" sid) diff |> Async.Ignore
            member __.RunServerTests() = 
                fetch (SF "RunServerTests?sid=%s" sid)
            member __.InitBrowserCapture(title) = async {
                let! msg = fetch (SF "InitBrowserCapture?sid=%s&title=%s" sid title) 
                if (msg.StartsWith("Error")) then
                    failwithf "InitBrowserCapture failed: %s" msg
                return msg;
            }
            member __.CheckRect _sid args = async {
                let! js = post (SF "CheckRect?sid=%s" sid) args
                return Conv.FromJson<int>(js)
            }

    let FindType typeName =
        let t = Type.GetType(typeName)
        if (t <> null) then
            Some t
        else 
            AppDomain.CurrentDomain.GetAssemblies()
            |> Seq.map(fun a -> a.GetType(typeName))
            |> Seq.tryFind(fun t -> t <> null)

    let ProxyType(config:IcuConfig) =
        // choose between Client, Server or Null Proxy
        if not config.EnableServer then
            typeof<NullProxy>
        else
            let sname = "IcuBlazor.ServerRpc+Handler, IcuBlazor.Server"
            match FindType(sname) with
            | Some srvType -> srvType
            | _ -> typeof<WebProxy>
