namespace IcuBlazor

open System
open System.IO
open IcuBlazor

module ServerRpc =

    type Handler(config:IcuConfig) =
        let dir = 
            let www, dir = config.WWWroot, config.TestDir
            if not(Directory.Exists(www)) then 
                raise (IcuException(SF "Invalid WWWroot %A." www, ""))

            let d = Path.Combine(www, dir)
            DBG.Info(SF "Test data dir = %A" d)

            if IcuConfig.DefaultTestDir.Equals(dir) then
                Directory.CreateDirectory(d) |> ignore

            if not(Directory.Exists(d)) then
                let help =
                    (SF "1) WWWroot(=%A) must be a full path (e.g. c:\\dir-path...\\wwwroot\\)\n" www) +
                    (SF "2) TestDir(=%A) must be below WWWroot \n" dir)
                raise (IcuException(SF "Can't find Test Directory %A." d, help))
            d

        let rec handleFileException(e:exn) =
            match e with
            | :? FileNotFoundException -> ""
            | :? DirectoryNotFoundException -> ""
            | :? AggregateException as ae -> handleFileException ae.InnerException
            | _ as e ->
                DBG.Err(SF "err: %s" (e.ToString()))
                e.Message

        let readTest tname = async {
            try
                let fname = IcuIO.TestToFile dir tname
                return! File.ReadAllTextAsync(fname) |> Async.AwaitTask
            with e ->
                return handleFileException e
        }

        let saveLogTest (diff:DiffAssert) = async {
            IcuIO.EnsureDirExists dir diff.Name
            let f = IcuIO.TestToFile dir diff.Name
            return! File.WriteAllTextAsync(f, diff.Result) |> Async.AwaitTask
        }

        let saveImageTest (diff:DiffAssert) = async {
            GC.Collect()
            let tname = diff.Name
            IcuIO.EnsureDirExists dir tname
            let cur_fname = IcuIO.CurrImageFile dir tname
            let new_fname = IcuIO.NewImageFile  dir tname
            File.Copy(new_fname, cur_fname, true)
        }

        let saveTest (diff:DiffAssert) =
            if diff.IsImgTest 
            then saveImageTest diff
            else saveLogTest diff

        let check_rect sid (args:SnapshotArgs) =            
            IcuIO.EnsureDirExists dir args.Name
            WinCapture.SnapAndCompare sid dir args

        let runServerTests() = // so we don't have to use Xunit, Nunit, etc...
            async {
                //AppDomain.CurrentDomain.GetAssemblies()
                //|> Seq.iter(fun asm ->
                //    asm
                //    |> Reflect.AllMethods
                //    |> Seq.filter(Reflect.WithAttr typeof<TestStarterAttribute>)
                //    |> Seq.iter(fun m -> 
                //        m.Invoke(null, [| config.SessionID |] ) |> ignore )
                //)
                return "later" //"OK"
            } //|> DBG.IndentA "Server.runServerTests"

        interface IRPCproxy with
            member val Config = config
            member __.ReadTest tname = readTest tname
            member __.SaveTest diff = saveTest diff
            member __.InitBrowserCapture(sid) = WinCapture.InitSession(sid)
            member __.CheckRect sid args = check_rect sid args
            member __.RunServerTests() = runServerTests()

    let CreateSrvProxy(config:IcuConfig) =
        if config.EnableServerTests 
        then Handler(config) :> IRPCproxy
        else RPC.Client.NullProxy(config) :> IRPCproxy

    let Init() =
        RPC.NewProxy <- CreateSrvProxy
