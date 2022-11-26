/** Returns the selected word or phrase */
export const getSelectedWordOrPhrase = (selection: Selection) => {
    const range = selection.getRangeAt(0).cloneRange() // clone to ensure we don't modify selection
    const node = selection.anchorNode

    if (node == null) {
        return null
    }

    setRangeStartOffset(range, node)
    setRangeEndOffset(range, node)

    return range.toString();
}

const setRangeStartOffset = (range: Range, node: Node) => {
    while (range.toString().indexOf(" ") != 0 && range.startOffset > 0) {
        range.setStart(node,(range.startOffset -1))
    }
    if (range.startOffset != 0 || range.toString()[0] == " ") {
        // if we reached a space, ignore it
        range.setStart(node, range.startOffset + 1)
    }
}

const setRangeEndOffset = (range: Range, node: Node) => {
    try {
        do {
            range.setEnd(node,range.endOffset + 1)
        } while(range.toString().indexOf(" ") == -1 && range.toString().trim() != "")
    } catch (e) {
        // TODO: find a less hacky way to end if at the end
    }
}