import { Ref, useImperativeHandle, useRef } from "react"
import YouTube, { YouTubePlayer, YouTubeProps } from "react-youtube"
import "./ComparisonTable.css"

export type Player = {
    seek: (time: number) => void
    /** The current playback time, or null if the video never loaded */
    getCurrentTime: () => number | null
}

// event.target needs work, as does 'opts'
/* eslint-disable @typescript-eslint/no-unsafe-return, @typescript-eslint/no-unsafe-call, @typescript-eslint/no-unsafe-member-access, @typescript-eslint/no-unsafe-assignment */
const YouTuber = ({
    videoId,
    startSeconds,
    autoplay,
    ref,
}: {
    videoId: string
    /** where playback begins, in seconds — a deep link's moment. Read once, at
     * mount: a change of `opts` reloads the embed, and a re-search that loses
     * the target line must not restart a video mid-listen. */
    startSeconds?: number
    /** play on load: only for a player a click summoned (the audio popup) —
     * anywhere else the reader has not asked to be spoken at */
    autoplay?: boolean
    ref?: Ref<Player>
}) => {
    const player = useRef<YouTubePlayer>(null)
    const start = useRef(startSeconds).current

    const seek = (time: number) => {
        if (player.current == undefined) {
            return
        }
        player.current.seekTo(time, true)
        player.current.playVideo()
    }

    useImperativeHandle(ref, () => ({
        seek,
        getCurrentTime: () => player.current?.getCurrentTime() ?? null,
    }))

    const onPlayerReady: YouTubeProps["onReady"] = (event) => {
        player.current = event.target
    }

    const opts: YouTubeProps["opts"] = {
        //width: "60%",
        playerVars: {
            // https://developers.google.com/youtube/player_parameters#autoplay
            autoplay: autoplay ? 1 : 0,
            // https://developers.google.com/youtube/player_parameters#start
            ...(start != null ? { start: Math.floor(start) } : {}),
        },
    }

    return (
        <>
            <YouTube
                videoId={videoId}
                opts={opts}
                iframeClassName={"youtube-iframe"}
                onReady={onPlayerReady}
            />
        </>
    )
    /* eslint-enable */
}

export default YouTuber
