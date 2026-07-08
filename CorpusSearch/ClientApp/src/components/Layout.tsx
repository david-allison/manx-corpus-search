import { ReactNode } from "react"
import { NavMenu } from "./NavMenu"

export const Layout = (props: {
    onRefresh: () => void
    children: ReactNode
}) => {
    return (
        <div>
            <NavMenu onRefreshState={props.onRefresh} />
            <div className="page-main">
                {props.children}
                <SiteFooter />
            </div>
        </div>
    )
}

const SiteFooter = () => (
    <footer className="site-footer">
        <span>
            Maintained by David Allison · A Manx Language Research Group project
            <br />
            With thanks to all the volunteers who transcribe, translate &amp;
            contribute texts.
        </span>
        <span>
            <a
                href="https://github.com/david-allison/manx-corpus-search"
                target="_blank"
                rel="noreferrer"
            >
                Free &amp; open source on GitHub
            </a>
        </span>
    </footer>
)
