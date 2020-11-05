namespace IcuBlazor

open System
open IcuBlazor.Models
open IcuBlazor.IcuCoreTree

module internal IcuCore =

    type internal TestExecution(tree:TestTree, config:IcuConfig) =

        let sched = new Proc.Scheduler()
        let msgBus = tree.MsgBus

        let mutable state = ExecutionState.Ready

        let abort_run msg  = 
            match state with
            | Executing _ -> state <- Aborted msg
            | _ -> ()

        let logCheckpoint (m:TestMethod) check = 
            m.AddCheckpoint check
            msgBus.Notify(Check check)
            m.OnChange.Notify(m)
            if (config.StopOnFirstFailure && check.Outcome = Outcome.Fail) then
                msgBus.Notify(Error(Exception("Abort on first failure.")))

        let logResult (m:TestMethod) ttype outcome hdr msg = 
            m.NextCheckpoint ttype outcome hdr msg 
            |> logCheckpoint m

        let abort_check() = 
            match state with
            | Aborted msg-> new Exception("Method aborted: "+msg) |> raise
            | _ -> ()

        let queue f = 
            abort_check()
            sched.QueueAsync f
            //f

        let logException (m:TestMethod) (e:Exception) =
            queue (async { logResult m Assert Outcome.Fail "Exception thrown" e })

        let executeMethod (m:TestMethod) = async {

            let timedTest = async {
                let testf = m.ThisFunc.Func
                let arg = m.ThisFunc.MakeArg(m)
                let t0 = DateTime.Now
                do! testf(arg)
                let dt = DateTime.Now - t0
                m.RunTime <- Math.Round(dt.TotalMilliseconds)
            }
               
            try
                do! timedTest
                //let! ok = timedTest |> Proc.WithTimeout config.TestTimeout
                //if not ok then
                //    new TimeoutException() |> raise

            with
                //| :? TaskCanceledException 
                //| :? TimeoutException ->
                //    let hdr = SF "Timeout after %A ms" config.TestTimeout
                //    logResult m Assert Fail hdr ""
                | _ as e -> logException m e
        }

        let runTestMethod tm = async {
            match state with
            | Aborted msg-> ()
            | _ ->
                state <- Executing tm                    
                tm.Outcome <- Outcome.Running
                msgBus.Notify(MethodStart tm)
                do! executeMethod tm
                //let _ = m.CalcOutcome()
                msgBus.Notify(MethodEnd tm)
        }

        let runTestSuite thisObj tests = async { 
            // thisObj is typically IcuTestSuite Blazor Component
            msgBus.Notify(SuiteStart thisObj)

            let suites = 
                tests 
                |> Seq.choose TestSuite.FromMethod 
                |> Seq.distinct |> Seq.toArray
            suites |> Seq.iter(fun st -> st.Outcome <- Outcome.Running)

            for tm in tests do
                let a = runTestMethod tm 
                sched.QueueAsync a
                do! sched.Flush()            
            do! sched.Flush()

            suites |> Seq.iter(fun st -> st.CalcOutcome())
            msgBus.Notify(SuiteEnd thisObj)
        }

        let runTests thisObj = async {
            match state with
            | Aborted _ -> () 
            | Executing _ -> () // Only start once--when `Ready`
            | Ready ->
                let tests = tree.TestsOf thisObj
                if (tests.Count > 0) then
                    do! runTestSuite thisObj tests
                if not state.HasAborted then
                    state <- Ready  // can start next suite
        } 
            

        member val Tree = tree
        member val LogCheckpoint = logCheckpoint

        member __.Queue = queue
        member __.AbortRun(msg) = abort_run msg
        member __.RunTestsAsync thisObj = runTests thisObj
        member __.VerifyDiff (cp:Checkpoint) = async {
            if config.Interactive && (cp.Outcome <> Outcome.Pass) then
                msgBus.Notify(Verify cp)
                let! _ = cp.Verify().Subscribable.Await() // wait for save/skip response
                msgBus.Notify(Verify Unchecked.defaultof<_>)
        }

    type internal TestChecker(exec:TestExecution, rpc:RPC.IProxy, m:TestMethod) =

        let infoLog = new Logger.StringBuilderLog()

        let logCheckpoint (cp:Checkpoint) =
            exec.LogCheckpoint m cp

        let queue (f:Async<Checkpoint>) = 
            let ilog = infoLog.Flush()
            exec.Queue (async {
                let! cp = f
                cp.InfoLog <- ilog
                logCheckpoint cp
            })

        let next_checkpoint ttype outcome hdr data =
            m.NextCheckpoint ttype outcome hdr data 

        let logAssert outcome hdr msg =
            next_checkpoint Assert outcome hdr msg 

        let logFail hdr msg =
            let cp = next_checkpoint Assert Outcome.Fail hdr msg
            cp.Open <- true
            cp

        let logIsTrue cond hdr =
            if cond 
            then logAssert Outcome.Pass hdr None
            else logFail hdr None

        let text_checkpoint tname ttype expect result hdr =
            let da = DiffFileAssert.Make(tname, expect, result)
            let outcome = if da.Same then Outcome.Pass else Outcome.Fail
            let cp = next_checkpoint ttype outcome hdr da
            cp.Open <- not da.Same
            cp

        let image_checkpoint name outcome =
            let ilog = infoLog.Flush()
            let da = DiffImageAssert.Make(name, ilog)
            let cp = next_checkpoint ImageDiff outcome name da
            cp.InfoLog <- ilog
            cp.Open <- (cp.Outcome <> Outcome.Pass)
            cp

        let check_text expect result hdr =
            text_checkpoint "" TextDiff expect result hdr
        
        let check_file tname result hdr = async {
            let! expect = rpc.ReadTest(tname)
            let cp = text_checkpoint tname FileDiff expect result hdr
            do! exec.VerifyDiff cp
            return cp
        }

        let check_snapshot sid args = async {
            let! diff_count = rpc.CheckRect sid args
            let outcome = match diff_count with
                          |  0 -> Outcome.Pass
                          | -1 -> Outcome.New
                          |  _ -> Outcome.Fail
            let cp = image_checkpoint args.Name outcome
            do! exec.VerifyDiff cp
            logCheckpoint cp
        }

        member val Logger = infoLog

        member __.MakeCheckpoint ttype outcome hdr data = 
            let cp = next_checkpoint ttype outcome hdr data
            logCheckpoint cp
            cp

        member __.Show outcome hdr dataModel =
            queue (async { return logAssert outcome hdr dataModel })

        member __.IsTrue state hdr =
            queue (async { return logIsTrue state hdr })

        member __.Skip hdr =
            queue (async { return logAssert Outcome.Skip hdr None })

        member __.CheckText expect result hdr =
            queue (async { return check_text expect result hdr })

        member __.CheckFile testName result hdr =
            queue (check_file testName result hdr)

        member __.Snapshot sid args =
            check_snapshot sid args

