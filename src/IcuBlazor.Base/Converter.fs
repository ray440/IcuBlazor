namespace IcuBlazor

open System
open System.Collections.Generic
open Microsoft.FSharp.Core

module Conv =
    open System.Text.Json
    open System.Runtime.InteropServices

    let private IndentedOpts =
        let opts = new JsonSerializerOptions()
        opts.WriteIndented <- true
        opts

    let FromJson<'T>(js:string) = JsonSerializer.Deserialize<'T>(js)
    let ToJson(v) = JsonSerializer.Serialize(v)

    [<Serializable>]
    type DynObj = Dictionary<string, obj>  

    let private is_primitive m =
        if (m = null) then true 
        else
            let tm = m.GetType()
            (tm.IsPrimitive || tm = typeof<string> || tm.IsEnum)

    let private as_array (m:obj) =  // only if array
        match m with
        | :? DynObj -> None // IEnumerable but treat as MObject
        | :? System.Collections.IEnumerable as e -> Seq.cast<obj> e |> Some
        | _ -> None

    let (|MArray|MObject|MSimple|) v = 
        if is_primitive v then 
            MSimple v
        else
            match as_array v with
            | Some arr -> MArray arr
            | _ -> MObject v


    // Makes a (more) human readable JSON string for editting or debugging
    // - should separate toDyn from formatting
    //   - ToDyn handles recusrion, $refs, qvars, custom serialization,  etc...
    //   - ToJson x = x |> ToDyn config |> FormattedJson indent
    // - Uses Hint to order fields
    // - When indent=0
    //   - Use {"field1":value, "field2":value, …}
    //	 - Not {"field1": value,"field2": value, …}
    // - When indent=-1, remove all unnecessary spaces
    //	 - e.g.{"field1":value,"field2":value,…}
    // - When indent>0, (default=2)
    //   - compact (sub)object if it's json string.Length < 80
    //   - indent ONLY IF the (sub)object cannot be compacted
    let rec private formatted_json max_depth indent path level m = 
        let buf = new System.Text.StringBuilder()
        let out (s:string) = buf.Append(s) |> ignore
        let mutable isArray = false // mutable OPT

        let out_compact es =
            let sep = if isArray || indent < 0 then "," else ", "
            es |> Seq.map (fun(f,s) -> sprintf "%s%s" f s) |> String.concat sep

        let out_expand es = // longform output of object/array elements
            let nl, sep, inStr0, inStr1 = 
                if indent <= 0 then "", ", ", "", ""
                else
                    "\n", ",\n",
                    Logger.IndentStrings (indent*level),
                    Logger.IndentStrings (indent*(level+1))
            let sep = if indent < 0 then "," else sep
            let gap = if isArray || indent <= 0 then "" else " "
            let content = 
                es 
                |> Seq.map (fun(f,v) -> inStr1+f+gap+v) 
                |> String.concat sep

            out(nl)
            out(content)
            // https://stackoverflow.com/questions/201782/can-you-use-a-trailing-comma-in-a-json-object
            out(",") // zzz this is not valid JSON, but it's better for editing
            out(nl+inStr0) 

        let fmt_fvar (f,v) = 
            (if isArray then "" else sprintf "\"%s\":" f), 
            (formatted_json max_depth indent (path+"."+f) (level+1) v)

        let fmt_list fvs =
            let es = fvs |> Seq.map fmt_fvar
            let try_compact = fvs |> Seq.forall (fun (_,v) -> is_primitive v)
            if try_compact then
                let flat_js = out_compact es
                if (flat_js.Length < 80) 
                then out flat_js
                else out_expand es
            else
                out_expand es

        let format fvs =
            let b0, b1 = if isArray then ("[","]") else ("{","}")
            out(b0)
            fmt_list fvs
            out(b1)
        
        let fmt_array a =
            isArray <- true
            a |> Seq.map(fun v -> "", v) |> format
        let fmt_object m = 
            isArray <- false
            m |> Reflect.FieldValues |> format
        
        //DBG.IndentF ("fmt_json "+path) <| fun ()->
        match m with
        | MSimple v -> 
            (if (v :? Enum) then v.ToString() :> obj else v)
            |> ToJson |> out
        | MArray  a ->             
            if (level > max_depth) then out("[...]") else fmt_array a
        | MObject m -> 
            if (level > max_depth) then out("{...}") else fmt_object m

        buf.ToString()
    
    /// Convert an object to a nicely formatted, mostly JSON, string
    /// Formats nulls and enums.
    /// Handles recursion by limiting max_depth.
    let AsString
        (m,
         [<Optional; DefaultParameterValue(2)>] indent:int,
         [<Optional; DefaultParameterValue(1)>] max_depth:int) =
        match m with
        | MSimple _ -> if (m = null) then "<null>" else m.ToString()
        | MArray  _ -> formatted_json max_depth indent "" 0 m
        | MObject m -> formatted_json max_depth indent "" 0 m

