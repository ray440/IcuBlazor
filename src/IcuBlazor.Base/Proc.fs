namespace IcuBlazor

open System
//open System.Threading
open System.Threading.Tasks

module Proc =

    let SubscribeTag ev action =
        ev |> Observable.subscribe action //|> DisposeBin.Add tag

    type Subscribable<'a>() =
        let ev = new Event<'a>()
        let pubs = ev.Publish :> IObservable<'a>
        member __.Notify v = ev.Trigger v
        member __.Do (f:'a->unit) = SubscribeTag pubs f
        member __.DoAction (act:Action<'a>) = SubscribeTag pubs (fun x -> act.Invoke(x))
        member __.Await() = Async.AwaitEvent ev.Publish

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


    let AsTask a = a |> Async.StartAsTask

    let Retry numRetries asyncFunc =
        let rec attempt n = async {
            try 
                DBG.Log(SF "attempt(%A)" n)
                return! asyncFunc
            with e ->
                if n < numRetries then
                    do! Async.Sleep 1000
                    return! attempt (n+1)
                else
                    let msg = SF "Task failed after %d retries" n
                    return raise (new Exception(msg, e))
        } 
        attempt 1 


    //let WithTimeout timeout operation = async { 
    //    let! child = Async.StartChild (operation, timeout) 
    //    let! result = child 
    //    return Some result
    //}

    //// Sigh! Seems like we can't abort a Task/Async like a Thread
    //// - asyncF needs to know about cancel token
    ////   - but it doesn't 
    ////   - & won't help for hung tasks
    //let WithTimeout (timeout:int) asyncF = async {
    //    let tcs = new System.Threading.CancellationTokenSource()
    //    let task = Async.StartAsTask(asyncF, TaskCreationOptions.None, tcs.Token) :> Task
    //    let! winner = Task.WhenAny(task, Task.Delay(timeout)) |> Async.AwaitTask
    //    let ok = (winner = task)
    //    if not ok then
    //        tcs.Cancel(true) // does nothing !!!
    //    tcs.Dispose()
    //    return ok
    //}


    type Msg =
        | Queue of Async<unit>
        | Await of Async<unit>*AsyncReplyChannel<unit> 
        | Flush of AsyncReplyChannel<unit> 
        override this.ToString() = 
            match this with
            | Queue p      -> "Queue"
            | Await (p,ch) -> "Await "
            | Flush ch     -> "Flush"


    type Scheduler() =
        let handle mb msg = 
            async {
                match msg with
                | Queue p -> 
                    do! p
                | Await(p,ch) -> 
                    do! p
                    ch.Reply()
                | Flush ch -> 
                    ch.Reply()
            } // |> DBG.IndentA (SF "Sched msg=%A" msg)

        let agent = MailboxProcessor.Start <| fun mb -> async {
            while true do
                let! msg = mb.Receive()
                do! handle mb msg
        }

        member __.Queue f = agent.Post(Queue(async{ f() }))
        member __.QueueAsync a = agent.Post(Queue a)
        member __.Flush() = agent.PostAndAsyncReply(Flush)
        member __.Await p = agent.PostAndAsyncReply(fun ch -> Await(p,ch))
        

module Web =
    open System.Text
    open System.Net.Http
    
    // zzz Hack to inject Blazor HttpClient
    let mutable Http = Unchecked.defaultof<HttpClient>
    let mutable clientFactory = Unchecked.defaultof<IHttpClientFactory>
    let GetHttp() =
        if ENV.IsWasm
        then Http
        else clientFactory.CreateClient("ICUapi")

    let unwrap_request(request : Task<HttpResponseMessage>) = async {
        let! resp = request |> Async.AwaitTask 
        if not resp.IsSuccessStatusCode then // propogate server error to client
            let! body = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
            failwith(body)
        return! resp.Content.ReadAsStringAsync() |> Async.AwaitTask
    }
    let PostJsonAsync (url:string, arg:obj) = async {
        let js = Conv.ToJson(arg)
        use c = new StringContent(js, Encoding.UTF8, "application/json")
        return! GetHttp().PostAsync(url, c) |> unwrap_request
    }
    let GetStringAsync (url:string) = async {
        return! GetHttp().GetAsync(url) |> unwrap_request
    }

