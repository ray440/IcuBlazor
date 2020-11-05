namespace IcuBlazor

open System
open IcuBlazor.Models

module internal IcuCoreTree =

    module ReflectTest =
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


        let AllMethods asm = Reflect.AllTypes asm |> Seq.map(DeclaredMethods) |> Seq.concat

        //let AllTestsWithAttr<'T> asm = 
        //    let attrType = typeof<'T>
        //    AllMethods asm
        //    |> Seq.filter(WithAttr attrType)
        //    |> Seq.filter(MethodIsTestable)


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
            Func = ReflectTest.ToAsyncMethod thisObj m 
            Name = Str.TitleCase m.Name
        }

        let add_default_tests makeArg (thisObj:obj) =
            set_suite (thisObj.GetType().Name |> Str.TitleCase)

            let toFunc = as_ThisFunc makeArg thisObj
            ReflectTest.DefaultTestsOf thisObj
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

