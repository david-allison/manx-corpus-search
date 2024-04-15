/*The name has nothing to do with bits and bytes, but refers to the term
"bit player" meaning an actor playing a minor part, 
since this is kind of a no-frills YouTube player*/

import "./DocumentView.css"
import React, {useRef, useState} from "react"
import YouTube, {YouTubeEvent, YouTubePlayer, YouTubeProps} from "react-youtube"
import useInterval from "../vendor/use-interval/UseInterval"

function Video({vId="rLEBp8R1_XA"}) {
    const r_player = useRef<YouTubePlayer>(null)
    const [dur  , setDur  ] = useState(0)
    const [ptime, setPtime] = useState(-1)
    const [time0, setTime0] = useState(-1)
    const [ktime, setKtime] = useState(-1)
    
    const [video, setVideoId] = useState(vId)
    const [startTime, setStartTime  ] = useState(50)
    const [endTime, setEndTime] = useState(60)

    const [loop, setLoop] = useState(false)

    /* eslint-disable @typescript-eslint/no-unsafe-argument,@typescript-eslint/no-unsafe-call,@typescript-eslint/no-unsafe-member-access,@typescript-eslint/no-unsafe-assignment */
    useInterval(() => {
        if (!loop) return
        const vt = r_player.current.getCurrentTime()
        if (!endTime || endTime <= startTime || endTime >= dur) return
        if (vt >= endTime) {
            restart()
        }
    }, 10)

    const onStateChange = (event : YouTubeEvent) => {
       if (loop && event.data === 0 ) restart()
    }

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
        setStartTime(constrained_val(parseFloat(e.target.value)) )
    }
    const onEChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setEndTime(constrained_val(parseFloat(e.target.value)) )
    }
    const onIdChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setVideoId(e.target.value)
    }
    const restart = () => {
        r_player.current.seekTo(startTime)
        r_player.current.playVideo()
    }
    const onClick = (/*e :MouseEvent<HTMLButtonElement>*/) => {
        setKtime(r_player.current.getCurrentTime())
        restart()
    }
    /* eslint-enable */
    const constrained_val = (n: number) => {
        const newVal = +n.toFixed(2)
        if (isNaN(newVal) || newVal < 0 ) return 0
        if (newVal > dur) return dur
        return newVal
    }
    const alterStartTime = (chg : number) => {
        setStartTime( currentVal => constrained_val(currentVal + chg ))
    }
    const alterEndTime = (chg : number) => {
        setEndTime( currentVal => constrained_val(currentVal + chg ))
    }
    return <>
        <div><YouTube videoId={video} onReady={onReady} onPlay={onPlay} onPause={onPause} onStateChange={onStateChange}/></div>
        <div> Video ID: <input type={"text"} value={video} onChange={onIdChange}/></div>
        <div>
            <span>Loop Start: </span>
            <button title =    "-1s" onClick={() => alterStartTime( -1)}>⏮️</button>
            <button title = "-100ms" onClick={() => alterStartTime(-.1)}>⏪</button>
            <input type={"number"} step={"0.01"} value={startTime > 0 ? startTime : " "}
                   onChange={onSChange}/>
            <button title = "+100ms" onClick={() => alterStartTime( .1)}>⏩️</button>
            <button title =    "+1s" onClick={() => alterStartTime(  1)}>⏭️</button>
        </div>
        <div>
            <span>Loop End: </span>
            <button title =    "-1s" onClick={() => alterEndTime(-1)}>⏮️</button>
            <button title = "-100ms" onClick={() => alterEndTime(-.1)}>⏪</button>
            <input type={"number"} step={"0.01"} value={endTime > 0 ? endTime : " "}
                   onChange={onEChange}/>
            <button title = "+100ms" onClick={() => alterEndTime(.1)}>⏩️</button>
            <button title =    "+1s" onClick={() => alterEndTime(1)}>⏭️</button>
        </div>
        <button onClick={onClick}>Click To Seek and Play</button>
        <div> Loop <input type="checkbox" checked={loop}
                          onChange={() => setLoop(x => !x)}/>
        </div>
        {dur > 0 ? <div>Duration: {dur}</div> : ""}
        {ktime >= 0 && <div>Seek from: {ktime.toFixed(2)}</div>}
        {time0 >= 0 && <div>Started at: {time0.toFixed(2)}</div>}
        {ptime >= 0 && <div> Paused at: {ptime.toFixed(2)}</div>}
    </>

}

export const BitPlayer = () => {
    return (<div className="BitPlayer">
        <header className="BitPlayer-header">
        </header>
        <Video/>
    </div>)
}
export default BitPlayer