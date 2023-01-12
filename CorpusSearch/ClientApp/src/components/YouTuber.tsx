import React, {ForwardedRef, useRef} from "react"
import YouTube, {YouTubePlayer, YouTubeProps} from "react-youtube"
import {setRef} from "../utils/ForwardRef"
import "./ComparisonTable.css"

export type Player = {
    seek: (time: number) => void
    getCurrentTime: () => number
}

// event.target needs work, as does 'opts'
/* eslint-disable @typescript-eslint/no-unsafe-return, @typescript-eslint/no-unsafe-call, @typescript-eslint/no-unsafe-member-access, @typescript-eslint/no-unsafe-assignment */
const YouTuber = React.forwardRef((props: {videoId: string}, forwardedRef : ForwardedRef<Player>) => {
    const ref = useRef<YouTubePlayer>(null)
    
    const onPlayerReady: YouTubeProps["onReady"] = (event) => {
        ref.current = event.target 
        setRef(forwardedRef, { seek: seek, getCurrentTime: () => event.target.getCurrentTime() })
    }

    const opts: YouTubeProps["opts"] = {
        //width: "60%",
        playerVars: {
            // https://developers.google.com/youtube/player_parameters#autoplay
            autoplay: 0,
        },
    }
    
    const seek = (time: number) => {
        if (ref.current == undefined) {
            return
        }
        ref.current.seekTo(time, true)
        ref.current.playVideo()
    }


    return <>
        <YouTube videoId={props.videoId} opts={opts} iframeClassName={"youtube-iframe"} onReady={onPlayerReady} />
    </>
    /* eslint-enable */
})


export default YouTuber