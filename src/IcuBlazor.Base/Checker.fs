namespace IcuBlazor

open System
open System.Diagnostics
open System.Runtime.InteropServices

open IcuBlazor.Models
open IcuBlazor.IcuCore

// Checker is a facade over TestChecker
type Checker internal (tc:TestChecker, config:IcuConfig) =

    static let help_Text =
        "1) Consider using Check.TextFile() instead.\n" +
        "2) Raise the limit with Check.Text(..., limit=1000).\n"
    static let help_Log =
        "1) Break test into smaller Check.TextFile() tests.\n" +
        "2) Raise the limit with Check.TextFile(..., limit=4000).\n"
    static let help_equal =
        "1) Try converting objects to a string or json.\n" +
        "2) Consider using Check.Text() or Check.TextFile()"
        
    [<DebuggerNonUserCode>]
    let equal same (a:'T) (b:'T) (hdr) =
        let cond = 
            let typ = typeof<'T>
            if (typ.IsPrimitive) then
                a.Equals(b)
            elif (typ = typeof<string>) then
                String.Equals(a,b)
            else
                new IcuException(
                    SF "Check.Equal()/NotEqual() don't work on general objects",
                    help_equal)
                |> raise

        let result = if same then cond else not(cond)
        let opStr = if cond then "==" else "!="
        let msg = SF "(%A %s %A) %s" a opStr b hdr
        let msg = if (msg.Length < 60) then msg else hdr
        tc.IsTrue result msg

    let skip_disabled name = 
        tc.Skip (name + ": Test disabled (EnableServer=false)")

    let check_text_length (text:string) limit help =
        if (box text <> null) then
            let len = text.Length
            if len > limit then
                (SF "Text exceeds limit (%A > %A)" len limit, help)
                |> IcuException |> raise

    let check_filename fname =        
        //if not(Str.IsValidTestName(fname)) then
        if (String.IsNullOrWhiteSpace(fname)) then
            failwithf "Invalid TestName '%s'" fname

    let check_SnapshotArgs (args:SnapshotArgs) =
        if args.W <= 0.0 then
            failwithf "Invalid Snapshot width=%A" args.W
        if args.H <= 0.0 then
            failwithf "Invalid Snapshot height=%A" args.H
        check_filename args.Name

    let check_file(logName, result, message, limit:int) =
        check_text_length result limit help_Log
        check_filename logName
        if not config.EnableServer 
        then skip_disabled logName
        else tc.CheckFile logName result message 
    


    member val internal Logger = tc.Logger

    member __.MakeCheckpoint = tc.MakeCheckpoint Assert Outcome.Unknown

    /// <summary>
    /// Displays `message` within test results.
    /// </summary>
    member __.Info message = tc.Logger.Log message

    /// <summary>
    /// Displays a custom CheckPoint.
    /// </summary>
    member __.Show(title, 
        [<Optional; DefaultParameterValue(null)>] dataModel:obj, 
        [<Optional; DefaultParameterValue(Outcome.Logging)>] outcome:Outcome) =
        tc.Show outcome title dataModel

    [<DebuggerNonUserCode>]
    /// <summary>
    /// Check that `cond` is true.
    /// </summary>
    /// <param name="cond">true/false condition.</param>
    /// <param name="message">Description of test condition.</param>
    member __.True state message = tc.IsTrue state message

    [<DebuggerNonUserCode>]
    /// <summary>
    /// Check that `cond` is true.
    /// </summary>
    /// <param name="cond">true/false condition.</param>
    /// <param name="message">Description of test condition.</param>
    member __.False state message = tc.IsTrue (not state) message

    [<DebuggerNonUserCode>]
    /// <summary>
    /// Declare a failed test.
    /// </summary>
    /// <param name="message">Description of test condition.</param>
    member __.Fail message = tc.IsTrue false message

    [<DebuggerNonUserCode>]
    /// <summary>
    /// Check if two primitive values (expected & actual) are equal.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual runtime value.</param>
    /// <param name="message">Description of test.</param>
    member __.Equal expected actual message = equal true expected actual message

    [<DebuggerNonUserCode>]
    /// <summary>
    /// Check if two primitive values (expected & actual) are NOT equal.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual runtime value.</param>
    /// <param name="message">Description of test.</param>
    member __.NotEqual expected actual message = equal false expected actual message

    /// <summary>
    /// Skip a test.
    /// </summary>
    /// <param name="message">Reason for skipping a test.</param>
    member __.Skip message = tc.Skip message

    /// <summary>
    /// Check that two lengthy strings are the same.
    /// </summary>
    /// <param name="expected">The expected string.</param>
    /// <param name="actual">The actual runtime string.</param>
    /// <param name="message">Description of expected text content.</param>
    member __.Text(expect, result, message, 
        [<Optional; DefaultParameterValue(800)>] limit:int) =
        check_text_length result limit help_Text
        tc.CheckText expect result message

    /// <summary>
    /// Check that a string `result` is the same as a the last string 
    /// saved in `{logName}.txt`.
    /// </summary>
    /// <param name="logName">Unique test name of file where text is stored.</param>
    /// <param name="result">The actual runtime string.</param>
    /// <param name="message">Description of expected content.</param>
    member __.TextFile(logName, result, message,
        [<Optional; DefaultParameterValue(3000)>] limit:int) = 
        check_file(logName, result, message, limit)

    /// <summary>
    /// Check that an image is the same as a the last saved image.
    /// </summary>
    member __.Snapshot args = // not called directly by user
        async {
            check_SnapshotArgs args
            if not config.EnableServer 
            then skip_disabled args.Name
            else do! tc.Snapshot config.SessionID args
        } |> Async.StartAsTask


