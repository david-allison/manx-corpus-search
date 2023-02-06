type MatchReference = {
    workIdent: string,
    matchNumber: number,
    manx: string
    matchIndexInLine: number,
    lineNumber: number
}

type MatchQuery = {
    docIdent: string,
    query: string,
    match: number
}

/** Given (document, query, matchIndex):  return a the matched line + information on the match, 
 * so we can display the Key Word in Context (KWIC) to the user 
 * 
 * example: 
 * (Noo Mian, "as", 0) => [matchIndexInLine: 0] "2 Hooar Abraham Isaac, as hooar Isaac Jacob, as hooar..."
 * (Noo Mian, "as", 1) => [matchIndexInLine: 1] "2 Hooar Abraham Isaac, as hooar Isaac Jacob, as hooar..." 
 * (Noo Mian, "as", 3) => [matchIndexInLine: 0] "3 As hooar Judas Phares as Zarah..." 
 * 
 * This data allows handling the KWIC (highlights/alignment of the match):
 * [0] => "2 Hooar Abraham Isaac, **as** hooar Isaac Jacob, as hooar"
 * [1] => "as hooar Isaac Jacob, **as** hooar Jacob Judas as e"
 * [3] => "3 **As** hooar Judas Phares as Zarah"
 */
export const GetMatch = async (params: MatchQuery): Promise<MatchReference> => {
    const response = await fetch(`search/Match/${params.docIdent}/?query=${params.query}&match=${params.match}`)
    return await response.json() as MatchReference
}