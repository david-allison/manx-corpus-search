/*The name has nothing to do with bits and bytes, but refers to the term
"bit player" meaning an actor playing a minor part, 
since this is kind of a no-frills YouTube player*/

import "./DocumentView.css"
import React, {useRef, useState} from "react"
import YouTube, {YouTubePlayer, YouTubeProps} from "react-youtube"
import useInterval from "../vendor/use-interval/UseInterval"

function Video({vId="rLEBp8R1_XA"}) {
    const r_player = useRef<YouTubePlayer>(null)
    const [dur  , setDur  ] = useState(0)
    const [ptime, setPtime] = useState(-1)
    const [time0, setTime0] = useState(-1)
    const [ktime, setKtime] = useState(-1)
    
    const [video, setVideoId] = useState(vId)
    const [startTime, setStartTime  ] = useState("50.00")
    const [endTime, setEndTime] = useState("60.00")

    const [loop, setLoop] = useState(false)

    /* eslint-disable @typescript-eslint/no-unsafe-argument,@typescript-eslint/no-unsafe-call,@typescript-eslint/no-unsafe-member-access,@typescript-eslint/no-unsafe-assignment */
    useInterval(() => {
        if (!loop) return
        const vt = r_player.current.getCurrentTime()
        const endTimeAsFloat = parseFloat(endTime)
        if (!endTimeAsFloat || endTimeAsFloat >= dur) return
        if (vt >= endTimeAsFloat) {
            restart()
        }
    }, 10)

    const onPlay: YouTubeProps["onPlay"] = (event) => {
        setTime0(event.target.getCurrentTime())
        setPtime(-1)
    }
    const onPause : YouTubeProps["onPause"] = (event) => {
        const player = event.target
        setPtime(player.getCurrentTime())
        //console.log("player is", player, "current time is", player.getCurrentTime())
    }
    const onReady: YouTubeProps["onReady"] = (event) => {
        //set the ref for player so visible for other events that don't pass it as target
        r_player.current = event.target
        const the_player = r_player.current
        setDur(the_player.getDuration())
    }
    const onSChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setStartTime(e.target.value)
    }
    const onEChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setEndTime(e.target.value)
    }
    const onChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setVideoId(e.target.value)
    }
    const restart = () => {
        r_player.current.seekTo(parseFloat(startTime))
        r_player.current.playVideo()
    }
    const onClick = (/*e :MouseEvent<HTMLButtonElement>*/) => {
        setKtime(r_player.current.getCurrentTime())
        restart()
    }

    /* eslint-enable */
    return <>
        <div><YouTube videoId={video} onReady={onReady} onPlay = {onPlay} onPause={onPause}/></div>
        <div> Video ID: <input type={"text"} value={video} onChange={onChange}/></div>
        <div> Loop Start: <input type={"number"} step={"0.01"} value={startTime} onChange={onSChange}/></div>
        <div> Loop End: <input type={"number"} step={"0.01"} value={endTime} onChange={onEChange}/></div>
        <button onClick={onClick}>Click To Seek and Play</button>
        <div> Loop <input type="checkbox" checked={loop} onChange={() => setLoop(x => !x)}/></div>
        {dur > 0 ? <div>Duration: {dur}</div> : ""}
        {ktime >= 0 && <div>Seek  from: {ktime.toFixed(2)}</div>}
        {time0 >= 0 && <div>Started at: {time0.toFixed(2)}</div>}
        {ptime >= 0 && <div> Paused at: {ptime.toFixed(2)}</div>}
    </> 

}
export const BitPlayer= () => { 
    return (<div className="BitPlayer">
        <header className="BitPlayer-header">
        </header>
        <Video/>
    </div>)
}
