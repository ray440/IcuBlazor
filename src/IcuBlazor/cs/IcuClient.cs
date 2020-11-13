using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using static IcuBlazor.Models;

namespace IcuBlazor
{
    public static class Wait
    {
        /// <summary>
        /// Wait by polling until <c>getter()</c> returns a non-null value.
        /// </summary>
        /// <param name="getter">Try getter() until it returns a non-null value.</param>
        /// <param name="timeout">Stop after timeout milliseconds.</param>
        /// <param name="interval">Polling interval in milliseconds.</param>
        /// <returns>non-null value if successful, otherwise raises a TimeoutException.</returns>
        public static async Task<T> ForAsync<T>
            (Func<Task<T>> getter, int timeout = 5000, int interval = 200)
        {
            var tries = (int)((timeout-1)/interval)+1;
            for (int t = 0; t < tries; t++) {
                var done = await getter();
                if (done != null) return done;
                await Task.Delay(interval);
            }
            throw new TimeoutException($"Wait.For failed after '{timeout}' ms");
        }

        /// <summary>
        /// Poll until <c>isDone()</c> is true.
        /// </summary>
        /// <param name="isDone">Try isDone() until it returns true.</param>
        /// <param name="timeout">Stop after timeout milliseconds.</param>
        /// <param name="interval">Polling interval in milliseconds.</param>
        /// <returns>Raises a TimeoutException if isDone() always returns false.</returns>
        public static async Task UntilAsync
            (Func<Task<bool>> isDone, int timeout = 5000, int interval = 200)
        {
            var tries = (int)((timeout-1)/interval)+1;
            for (int t = 0; t < tries; t++) {
                var done = await isDone();
                if (done) return;
                await Task.Delay(interval);
            }
            throw new TimeoutException($"Wait.Until failed after '{timeout}' ms");
        }

        /// <summary>
        /// Wait by polling until getter() returns a non-null value.
        /// </summary>
        /// <param name="getter">Try getter() until it returns a non-null value.</param>
        /// <param name="timeout">Stop after timeout milliseconds.</param>
        /// <param name="interval">Polling interval in milliseconds.</param>
        /// <returns>non-null value if successful, otherwise raises a TimeoutException.</returns>
        public static Task<T> For<T>
            (Func<T> getter, int timeout = 5000, int interval = 200)
        {
            return ForAsync(() => Task.FromResult(getter()), timeout, interval);
        }
        /// <summary>
        /// Wait until isDone() is true.
        /// </summary>
        /// <param name="isDone">Try isDone() until it returns true.</param>
        /// <param name="timeout">Stop after timeout milliseconds.</param>
        /// <param name="interval">Polling interval in milliseconds.</param>
        /// <returns>Raises a TimeoutException if isDone() always returns false.</returns>
        public static Task Until
            (Func<bool> isDone, int timeout = 5000, int interval = 200)
        {
            return UntilAsync(() => Task.FromResult(isDone()), timeout, interval);
        }
    }


    public class UIHelper
    {
        IJSRuntime JSR;
        internal JSInit JSinit = null!; // set in InitAsync()
        internal JSInterop JS = null!; // set in InitAsync()
        internal LocalStorage LS = null!; // set in InitAsync()

        public UIHelper(IJSRuntime jsr) // must be @inject-able
        {
            JSR = jsr;
        }

        public async Task InitAsync()
        {
            if (JS == null) {
                JSinit = await JSInit.InitAsync(JSR);
                JS = JSinit.JSI;
                LS = JSinit.LS;
                DBG.SetLogger(JS.Log);
                DBG.SetSystem(ENV.IsWasm ? "CSB" : "SSB");
            }
        }


        public void Log(string s)
        {
            JS.Log(s);
        }

        /// <summary>
        /// Excute javascript code.
        /// </summary>
        /// <returns>result as a string</returns>
        public ValueTask<T> Eval<T>(string code) => JS.Eval<T>(code);        

        public ValueTask Cleanup() => JS.Cleanup();

        /// <summary>
        /// Send a click event to an html element.
        /// </summary>
        public ValueTask Click(ElemRef e) => JS.Click(e);

        /// <summary>
        /// Set html element.value = v
        /// </summary>
        public ValueTask SetValue(ElemRef e, string v) => JS.SetValue(e, v);

        public ValueTask DispatchEvent(ElemRef e, string eType) => JS.DispatchEvent(e, eType);

        // Simulating user actions in the browser is a security issue.
        // Manually firing events does NOT trigger default actions.
        // e.g. Firing a KeyEvent on a text field will not update the UI.
        //public ValueTask SendKeys(ElemRef e, string v) => JS.SendKeys(e, v);

        ValueTask<string> GetContent(ElemRef e, bool asHtml) => JS.GetContent(e, asHtml);

        /// <summary>
        /// Replace the content of an element with new content.
        /// </summary>
        public ValueTask<string> SetContent(ElemRef e, string html) => JS.SetContent(e, html);

        /// <summary>
        /// Get the html content of an html element.
        /// </summary>
        public ValueTask<string> HtmlContent(ElemRef e) => GetContent(e, true);

        /// <summary>
        /// Get the text content of an html element 
        /// </summary>
        public ValueTask<string> TextContent(ElemRef e) => GetContent(e, false);
        
        async Task<ElemRef[]> find_elements(string sel)
        {
            // Element may not be ready when called, so check-wait for 5s.
            var ids = new int[0];
            await Wait.UntilAsync(async ()=> {
                ids = await JS.FindAll(sel);
                return (ids.Length > 0);
            }, 5000, 100);
            return ids.Select(x => new ElemRef(x)).ToArray();
        }

