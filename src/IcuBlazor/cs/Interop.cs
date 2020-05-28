
using System;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace IcuBlazor
{

    internal static class XSS
    {
        static public string darker(double x) => $"rgba(0,0,0,{x})";
        static public string lighter(double x) => $"rgba(255,255,255,{x})";
        static public string units(double x, string m) => $"{x}{m}";
        static public string pixels(double x) => units(x, "px");
    }

    public struct ElemRef
    {
        public int Key;
        public ElemRef(int k) { Key = k; }
    }

    internal class JSInterop
    {
        public readonly IJSRuntime JSR;
        public JSInterop(IJSRuntime jsr)
        {
            JSR = jsr;
        }

        public ValueTask<string> Eval(string code) =>
            JSR.InvokeAsync<string>("IcuTest.Act.Eval", code);
        public async ValueTask<T> EvalJson<T>(string code)
        {
            var js = await JSR.InvokeAsync<string>("IcuTest.Act.EvalJson", code);
            return Conv.FromJson<T>(js);
        }
        public async ValueTask<string> DetectBrowser()
        {
            string[] bs = { "Edge", "Chrome", "Firefox", "MSIE" };
            var spec = await this.Eval("navigator.userAgent");
            return bs.Where((b) => spec.IndexOf(b) >= 0).First();
        }

        public ValueTask<object> LS_SetItem(string key, string val) =>
            JSR.InvokeAsync<object>("IcuTest.LS.setItem", key, val);
        public ValueTask<string> LS_GetItem(string key) =>
            JSR.InvokeAsync<string>("IcuTest.LS.getItem", key);
        public ValueTask<string> LS_RemoveItem(string key) =>
            JSR.InvokeAsync<string>("IcuTest.LS.removeItem", key);
        //public Task LS_Clear() =>
        //    JSR.InvokeAsync<bool>("IcuTest.LS.clear");

        public ValueTask<string> InitBrowserCapture(string title, bool start) =>
            JSR.InvokeAsync<string>("IcuTest.UI.InitBrowserCapture", title, start);
        public ValueTask<string> GetPosition(string sel) =>
            JSR.InvokeAsync<string>("IcuTest.UI.GetPosition", sel);

        public ValueTask<bool> SyncScrollbars(ElementReference oldp, ElementReference newp) =>
            JSR.InvokeAsync<bool>("IcuTest.UI.SyncScrollbars", oldp, newp);

        public ValueTask<int> PanZoomInit(string sel) =>
            JSR.InvokeAsync<int>("IcuTest.UI.PanZoomInit", sel);
        public ValueTask PanZoomReset(int ekey) =>
            JSR.InvokeVoidAsync("IcuTest.UI.PanZoomReset", ekey);

        public ValueTask Click(ElemRef e) =>
            JSR.InvokeVoidAsync("IcuTest.Act.Click", e.Key);
        public ValueTask SetValue(ElemRef e, string v) =>
            // Useful for setting component values that are private
            JSR.InvokeVoidAsync("IcuTest.Act.SetValue", e.Key, v);
        public ValueTask Cleanup() =>
            JSR.InvokeVoidAsync("IcuTest.Act.Cleanup");
        public ValueTask<string> Content(ElemRef e, bool htmlFormat) =>
            JSR.InvokeAsync<string>("IcuTest.Act.Content", e.Key, htmlFormat);
        public ValueTask<int[]> FindAll(string sel) =>
            JSR.InvokeAsync<int[]>("IcuTest.Act.FindAll", sel);

        public async Task CheckIcuInstallation()
        {
            try {
                var res = await JSR.InvokeAsync<bool>("IcuTest.UI.IsInstalled", "");
                ENV.Browser = await DetectBrowser();
                DBG.Info($"Detected Browser = {ENV.Browser}");
            } catch (Exception e) {
                DBG.Err(e.Message); // may be actual script error
                var (page, cw) = ENV.IsWasm ? ("index.html", "webassembly") : ("_Host.cshtml", "Server");
                throw new IcuException("Configuration Error",
                   $"1) You must have these js scripts in {page}.\n"+
                    "    <script src=\"_content/IcuBlazor/interop.js\"></script>\n"+
                    "    <script src=\"_content/IcuBlazor/panzoom.min.js\"></script>\n"+
                   $"    <script src=\"_framework/blazor.{cw}.js\"></script>\n"
                    );
            }
        }

        public async Task InitBrowserCapture(IcuSession ss)
        {
            try {
                var title = ss.ID;
                var prev = await InitBrowserCapture(title, true);
                await Task.Delay(200);
                var s = await IcuRpc.InitImageCapture(ss, title);
                var _ = await InitBrowserCapture(prev, false);
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

    internal class LocalStorage
    {
        readonly JSInterop JS;
        readonly Dictionary<string, object> cache = new Dictionary<string, object>();

        public LocalStorage(JSInterop jsi)
        {
            JS = jsi;
        }

        public T Deserialize<T>(string json, T defValue)
        {
            try {
                return (JsonSerializer.Deserialize<T>(json));
            } catch (Exception) {
                var typ = defValue.GetType().Name;
                DBG.Err($"Json Deserialize error: '{json}' is not a '{typ}'");
                return defValue;
            }
        }
        async Task<T> LS_GetItem<T>(string key, T defValue)
        {
            var json = await JS.LS_GetItem(key);
            if (String.IsNullOrEmpty(json))
                return defValue;
            else
                return Deserialize<T>(json, defValue);
        }
        async Task LS_SetItem(string key, object data)
        {
            var json = JsonSerializer.Serialize(data);
            await JS.LS_SetItem(key, json);
        }

        public async Task<T> GetItem<T>(string key, T defValue)
        {
            if (cache.ContainsKey(key)) {
                return ((T)cache[key]);
            } else {
                var data = await LS_GetItem<T>(key, defValue);
                cache[key] = data;
                return data;
            }
        }
        public async Task<bool> SetItem(string key, object data)
        {
            if (cache.ContainsKey(key) && cache[key]==data) {
                return false; // OPT: reduce LS sets
            } else {
                await LS_SetItem(key, data);
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
}
