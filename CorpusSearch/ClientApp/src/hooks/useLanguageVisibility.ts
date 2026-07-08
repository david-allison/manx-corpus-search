import {useEffect, useState} from "react"

export type VisibleLanguage = "Manx" | "English" | "Both"

export type LanguageVisibility = {
    manxVisible: boolean,
    englishVisible: boolean,
    visibleLanguage: VisibleLanguage,
    setVisibleLanguage: (language: VisibleLanguage) => void,
}

const isTextEntry = (target: EventTarget | null): boolean =>
    target instanceof HTMLElement && ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName)

/**
 * Which language column(s) are displayed (the "Show" toggle), defaulting to both.
 * Keyboard shortcuts: 'm' shows Manx only, 'e' shows English only; pressing the
 * same key again returns to both.
 */
export const useLanguageVisibility = (): LanguageVisibility => {
    const [visibleLanguage, setVisibleLanguage] = useState<VisibleLanguage>("Both")

    useEffect(() => {
        function handleKeyUp(e: KeyboardEvent) {
            if (isTextEntry(e.target)) {
                return // don't toggle columns while typing a search
            }
            if (e.key?.toLowerCase() == "e") {
                setVisibleLanguage(current => current == "English" ? "Both" : "English")
            } else if (e.key?.toLowerCase() == "m") {
                setVisibleLanguage(current => current == "Manx" ? "Both" : "Manx")
            }
        }

        document.addEventListener("keyup", handleKeyUp)
        return () => document.removeEventListener("keyup", handleKeyUp)
    }, [])

    return {
        visibleLanguage,
        setVisibleLanguage,
        manxVisible: visibleLanguage != "English",
        englishVisible: visibleLanguage != "Manx",
    }
}
