/* eslint-disable @typescript-eslint/no-unsafe-argument,@typescript-eslint/no-unsafe-assignment,@typescript-eslint/no-unsafe-member-access,@typescript-eslint/no-unsafe-call */
import "./DocumentView.css"
import React, {Fragment, useState} from "react"
import YouTube, {YouTubeProps} from "react-youtube"

function Video({vId="rLEBp8R1_XA"}) {
    const [ptime, setPtime] = useState(-1)
    const [video, setVideoId] = useState(vId)
    const onPause : YouTubeProps["onPause"] = (event) => {
        const player = event.target
        setPtime(player.getCurrentTime())
    }
    const onChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setVideoId(e.target.value)
    }

    return <><Fragment>
        <div>
            <YouTube videoId={video} onPause={onPause}/>
        </div>
        {ptime >= 0 && <div>Paused at: {ptime.toFixed(1)}</div>}
        Enter video ID:
        <input type="search" id="corpus-bit-player" style = {{flexGrow: 1}}  value = {video} onChange={onChange}/>
    </Fragment></>

}
export const BitPlayer= () => { 
    return (<div className="BitPlayer">
        <header className="BitPlayer-header">
        </header>
        <Video/>
    </div>)
}
