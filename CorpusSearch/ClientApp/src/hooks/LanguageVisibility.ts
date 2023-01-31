import {useEffect, useState} from "react"

export type LanguageVisibility = { manxVisible: boolean, englishVisible: boolean}
export const useLanguageVisibility = (): LanguageVisibility => {
    const [manxVisible, setManxVisible] = useState(true)
    const [englishVisible, setEnglishVisible] = useState(true)
    
    // rare issue, tapping too quickly can hide both
    if (!manxVisible && !englishVisible) {
        setManxVisible(true)
    }
    
    useEffect(() => {
        function handleKeyDown(e: KeyboardEvent) {
            if (e.key?.toLowerCase() == "e") {
                if (!manxVisible && englishVisible) {
                    // the user is making English invisible. Show the Manx
                    setManxVisible(true)
                }
                setEnglishVisible(x => !x)
                
            } else if (e.key?.toLowerCase() == "m") {
                if (!englishVisible && manxVisible) {
                    // the user is making Manx invisible. Show the English
                    setEnglishVisible(true)
                } 
                setManxVisible(x => !x)
            }
        }

        document.addEventListener("keyup", handleKeyDown)
        return () => document.removeEventListener("keyup", handleKeyDown)
    }, [englishVisible, manxVisible])

    return { manxVisible, englishVisible }
}