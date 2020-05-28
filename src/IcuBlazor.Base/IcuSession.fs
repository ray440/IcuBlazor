namespace IcuBlazor

open System
open System.Diagnostics
open System.Runtime.InteropServices
open IcuCore

module DiffService =
    open DiffPlex
    
    let diffBuilder = Differ() |> DiffBuilder.SideBySideDiffBuilder

    let GetDiffs(oldText, newText) =
        diffBuilder.BuildDiffModel(oldText, newText, false)

// Checker is a facade over TestChecker
type Checker internal (tc:TestChecker, config:IcuConfig) =

    static let help_Text =
        "1) Consider using Check.Log() instead.\n" +
        "2) Raise the limit with Check.Text(..., limit=1000).\n"
    static let help_Log =
        "1) Break test into smaller Check.Log() tests.\n" +
        "2) Raise the limit with Check.Log(..., limit=4000).\n"
    static let help_equal =
        "1) Try converting objects to a string or json.\n" +
        "2) Consider using Check.Text() or Check.Log()"
        
    [<DebuggerNonUserCode>]
    let equal same (a:'T) (b:'T) (hdr) =
        let cond = 
            let typ = typeof<'T>
            if (typ.IsPrimitive) then
                a.Equals(b)
            elif (typ = typeof<string>) then
                String.Equals(a,b)
            else
                new IcuException(
                    SF "Check.Equal()/NotEqual() don't work on general objects",
                    help_equal)
                |> raise

        let result = if same then cond else not(cond)
        let opStr = if cond then "==" else "!="
        let msg = SF "(%A %s %A) %s" a opStr b hdr
        tc.IsTrue result msg

    let skip_disabled name = 
        tc.Skip (name + ": Test disabled (IcuConfig.EnableServerTests=false)")

    let check_text_length (text:string) limit help =
        if (box text <> null) then
            let len = text.Length
            if len > limit then
                (SF "Text exceeds limit (%A > %A)" len limit, help)
                |> IcuException |> raise

    let check_filename fname =        
        //if not(Str.IsValidTestName(fname)) then
        if (String.IsNullOrWhiteSpace(fname)) then
            failwithf "Invalid TestName '%s'" fname

    let check_SnapshotArgs (args:SnapshotArgs) =
        if args.W <= 0.0 then
            failwithf "Invalid Snapshot width=%A" args.W
        if args.H <= 0.0 then
            failwithf "Invalid Snapshot height=%A" args.H
        check_filename args.Name

    [<DebuggerNonUserCode>]
    /// <summary>
    /// Check that `cond` is true.
    /// </summary>
    /// <param name="cond">true/false condition.</param>
    /// <param name="message">Description of test condition.</param>
    member __.True state message = tc.IsTrue state message

    [<DebuggerNonUserCode>]
    /// <summary>
    /// Check that `cond` is true.
    /// </summary>
    /// <param name="cond">true/false condition.</param>
    /// <param name="message">Description of test condition.</param>
    member __.False state message = tc.IsTrue (not state) message

    [<DebuggerNonUserCode>]
    /// <summary>
    /// Declare a failed test.
    /// </summary>
    /// <param name="message">Description of test condition.</param>
    member __.Fail message = tc.IsTrue false message

    [<DebuggerNonUserCode>]
    /// <summary>
    /// Check if two primitive values (expected & actual) are equal.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual runtime value.</param>
    /// <param name="message">Description of test.</param>
    member __.Equal expected actual message = equal true expected actual message

    [<DebuggerNonUserCode>]
    /// <summary>
    /// Check if two primitive values (expected & actual) are NOT equal.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual runtime value.</param>
    /// <param name="message">Description of test.</param>
    member __.NotEqual expected actual message = equal false expected actual message

    /// <summary>
    /// Skip a test.
    /// </summary>
    /// <param name="message">Description of test.</param>
    member __.Skip message = tc.Skip message

    /// <summary>
    /// Check that two lengthy strings are the same.
    /// </summary>
    /// <param name="expected">The expected string.</param>
    /// <param name="actual">The actual runtime string.</param>
    /// <param name="message">Description of test.</param>
    member __.Text(expect, result, message, 
        [<Optional; DefaultParameterValue(800)>] limit:int) =
        check_text_length result limit help_Text
        tc.CheckText expect result message

    /// <summary>
    /// Check that a string is the same as a the last saved string.
    /// </summary>
    /// <param name="logName">Unique test name of file where text is stored.</param>
    /// <param name="result">The actual runtime string.</param>
    /// <param name="message">Description of test.</param>
    member __.Log(logName, result, message,
        [<Optional; DefaultParameterValue(3000)>] limit:int) = 
        check_text_length result limit help_Log
        check_filename logName
        if not config.EnableServerTests 
        then skip_disabled logName
        else tc.CheckLog logName result message 

    /// <summary>
    /// Check that an image is the same as a the last saved image.
    /// </summary>
    member __.Snapshot args = // not called directly by user
        async {
            check_SnapshotArgs args
            if not config.EnableServerTests 
            then skip_disabled args.Name
            else do! tc.Snapshot config.SessionID args
        } |> Async.StartAsTask

// IcuSession is primarily a Facade over 
// - TestTree, TestExecution & TestChecker which are internal
type IcuSession(config) =

    let rpc = RPC.NewProxy(config)

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
        if (String.IsNullOrEmpty(config.WWWroot)) then
            let h = if ENV.IsWasm 
                    then ""
                    else "1) Try calling app.UseIcuServer(env.WebRootPath) in Startup.Configure."
            raise (new IcuException("IcuConfig.WWWroot is not set", h))
        if (String.IsNullOrEmpty(config.TestDir)) then
            raise (new IcuException("IcuConfig.TestDir is not set", ""))
        if (String.IsNullOrEmpty(config.IcuServer) && ENV.IsWasm) then
            raise (new IcuException("IcuConfig.IcuServer is not set", ""))

    static do
        Reflect.TestArgType <- typedefof<Checker>

    member val ID = config.SessionID
    member val Config = config
    member val TreeRoot = root
    member val MsgBus = msgBus

    member internal __.Rpc = rpc

    member __.Validate() =
        if config.EnableServerTests then
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
    member __.AllSubclassOf<'T>(asm) = IcuCore.Reflect.AllSubclassOf<'T>(asm)


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

    let SaveTest (ss:IcuSession) diffResult =
        Async.StartAsTask <| ss.Rpc.SaveTest diffResult
    
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

    let Get name (config:IcuConfig) =
        config.Name <- name
        let id = config.SessionID
        match sess.Find id with
        | Some ss -> ss
        | _ -> 
            flush_old()
            let ss = new IcuSession(config)
            sess.Set(id, ss)
            ss

    let Find id =
        match sess.Find id with
        | Some ss -> ss
        | _ -> failwithf "IcuSession: Can't find session %A" id


module InternalTests =

    let TestFsharpAsync(check:Checker) = async {
        DBG.Log("Pre-Sleep");
        let t0 = DateTime.Now
        do! Async.Sleep(300)
        let dt = (DateTime.Now - t0).TotalMilliseconds
        DBG.Log("Post-Sleep");
        check.True (300.0 < dt && dt < 350.0) (SF "F# Async test waits: %A" dt)
    }

