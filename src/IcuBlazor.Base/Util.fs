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
    let inline Split (c:string) (text:string) = 
        text.Split([| c |], StringSplitOptions.None)

    let SplitAndTrim sep s =
        Split sep s
        |> Seq.map (fun s -> s.Trim())
        |> Seq.filter(fun l -> l.Length > 0)
        |> Seq.toArray

    let Join (sep:string) (strs:seq<string>) = String.Join(sep, strs)

    let Contains (part:string) (s:string) = s.Contains(part)

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

module Conv =
    open Newtonsoft.Json

    let ToJson(v) = JsonConvert.SerializeObject(v)
    let FromJson<'T>(js:string) = JsonConvert.DeserializeObject<'T>(js)



[<AutoOpen>]
module BaseUtils =

    let SF = sprintf
    
    let CopyDict (d:IDictionary<'K, 'V>) =
        if (box d) = null 
        then new Dictionary<'K, 'V>()
        else new Dictionary<'K, 'V>(d)

    // Create a thread-safe List<> or ResizeArray<>
    type CList<'T>() = 
        let _d = ResizeArray<'T>()
        let L f = lock _d f
        member __.List() = _d

        member __.Item
            with get i   = L (fun () -> _d.[i])
            and  set i v = L (fun () -> _d.[i] <- v)
        member __.Add(v) = L (fun () -> _d.Add v)
        member __.Remove(v) = L (fun () -> _d.Remove v)
        member __.Clear()= L (fun () -> _d.Clear())
        member __.Count  = L (fun () -> _d.Count)
        member __.Copy() = L (fun () -> _d |> Seq.toArray) // avoid collection modified exn.

    // Create a thread-safe dictionary for .Net
    type CDict<'K,'V when 'K:equality>() =
        let _d = Dictionary<'K,'V>()
        let L f = lock _d f
        member __.Dict() = _d

        abstract Get : 'K -> 'V
        abstract Set : 'K * 'V -> unit
        override __.Get k     = L (fun () -> _d.[k])
        override __.Set(k, v) = L (fun () -> _d.[k] <- v)

        member this.Item
            with get k   =   L (fun () -> this.Get k)
            and  set k v =   L (fun () -> this.Set(k, v))
        member __.Remove(k:'K) = L (fun () -> _d.Remove k)
        member __.Clear() =  L (fun () -> _d.Clear())
        member __.Count =    L (fun () -> _d.Count)
        member __.ContainsKey k = 
            L (fun () -> (_d.ContainsKey k = true))
        member __.Find k =   
            L (fun () -> if _d.ContainsKey k then Some(_d.[k]) else None)

    type LRUCache<'K,'V when 'K:equality>(limit:int) =
        inherit CDict<'K,'V>()

        let usage = LinkedList<'K>()

        let move_to_front(k:'K) =
            usage.Remove(k) |> ignore
            usage.AddFirst(k) |> ignore
            
        let remove_last(this:CDict<'K,'V>) =
            let k = usage.Last.Value
            usage.RemoveLast()
            this.Remove k |> ignore

        let prune this count =
            for i in 0..count do 
                remove_last this

        member val Usage = usage
        override this.Set(k, v) =
            if this.Count >= limit then
                prune this (limit*2/10) // remove last 20%
                move_to_front(k) // may have been removed
            base.Set(k, v)
        override __.Get(k:'K) =
            move_to_front k
            base.Get(k)
            
    /// Memoize the given function using the given dictionary and make_key
    let memoizeWith (d:CDict<'K,'V>) make_key f =
        fun x ->
            lock d <| fun () ->
                let k = make_key x
                if not(d.ContainsKey k) then 
                    d.[k] <- f x // ok, but note recursive calls can call f(x) twice
                d.[k]

    /// Memoize the given function.
    let memoize f = memoizeWith (CDict<_,_>()) id f

    type DVal(calc) =
        let mutable d = true
        member __.IsDirty() = d <- true
        member __.Refresh() =
            if d then
                calc()
                d <- false

    type Counter(tag) =
        let mutable c = 11000
        member __.Next() = lock tag <| fun () -> c <- c+1; c

    let instCounter = new Counter("inst")
    let PrefixedID pre = pre + (instCounter.Next().ToString())


module SlashPath = 
    let FromString = 
        let toArray path = 
            path |> Str.Split "/" |> Array.filter(fun p -> p.Length > 0)
        memoize toArray

    let Make (root:string) (node:string) = 
        if root.EndsWith("/")
        then root + node
        else SF "%s/%s" root node


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
