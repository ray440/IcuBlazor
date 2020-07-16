namespace IcuBlazor.Native.Win

open System
open System.IO
open System.Security.Permissions
open System.Windows
open System.Windows.Media
open System.Windows.Media.Imaging

module private XBM =

    [<PermissionSet(SecurityAction.Assert, Name = "FullTrust")>] // FxCop: LinkDemand on BitsPerPixel
    let Stride(image:BitmapSource) =  
        // perhaps only true for RGBA type bitmaps
        image.PixelWidth * ((image.Format.BitsPerPixel + 7) / 8)

    let Create(img:BitmapSource, w, h, pixels) =  
        BitmapSource.Create(
            w, h, 96., 96., img.Format, img.Palette, pixels, Stride(img))

    // Optimized Bitmap Reader 
    // - can read past image width & height
    //   - useful for image diffs
    type XBitmapReader(img:BitmapSource) = 

        let mutable row = -1
        let mutable rect = new Int32Rect(0, row, 0, 1)
        let stride = Stride(img)
        let pix_width = stride/4
        let pix_height = img.PixelHeight
        let bufferRow = Array.zeroCreate<int32> pix_width
        let emptyRow = Array.zeroCreate<int32> pix_width
        let mutable currRow = emptyRow

        let get_row(r) =
            row <- r
            rect.Y <- r
            if (r < pix_height) then
                img.CopyPixels(rect, bufferRow, stride, 0)
                currRow <- bufferRow
            else
                currRow <- emptyRow
            currRow

        member __.GetRow(r) =
            if (row <> r) then (get_row r) else currRow

        member __.GetPixel(r, c) =
            if (row <> r) then (get_row(r) |> ignore)
            if (c < pix_width) then (currRow.[c]) else (int32 0)

    let FromArgb(argb: int32) =
        let b = byte((argb)        &&& 0xFF)
        let g = byte((argb >>>  8) &&& 0xFF)
        let r = byte((argb >>> 16) &&& 0xFF)
        let a = byte((argb >>> 24) &&& 0xFF)
        Color.FromArgb(a, r, g, b)
    

    let inline far (a:byte) (b:byte) =
        (a <> b)

    let inline diff_hue (a:byte) (b:byte) = 
        if   a < b then 0x8F
        elif a > b then 0x4F
        else            0xFF

    let inline diff_color (s:Color) (t:Color) = 
        let cr = (diff_hue s.R t.R) <<< 16
        let cg = (diff_hue s.G t.G) <<< 8
        let cb = (diff_hue s.B t.B)
        0xff000000 ||| cr ||| cg ||| cb

    let diff_by_pixel(img0:BitmapSource, img1:BitmapSource) =
        
        let maxw = Math.Max(img0.PixelWidth, img1.PixelWidth)
        let maxh = Math.Max(img0.PixelHeight, img1.PixelHeight)
        let bm0 = new XBitmapReader(img0)
        let bm1 = new XBitmapReader(img1)

        let pixels = Array.zeroCreate<int32>(maxh*maxw)
        let mutable diff = 0
        let mutable color = 0
        for r = 0 to (maxh-1) do
            let mutable ind = r*maxw
            for c = 0 to (maxw-1) do
                let p0 = bm0.GetPixel(r, c)
                let p1 = bm1.GetPixel(r, c)
                if (p0 = p1) then
                    color <- 0xffffffff
                else
                    let s = FromArgb(p0)
                    let t = FromArgb(p1)
                    if (far s.R t.R) || (far s.G t.G) || (far s.B t.B) || (far s.A t.A) then
                        diff <- diff + 1
                        color <- diff_color s t
                    else
                        color <- 0xffffffff

                pixels.[ind] <- color
                ind <- ind + 1

        (diff, pixels, maxw, maxh)

module XBitmap =
    open XBM

    [<PermissionSet(SecurityAction.Assert, Name="FullTrust")>] // FxCop: LinkDemand on Save()
    let Save(fileName, image:BitmapSource) =
        let enc = new PngBitmapEncoder()
        enc.Frames.Add(BitmapFrame.Create(image))
        use s = File.OpenWrite(fileName)
        enc.Save(s)

    let Load(fileName) =
        try 
            let b = new BitmapImage()
            b.BeginInit()
            // Turn cache off otherwise file will be locked!
            b.CacheOption <- BitmapCacheOption.OnLoad
            b.CreateOptions <- BitmapCreateOptions.IgnoreImageCache
            b.UriSource <- new Uri(fileName, UriKind.RelativeOrAbsolute)
            b.EndInit()
            b
        with 
            :? FileNotFoundException ->
            null

    let FindPixels(image:BitmapSource, m:int32[]) =
        let mlen = m.Length
        let m0 = m.[0]
        let (w, h) = (image.PixelWidth - mlen, image.PixelHeight)

        let bm = new XBitmapReader(image)
        let mutable found = None
        let mutable r = 0
        while (r < h) do
            let row = bm.GetRow(r)
            let mutable c = 0
            while (c < w) do                
                if (row.[c] = m0) then
                    let mutable matched = true
                    for i = 0 to mlen-1 do
                        matched <- matched && (row.[c+i] = m.[i])
                    if (matched) then 
                        found <- Some(new Point(float c, float r))
                        c <- w
                        r <- h
                c <- c + 1
            r <- r + 1
        found

    let ImageDiff (img0:BitmapSource) (img1:BitmapSource) (fname:string) =
        let (diff, pixels, maxw, maxh) = diff_by_pixel(img0, img1)
        if not(diff = 0 || String.IsNullOrEmpty(fname)) then
            let bigger_img = 
                if (img0.PixelWidth > img1.PixelWidth) then img0 else img1
            let img = Create(bigger_img, maxw, maxh, pixels)
            Save(fname, img)
        diff
