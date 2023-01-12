import {ForwardedRef} from "react"

export const setRef = <T,>(ref: ForwardedRef<T>, value: T) => {
    if (ref == null) {
        return
    }

    if (typeof ref === "function") {
        ref(value)
    } else if (ref) {
        ref.current = value
    }
}