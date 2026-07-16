/** Returns the selected word or phrase */
export const getSelectedWordOrPhrase = (
    selection: Selection,
): string | null => {
    // a touch tap fires a click without placing a caret: there is no range
    // to expand (use getWordAtPoint instead)
    if (selection.rangeCount == 0) {
        return null
    }

    // a double-click or drag selection: use the selected text as-is
    const selected = selection.toString()
    if (selected != "") {
        return selected
    }

    // a single click places a collapsed caret: expand it to the word around it
    const node = selection.anchorNode
    if (node == null) {
        return null
    }
    return getWordAround(node, selection.anchorOffset)
}

/** Returns the word at a viewport point. A touch tap does not place a caret
 * for getSelectedWordOrPhrase to expand: the word under the finger is derived
 * from the tap position instead. */
export const getWordAtPoint = (x: number, y: number): string | null => {
    const caret = caretFromPoint(x, y)
    if (caret == null) {
        return null
    }
    return getWordAround(caret.node, caret.offset)
}

/** The text position under a viewport point: Firefox implements the standard
 * caretPositionFromPoint; Chrome and Safari the older caretRangeFromPoint */
const caretFromPoint = (
    x: number,
    y: number,
): { node: Node; offset: number } | null => {
    if (document.caretPositionFromPoint != null) {
        const position = document.caretPositionFromPoint(x, y)
        return position == null
            ? null
            : { node: position.offsetNode, offset: position.offset }
    }
    if (document.caretRangeFromPoint != null) {
        const range = document.caretRangeFromPoint(x, y)
        return range == null
            ? null
            : { node: range.startContainer, offset: range.startOffset }
    }
    return null
}

/**
 * Expands a caret position to the whitespace-delimited word around it.
 *
 * A word's letters can be split across highlight/diff elements (e.g.
 * "Ta <mark>çhengey</mark> aym"), so the surrounding line's text nodes are
 * stitched back together before expanding.
 */
const getWordAround = (node: Node, offset: number): string | null => {
    // the nearest <div> is the displayed line (see .doc-line)
    const line = node.parentElement?.closest("div")
    if (line == null) {
        return null
    }

    // a diffed line interleaves two readings: the original's removed text and
    // the correction's added text. Stitch the reading the tap landed on, so
    // both spellings can be looked up. Buttons (note markers, reveal chips)
    // are part of neither.
    const excluded =
        node.parentElement?.closest(".part-removed") != null
            ? ".part-added, button"
            : ".part-removed, button"

    let text = ""
    let caret = -1
    const walker = document.createTreeWalker(line, NodeFilter.SHOW_TEXT)
    for (
        let current = walker.nextNode();
        current != null;
        current = walker.nextNode()
    ) {
        if (current.parentElement?.closest(excluded) != null) {
            continue
        }
        if (current == node) {
            caret = text.length + offset
        }
        text += current.textContent ?? ""
    }
    if (caret < 0) {
        return null // the caret is in text excluded above
    }

    let start = caret
    while (start > 0 && !isWhitespace(text[start - 1])) {
        start--
    }
    let end = caret
    while (end < text.length && !isWhitespace(text[end])) {
        end++
    }
    const word = text.slice(start, end)
    return word == "" ? null : word
}

const isWhitespace = (character: string) => /\s/.test(character)
