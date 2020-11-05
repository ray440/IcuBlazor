class LSStorage {
    constructor() {
        this.ls = window.localStorage;
    }
    setItem(key, data) { this.ls.setItem(key, data); }
    getItem(key) { return this.ls.getItem(key); }
    removeItem(key) { this.ls.removeItem(key); }
    clear() { this.ls.clear(); }
    length() { return this.ls.length; }
}
class RefCache {
    constructor() {
        this.cache = {};
        this.idc = 33000;
    }
    Add(e) {
        const key = this.idc++;
        this.cache[key] = e;
        return key;
    }
    Get(key) { return this.cache[key]; }
    Cleanup() { this.cache = {}; }
}
const refs = new RefCache();
class Automation {
    Cleanup() { refs.Cleanup(); }
    Eval(code) {
        return eval(code);
    }
    static findOne(sel) {
        const es = document.querySelectorAll(sel);
        const len = es.length;
        if (len === 0)
            throw ("Can't find element '" + sel + "'");
        if (len > 1)
            throw ("Too many '" + sel + "' elements found");
        return es[0];
    }
    FindAll(sel) {
        const es = document.querySelectorAll(sel);
        const ids = [];
        es.forEach((e, i) => ids[i] = refs.Add(e));
        return ids;
    }
    GetContent(ekey, htm) {
        const e = refs.Get(ekey);
        return htm ? e.innerHTML : e.textContent;
    }
    SetContent(ekey, newContent) {
        const e = refs.Get(ekey);
        const prev = e.innerHTML;
        e.innerHTML = newContent;
        return prev;
    }
    Click(ekey) {
        return refs.Get(ekey).click();
    }
    SetValue(ekey, v) {
        refs.Get(ekey).value = v;
    }
    DispatchEvent(ekey, eventType) {
        const e = refs.Get(ekey);
        const event = new Event(eventType);
        e.dispatchEvent(event);
    }
}
class PanZoomer {
    constructor(sel) {
        this.sc = 1.0;
        this.tx = 0.0;
        this.ty = 0.0;
        this.x0 = 0.0;
        this.y0 = 0.0;
        this.drag = false;
        this.setTransform = (tx, ty, scale) => {
            this.sc = scale;
            this.tx = tx;
            this.ty = ty;
        };
        this.resetTransform = () => {
            this.setTransform(0.0, 0.0, 1 / window.devicePixelRatio);
        };
        this.applyTransform = () => {
            const s = this.sc.toString();
            this.e.style.transform = "matrix(" +
                s + ", 0, 0, " + s + ", " + this.tx + ", " + this.ty + ")";
        };
        this.applyDelta = (dx, dy, ds) => {
            if (ds !== 0) {
                this.sc = this.sc * ((ds < 0) ? 1.1 : 0.9);
            }
            if (dx !== 0 || dy !== 0) {
                this.tx += dx;
                this.ty += dy;
            }
            this.applyTransform();
        };
        this.onDrag = (d, on) => {
            this.drag = on;
            if (on) { // track events outside element
                d.addEventListener("mousemove", this.onMouseMove);
                d.addEventListener("mouseup", this.onMouseUp);
            }
            else {
                d.removeEventListener("mousemove", this.onMouseMove);
                d.removeEventListener("mouseup", this.onMouseUp);
            }
        };
        this.onMouseDown = (ev) => {
            this.x0 = ev.clientX;
            this.y0 = ev.clientY;
            this.onDrag(window.document, true);
        };
        this.onMouseUp = (ev) => {
            this.onDrag(window.document, false);
        };
        this.onMouseMove = (ev) => {
            if (!this.drag)
                return;
            const dx = ev.clientX - this.x0;
            const dy = ev.clientY - this.y0;
            this.x0 = ev.clientX;
            this.y0 = ev.clientY;
            this.applyDelta(dx, dy, 0);
        };
        this.onWheel = (ev) => {
            ev.preventDefault();
            const rect = this.e.parentElement.getBoundingClientRect();
            const px = ev.clientX - rect.x;
            const py = ev.clientY - rect.y;
            const s = (ev.deltaY < 0) ? 1.1 : 0.9;
            this.tx = (1 - s) * px + s * this.tx;
            this.ty = (1 - s) * py + s * this.ty;
            this.sc = s * this.sc;
            this.applyTransform();
        };
        this.onKey = (ev) => {
            ev.preventDefault();
            let dx = 0, dy = 0, ds = 0;
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
                case 82:
                    this.resetTransform();
                    break; // 'r' to reset                    
                default: break;
            }
            const mag = 20;
            this.applyDelta(dx * mag, dy * mag, ds);
        };
        const e = this.e = Automation.findOne(sel);
        if (!e)
            throw "Can't find element'" + sel + "'";
        const opt = { passive: false };
        e.addEventListener("mousedown", this.onMouseDown, opt);
        e.addEventListener("mouseup", this.onMouseUp, opt);
        e.addEventListener("mousemove", this.onMouseMove, opt);
        e.addEventListener("keydown", this.onKey, opt);
        e.addEventListener("wheel", this.onWheel, opt);
        e.draggable = false;
        e.setAttribute("tabindex", "0");
        e.style.outline = "none";
        e.style.transformOrigin = "0 0 0";
        this.resetTransform();
        this.applyTransform();
    }
}
class UIutil {
    //private getStyleSheet(cssSheet: string) {
    //    if (cssSheet === "") return true;
    //    const sheets = document.styleSheets;
    //    for (let i = 0; i < sheets.length; i++) {
    //        const s = sheets[i];
    //        if (s.href.indexOf(cssSheet) >= 0) {
    //            return true;
    //        }
    //    }
    //    return false;
    //}
    //public IsInstalled(cssSheet: string) {
    //    return (this.getStyleSheet(cssSheet));
    //}
    IsInstalled(x) {
        return "icu-" + x;
    }
    InitBrowserCapture(title, start) {
        if (start) {
            const prev = document.title;
            document.title = title;
            window.scrollTo(0, 0);
            document.body.insertAdjacentHTML("afterbegin", '<div id="icu_cap" style="border-top: 2px solid #628319;"></div>');
            return prev;
        }
        else {
            // remove div & reset title
            const e = document.getElementById("icu_cap");
            document.body.removeChild(e);
            document.title = title;
            return "";
        }
    }
    GetClientRect(sel) {
        if (sel.length === 0) {
            const d = document.documentElement;
            return [0, 0, d.clientWidth, d.clientHeight];
        }
        else {
            const e = Automation.findOne(sel);
            const b = e.getBoundingClientRect();
            return [b.left, b.top, b.width, b.height];
        }
    }
    GetPosition(sel) {
        const e = Automation.findOne(sel);
        const b = e.getBoundingClientRect();
        const r = window.devicePixelRatio;
        // OPT: rather than convert to/from array across services just pass string
        return JSON.stringify([r * b.left, r * b.top, r * b.width, r * b.height]);
    }
    IntAlign(sel) {
        const e = Automation.findOne(sel);
        const b = e.getBoundingClientRect();
        e.style.marginTop = (2 - (b.top) % 1) + "px";
        e.style.marginLeft = (2 - (b.left) % 1) + "px";
    }
    SyncScrollbars(leftPanel, rightPanel) {
        rightPanel.onscroll = () => {
            leftPanel.scrollTop = rightPanel.scrollTop;
        };
        return true;
    }
    PanZoomInit(sel) {
        new PanZoomer(sel);
    }
}
export const LS = new LSStorage();
export const Act = new Automation();
export const UI = new UIutil();
//# sourceMappingURL=interop.js.map