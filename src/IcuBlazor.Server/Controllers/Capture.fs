namespace IcuBlazor

open System
open System.IO

open IcuBlazor


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

    let MakeImageDiff dir tname =
        let new_fname = IcuIO.NewImageFile  dir tname
        let cur_fname = IcuIO.CurrImageFile dir tname
        let new_img = XBitmap.Load(new_fname)
        let cur_img = XBitmap.Load(cur_fname)

        if (box cur_img = null) then
            -1
        else
            let img, diff = XBitmap.ImageDiff(new_img, cur_img)
            if (diff > 0) then
                let diff_fname = IcuIO.DiffImageFile dir tname
                XBitmap.Save(diff_fname, img)
            diff

    let SnapAndCompare sid dir (a:SnapshotArgs) = async {
        let new_fname = IcuIO.NewImageFile dir a.Name
        let! _img = Snapshot sid new_fname a
        return MakeImageDiff dir a.Name
    }

