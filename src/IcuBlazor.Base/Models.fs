namespace IcuBlazor

open System
open System.ComponentModel.DataAnnotations

//[<AutoOpen>]
//module Exception = 

type IcuException(title:string, help:string) =
    inherit Exception(title)

    member val Help = help

    override e.ToString() =
        SF "IcuException: %s\n%s\n%s" title help e.StackTrace

type IcuHelpException(tag:string, title:string, context:obj) =
    inherit Exception(title)

    member val Tag = tag
    member val Context = context
    //override e.ToString() =
    //    SF "IcuException: %s\n   (see %s)\n%s\n" title e.Link e.StackTrace

type ViewLayout =
    | Tree = 0
    | Flat = 1
    | TreeDetail = 2

type IcuConfig() =
    static member DefaultTestDir = "icu_data"
    member val SessionID = Guid.NewGuid().ToString() with get, set
    member val Time = DateTime.Now  // for auditing, add username, machine, browser, etc...

    /// <summary>Root name for all Test Suites.</summary>
    member val Name = "All Tests" with get, set
    /// <summary>IcuBlazor server url. (For standalone Client Side Blazor apps).</summary>
    member val IcuServer = "" with get, set
    /// <summary>Directory under WWWroot where test data files are stored (e.g. "MyTestData").</summary>
    member val TestDir = IcuConfig.DefaultTestDir with get, set

    /// <summary>Disable checks that require a local IcuBlazor server.</summary>
    member val ViewLayout = ViewLayout.Flat with get, set
    /// <summary>Disable checks that require a local IcuBlazor server.</summary>
    member val EnableServer = true with get, set
    member val CanSaveTestData = true with get, set
    /// <summary>Pause execution and show verify dialog when a verify test fails.</summary>
    member val Interactive = false with get, set
    /// <summary>Filter Test methods.</summary>
    member val Filter = "" with get, set
    /// <summary>Stop test execution when the first check fails.</summary>
    member val StopOnFirstFailure = false with get, set

    member val Verbosity = Microsoft.Extensions.Logging.LogLevel.Information with get, set
    member val RunRandomly = true with get, set
    member val OutFile = "" with get, set

    //member this.Clone() = this.MemberwiseClone() :?> IcuConfig

    //static member NewSession(c:IcuConfig) = 
    //    let nc = c.Clone()
    //    nc.SessionID <- Guid.NewGuid().ToString()
    //    nc

type Outcome = 
    | Unknown   = -2
    | Logging   = -1
    | Pass      = 0
    | Skip      = 1
    | New       = 3
    | Fail      = 4
    | Running   = 10
   
