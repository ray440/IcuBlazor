var IcuTest;
(function (IcuTest) {
    var LSStorage = /** @class */ (function () {
        function LSStorage() {
            this.ls = window.localStorage;
        }
        LSStorage.prototype.setItem = function (key, data) { this.ls.setItem(key, data); };
        LSStorage.prototype.getItem = function (key) { return this.ls.getItem(key); };
        LSStorage.prototype.removeItem = function (key) { this.ls.removeItem(key); };
        LSStorage.prototype.clear = function () { this.ls.clear(); };
        LSStorage.prototype.length = function () { return this.ls.length; };
        return LSStorage;
    }());
    var RefCache = /** @class */ (function () {
        function RefCache() {
            this.cache = {};
            this.idc = 33000;
        }
        RefCache.prototype.Add = function (e) {
            var key = this.idc++;
            this.cache[key] = e;
            return key;
        };
        RefCache.prototype.Get = function (key) { return this.cache[key]; };
        RefCache.prototype.Cleanup = function () { this.cache = {}; };
        return RefCache;
    }());
    var refs = new RefCache();
    var Automation = /** @class */ (function () {
        function Automation() {
        }
        Automation.prototype.Cleanup = function () { refs.Cleanup(); };
        Automation.prototype.Eval = function (code) {
            return eval(code);
        };
        Automation.prototype.EvalJson = function (code) {
            return JSON.stringify(eval(code));
        };
        Automation.findOne = function (sel) {
            var es = document.querySelectorAll(sel);
            var len = es.length;
            if (len === 0)
                throw ("Can't find element '" + sel + "'");
            if (len > 1)
                throw ("Too many '" + sel + "' elements found");
            return es[0];
        };
        Automation.prototype.FindAll = function (sel) {
            var es = document.querySelectorAll(sel);
            var ids = [];
            es.forEach(function (e, i) { return ids[i] = refs.Add(e); });
            return ids;
        };
        Automation.prototype.Content = function (ekey, htm) {
            var e = refs.Get(ekey);
            return htm ? e.innerHTML : e.textContent;
        };
        Automation.prototype.Click = function (ekey) {
            return refs.Get(ekey).click();
        };
        Automation.prototype.SetValue = function (ekey, v) {
            refs.Get(ekey).value = v;
        };
        return Automation;
    }());
    var UIutil = /** @class */ (function () {
        function UIutil() {
        }
        UIutil.prototype.getStyleSheet = function (cssSheet) {
            if (cssSheet === "")
                return true;
            var sheets = document.styleSheets;
            for (var i = 0; i < sheets.length; i++) {
                var s = sheets[i];
                if (s.href.indexOf(cssSheet) >= 0) {
                    return true;
                }
            }
            return false;
        };
        UIutil.prototype.IsInstalled = function (cssSheet) {
            if (!window["panzoom"])
                throw "Can't find panzoom.js";
            return (this.getStyleSheet(cssSheet));
        };
        UIutil.prototype.InitBrowserCapture = function (title, start) {
            if (start) {
                //const r = window.devicePixelRatio;
                //if (r !== 1.0)
                //    throw ("Browser zoom is " + r + ".  For consistent tests it must be 1.0");
                var prev = document.title;
                document.title = title;
                document.body.insertAdjacentHTML('afterbegin', '<div id="icu_cap" style="border-top: 2px solid #628319;"></div>');
                return prev;
            }
            else {
                // remove div & reset title
                var e = document.getElementById("icu_cap");
                document.body.removeChild(e);
                document.title = title;
                return "";
            }
        };
        UIutil.prototype.GetPosition = function (sel) {
            var e = Automation.findOne(sel);
            var b = e.getBoundingClientRect();
            //const r=window.devicePixelRatio;
            //const js=JSON.stringify([r*b.left, r*b.top, r*b.width, r*b.height]);
            var js = JSON.stringify([b.left, b.top, b.width, b.height]);
            return js;
        };
        UIutil.prototype.SyncScrollbars = function (leftPanel, rightPanel) {
            rightPanel.onscroll = function () {
                leftPanel.scrollTop = rightPanel.scrollTop;
            };
            return true;
        };
        UIutil.prototype.PanZoomInit = function (sel) {
            // can use +/- keys zoom, arrow keys to pan
            var e = Automation.findOne(sel);
            var pz = panzoom(e);
            return refs.Add(pz);
        };
        UIutil.prototype.PanZoomReset = function (ekey) {
            var pz = refs.Get(ekey);
            pz.zoomAbs(0, 0, 1);
            pz.moveTo(0, 0);
        };
        return UIutil;
    }());
    IcuTest.LS = new LSStorage();
    IcuTest.Act = new Automation();
    IcuTest.UI = new UIutil();
})(IcuTest || (IcuTest = {}));
//# sourceMappingURL=interop.js.map