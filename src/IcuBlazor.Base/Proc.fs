namespace IcuBlazor

open System
//open System.Threading
open System.Threading.Tasks

module Proc =

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


    let AsTask a = a |> Async.StartAsTask

    let Retry numRetries asyncFunc =
        let rec attempt n = async {
            try 
                DBG.Log(SF "attempt(%A)" n)
                //DBG.Log($"attempt({n})")
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
    
    type Api(httpFactory:IHttpClientFactory, apiRoot:string) =

        let _http_instance = new HttpClient()
        let http() = 
            _http_instance
            //httpFactory.CreateClient("ICUapi") // occasional cache bug??

        let uri cmd = apiRoot + cmd

        let unwrap_request(request : Task<HttpResponseMessage>) = async {
            let! resp = request |> Async.AwaitTask 
            let! body = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
            if not resp.IsSuccessStatusCode then // propogate server error to client
                failwith(body)
            return body
        }

        let PostJsonAsync (url:string) (arg:obj) = async {
            let js = Conv.ToJson(arg)
            use c = new StringContent(js, Encoding.UTF8, "application/json")
            return! http().PostAsync(url, c) |> unwrap_request
        }

        let GetStringAsync (url:string) = async {
            return! http().GetAsync(url) |> unwrap_request
        }

        member __.Gethttp() = http()
        member __.GetAsync cmd = http().GetAsync (uri cmd)
        member __.GetString cmd = GetStringAsync (uri cmd)
        member __.PostJson cmd args = PostJsonAsync (uri cmd) args