module Models = 

    type TestType = Assert | TextDiff | FileDiff | ImageDiff

    type SnapshotArgs() = // Note: this must be serializable
        member val Name = "" with get, set
        member val X = 0. with get, set
        member val Y = 0. with get, set
        member val W = 0. with get, set
        member val H = 0. with get, set
        member val Local = ENV.IsLocalhost

        static member FromJson name json_rect =
            let r = Conv.FromJson<float[]>(json_rect)
            let a = new SnapshotArgs()
            a.Name <- name
            a.X <- r.[0]
            a.Y <- r.[1]
            a.W <- r.[2]
            a.H <- r.[3]
            a

    type InfoLog(log:string) =
        member val Log = log

    // Note: Checkpoint must be serializable
    type Checkpoint(parent, name, 
                    ttype:TestType, outcome:Outcome, hdr:string) =
        inherit Tree.Node<string>(parent, name)
    
        let _verify = lazy(new Proc.Observer<bool>(false))

        member val testType = ttype
        member val header = hdr
        member val model = null with get, set
        member val InfoLog = "" with get, set

        member val Outcome : Outcome = outcome with get, set
        member __.Verify() = _verify.Value

        member this.SetSaveSkip(save) = 
            if save then
                this.Outcome <- Outcome.Pass
            elif (this.Outcome <> Outcome.New) then
                this.Outcome <- Outcome.Skip
        

    type CheckpointGrouping() = // zzz replace with dynamically sorted list
        let g = new MultiDict<_,_>()
        static let EMPTY_CPS = new ResizeArray<_>()

        member val Group = g
        member __.Add(cp:Checkpoint) = g.Add cp.Outcome cp
        member __.GetGroup(oc) =
            if (g.ContainsList(oc))
            then g.GetList(oc).List()
            else EMPTY_CPS

    type DiffFileAssert() = // Note: this must be serializable
        member val Name = "" with get, set
        member val Expect = "" with get, set
        member val Result = "" with get, set
        member val Same = false with get, set
        member val WSdiffs = false with get, set
        static member Make(name, expect, result) =
            let (same, wsdiff) = DiffService.WSdiffs expect result
            let d = new DiffFileAssert()
            d.Name <- name
            d.Expect <- expect
            d.Result <- result
            d.Same <- same
            d.WSdiffs <- wsdiff
            d

    type DiffImageAssert() = // Note: this must be serializable
        member val Name = "" with get, set
        member val Desc = "" with get, set
        static member Make(name, desc) =
            let d = new DiffImageAssert()
            d.Name <- name
            d.Desc <- desc
            d

    let OutcomeType_Max (a:Outcome) (b:Outcome) =
        if a > b then a else b

    let OutcomeType_MaxOf lst = 
        lst |> Seq.fold OutcomeType_Max Outcome.Unknown

    type ThisFunc = {
        Name: string
        This: obj
        Func: obj -> Async<unit>
        MakeArg : TestMethod -> obj
    }
    and TestMethod(parent, thisFunc:ThisFunc) =
        inherit Tree.Node<Checkpoint>(parent, thisFunc.Name)

        member val ThisFunc = thisFunc

        member val RunTime = 0.0 with get, set
        member val Outcome = Outcome.Unknown with get, set
        member this.CalcOutcome() = 
            let v = this.Kids.Copy() 
                    |> Seq.map(fun r -> r.Outcome) 
                    |> OutcomeType_MaxOf
            this.Outcome <- v
            v
        member this.NextCheckpoint ttype outcome hdr model = 
            let parent = this :> Tree.Base |> Some
            let nth = this.Kids.Count.ToString()
            let cp = Checkpoint(parent, nth, ttype, outcome, hdr)
            cp.model <- model
            cp

        member this.AddCheckpoint(check:Checkpoint) = 
            lock this <| fun () ->
                this.Add check.header check


    type TestSuite(parent, name) as tree =
        inherit Tree.Node<TestSuite>(parent, name)

        let sub = tree.Kids
        let baseTree = tree :> Tree.Base |> Some
        let methods = new ResizeArray<TestMethod>()
        let mutable oc = Outcome.Unknown

        let calc_outcome() = 
            methods |> Seq.iter(fun tm -> tm.CalcOutcome() |> ignore)
            let x = sub.Copy() |> Seq.map (fun ts -> ts.Outcome) |> OutcomeType_MaxOf
            let y = methods |> Seq.map (fun a -> a.Outcome) |> OutcomeType_MaxOf
            oc <- OutcomeType_Max x y

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
        member __.Outcome
            with get () = oc
            and set (value) = oc <- value
        member __.CalcOutcome() = calc_outcome()

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

        static member FromMethod(tm:TestMethod) =
            match tm.Parent with
            | Some p ->
                match p with
                | :? TestSuite as st -> Some st
                | _ -> None
            | _ -> None


    type IcuEvent = 
        | AddTest of TestMethod
        | RunStart of obj
        | RunEnd of obj
        | SuiteStart of obj
        | SuiteEnd of obj
        | MethodStart of TestMethod
        | MethodEnd of TestMethod
        | Selected of TestMethod
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

        let IsSafeDir (dir:string) =
            // dir must be a simple dir like "testdir" or "tests/doc"
            let d0 = dir.[0]
            not(d0='.'                  // "..\up_a_dir"
                || d0='/' || d0='\\'    // "/usr/root"
                || dir.Contains(":"))   // "c:\tmp\admin"

        let EnsureDirExists dir name = 
            let f = TestToFile dir name
            FileInfo(f).Directory.Create() // ensure dir exists
