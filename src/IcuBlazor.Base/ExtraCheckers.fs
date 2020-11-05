namespace IcuBlazor

open System
open System.Threading.Tasks
open System.Runtime.InteropServices

open IcuBlazor.Models
open FsCheck

type FsCheckResult(cx:Checker, cp:Checkpoint, f) =

    let mutable ex = None

    let set_outcome e =
        ex <- e
        cp.Outcome <- if ex.IsSome then Outcome.Fail else Outcome.Pass
        cp.InfoLog <- cx.Logger.Flush()

    member __.Error() = ex

    member __.Run() =
        try 
            f()
            set_outcome None
        with ex ->
            set_outcome (Some ex)

    static member New (cx:Checker) name  act =
        let cp = cx.MakeCheckpoint name null
        let res = new FsCheckResult(cx, cp, act)
        cp.model <- res
        res


type PropertyChecker(cx:Checker) =

    let pcfg = lazy(Configuration.QuickThrowOnFailure)
    let arbList = new ResizeArray<Type>()

    let to_FsConfig(c:Configuration) = { 
        Config.QuickThrowOnFailure with 
            MaxTest=c.MaxNbOfTest
            MaxFail=c.MaxNbOfFailedTests
            Name = c.Name
            Every = fun n (a:obj list) -> c.Every.Invoke(n,a |> Seq.toArray)
            EveryShrink = fun(a:obj list) -> c.EveryShrink.Invoke(a |> Seq.toArray)
            StartSize = c.StartSize
            EndSize = c.EndSize
            QuietOnSuccess = c.QuietOnSuccess
            Runner = c.Runner
            Replay = if (box c.Replay) = null then None else Some(c.Replay)
            Arbitrary = arbList |> Seq.toList
    }

    member val Config = pcfg.Value
    member val Types = arbList

    member __.Check name (f:Func<'a, bool>) = 
        let cfg = to_FsConfig(pcfg.Value)
        let act() = FsCheck.Check.One(name, cfg, f.Invoke)
        let model = FsCheckResult.New cx name act
        model.Run()

    member __.PickAny<'a>([<Optional; DefaultParameterValue(1000)>] size:int) = 
        Arb.generate<'a> |> Gen.sample size 1 |> Seq.head
        

type LogChecker(cx:Checker) =
    let sb = new Logger.StringBuilderLog()
    let mutable ignore_nl = false;

    let flush() = 
        let s = sb.Flush()
        if ignore_nl 
        then s |> Str.ReplaceAll "\r\n" "\n"
        else s

    let check_text(expect, message) = 
        cx.Text(expect, flush(), message)

    let check_file(logName, message) =
        cx.TextFile(logName, flush(), message)

    let indent inc title =
        if inc > 0 
        then sb.Indent(inc, title + " {")
        else sb.Indent(inc, "}")

    let sectionF title filename f = // for F#
        try
            indent 3 title
            try
                f()
            with e ->
                sb.Log(e.ToString())
        finally 
            indent -3 title
            check_file(filename, title)

    let sectionAction title filename (f:Action) = // for C#
        sectionF title filename (f.Invoke)

    let sectionFSAsync title filename f = async {  
        try
            indent 3 title
            try
                do! f
            with e ->
                sb.Log(e.ToString())
        finally 
            indent -3 title
            check_file(filename, title)
    }

    let sectionTask title filename (t:Func<Task>) = async {
        try
            indent 3 title
            try
                // Don't refactor. Must be called here for proper logging
                do! t.Invoke() |> Async.AwaitTask
            with e ->
                sb.Log(e.ToString())
        finally 
            indent -3 title
            check_file(filename, title)
    }

    member val Buffer = sb

    member __.Clear() = sb.Buffer.Clear()
    member __.Note(s:string) = sb.Log(s)
    member __.Json(x) = sb.Log(Conv.ToJson(x))
    member __.IgnoreNewLines(v) = ignore_nl <- v

    member __.Section(title, filename, f) = 
        sectionAction title filename f

    member __.Section(title, filename, f) = 
        sectionFSAsync title filename f

    member __.Section(title, filename, t:Func<Task>) = 
        //t.Invoke() |> Async.AwaitTask |> sectionA title filename 
        //|> Async.StartAsTask :> Task
        sectionTask title filename t |> Async.StartAsTask :> Task

    member __.CheckText(expect, message) = check_text(expect, message)
    member __.CheckFile(logName, message) = check_file(logName, message)


module InternalTests =

    let TestFsharpAsync(check:Checker) = async {
        DBG.Log("Pre-Sleep");
        let t0 = DateTime.Now
        do! Async.Sleep(300)
        let dt = (DateTime.Now - t0).TotalMilliseconds
        DBG.Log("Post-Sleep");
        check.True (300.0 < dt && dt < 350.0) (SF "F# Async test waits: %A" dt)
    }

