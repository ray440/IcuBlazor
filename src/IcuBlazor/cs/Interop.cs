
using System;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Drawing;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace IcuBlazor
{

    public struct ElemRef
    {
        public int Key;
        public ElemRef(int k) { Key = k; }
    }

    public class JSInterop
    {
        readonly IJSRuntime JSR;
        readonly IJSObjectReference mod;

        public JSInterop(IJSObjectReference module, IJSRuntime jsr)
        {
            mod = module;
            JSR = jsr;
        }

        public void Log(string s)
        {
            JSR.InvokeVoidAsync("console.log", s); // f&f Async Task
        }

        public ValueTask<T> Eval<T>(string code) =>
            mod.InvokeAsync<T>("Act.Eval", code);

        public async ValueTask<string> DetectBrowser()
        {
            string[] bs = { "Edge", "Chrome", "Firefox", "MSIE" };
            var spec = await this.Eval<string>("navigator.userAgent");
            return bs.Where((b) => spec.IndexOf(b) >= 0).First();
        }

#if true
        public ValueTask<object> LS_SetItem(string key, string val) =>
            mod.InvokeAsync<object>("LS.setItem", key, val);
        public ValueTask<string> LS_GetItem(string key) =>
            mod.InvokeAsync<string>("LS.getItem", key);
        public ValueTask<string> LS_RemoveItem(string key) =>
            mod.InvokeAsync<string>("LS.removeItem", key);
#else
        // currently ProtectedLocalStorage doesn't work in the CSB Browser!
        public ValueTask LS_SetItem(string key, string val) =>
            LS.SetAsync(key, val);
        public async ValueTask<string> LS_GetItem(string key)
        {
            var pval = await LS.GetAsync<string>(key);
            return pval.Success ? pval.Value : "";
        }
        public ValueTask LS_RemoveItem(string key) =>
            LS.DeleteAsync(key);
#endif

        public ValueTask<string> InitBrowserCapture(string title, bool start) =>
            mod.InvokeAsync<string>("UI.InitBrowserCapture", title, start);
        public ValueTask<string> GetPosition(string sel) =>
            mod.InvokeAsync<string>("UI.GetPosition", sel);
        public async ValueTask<RectangleF> GetClientRect(string sel)
        {
            var a = await mod.InvokeAsync<float[]>("UI.GetClientRect", sel);
            return (new RectangleF(a[0], a[1], a[2], a[3]));
        }

        public ValueTask<string> IntAlign(string sel) =>
            mod.InvokeAsync<string>("UI.IntAlign", sel);

        public ValueTask<bool> SyncScrollbars(ElementReference oldp, ElementReference newp) =>
            mod.InvokeAsync<bool>("UI.SyncScrollbars", oldp, newp);

        public ValueTask PanZoomInit(string sel) =>
            mod.InvokeVoidAsync("UI.PanZoomInit", sel);

        public ValueTask Click(ElemRef e) =>
            mod.InvokeVoidAsync("Act.Click", e.Key);

        public ValueTask SetValue(ElemRef e, string v) =>
            // Useful for setting component values that are private
            mod.InvokeVoidAsync("Act.SetValue", e.Key, v);
        public ValueTask DispatchEvent(ElemRef e, string eventType) =>
            mod.InvokeVoidAsync("Act.DispatchEvent", e.Key, eventType);

        //public ValueTask SendKeys(ElemRef e, string v) =>
        // This would be a serious security violation.
        //    JSR.InvokeVoidAsync("Act.SendKeys", e.Key, v);

        public ValueTask Cleanup() =>
            mod.InvokeVoidAsync("Act.Cleanup");
        public ValueTask<string> GetContent(ElemRef e, bool htmlFormat) =>
            mod.InvokeAsync<string>("Act.GetContent", e.Key, htmlFormat);
        public ValueTask<string> SetContent(ElemRef e, string newContent) =>
            mod.InvokeAsync<string>("Act.SetContent", e.Key, newContent);
        public ValueTask<int[]> FindAll(string sel) =>
            mod.InvokeAsync<int[]>("Act.FindAll", sel);

        public async Task HealthCheck()
        {
            var check = DateTime.Now.Ticks.ToString();
            var res = await mod.InvokeAsync<string>("UI.IsInstalled", check);
            if (!res.Equals("icu-"+check))
                throw new IcuException("Javascript interop failed.", "");
        }

    }

    internal class LocalStorage
    {
        readonly JSInterop JS;
        readonly Dictionary<string, object> cache = new Dictionary<string, object>();

        public LocalStorage(JSInterop jsi)
        {
            JS = jsi;
        }

        public T Deserialize<T>(string json, T defValue)
            where T : notnull
        {
            try {
                if (String.IsNullOrEmpty(json))
                    return defValue;
                var r = JsonSerializer.Deserialize<T>(json);
                return (r==null) ? defValue : r;
            } catch (Exception) {
                var typ = defValue.GetType().Name;
                DBG.Err($"Json Deserialize error: '{json}' is not a '{typ}'");
                return defValue;
            }
        }
        async Task<T> LS_GetItem<T>(string key, T defValue)
            where T : notnull
        {
            var json = await JS.LS_GetItem(key);
            return Deserialize<T>(json, defValue);
        }
        async Task LS_SetItem(string key, object data)
        {
            var json = JsonSerializer.Serialize(data);
            await JS.LS_SetItem(key, json);
        }

        public async Task<T> GetItem<T>(string key, T defValue)
            where T : notnull
        {
            if (cache.ContainsKey(key)) {
                return ((T)cache[key]);
            } else {
                var data = await LS_GetItem<T>(key, defValue);
                cache[key] = data;
                return data;
            }
        }
        public async Task<bool> SetItem(string key, object? data)
        {
            if (cache.ContainsKey(key) && cache[key]==data) {
                return false; // OPT: reduce LS sets
            } else {
                if (data == null) {
                    cache.Remove(key);
                    await JS.LS_RemoveItem(key);
                } else {
                    cache[key] = data;
                    await LS_SetItem(key, data);
                }
                return true;
            }
        }

        public async Task<string> GetStr(string key, string defValue)
        {
            return await GetItem<string>(key, defValue);
        }
        public Task<bool> SetStr(string key, string val)
        {
            if (val != null) {
                return SetItem(key, val);
            } else {
                _ = JS.LS_RemoveItem(key);
                return Task.FromResult(true);
            }
        }

    }

    public class JSInit
    {
        internal readonly IJSRuntime JSR;
        //public readonly IJSInProcessRuntime JSPR;     // zzz not officially supported
        //public readonly IJSUnmarshalledRuntime JSUR;  // zzz not officially supported
        //private readonly ProtectedLocalStorage LS;    // zzz doesn't work in browser!?!

        internal JSInterop JSI;
        internal LocalStorage LS;

        private JSInit(IJSRuntime jsr, IJSObjectReference module) //, ProtectedLocalStorage ls, IJSInProcessRuntime jspr, IJSUnmarshalledRuntime jsur)
        {
            JSR = jsr;
            JSI = new JSInterop(module, JSR);
            LS = new LocalStorage(JSI);
            //JSPR = jspr;
            //JSUR = jsur;
        }

        static public async Task<JSInit> InitAsync(IJSRuntime jsr)
        {
            try {
                var module = await jsr.InvokeAsync<IJSObjectReference>(
                   "import", "/_content/IcuBlazor/interop.js");
                var jsinit = new JSInit(jsr, module);
                await jsinit.JSI.HealthCheck();
                ENV.Browser = await jsinit.JSI.DetectBrowser();
                return jsinit;
            } catch (Exception e) {
                DBG.Err(e.Message); // may be actual script error
                throw;
            }
        }

        public async Task InitBrowserCapture(IcuSession ss)
        {
            try {
                var title = ss.ID;
                var prev = await JSI.InitBrowserCapture(title, true);
                await Task.Delay(200);
                var s = await IcuRpc.InitImageCapture(ss, title);
                var _ = await JSI.InitBrowserCapture(prev, false);
            } catch (JSException e) {
                if (e.Message.StartsWith("Browser zoom")) {
                    throw new IcuException("Inconsistent zoom",
                       $"1) {e.Message}\n"+
                        "2) Also ensure that your monitor scale is 100%");
                } else
                    throw;
            }
        }

    }

}
