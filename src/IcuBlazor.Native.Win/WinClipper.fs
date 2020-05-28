namespace IcuBlazor.Native.Win

open System

type WinClipper(hwnd:IntPtr) =
    
    let is_valid() =
        //let s = Win.getScalingFactor(hwnd)
        let hr = Win.WindowRect(hwnd)
        (hr.Width > 0.0 && hr.Height > 0.0) //&& (s = 1.0) 

    let find_rendering_area_offset() =
        let bm = Win.CaptureWindow(hwnd, 0.0, 0.0, 100.0, 300.0)
        XBitmap.Save(@"C:\tmp\moo.png", bm)
        let matches = Array.create 4 0xff628319
        XBitmap.FindPixels(bm, matches)

    let offset = 
        if is_valid() 
        then find_rendering_area_offset()
        else None

    member this.Validate() =
        if (offset.IsNone)
        then None
        else Some this

    member __.Capture(x, y, w, h) = 
        let off = offset.Value
        Win.CaptureWindow(hwnd, x + off.X, y + off.Y, w, h)

    static member FromHwnd(hwnd) = WinClipper(hwnd).Validate()

    static member Find(title) =
        let ws = Win.FindWindowsWithText(title).ToArray()
        match ws |> Seq.tryPick WinClipper.FromHwnd with
        | Some clip -> clip
        | _ -> failwith "Can't find browser window"


module Editor =
    let OpenFile(file, line, col) =
        let num = Int32.Parse(line)
        ()

