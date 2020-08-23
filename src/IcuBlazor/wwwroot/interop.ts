
namespace IcuTest { // eslint-disable-line

    class LSStorage {
        private ls = window.localStorage;
        public setItem(key: string, data: string) { this.ls.setItem(key, data); }
        public getItem(key: string) { return this.ls.getItem(key); }
        public removeItem(key: string) { this.ls.removeItem(key); }
        public clear() { this.ls.clear(); }
        public length() { return this.ls.length; }
    }

    class RefCache { // keep a pointer to JS objects
        private cache = {} as Record<number, any>;
        private idc = 33000;
        public Add(e) {
            const key = this.idc++;
            this.cache[key] = e;
            return key;
        }
        public Get<T>(key: number) { return (this.cache[key] as T); }
        public Cleanup() { this.cache = {}; }
    }
    const refs = new RefCache();

    class Automation {

        public Cleanup() { refs.Cleanup(); }

        public Eval(code: string) {
            return eval(code);
        }
        public EvalJson(code: string) {
            return JSON.stringify(eval(code));
        }
        public static findOne(sel: string) {
            const es = document.querySelectorAll(sel);
            const len = es.length;
            if (len === 0) throw ("Can't find element '" + sel + "'");
            if (len > 1) throw ("Too many '" + sel + "' elements found");
            return es[0];
        }
        public FindAll(sel: string) {
            const es = document.querySelectorAll(sel);
            const ids = [];
            es.forEach((e: HTMLElement, i) => ids[i] = refs.Add(e));
            return ids;
        }

        public Content(ekey: number, htm: boolean) {
            const e = refs.Get<HTMLElement>(ekey);
            return htm ? e.innerHTML : e.textContent;
        }
        public Click(ekey: number) {
            return refs.Get<HTMLElement>(ekey).click();
        }
        public SetValue(ekey: number, v: string) {
            refs.Get<HTMLInputElement>(ekey).value = v;
        }
        public DispatchEvent(ekey: number, eventType: string) {
            const e = refs.Get<HTMLInputElement>(ekey);
            const event = new Event(eventType);
            e.dispatchEvent(event);
        }
    }

    class PanZoomer {
        private sc = 1.0;
        private tx = 0.0;
        private ty = 0.0;
        private x0 = 0.0;
        private y0 = 0.0;
        private e: HTMLElement;
        private drag = false;

        setTransform = (tx: number, ty: number, scale: number) => {
            this.sc = scale;
            this.tx = tx;
            this.ty = ty;
        }
        resetTransform = () => {
            this.setTransform(0.0, 0.0, 1 / window.devicePixelRatio);
        }
        applyTransform = () => {
            const s = this.sc.toString();
            this.e.style.transform = "matrix(" +
                s + ", 0, 0, " + s + ", " + this.tx + ", " + this.ty + ")";
        }
        applyDelta = (dx: number, dy: number, ds: number) => {
            if (ds !== 0) {
                this.sc = this.sc * ((ds < 0) ? 1.1 : 0.9);
            }
            if (dx !== 0 || dy !== 0) {
                this.tx += dx;
                this.ty += dy;
            }
            this.applyTransform();
        }

        onDrag = (d: Document, on: boolean) => {
            this.drag = on;
            if (on) {  // track events outside element
                d.addEventListener("mousemove", this.onMouseMove);
                d.addEventListener("mouseup", this.onMouseUp);
            } else {
                d.removeEventListener("mousemove", this.onMouseMove);
                d.removeEventListener("mouseup", this.onMouseUp);
            }
        }
        onMouseDown = (ev: MouseEvent) => {
            this.x0 = ev.clientX;
            this.y0 = ev.clientY;
            this.onDrag(window.document, true);
        }
        onMouseUp = (ev: MouseEvent) => {
            this.onDrag(window.document, false);
        }
        onMouseMove = (ev: MouseEvent) => {
            if (!this.drag) return;
            const dx = ev.clientX - this.x0;
            const dy = ev.clientY - this.y0;
            this.x0 = ev.clientX;
            this.y0 = ev.clientY;
            this.applyDelta(dx, dy, 0);
        }
        onWheel = (ev: WheelEvent) => {
            ev.preventDefault();

            const rect = this.e.parentElement.getBoundingClientRect();
            const px = ev.clientX - rect.x;
            const py = ev.clientY - rect.y;
            const s = (ev.deltaY < 0) ? 1.1 : 0.9;

            this.tx = (1 - s) * px + s * this.tx;
            this.ty = (1 - s) * py + s * this.ty;
            this.sc = s * this.sc;
            this.applyTransform();
        }
        onKey = (ev: KeyboardEvent) => {
            ev.preventDefault();
            let dx = 0, dy = 0, ds = 0;
            switch (ev.keyCode) {
                case 37: dx = -1; break;    // left
                case 39: dx =  1; break;    // right
                case 38: dy = -1; break;    // up
                case 40: dy =  1; break;    // down
                case 109:                   // subtract `-`
                case 189: ds = 1; break;    // underscore  `_`
                case 107:                   // equal `=` 
                case 187: ds = -1; break;   // plus `+`
                case 48:                    // '0'
                case 114:
                case 82: this.resetTransform(); break; // 'r' to reset                    
                default: break;
            }
            const mag = 20;
            this.applyDelta(dx*mag, dy*mag, ds);
        }

        public constructor(sel: string) {
            const e = this.e = Automation.findOne(sel) as HTMLElement;
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

        private getStyleSheet(cssSheet: string) {
            if (cssSheet === "") return true;
            const sheets = document.styleSheets;
            for (let i = 0; i < sheets.length; i++) {
                const s = sheets[i];
                if (s.href.indexOf(cssSheet) >= 0) {
                    return true;
                }
            }
            return false;
        }

        public IsInstalled(cssSheet: string) {
            return (this.getStyleSheet(cssSheet));
        }
        public InitBrowserCapture(title: string, start: boolean) {
            if (start) {
                const prev = document.title;
                document.title = title;
                document.body.insertAdjacentHTML("afterbegin",
                    '<div id="icu_cap" style="border-top: 2px solid #628319;"></div>');
                return prev;
            } else {
                // remove div & reset title
                const e = document.getElementById("icu_cap");
                document.body.removeChild(e);
                document.title = title;
                return "";
            }
        }
        public GetPosition(sel: string) {
            const e = Automation.findOne(sel);
            const b = e.getBoundingClientRect();
            const r = window.devicePixelRatio;
            const js = JSON.stringify([r*b.left, r*b.top, r*b.width, r*b.height]);
            return js;
        }
        public SyncScrollbars(leftPanel: HTMLElement, rightPanel: HTMLElement) {
            rightPanel.onscroll = () => { 
                leftPanel.scrollTop = rightPanel.scrollTop;
            };
            return true;
        }
        public PanZoomInit(sel: string) {
            new PanZoomer(sel);
        }
    }

    export const LS = new LSStorage();
    export const Act = new Automation();
    export const UI = new UIutil();
}
