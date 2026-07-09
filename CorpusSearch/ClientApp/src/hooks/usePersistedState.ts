import { useState } from "react"

/**
 * useState persisted to localStorage, for display preferences.
 *
 * `parse` maps the stored string to the value, and supplies the default for a
 * missing key (null) or unreadable storage; `serialize` maps the value back.
 * Writes are best-effort: a preference is not worth surfacing a storage error.
 */
export const usePersistedState = <T>(
    key: string,
    parse: (stored: string | null) => T,
    serialize: (value: T) => string,
): [T, (next: T) => void] => {
    const [value, setValue] = useState<T>(() => {
        try {
            return parse(localStorage.getItem(key))
        } catch {
            return parse(null)
        }
    })

    const setAndStore = (next: T) => {
        setValue(next)
        try {
            localStorage.setItem(key, serialize(next))
        } catch {
            /* preference only - ignore storage failures */
        }
    }

    return [value, setAndStore]
}
