namespace IcuBlazor

open System
open System.IO

open IcuBlazor
open IcuBlazor.Models


module WinCapture =
    open IcuBlazor.Native.Win

    let private find_clipper sid = async {
        try            
            return! 
                (async { return WinClipper.Find(sid) })
                |> Proc.Retry 3
                //|> DBG.IndentA "find_screen_area"
        with _ -> 
            return raise(
                new IcuException(
                    SF "Can't find browser window %A.\n" sid,
                    "1) Try refreshing this page.\n"))
    }

    let private FetchClipper = 
        find_clipper >> Async.RunSynchronously |> memoize

    let InitSession sid = async {
        let _ = FetchClipper(sid)
        return "Cool"
    }

    let Snapshot sid fileName (a:SnapshotArgs) = async {
        let clipper =  FetchClipper sid
        let img =  clipper.Capture(a.X, a.Y, a.W, a.H)
        XBitmap.Save(fileName, img)
        return img
    }

    let SnapAndCompare sid dir (a:SnapshotArgs) capture savediff = async {

        let new_fname = IcuIO.NewImageFile  dir a.Name
        let cur_fname = IcuIO.CurrImageFile dir a.Name

        if capture then
            do! Snapshot sid new_fname a |> Async.Ignore

        let new_img = XBitmap.Load(new_fname)
        let cur_img = XBitmap.Load(cur_fname)

        if (box cur_img = null) then 
            return -1
        else
            let diff_fname = IcuIO.DiffImageFile dir a.Name
            let fname = if savediff then diff_fname else ""
            return (XBitmap.ImageDiff new_img cur_img fname)
    }

