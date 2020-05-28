namespace IcuBlazor

open System
open Microsoft.Extensions.Logging

type IcuConfig() =
    static member DefaultTestDir = "icu_data"
    member val SessionID = Guid.NewGuid().ToString() with get, set
    member val Time = DateTime.Now  // for auditing, add username, machine, browser, etc...

    /// <summary>Root name for all Test Suites.</summary>
    member val Name = "All Tests" with get, set
    /// <summary>IcuBlazor server url. (For standalone Client Side Blazor apps).</summary>
    member val IcuServer = "" with get, set
    /// <summary>Full path of www root dir (e.g. "c:\\myproj\\wwwroot\\").</summary>
    member val WWWroot = "" with get, set
    /// <summary>Directory under WWWroot where test data files are stored (e.g. "MyTestData").</summary>
    member val TestDir = IcuConfig.DefaultTestDir with get, set
    /// <summary>Disable checks that require a local IcuBlazor server.</summary>
    member val EnableServerTests = true with get, set
    /// <summary>Pause execution and show verify dialog when a verify test fails.</summary>
    member val Interactive = false with get, set
    /// <summary>Filter Test methods.</summary>
    member val Filter = "" with get, set
    /// <summary>Stop test execution when the first check fails.</summary>
    member val StopOnFirstFailure = false with get, set

    member val Verbosity = Microsoft.Extensions.Logging.LogLevel.Information with get, set
    member val RunRandomly = true with get, set
    member val OutFile = "" with get, set

type BaseTree(parent:Option<BaseTree>, name) =
    let path = 
        let root = if parent.IsSome then parent.Value.Path else ""
        SlashPath.Make root name
    let onChange = new Proc.Subscribable<obj>()
    
    member val Path = path
    member val Name = name
    member val OnChange = onChange
    member val Open = true with get, set
    member g.ToggleOpen() = g.Open <- not g.Open


type ATree<'Kid>(parent, name) =
    inherit BaseTree(parent, name)

    let kids = new CList<'Kid>();
    let lookup = new CDict<string, 'Kid>();

    member val Parent = parent
    member val Kids = kids
    member val Lookup = lookup

    member __.Add key c =
        kids.Add(c)
        lookup.Set(key, c)
    
    member __.Clear() =
        kids.Clear()
        lookup.Clear()


type TestType = Assert | TextDiff | LogDiff | ImageDiff

type OutcomeType = 
    Unknown | Running | Fail | Skip | Pass | New
    with
        member r.Order = 
            match r with
            | Unknown -> -2
            | Pass -> 0
            | Skip -> 1
            | New -> 3
            | Fail -> 4
            | Running -> 10

        static member Max (a:OutcomeType) (b:OutcomeType) =
            if a.Order > b.Order then a else b

        static member MaxOf lst = 
            lst |> Seq.fold OutcomeType.Max Unknown

   
type DiffAssert() = // Note: this must be serializable
    member val Name = "" with get, set
    member val Expect = "" with get, set
    member val Result = "" with get, set
    member val IsImgTest = false with get, set
    

type SnapshotArgs() = // Note: this must be serializable
    member val Name = "" with get, set
    member val X = 0. with get, set
    member val Y = 0. with get, set
    member val W = 0. with get, set
    member val H = 0. with get, set
    static member FromJson name json_rect =
        let r = Conv.FromJson<float[]>(json_rect)
        let a = new SnapshotArgs()
        a.Name <- name
        a.X <- r.[0]
        a.Y <- r.[1]
        a.W <- r.[2]
        a.H <- r.[3]
        a


// Note: Checkpoint must be serializable
type Checkpoint(parent, name, 
                ttype:TestType, outcome:OutcomeType, hdr:string, msg:string) =
    inherit ATree<string>(parent, name)
    
    let _diff = new DiffAssert()
    let _verify = lazy(new Proc.Observer<bool>(false))
    member val testType = ttype
    member val header = hdr
    member val msg = msg
    member val Outcome : OutcomeType = outcome with get, set
    member val DiffAssert = _diff
    member __.Verify() = _verify.Value
    member this.SetSaveSkip(save) = 
        if save then
            this.Outcome <- Pass
        elif (this.Outcome <> New) then
            this.Outcome <- Skip
        
