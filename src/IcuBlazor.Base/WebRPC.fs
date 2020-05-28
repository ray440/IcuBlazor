namespace IcuBlazor

open System
open System.IO
open IcuCore

// Forward RPC requests from App to Server
// BrowserApp -> IcuSession.Rpc -> RPC.Client -> IcuTestController
//                                                    ↓
// ServerApp ----------------------------------> IcuSession.Rpc -> ServerRpc


module RPC = 

    module Client =
        open System.Net

        type WebProxy(config:IcuConfig) =

            let sid = config.SessionID

            let APIhost = config.IcuServer
            let API = APIhost+"api/IcuTest/"

            let connection_help() = 
               if ENV.IsWasm then
                "1) Check server configuration (e.g. app.UseIcuServer() & services.AddIcuBlazor() are called).\n" +
                "2) Ensure server project has started. Standalone CSB projects need a server.\n" +
                "3) Check servers launchSettings.json applicationUrl/sslPort property.\n" +
                "4) Cannot run a CSB on Firefox due to CORS/localhost issues.\n"
               else
                "1) For SSB call app.UseIcuServer() in Startup.Configure()\n" +
                "2) Check servers launchSettings.json applicationUrl property."

            let mutable isOnline = false;
            let IsOnline() = async {
                try 
                    isOnline <- false
                    let! resp = Web.GetHttp().GetAsync(API + "Ping") |> Async.AwaitTask
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
                    let h = connection_help()
                    return raise (IcuException(msg, h))
            }

            let mutable doset = true
            let init_session_async() = 
                async {
                    let tdir = Path.Combine(config.WWWroot, config.TestDir)
                    DBG.Info(SF "IcuBlazor server: %A" APIhost)
                    DBG.Info(SF "IcuBlazor test dir: %A" tdir)
                    do! IsOnline()
                    let uri = API + "SetConfig";
                    let! resp = Web.PostJsonAsync(uri, config)
                    if isOnline 
                    then DBG.Info("IcuBlazor connected.")
                    else DBG.Err("Error: IcuBlazor is not connected.")
                } // |> DBG.IndentA("IcuBlazor Init Session")
            let init_session() = async {
                if doset then
                    do! init_session_async()
                    doset <- false
            }

            let fetch uri = 
                async {
                    do! init_session()
                    return! Web.GetStringAsync(API + uri)
                } // |> DBG.IndentA ("fetch "+uri)
            let post uri arg = 
                async {
                    do! init_session()
                    return! Web.PostJsonAsync(API + uri, arg)
                } // |> DBG.IndentA ("post "+uri)

            interface IRPCproxy with
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

        type NullProxy(config:IcuConfig) =
            let err() = failwith "Server tests disabled"
            interface IRPCproxy with
                member val Config = config
                member __.ReadTest tname = err()
                member __.SaveTest diff = err()
                member __.RunServerTests() = err()
                member __.InitBrowserCapture(title) = err()
                member __.CheckRect _sid args = err()

        let CreateProxy(config:IcuConfig) =
            if config.EnableServerTests 
            then WebProxy(config) :> IRPCproxy
            else NullProxy(config) :> IRPCproxy

    // Server redefines & sets a different IRPCproxy
    let mutable NewProxy = Client.CreateProxy
