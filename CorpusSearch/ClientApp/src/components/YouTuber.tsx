import { Ref, useImperativeHandle, useRef } from "react"
import YouTube, { YouTubePlayer, YouTubeProps } from "react-youtube"
import "./ComparisonTable.css"

export type Player = {
    seek: (time: number) => void
    getCurrentTime: () => number
}

// event.target needs work, as does 'opts'
/* eslint-disable @typescript-eslint/no-unsafe-return, @typescript-eslint/no-unsafe-call, @typescript-eslint/no-unsafe-member-access, @typescript-eslint/no-unsafe-assignment */
const YouTuber = ({ videoId, ref }: { videoId: string; ref?: Ref<Player> }) => {
    const player = useRef<YouTubePlayer>(null)

    const seek = (time: number) => {
        if (player.current == undefined) {
            return
        }
        player.current.seekTo(time, true)
        player.current.playVideo()
    }

    useImperativeHandle(ref, () => ({
        seek,
        getCurrentTime: () => player.current?.getCurrentTime() ?? 0,
    }))

    const onPlayerReady: YouTubeProps["onReady"] = (event) => {
        player.current = event.target
    }

    const opts: YouTubeProps["opts"] = {
        //width: "60%",
        playerVars: {
            // https://developers.google.com/youtube/player_parameters#autoplay
            autoplay: 0,
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
