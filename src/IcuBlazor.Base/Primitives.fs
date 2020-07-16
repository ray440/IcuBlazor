namespace IcuBlazor

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.Core

module ENV =

    //let mutable IsWasm = RuntimeInformation.IsOSPlatform(OSPlatform.Create("WEBASSEMBLY"))
    let mutable IsWasm = (Type.GetType("Mono.Runtime") |> box <> null)
    let mutable IsIcuEnabled = false
    let mutable Browser = ""
    let mutable wwwroot = ""
    let mutable IsLocalhost = true

module Logger =

    let mutable sys = "[] "

    let Out(s:string) = 
        //System.Diagnostics.Debug.WriteLine(s)
        System.Console.WriteLine(s)

    let private indent_strings =
        let d = new Dictionary<_,_>()
        fun k ->
            match d.TryGetValue k with
            | true, v -> v
            | _  ->
                d.[k] <- String.replicate k " "
                d.[k]

    let inline now() = DateTime.Now.Ticks
    let default_log =
        if ENV.IsWasm then
            fun str -> Out(sys + str)
            //let mutable t0 = now()
            //fun str ->
            //    let ts = sprintf "%6d" ((now()-t0)/10000L)
            //    Out(ts + sys + str)
            //    t0 <- now()
        else 
            fun str ->
                let tid = Threading.Thread.CurrentThread.ManagedThreadId
                Out(sprintf "%3d%s%s" tid sys str)

    type Indenter(logger: string -> unit) =
        let mutable in_num = 0;
        let mutable in_str = ""
    
        let log str = logger(in_str + str)

        let set_indent i = 
            in_num <- max 0 i
            in_str <- indent_strings in_num
        let set_inc inc = set_indent (in_num + inc)

        member __.Reset() =
            set_indent 0

        member __.Indent(inc, title) =
            if (inc > 0) then log(title)
            set_inc inc
            if (inc < 0) then log(title)

        member __.Log s = log (s)

module DBG =
    open Microsoft.Extensions.Logging

    let mutable Verbosity = LogLevel.Information

    let private _ind = new Logger.Indenter(Logger.default_log)

    let SetSystem sys = 
        Logger.sys <- sprintf "[%s] " sys
        _ind.Indent(0, "")

    let Indent(inc, title) = 
        if Verbosity <= LogLevel.Information then _ind.Indent(inc, title)
    let private levelLog level s = 
        if Verbosity <= level then _ind.Log s

    let Log s  = levelLog LogLevel.Trace s
    let Info s = levelLog LogLevel.Information s 
    let Err s  = levelLog LogLevel.Error s 

    let IndentF title f = 
        Indent(3, title + " {")
        let x = f()
        Indent(-3, "}")
        x
    let IndentFR title f = 
        Indent(3, title + " { ")
        let x = f()
        Indent(-3, sprintf "} => %A " x)
        x
    let IndentA title f = async {  
        Indent(3, title + " {")
        let! x = f
        Indent(-3, "}")
        return x
    }

    //open System.Threading.Tasks

    //let IndentAct title (f:Action) = // for c#
    //    Indent(3, title + " {")
    //    f.Invoke()
    //    Indent(-3, "}")
        
    //let IndentTask title (f:Task) = 
    //    async {
    //        let iid = Logger.PrefixedID ".."
    //        Indent(3, title + " { "+iid)
    //        do! f |> Async.AwaitTask
    //        Indent(-3, "} "+iid)
    //    } |> Async.StartAsTask :> Task

    //let IndentT title f = 
    //    async {  
    //        let iid = Logger.PrefixedID ".."
    //        Indent(3, title + " { "+iid)
    //        let! x = f |> Async.AwaitTask
    //        Indent(-3, "} "+iid)
    //        return x
    //    } |> Async.StartAsTask


module Seq =
    let inline exclude f (s:seq<'a>) = s |> Seq.filter (f >> not)

module Str =

    let isOk s = not(String.IsNullOrWhiteSpace(s))

    let inline Split (c:string) (text:string) = 
        text.Split([| c |], StringSplitOptions.None)

    let SplitAndTrim sep s =
        Split sep s
        |> Seq.map (fun s -> s.Trim())
        |> Seq.filter(fun l -> l.Length > 0)
        |> Seq.toArray

    let Join (sep:string) (strs:seq<string>) = String.Join(sep, strs)

    let Contains (part:string) (s:string) = s.Contains(part)

    let inline SplitAt i (s: string) =
        if (i < 0) then (s,"") else (s.Substring(0,i), s.Substring(i+1))
    let SplitAtFirst (sep:char) s = s |> SplitAt (s.IndexOf(sep))
    let SplitAtLast  (sep:char) s = s |> SplitAt (s.LastIndexOf(sep))

    let FirstPart (sep:char) (s: string) =
        let i = s.IndexOf(sep)
        if (i < 0) then s else s.Substring(0,i)
    let LastPart (sep:char) (s: string) =
        let i = s.LastIndexOf(sep)
        if (i < 0) then s else s.Substring(i+1)

    // Converts (somewhat intelligently) a string to an array of words
    // - handles camelCase, snake_case, slug-case. 
    // - doesn't have to be perfect, just a suggestion
    type CharType = Lower | Upper | Digit | Wild | WhiteSpace
    let AsWords (text:string) = 
        let char_type c =
            if Char.IsLower c then Lower
            elif Char.IsUpper c then Upper
            elif Char.IsDigit c then Digit
            //elif Char.IsWhiteSpace c then WhiteSpace
            else Wild
        let canBreak c0 c1 =
            let a = char_type c0
            let b = char_type c1
            a <> b        // same char type. Group num & uppers together
            && a <> Upper // not already capitalized
            && a <> Wild  // wildcard match
            && b <> Wild 

        let s = // handle dot/slug/snake Case
            text.Split([| " "; "."; "_"; "-" |], StringSplitOptions.None)
            |> String.concat " "

        let buf = ResizeArray()
        s |> Seq.iteri(fun i c -> // for camelCase insert spaces on "breaks"
            if i > 0 && (canBreak s.[i-1] s.[i]) then 
                buf.Add(' ')
            buf.Add(c)
            )        
        (new string(buf.ToArray())) |> SplitAndTrim " "

    let Cap c = c.ToString().ToUpper()
    let Capitalize s = 
        if String.IsNullOrEmpty s then s else Cap(s.[0]) + s.Substring(1)    
    let Entitle(s:string) = 
        // heuristic: don't capitalize small words (e.g. a, of, in, ...)
        if s.Length < 3 then s else Capitalize s
 
    let TitleCase s = 
        let t = AsWords s |> Seq.map Entitle |> String.concat " "
        Capitalize t  // first word may NOT be capitalized (e.g. "an_egg")

    //let private invalid_chars = Path.GetInvalidFileNameChars()
    let private invalid_chars = [|
      '"'; '<'; '>'; '|'; '/'; ':'; '*'; '?'; '\b';
      |]
    let isValidChar(c:char) = 
        (c > '\031') && not(Array.contains c invalid_chars)
    let IsValidTestName name =
        (not(String.IsNullOrWhiteSpace name))
        && (name |> Seq.forall isValidChar) // IndexOfAny(invalid_chars) < 0)

