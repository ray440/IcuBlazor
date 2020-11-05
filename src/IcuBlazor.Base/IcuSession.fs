namespace IcuBlazor

open System

open IcuBlazor.Models
open IcuBlazor.IcuCore
open IcuBlazor.IcuCoreTree


// IcuSession is primarily a Facade over 
// - TestTree, TestExecution & TestChecker which are internal
type IcuSession(config, rpc:RPC.IProxy) =

    let root = 
        let t = new TestSuite(None, rpc.Config.Name)
        t.Open <- true
        t
    
    let msgBus = new MessageBus()
    let tree = new TestTree(root, config, msgBus)
    let exec = new TestExecution(tree, config)

    let make_arg tm = 
        let tc = new TestChecker(exec, rpc, tm)
        new Checker(tc, config) |> box
    let add_test testName thisObj testAsync =
        tree.Add testName make_arg thisObj testAsync

    let validate_server() =
        if (String.IsNullOrEmpty(config.TestDir)) then
            raise (new IcuException("IcuConfig.TestDir is not set", ""))
        if not (IcuIO.IsSafeDir(config.TestDir)) then
            raise (new IcuException("IcuConfig.TestDir is not safe", ""))
        if (String.IsNullOrEmpty(config.IcuServer) && ENV.IsWasm) then
            raise (new IcuException("IcuConfig.IcuServer is not set", ""))

    static do
        ReflectTest.TestArgType <- typedefof<Checker>

    member val ID = config.SessionID
    member val Config = config
    member val TreeRoot = root
    member val MsgBus = msgBus

    member internal __.Rpc = rpc

    member __.Validate() =
        if config.EnableServer then
            validate_server()

    member __.Suite path = tree.Suite path // define current Suite

    member __.TestAsync testName thisObj testf =
        add_test testName thisObj testf
    member __.Test testName thisObj (testf:Action<Checker>) =
        fun c -> async { testf.Invoke(unbox c)}
        |> add_test testName thisObj 
    member __.TestTask testName thisObj (testf:Func<Checker, Threading.Tasks.Task>) =
        fun c -> async { do! testf.Invoke(unbox c) |> Async.AwaitTask }
        |> add_test testName thisObj 

    member this.Add thisObj testf =
        testf |> this.Test (Str.TitleCase testf.Method.Name) thisObj 
    member this.AddTask thisObj testf =
        testf |> this.TestTask (Str.TitleCase testf.Method.Name) thisObj 
    member __.AddDefaultTests(thisObj) = 
        tree.AddDefaultTests make_arg thisObj 

    member __.FilteredMethods ts = tree.FilteredMethods(ts)

    member __.RunTests(thisObj) = exec.RunTestsAsync(thisObj)
    member __.RunServerTests() = rpc.RunServerTests() |> Async.StartAsTask
    member __.AbortRun(msg) = exec.AbortRun(msg)


(*
module IcuFS = // F# specific API

    let TestAsync (ss:IcuSession) testName thisObj testf =
        ss.TestAsync testName thisObj testf

    let Test (ss:IcuSession) testName thisObj testf = // make every test async
        ss.TestAsync testName thisObj (fun c -> async { testf c })

    let RunTests (ss:IcuSession) thisObj = ss.RunTests(thisObj)
*)

module IcuRpc = 
    // IcuHelper: internal to IcuTest. 
    // - Converts Async -> Task (for C# code)
    // - Works on Client or Server (hides internal icu.Rpc)

    let ReadTest (ss:IcuSession) testName = 
        Async.StartAsTask <| ss.Rpc.ReadTest testName

    let SaveFileTest (ss:IcuSession) diffResult =
        Async.StartAsTask <| ss.Rpc.SaveFileTest diffResult
    
    let SaveImageTest (ss:IcuSession) diffResult =
        Async.StartAsTask <| ss.Rpc.SaveImageTest diffResult

    let RunServerTests (ss:IcuSession) =
        Async.StartAsTask <| ss.Rpc.RunServerTests()

    let CheckRect (ss:IcuSession) args = 
        Async.StartAsTask <| ss.Rpc.CheckRect ss.ID args

    let InitImageCapture (ss:IcuSession) title = 
        Async.StartAsTask <| ss.Rpc.InitBrowserCapture title
        
module IcuSessions =
    
    let private sess = new CDict<string,IcuSession>()

    let private flush_old() =
        if sess.Count > 3 then
            // remove sessions older than 1 hour
            let old = DateTime.Now.AddHours(-1.0).Ticks
            let oldKeys = 
                sess.Dict()
                |> Seq.filter(fun kv -> kv.Value.Config.Time.Ticks < old)
                |> Seq.map (fun kv -> kv.Key)
                |> Seq.toArray
            oldKeys |> Seq.iter(fun k -> sess.Remove(k) |> ignore)

    let Register (rpc:RPC.IProxy) =
        flush_old()
        let ss = new IcuSession(rpc.Config, rpc)
        sess.Set(rpc.Config.SessionID, ss)
        ss

    let Find id =
        match sess.Find id with
        | Some ss -> ss
        | _ -> failwithf "IcuSession: Can't find session %A" id


