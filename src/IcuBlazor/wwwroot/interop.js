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
        Automation.prototype.DispatchEvent = function (ekey, eventType) {
            var e = refs.Get(ekey);
            var event = new Event(eventType);
            e.dispatchEvent(event);
        };
        return Automation;
    }());
    var PanZoomer = /** @class */ (function () {
        function PanZoomer(sel) {
            var _this = this;
            this.sc = 1.0;
            this.tx = 0.0;
            this.ty = 0.0;
            this.x0 = 0.0;
            this.y0 = 0.0;
            this.drag = false;
            this.setTransform = function (tx, ty, scale) {
                _this.sc = scale;
                _this.tx = tx;
                _this.ty = ty;
            };
            this.applyTransform = function () {
                var s = _this.sc.toString();
                _this.e.style.transform = "matrix(" +
                    s + ", 0, 0, " + s + ", " + _this.tx + ", " + _this.ty + ")";
            };
            this.applyDelta = function (dx, dy, ds) {
                if (ds !== 0) {
                    _this.sc = _this.sc * ((ds < 0) ? 1.1 : 0.9);
                }
                if (dx !== 0 || dy !== 0) {
                    _this.tx += dx;
                    _this.ty += dy;
                }
                _this.applyTransform();
            };
            this.onDrag = function (d, on) {
                _this.drag = on;
                if (on) { // track events outside element
                    d.addEventListener("mousemove", _this.onMouseMove);
                    d.addEventListener("mouseup", _this.onMouseUp);
                }
                else {
                    d.removeEventListener("mousemove", _this.onMouseMove);
                    d.removeEventListener("mouseup", _this.onMouseUp);
                }
            };
            this.onMouseDown = function (ev) {
                _this.x0 = ev.clientX;
                _this.y0 = ev.clientY;
                _this.onDrag(window.document, true);
            };
            this.onMouseUp = function (ev) {
                _this.onDrag(window.document, false);
            };
            this.onMouseMove = function (ev) {
                if (!_this.drag)
                    return;
                var dx = ev.clientX - _this.x0;
                var dy = ev.clientY - _this.y0;
                _this.x0 = ev.clientX;
                _this.y0 = ev.clientY;
                _this.applyDelta(dx, dy, 0);
            };
            this.onWheel = function (ev) {
                ev.preventDefault();
                var rect = _this.e.parentElement.getBoundingClientRect();
                var px = ev.clientX - rect.x;
                var py = ev.clientY - rect.y;
                var s = (ev.deltaY < 0) ? 1.1 : 0.9;
                _this.tx = (1 - s) * px + s * _this.tx;
                _this.ty = (1 - s) * py + s * _this.ty;
                _this.sc = s * _this.sc;
                _this.applyTransform();
            };
            this.onKey = function (ev) {
                ev.preventDefault();
                var dx = 0, dy = 0, ds = 0;
                switch (ev.keyCode) {
                    case 37:
                        dx = -1;
                        break; // left
                    case 39:
                        dx = 1;
                        break; // right
                    case 38:
                        dy = -1;
                        break; // up
                    case 40:
                        dy = 1;
                        break; // down
                    case 109: // subtract `-`
                    case 189:
                        ds = 1;
                        break; // underscore  `_`
                    case 107: // equal `=` 
                    case 187:
                        ds = -1;
                        break; // plus `+`
                    case 48: // '0'
                    case 114:
                    case 82: // 'r' to reset
                        _this.setTransform(0.0, 0.0, 1.0);
                        break;
                    default: break;
                }
                var mag = 20;
                _this.applyDelta(dx * mag, dy * mag, ds);
            };
            var e = this.e = Automation.findOne(sel);
            if (!e)
                throw "Can't find element'" + sel + "'";
            var opt = { passive: false };
            e.addEventListener("mousedown", this.onMouseDown, opt);
            e.addEventListener("mouseup", this.onMouseUp, opt);
            e.addEventListener("mousemove", this.onMouseMove, opt);
            e.addEventListener("keydown", this.onKey, opt);
            e.addEventListener("wheel", this.onWheel, opt);
            e.draggable = false;
            e.setAttribute("tabindex", "0");
            e.style.outline = "none";
            e.style.transformOrigin = "0 0 0";
            this.applyTransform();
        }
        return PanZoomer;
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
            return (this.getStyleSheet(cssSheet));
        };
        UIutil.prototype.InitBrowserCapture = function (title, start) {
            if (start) {
                //const r = window.devicePixelRatio;
                //if (r !== 1.0)
                //    throw ("Browser zoom is " + r + ".  For consistent tests it must be 1.0");
                var prev = document.title;
                document.title = title;
                document.body.insertAdjacentHTML("afterbegin", '<div id="icu_cap" style="border-top: 2px solid #628319;"></div>');
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
            new PanZoomer(sel);
        };
        return UIutil;
    }());
    IcuTest.LS = new LSStorage();
    IcuTest.Act = new Automation();
    IcuTest.UI = new UIutil();
})(IcuTest || (IcuTest = {}));
//# sourceMappingURL=interop.js.map