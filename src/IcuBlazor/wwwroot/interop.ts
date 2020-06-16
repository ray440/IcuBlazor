
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

    declare function panzoom(e: Element): any;

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
            if (!window["panzoom"]) 
                throw "Can't find panzoom.js"
            return (this.getStyleSheet(cssSheet));
        }
        public InitBrowserCapture(title: string, start: boolean) {
            if (start) {
                //const r = window.devicePixelRatio;
                //if (r !== 1.0)
                //    throw ("Browser zoom is " + r + ".  For consistent tests it must be 1.0");

                const prev = document.title;
                document.title = title;
                document.body.insertAdjacentHTML('afterbegin',
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
            //const r=window.devicePixelRatio;
            //const js=JSON.stringify([r*b.left, r*b.top, r*b.width, r*b.height]);
            const js = JSON.stringify([b.left, b.top, b.width, b.height]);
            return js;
        }
        public SyncScrollbars(leftPanel: HTMLElement, rightPanel: HTMLElement) {
            rightPanel.onscroll = () => { 
                leftPanel.scrollTop = rightPanel.scrollTop;
            };
            return true;
        }
        public PanZoomInit(sel: string) {
            // can use +/- keys zoom, arrow keys to pan
            const e = Automation.findOne(sel);
            const pz = panzoom(e);
            return refs.Add(pz);            
        }
        public PanZoomReset(ekey: number) {
            const pz = refs.Get<any>(ekey);
            pz.zoomAbs(0, 0, 1);
            pz.moveTo(0, 0);
        }
    }

    export const LS = new LSStorage();
    export const Act = new Automation();
    export const UI = new UIutil();
}
