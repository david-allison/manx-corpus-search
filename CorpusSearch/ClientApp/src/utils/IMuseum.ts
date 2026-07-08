import { isUrl } from "./Url"

/**
 * The URL to be used for `iMuseum` metadataL
 *
 * - "MS 14725" => https://imuseum.im/search//all/search?tab=all&view=&subm=s&term=%22MS+14725%22
 * - a URL - returned as-is
 */
export const iMuseumUrl = (value: string): string => {
    const reference = value.trim()
    if (isUrl(reference)) {
        return reference
    }
    const query = new URLSearchParams({
        tab: "all",
        view: "",
        subm: "s",
        term: `"${reference}"`,
    })
    return `https://imuseum.im/search//all/search?${query.toString()}`
}