type ThisFunc = {
    Name: string
    This: obj
    Func: obj -> Async<unit>
    MakeArg : TestMethod -> obj
}
and TestMethod(parent, thisFunc:ThisFunc) =
    inherit ATree<Checkpoint>(parent, thisFunc.Name)

    member val ThisFunc = thisFunc

    member val RunTime = 0.0 with get, set
    member val Outcome = Unknown with get, set
    member this.CalcOutcome() = 
        let v = this.Kids.Copy() 
                |> Seq.map(fun r -> r.Outcome) 
                |> OutcomeType.MaxOf
        this.Outcome <- v
        v
    member this.NextCheckpoint ttype outcome hdr msg = 
        let parent = this :> BaseTree |> Some
        let nth = this.Kids.Count.ToString()
        Checkpoint(parent, nth, ttype, outcome, hdr, msg)

    member this.AddCheckpoint(check:Checkpoint) = 
        lock this <| fun () ->
            this.Add check.header check


type TestSuite(parent, name) as tree =
    inherit ATree<TestSuite>(parent, name)

    let sub = tree.Kids
    let baseTree = tree :> BaseTree |> Some
    let methods = new ResizeArray<TestMethod>()
    let mutable oc = Unknown

    let calc_outcome() = 
        let x = sub.Copy() |> Seq.map (fun g -> g.Outcome) |> OutcomeType.MaxOf
        let y = methods |> Seq.map (fun a -> a.Outcome) |> OutcomeType.MaxOf
        oc <- OutcomeType.Max x y
        oc

    let ts_make name = new TestSuite(baseTree, name)
    let ts_find name = tree.Lookup.Find name
    let ts_add name =
        let suite = ts_make name
        tree.Add name suite
        suite
    let ts_fetch name =
        match ts_find name with
        | Some suite -> suite
        | _ -> ts_add name


    member val Methods = methods
    member __.Outcome = calc_outcome()
    member __.TestCount = sub.Count + methods.Count

    member __.FindSuite name = ts_find name
    member __.FetchSuite name = ts_fetch name

    member __.AddMethod thisFunc =
        let tm = new TestMethod(baseTree, thisFunc)
        methods.Add tm
        tm

    member this.Reset() =
        this.Clear()
        methods.Clear()


type IRPCproxy =
    abstract member Config : IcuConfig
    abstract member ReadTest: string -> Async<string>
    abstract member SaveTest: DiffAssert -> Async<unit>
    abstract member RunServerTests: unit -> Async<string>
    abstract member InitBrowserCapture: string -> Async<string>
    abstract member CheckRect: string -> SnapshotArgs -> Async<int>


type IcuEvent = 
    | AddTest of TestMethod
    | RunStart of obj
    | RunEnd of obj
    | SuiteStart of obj
    | SuiteEnd of obj
    | MethodStart of TestMethod
    | MethodEnd of TestMethod
    | Error of Exception
    | Check of Checkpoint
    | Verify of Checkpoint
    | RefreshView of obj

type MessageBus() =
    inherit Proc.Subscribable<IcuEvent>()


type ExecutionState = 
    | Ready
    | Executing of TestMethod
    | Aborted of string
    with 
        member this.HasAborted = 
            match this with
            | Aborted _ -> true
            | _ -> false


module IcuIO =
    open System.IO

    let TestToFile dir tname = Path.Combine(dir, tname + ".txt")
    let private TestToImage dir tname = Path.Combine(dir, tname + ".png")
    let private imageFile dir kind tname = TestToImage dir (tname+"_"+kind)
    let CurrImageFile dir tname = TestToImage dir tname
    let NewImageFile dir tname = imageFile dir "new" tname
    let DiffImageFile dir tname = imageFile dir "diff" tname

    let EnsureDirExists dir name = 
        let f = TestToFile dir name
        FileInfo(f).Directory.Create() // ensure dir exists
