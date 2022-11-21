type DictionaryResponse = string

export const manxDictionaryLookup = async (queryUnsafe: string): Promise<DictionaryResponse> => {
    const query = encodeURIComponent(queryUnsafe)
    const response = await fetch(`api/Dictionary/?lang=gv&word=${query}`)
    // TODO: Validation
    return await response.text() 
}