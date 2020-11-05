namespace IcuBlazor

open System
open System.IO
open IcuBlazor
open IcuBlazor.Models

module WWWRoot =
    open Microsoft.Extensions.FileProviders
    open Microsoft.AspNetCore.Hosting

    // The default wwwroot is different if run as CSB or SSB.
    // And we need a consistent wwwroot for saving test data.

    let extract_root (fi:IFileInfo) (file:string) = 
        if (fi.Exists) then
            let path = fi.PhysicalPath
            let wwwroot,_ = path |> Str.SplitAt (path.LastIndexOf(file))
            ENV.wwwroot <- wwwroot
            //DBG.Info(SF "IcuBlazor wwwroot: %A" wwwroot)
            DBG.Info($"IcuBlazor wwwroot: %A{wwwroot}")

    let FromFile (env:IWebHostEnvironment) file =
        let fi = env.WebRootFileProvider.GetFileInfo file
        extract_root fi file

    let FromDir (env:IWebHostEnvironment) (testdir) =
        let cs = env.WebRootFileProvider.GetDirectoryContents("")
        let fo = cs |> Seq.tryFind(fun f -> f.IsDirectory && f.Name.Equals(testdir))
        match fo with
        | Some fi -> extract_root fi testdir
        | _ -> () // delay error msg until browser is ready (see ServerRpc.Handler.dir)

    let FromConfig (env:IWebHostEnvironment) (config:IcuConfig) =
        if (Str.isEmpty ENV.wwwroot) then
            FromFile env "index.html" // by default place `icu_data/` where index.html is found
            if (Str.isEmpty ENV.wwwroot) then
                FromDir env config.TestDir


module ServerRpc =

    type Handler(config:IcuConfig) =
        let dir = 
            if Str.isEmpty ENV.wwwroot then
                failwithf "Cannot find TestDir='wwwroot/%s'.  It must be created manually." config.TestDir
            let d = Path.Combine(ENV.wwwroot, config.TestDir)
            DBG.Info(" ----")
            DBG.Info(SF "Test data dir = %A" d)
            if not(Directory.Exists(d)) then
                Directory.CreateDirectory(d) |> ignore
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

        let saveLogTest (diff:DiffFileAssert) = async {
            IcuIO.EnsureDirExists dir diff.Name
            let f = IcuIO.TestToFile dir diff.Name
            return! File.WriteAllTextAsync(f, diff.Result) |> Async.AwaitTask
        }

        let saveImageTest (diff:DiffImageAssert) = async {
            GC.Collect()
            let tname = diff.Name
            IcuIO.EnsureDirExists dir tname
            let cur_fname = IcuIO.CurrImageFile dir tname
            let new_fname = IcuIO.NewImageFile  dir tname
            File.Copy(new_fname, cur_fname, true)
        }

        let check_rect sid (args:SnapshotArgs) = async {         
            IcuIO.EnsureDirExists dir args.Name
            let savediff = config.CanSaveTestData // if from CLI or readonly mode
            let capture = args.Local && savediff  // if on local machine && savediff
            return! WinCapture.SnapAndCompare sid dir args capture savediff
        }
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

        interface RPC.IProxy with
            member val Config = config
            member __.ReadTest tname = readTest tname
            member __.SaveFileTest diff = saveLogTest diff
            member __.SaveImageTest diff = saveImageTest diff
            member __.InitBrowserCapture(sid) = WinCapture.InitSession(sid)
            member __.CheckRect sid args = check_rect sid args
            member __.RunServerTests() = runServerTests()

    let CreateSrvProxy(config:IcuConfig) =
        if config.EnableServer 
        then Handler(config) :> RPC.IProxy
        else RPC.NullProxy(config) :> RPC.IProxy