        async Task<ElemRef[]> elements_with(ElemRef[] elems, string withText)
        {
            if (String.IsNullOrEmpty(withText))
                return elems;
            var lst = new List<ElemRef>();
            foreach (var e in elems) {
                var t = await TextContent(e);
                if (t.Contains(withText)) lst.Add(e);
            }
            return lst.ToArray();
        }

        async Task<ElemRef[]> search_elements
            (string sel, string withText = "", bool all = false)
        {
            try {
                var elems = await find_elements(sel);
                var es = await elements_with(elems, withText);
                var len = es.Length;
                if (!all && len != 1) {
                    var search = $"'{sel}'";
                    if (withText.Length > 0)
                        search = search + $" with text '{withText}'";
                    if (len == 0)
                        throw new Exception($"No matches for {search}.");
                    else
                        throw new Exception($"Too many matches({len}) for {search}.");
                }
                return es;
            } catch (TimeoutException) {
                throw new Exception($"Cannot find element with '{sel}'");
            }
        }

        /// <summary>
        ///  Find html elements that match selector.
        /// </summary>
        /// <param name="selector">css selector.</param>
        /// <param name="withText">get elements that contain given text.</param>
        public Task<ElemRef[]> FindAll(string selector, string withText = "")
        {
            return search_elements(selector, withText, true);
        }

        /// <summary>
        /// Find a single element that matches selector.
        /// </summary>
        /// <param name="selector">css selector.</param>
        /// <param name="withText">get elements that contain given text.</param>
        public async Task<ElemRef> Find(string selector, string withText = "")
        {
            var es = await search_elements(selector, withText, false);
            return es[0];
        }

        /// <summary>
        /// Poll for a single element to be added to DOM.
        /// </summary>
        /// <param name="selector">css selector.</param>
        /// <param name="withText">get elements that contain given text.</param>
        /// <param name="timeout">Stop after timeout milliseconds.</param>
        /// <param name="interval">Polling interval in milliseconds.</param>
        public Task<ElemRef> WaitForElement
            (string selector, string withText = "", 
            int timeout = 5000, int interval = 200)
        {
            return Wait.ForAsync(
                () => Find(selector, withText), timeout, interval);
        }

    }


    internal class IcuClient // not publicly accessible 
    {
        internal readonly IcuConfig Config;
        public IcuSession Session;
        internal UIHelper UI;
        internal MessageBus MsgBus;
        internal CheckpointGrouping CheckGroup = new CheckpointGrouping();

        public IcuClient(IcuConfig config, RPC.IProxy rpc, UIHelper ui)
        {
            if (!ENV.IsWasm)
                config.SessionID = Guid.NewGuid().ToString();

            this.UI = ui;
            this.Config = config;
            this.Session = new IcuSession(config, rpc);
            this.MsgBus = Session.MsgBus;
        }

        internal void RefreshModels()
        {
            Session.TreeRoot.CalcOutcome();
        }

        internal string GetLogo()
        {
            return "http://icublazor.com/docs/images/logo-dark.svg?help_131";
        }

        public string ConfigKey(string part)
        {
            return SlashPath.Make(Config.Name, part);
        }

        public Task LocalSet<T>(string k, T v)
        {
            return UI.LS.SetItem(ConfigKey(k), v);
        }
        public Task<T> LocalGet<T>(string k, T def)
            where T : notnull
        {
            return UI.LS.GetItem(ConfigKey(k), def);
        }
        async Task LoadConfig()
        {
            Config.ViewLayout = await LocalGet("ViewLayout", ViewLayout.Tree);
            Config.Filter = await LocalGet("Filter", "");
            Config.Interactive = await LocalGet("Interactive", false);
            Config.StopOnFirstFailure = await LocalGet("StopOnFirstFailure", false);
        }
        internal void SaveConfigVar(string f)
        {
            var _ = f switch
            {
                "ViewLayout" => LocalSet(f, Config.ViewLayout),
                "Filter" => LocalSet(f, Config.Filter),
                "Interactive" => LocalSet(f, Config.Interactive),
                "StopOnFirstFailure" => LocalSet(f, Config.StopOnFirstFailure),
                _ => throw new ArgumentException($"Unhandled var '{f}'."),
            };
        }

        internal void ParseUri(string uri)
        {
            if (String.IsNullOrWhiteSpace(uri)) return;
            var urlspec = Str.Split("?", uri);
            if (urlspec.Length < 2) return;
            var q = QueryHelpers.ParseQuery(urlspec[1]);
            if (!q.ContainsKey("output")) return;
            Config.OutFile = q["output"];
            Config.Interactive = false; // assume CI/CD mode
        }

        internal async Task InitAsync()
        {
            await UI.InitAsync();
            await LoadConfig();
            Session.Validate();
            if (Config.EnableServer && Config.CanSaveTestData)
                await UI.JSinit.InitBrowserCapture(Session);
        }

        static internal IcuClient Fetch(IServiceProvider sp)
        {
            if (!ENV.IsIcuEnabled) {
                var cfg = ENV.IsWasm ? "builder.Services.AddIcuBlazor(...)" : "services.AddIcuBlazor()";
                throw new IcuException("IcuBlazor is not enabled.",
                   $"1) Try calling {cfg}.");
            }

            IcuClient? icu = sp.GetService<IcuClient>();
            if (icu == null) {
                throw new IcuException("IcuBlazor has not been configured properly.",
                   $"1) Have you called services.AddIcuBlazor() in Startup.ConfigureServices?\n" +
                    "2) Maybe this app is configured to not use IcuBlazor in production.");
            }
            return icu;
        }

    }

}
