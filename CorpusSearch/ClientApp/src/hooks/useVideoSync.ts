import { useEffect, useRef, useState } from "react"
import { Player } from "../components/YouTuber"
import { SearchWorkResult } from "../api/SearchWorkApi"
import useInterval from "../vendor/use-interval/UseInterval"

export const formatTime = (seconds: number): string => {
    const m = Math.floor(seconds / 60)
    const s = Math.floor(seconds % 60)
    return `${m}:${String(s).padStart(2, "0")}`
}

/** The video id, when the source is a YouTube watch URL */
export const getVideoId = (source: string): string | null => {
    let url: URL
    try {
        url = new URL(source)
    } catch {
        return null // most sources are not URLs at all
    }
    // [security] block 'www.youtube.evil.com'
    if (
        url.protocol != "https:" ||
        (url.hostname != "www.youtube.com" && url.hostname != "youtube.com")
    ) {
        return null
    }
    return url.searchParams.get("v")
}

/**
 * Playback state for a video transcript: the currently-playing line, and
 * scroll-following it. The caller renders the player into `videoDock`/`player`
 * and registers each line's `<tr>` in `rowElements` by its result index.
 */
export const useVideoSync = (
    source: string | undefined,
    results: SearchWorkResult[],
) => {
    const videoId = source ? getVideoId(source) : null
    const isVideo = Boolean(videoId)
    const player = useRef<Player>(null)

    // null until the video loads: lines with subStart 0 must not highlight before then
    const [videoTime, setVideoTime] = useState<number | null>(null)

    // while paused the time doesn't change, so setState skips the re-render
    useInterval(
        () => setVideoTime(player.current?.getCurrentTime() ?? null),
        250,
    )

    const isPlaying = (line: SearchWorkResult): boolean => {
        if (!isVideo || videoTime == null) return false
        if (line.subStart == null || line.subEnd == null) return false
        return videoTime >= line.subStart && videoTime <= line.subEnd
    }

    const playingIndex = results.findIndex(isPlaying)
    const videoDock = useRef<HTMLDivElement>(null)
    const rowElements = useRef(new Map<number, HTMLTableRowElement>())
    const lastPlayingIndex = useRef(-1)

    // Follow the playback through the transcript, but only while the user is
    // reading around the playhead: once they scroll elsewhere, leave them be.
    useEffect(() => {
        if (playingIndex === -1 || playingIndex === lastPlayingIndex.current) {
            return
        }
        const previousRow = rowElements.current.get(lastPlayingIndex.current)
        lastPlayingIndex.current = playingIndex
        const row = rowElements.current.get(playingIndex)
        if (row == null) {
            return
        }
        const onScreen = (element: HTMLElement | undefined) => {
            if (element == null) return false
            const dockBottom =
                videoDock.current?.getBoundingClientRect().bottom ?? 0
            const rect = element.getBoundingClientRect()
            return rect.bottom > dockBottom && rect.top < window.innerHeight
        }
        if (!onScreen(previousRow) && !onScreen(row)) {
            return
        }
        row.scrollIntoView({
            block: "center",
            behavior: window.matchMedia("(prefers-reduced-motion: reduce)")
                .matches
                ? "auto"
                : "smooth",
        })
    }, [playingIndex])

    return { videoId, isVideo, player, videoDock, rowElements, isPlaying }
}
