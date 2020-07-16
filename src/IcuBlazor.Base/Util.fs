namespace IcuBlazor

open System
open System.Collections.Generic
open Microsoft.FSharp.Core

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

    // allows you to store multiple items for one key
    type MultiDict<'K, 'V when 'K:equality>() = 
        // NOTE: different layers of locking are required
        // - CDict is locked on internal Dict _d._d = Dictionary<>
        // - for consistency make sure we lock on _d._d
        let _d = CDict<'K, CList<'V>>()
        let _dl = _d.Dict() // lock on CDict internal storage Dictionary<>
        let sub_list = memoizeWith _d id (fun _k -> CList<'V>())
        let get_list k = lock _dl (fun() -> sub_list k)

        member val Dict = _d.Dict
        member __.Item k = _d.[k]
        member __.GetList k = get_list k
        member __.RemoveList k = _d.Remove(k)
        member __.ContainsList k = _d.ContainsKey k
        member __.Add k v = (get_list k).Add(v)
        member __.RemoveItem k v = (get_list k).Remove(v)
        member __.Clear() = _d.Clear()
        member __.Count() = _d.Count


    type Counter(tag) =
        let mutable c = 11000
        member __.Next() = lock tag <| fun () -> c <- c+1; c

    let instCounter = new Counter("inst")
    let PrefixedID pre = pre + (instCounter.Next().ToString())

//[<AutoOpen>]
//module Obs =

    type Subscribable<'a>() =
        let ev = new Event<'a>()
        let obs = ev.Publish :> IObservable<'a>
        member __.Notify v = ev.Trigger v
        member __.Await() = Async.AwaitEvent ev.Publish
        member __.Do (f:'a->unit) = 
            obs |> Observable.subscribe f
        member __.DoAction (act:Action<'a>) = 
            obs |> Observable.subscribe (fun x -> act.Invoke(x))

    type Observer<'a>(initValue:'a) =
        let mutable v = initValue
        let subs = new Subscribable<'a>() // does not hold a value

        member val Subscribable = subs
        member __.Subscribe action = subs.Do action
        member __.Value
            with get() = v
            and  set x =
                if not(Unchecked.equals v x) then
                    v <- x
                    subs.Notify v 

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

module Conv =
    open Newtonsoft.Json

    let ToJson(v) = JsonConvert.SerializeObject(v)
    let FromJson<'T>(js:string) = JsonConvert.DeserializeObject<'T>(js)

module DiffService =
    open DiffPlex
    
    let diffBuilder = Differ() |> DiffBuilder.SideBySideDiffBuilder

    let GetDiffs(oldText, newText) =
        diffBuilder.BuildDiffModel(oldText, newText, false)


module SlashPath = 
    let FromString = 
        let toArray path = 
            path |> Str.Split "/" |> Array.filter(fun p -> p.Length > 0)
        memoize toArray

    let Make (root:string) (node:string) = 
        if root.EndsWith("/")
        then root + node
        else SF "%s/%s" root node

module Tree =

    type Base(parent:Option<Base>, name) =
        let path = 
            let root = if parent.IsSome then parent.Value.Path else ""
            SlashPath.Make root name
        let onChange = new Subscribable<obj>()
    
        member val Path = path
        member val Name = name
        member val OnChange = onChange
        member val Open = false with get, set
        member g.ToggleOpen() = g.Open <- not g.Open


    type Node<'Kid>(parent, name) =
        inherit Base(parent, name)

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

    let rec DepthFirstWalk(root:Node<_>) = seq {
        for s in root.Kids.List() do
            yield! (DepthFirstWalk s)
        yield box root
    }

