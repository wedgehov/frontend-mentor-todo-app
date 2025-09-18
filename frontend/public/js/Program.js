import { createElement } from "react";
import React from "react";
import { reactApi } from "./fable_modules/Feliz.2.9.0/Interop.fs.js";
import { createObj } from "./fable_modules/fable-library-js.4.25.0/Util.js";
import { tryParse } from "./fable_modules/fable-library-js.4.25.0/Int32.js";
import { FSharpRef } from "./fable_modules/fable-library-js.4.25.0/Types.js";
import { ofArray } from "./fable_modules/fable-library-js.4.25.0/List.js";
import * as client from "react-dom/client";

export function Counter() {
    let elems_1, elems;
    const patternInput = reactApi.useState(0);
    const setCount = patternInput[1];
    const count = patternInput[0] | 0;
    const patternInput_1 = reactApi.useState(1);
    const step = patternInput_1[0] | 0;
    const setStep = patternInput_1[1];
    return createElement("main", createObj(ofArray([["style", {
        minHeight: 100 + "vh",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 16 + "px ",
        fontFamily: "ui-sans-serif, system-ui, -apple-system",
    }], (elems_1 = [createElement("h1", {
        children: `Count: ${count}`,
    }), createElement("div", createObj(ofArray([["style", {
        display: "flex",
        gap: 8 + "px ",
    }], (elems = [createElement("button", {
        children: "-",
        onClick: (_arg) => {
            setCount(count - step);
        },
    }), createElement("input", {
        type: "number",
        value: step,
        onChange: (ev) => {
            let n;
            let matchValue;
            let outArg = 0;
            matchValue = [tryParse(ev.target.value, 511, false, 32, new FSharpRef(() => outArg, (v_1) => {
                outArg = (v_1 | 0);
            })), outArg];
            let matchResult;
            if (matchValue[0]) {
                if ((n = (matchValue[1] | 0), n > 0)) {
                    matchResult = 0;
                }
                else {
                    matchResult = 1;
                }
            }
            else {
                matchResult = 1;
            }
            switch (matchResult) {
                case 0: {
                    const n_1 = matchValue[1] | 0;
                    setStep(n_1);
                    break;
                }
                case 1: {
                    break;
                }
            }
        },
        style: {
            width: 80,
        },
    }), createElement("button", {
        children: "+",
        onClick: (_arg_1) => {
            setCount(count + step);
        },
    })], ["children", reactApi.Children.toArray(Array.from(elems))])]))), createElement("p", {
        children: "Fable + Vite + Feliz (hooks, no Elmish)",
    })], ["children", reactApi.Children.toArray(Array.from(elems_1))])])));
}

export const reactDom = client;

(function () {
    const el = document.getElementById("root");
    const root = reactDom.createRoot(el);
    root.render(createElement(Counter, null));
})();

