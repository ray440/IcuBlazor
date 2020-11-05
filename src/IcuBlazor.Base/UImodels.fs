namespace IcuBlazor

open System
open System.Collections.Generic
open Microsoft.FSharp.Core

module DiffService =
    open DiffPlex
    open System.Text
    
    let diffBuilder = Differ() |> DiffBuilder.SideBySideDiffBuilder

    let GetDiffs oldText newText =
        diffBuilder.BuildDiffModel(oldText, newText, false)

    let WSdiffs a b =
        // Some white space diffs can't be seen on GUI. Detect and notify.
        let stripws (s:string) = 
            s |> Seq.exclude Char.IsWhiteSpace |> Seq.toArray |> String
        let strip_equal() = 
            String.Equals(stripws a, stripws b)

        let same = String.Equals(a,b)
        let wsdiff = (not same) && strip_equal()
        (same, wsdiff)

    let WSescape s =
        let sb = new StringBuilder()
        for c in s do
            match c with
            | '\r' -> sb.Append("\\r")
            | '\n' -> sb.Append("\\n\n")
            | '\t' -> sb.Append("\\t")
            | _ -> sb.Append(c)
            |> ignore
        sb.ToString()
        
module Lorem =
    open System.Text

    let lorem = "Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. ";
    let len = lorem.Length

    let OfLength(n:int) = 
        let sb = new StringBuilder(n)
        for i in 1..(n/len) do
            sb.Append(lorem) |> ignore
        sb.Append(lorem.Substring(0, n % len)) |> ignore
        sb.ToString()


module BlazorUI =

    let private get_attr (attrs:IDictionary<string, obj>) n = 
        if attrs.ContainsKey(n) then attrs.[n].ToString() else ""

    let private with_class attrs extraClass = 
        let n = "class"
        let v = get_attr attrs n
        attrs.[n] <- extraClass + " " + v

    let private with_style attrs extraStyle = 
        let n = "style"
        let v = get_attr attrs n
        attrs.[n] <- v + ";" + extraStyle
    
    let AddAttrs dict extraClass extraStyle = 
        let attrs = BaseUtils.CopyDict(dict)
        with_class attrs extraClass
        with_style attrs extraStyle
        attrs

    let private find_type : (Type*string) -> Option<Type> = 
        let rec findr (t:Type, suffix:string) =
            // Find matching type(+suffix). If not found move up type hierarchy.
            match Reflect.FindClass(t.Name+suffix) with
            | Some typ -> Some typ
            | _ -> 
                match t.BaseType with
                | null -> None
                | _ as bt -> findr(bt, suffix)

        memoize findr
       
    let FindTypeWithSuffix model suffix defType =
        match model with
        | null -> defType
        | _ -> 
            match find_type(model.GetType(),suffix) with
            | Some t -> t
            | _ -> defType


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
        let _id = Guid.NewGuid().ToString()

        member val ID = _id
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

