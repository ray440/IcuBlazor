namespace IcuBlazor.Native.Win

open System
open System.Collections.Generic
open System.Drawing
open System.Runtime.InteropServices
open System.Text
open System.Windows
open System.Windows.Interop
open System.Windows.Media.Imaging

module internal NativeMethods =

    [<StructLayout(LayoutKind.Sequential)>]
    type RECT =
        struct
            val Left: int
            val Top: int
            val Right: int
            val Bottom: int
            new(l, t, r, b) = { Left=l; Top=t; Right=r; Bottom=b }
            member r.Height = r.Bottom - r.Top
            member r.Width = r.Right - r.Left
        end

    type ShowCmd =
        | SHOW = 5
        | RESTORE = 9

    type DeviceCap =
        | VERTRES = 10
        | DESKTOPVERTRES = 117

    type EnumWindowsProc = delegate of IntPtr * IntPtr -> bool

    [<DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern int ShowWindow(IntPtr hWnd, int cmd)

    [<DllImport("gdi32.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern int GetDeviceCaps(IntPtr hdc, int nIndex)

    [<DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl, CharSet=CharSet.Ansi, SetLastError=true, ExactSpelling=true)>]
    extern bool SetForegroundWindow(IntPtr hwnd)

    [<DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl, CharSet=CharSet.Ansi, SetLastError=true, ExactSpelling=true)>]
    extern bool IsIconic(IntPtr hWnd)

    [<DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)>]
    extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount)

    [<DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)>]
    extern int GetWindowTextLength(IntPtr hWnd)

    [<DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam)

    [<DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern bool GetWindowRect(IntPtr hwnd, RECT& rectangle)


module internal Win =
    open NativeMethods

    let EmptyPoint = new Point(0.0, 0.0)

    let WindowRect(hwnd) =
        let mutable r = new RECT()
        GetWindowRect(hwnd, &r) |> ignore
        if (r.Width=0 || r.Height=0) then
            failwith "Invalid window"
        new Rect(float r.Left, float r.Top, float r.Width, float r.Height)
    
    let BringToFront hwnd =
        if (IsIconic(hwnd)) then
            ShowWindow(hwnd, (int)ShowCmd.RESTORE) |> ignore
            ShowWindow(hwnd, (int)ShowCmd.SHOW) |> ignore
        SetForegroundWindow(hwnd)

    let FindWindows(filter) =
        let windows = new List<IntPtr>()
        let callback = new EnumWindowsProc(fun wnd param ->
            if (filter(wnd, param)) then
                windows.Add(wnd)
            true)
        EnumWindows(callback, IntPtr.Zero) |> ignore
        windows

    let GetWindowWithText(hWnd) =
        let size = GetWindowTextLength(hWnd)
        if (size = 0) then
            ""
        else 
            let builder = new StringBuilder(size + 1)
            GetWindowText(hWnd, builder, builder.Capacity) |> ignore
            builder.ToString()

    let FindWindowsWithText(titleText:string) =
        FindWindows(fun(wnd, param) ->
            let t = GetWindowWithText(wnd)
            t.Contains(titleText)
        )

    let CaptureScreenRect(r:Rect) =
        let (x, y, w, h) = (int r.X, int r.Y, int r.Width, int r.Height)
        use image = new Bitmap(w,h)
        use gr = Graphics.FromImage(image)
        gr.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h))            
        Imaging.CreateBitmapSourceFromHBitmap(
            image.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions())

    let CaptureWindow(hwnd, x, y, w, h) =
        BringToFront(hwnd) |> ignore
        let wr = WindowRect(hwnd)
        let sc = new Rect(x + wr.X, y + wr.Y, w, h)
        CaptureScreenRect(sc)


