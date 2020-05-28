namespace IcuBlazor

open System

module internal IcuCore =

    module Reflect =
        open System.Reflection
        open System.Threading.Tasks
        open System.Runtime.ExceptionServices

        let Create(typ:Type) = // create a type with an empty ctor()
            typ.GetConstructor(Array.empty).Invoke(Array.empty)

        [<System.Diagnostics.DebuggerNonUserCode>]
        let Async_VoidMethod this (m:MethodInfo) args = async {
            try 
                m.Invoke(this, args) |> ignore 
            with 
            | :? TargetInvocationException as e ->
                // All this to get actual exception...
                ExceptionDispatchInfo.Capture(e.InnerException).Throw()
        }
        [<System.Diagnostics.DebuggerNonUserCode>]
        let Async_TaskMethod this (m:MethodInfo) args = async { // Note: outer async is needed
            do! m.Invoke(this, args) :?> Task |> Async.AwaitTask
        }
        [<System.Diagnostics.DebuggerNonUserCode>]
        let Async_AsyncMethod this (m:MethodInfo) args = async {
            do! m.Invoke(this, args) :?> Async<unit>
        }

        let TypeTask = typeof<Task>
        let TypeAsync = typeof<Async<unit>>
        let TypeVoid = typeof<Void>

        [<System.Diagnostics.DebuggerNonUserCode>]
        let ToAsyncMethod this (m:MethodInfo) arg =
            let typ = m.ReturnType
            let args = [| arg |]
            if TypeVoid.Equals(typ) then    Async_VoidMethod this m args
            elif TypeTask.Equals(typ) then  Async_TaskMethod this m args
            elif TypeAsync.Equals(typ) then Async_AsyncMethod this m args
            else
                new IcuException(
                    SF "Invalid return type %s.%s -> %A" m.DeclaringType.Name m.Name typ.Name,
                    "1) Tests can only return void, Task or F# Async<unit>\n" )
                |> raise

        let private flags =             
            BindingFlags.Instance
            ||| BindingFlags.DeclaredOnly // No inherited methods.
            ||| BindingFlags.Public
            ||| BindingFlags.NonPublic
        let DeclaredMethods (t:Type) = t.GetMethods(flags)

        let mutable TestArgType = Type.Missing.GetType()

        let MethodIsTestable (m:MethodInfo) =
            let ps = m.GetParameters()
            let n = m.Name
            (ps.Length = 1)
            && (ps.[0].ParameterType.Equals(TestArgType))
            && (not m.IsConstructor)
            && (not(n.[0] = '_' || n.StartsWith "SKIP"))

        let DefaultTestsOf thisObj =
            thisObj.GetType() 
            |> DeclaredMethods 
            |> Seq.filter MethodIsTestable
            |> Seq.toArray
            |> Array.sortBy (fun m -> m.MetadataToken)
            //|> Array.sortBy (fun m -> m.Name)


        let WithAttr attrType (m:MemberInfo) =
            m.GetCustomAttributes(attrType, false).Length > 0

        let AllTypes(asm:Assembly) = asm.GetTypes()

        let AllSubclassOf<'T>(asm) =
            let baseType = typeof<'T>
            AllTypes asm |> Seq.filter(fun t -> t.IsSubclassOf(baseType))

        let AllMethods asm = AllTypes asm |> Seq.map(DeclaredMethods) |> Seq.concat

        //let AllTestsWithAttr<'T> asm = 
        //    let attrType = typeof<'T>
        //    AllMethods asm
        //    |> Seq.filter(WithAttr attrType)
        //    |> Seq.filter(MethodIsTestable)


    module Excep =
        let clean_stack(stack:string) =
            let remove_after finder (st:string[]) =
                match st |> Seq.tryFindIndexBack finder with
                | Some i -> st.[0..(i-1)]
                | _ -> st

            DBG.Err(SF "\n\n%s\n" stack)
            stack 
            |> Str.Split "\n"
            |> remove_after(Str.Contains "--- End of stack trace from previous")
            |> remove_after(Str.Contains "--- End of inner exception stack trace ---")
            |> Seq.exclude(Str.Contains "  at IcuBlazor.")
            |> Str.Join "\n"

        let clean(e:Exception) = clean_stack(e.ToString())


    type internal TestTree(root:TestSuite, config:IcuConfig, msgBus:MessageBus) =

        let tests = new CDict<string, TestMethod>()

        let mutable topSuite = root.Path

        let set_suite path = topSuite <- SlashPath.Make root.Path path

        let is_runnable (tm:TestMethod) = 
            tm.Name.ToLower().Contains(config.Filter)

        let filtered_methods ts =
            if config.Filter.Length > 0 
            then ts |> Seq.filter is_runnable
            else ts
            |> ResizeArray

        let tests_of thisObj =
            tests.Dict()
            |> Seq.map(fun kv -> kv.Value)
            |> Seq.filter(fun t -> t.ThisFunc.This = thisObj)
            |> filtered_methods

        let fetch_suite testPath =
            let path = testPath |> SlashPath.FromString
            let n = path.Length
            let mutable st = root
            for i in 1..(n-2) do
                st <- st.FetchSuite (path.[i])
            st

        let add_func thisFunc =
            let testPath = SlashPath.Make topSuite thisFunc.Name
            if not(tests.ContainsKey testPath) then
                let suite = fetch_suite testPath
                let tm = suite.AddMethod thisFunc 
                tests.Set(testPath, tm)
                msgBus.Notify(AddTest tm)

        let add_test testName makeArg thisObj testf = 
            { Name=testName; This=thisObj; Func=testf; MakeArg=makeArg }
            |> add_func 

        let as_ThisFunc makeArg thisObj m = {
            This = thisObj
            MakeArg = makeArg 
            Func = Reflect.ToAsyncMethod thisObj m 
            Name = Str.TitleCase m.Name
        }

        let add_default_tests makeArg (thisObj:obj) =
            set_suite (thisObj.GetType().Name |> Str.TitleCase)

            let toFunc = as_ThisFunc makeArg thisObj
            Reflect.DefaultTestsOf thisObj
            |> Seq.map toFunc
            |> Seq.iter add_func

        member val Root = root
        member val Tests = tests
        member val MsgBus = msgBus

        member __.Suite path = set_suite path 
        member __.Add = add_test 
        member __.AddDefaultTests = add_default_tests
        member __.FilteredMethods = filtered_methods
        member __.TestsOf thisObj = tests_of thisObj


    type internal TestExecution(tree:TestTree, config:IcuConfig) =

        let sched = new Proc.Scheduler()
        let msgBus = tree.MsgBus

        let mutable state = ExecutionState.Ready

        let abort_run msg  = 
            match state with
            | Executing _ -> state <- Aborted msg
            | _ -> ()

        let logCheck (m:TestMethod) check = 
            m.AddCheckpoint check
            msgBus.Notify(Check check)
            m.OnChange.Notify(m)
            if (config.StopOnFirstFailure && check.Outcome = Fail) then
                msgBus.Notify(Error(Exception("Abort on first failure.")))

        let logResult (m:TestMethod) ttype outcome hdr msg = 
            m.NextCheckpoint ttype outcome hdr msg 
            |> logCheck m

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
                | _ as e -> 
                    let hdr = SF "Fail: %A" m.Path
                    logResult m Assert Fail hdr (Excep.clean e)
        }

        let runTestMethod tm = async {
            match state with
            | Aborted msg-> ()
            | _ ->
                state <- Executing tm
                tm.Outcome <- Running
                msgBus.Notify(MethodStart tm)
                do! executeMethod tm
                //let _ = m.CalcOutcome()
                msgBus.Notify(MethodEnd tm)
        }

        let runTestSuite thisObj tests = async {
            msgBus.Notify(SuiteStart thisObj)

            for tm in tests do
                let a = runTestMethod tm 
                sched.QueueAsync a
                do! sched.Flush()            
            do! sched.Flush()

            msgBus.Notify(SuiteEnd thisObj)
        }

        let runTests thisObj = async {
            match state with
            | Aborted _ -> () 
                // Something bad happened. 
                // User should recompile and/or refresh page
            | Executing _ -> () 
                // Only start once--when `Ready`
            | Ready ->
                let tests = tree.TestsOf thisObj
                if (tests.Count > 0 ) then
                    do! runTestSuite thisObj tests
                if not state.HasAborted then
                    state <- Ready  // can start next suite
        } 
            

        member val Sched = sched
        member val Tree = tree
        member val LogCheck = logCheck

        member __.State = state
        member __.AbortRun(msg) = abort_run msg
        member __.RunTestsAsync thisObj = runTests thisObj
        member __.VerifyDiff (cp:Checkpoint) = async {
            if config.Interactive && (cp.Outcome <> Pass) then
                msgBus.Notify(Verify cp)
                let! _ = cp.Verify().Subscribable.Await() // wait for save/skip response
                msgBus.Notify(Verify Unchecked.defaultof<_>)
        }


    type internal TestChecker(exec:TestExecution, rpc:IRPCproxy, m:TestMethod) =

        let abort_check() = 
            match exec.State with
            | Aborted msg-> new Exception("Method aborted: "+msg) |> raise
            | _ -> ()

        let queue f = 
            abort_check()
            exec.Sched.QueueAsync f
            //f

        let logCheck cp = exec.LogCheck m cp

        let next_checkpoint ttype outcome hdr msg =
            m.NextCheckpoint ttype outcome hdr msg 

        let logAssert outcome hdr msg =
            let cp = next_checkpoint Assert outcome hdr msg 
            logCheck cp

        let logFail hdr msg =
            let cp = next_checkpoint Assert Fail hdr msg
            cp.Open <- true
            logCheck cp

        let logIsTrue cond hdr =
            if cond 
            then logAssert Pass hdr ""
            else logFail hdr "" 

        let text_checkpoint tname ttype expect result hdr =
            let fail = not(String.Equals(expect, result))
            let outcome = if fail then Fail else Pass
            let cp = next_checkpoint ttype outcome hdr "text differs"
            cp.DiffAssert.Name <- tname
            cp.DiffAssert.Expect <- expect 
            cp.DiffAssert.Result <- result
            cp.Open <- fail
            cp

        let image_checkpoint tname outcome =
            let cp = next_checkpoint ImageDiff outcome tname "image differs"
            cp.DiffAssert.Name <- tname
            cp.DiffAssert.IsImgTest <- true
            cp.Open <- (cp.Outcome <> Pass)
            cp

        let check_text expect result hdr =
            let cp = text_checkpoint "" TextDiff expect result hdr
            logCheck cp
        
        let check_log tname result hdr = async {
            let! expect = rpc.ReadTest(tname)
            let cp = text_checkpoint tname LogDiff expect result hdr
            do! exec.VerifyDiff cp
            logCheck cp
        }

        let check_snapshot sid args = async {
            let! diff_count = rpc.CheckRect sid args
            let outcome = match diff_count with
                          | 0 -> Pass
                          | -1 -> New
                          | _ -> Fail
            let cp = image_checkpoint args.Name outcome
            do! exec.VerifyDiff cp
            logCheck cp
        }

        member __.Log outcome hdr msg =
            queue (async { logAssert outcome hdr msg })

        member __.IsTrue state hdr =
            queue (async { logIsTrue state hdr })

        member __.Skip hdr =
            queue (async { logAssert Skip hdr "" })

        member __.CheckText expect result hdr =
            queue (async { check_text expect result hdr })

        member __.CheckLog testName result hdr =
            queue (check_log testName result hdr)

        member __.Snapshot sid args =
            check_snapshot sid args

