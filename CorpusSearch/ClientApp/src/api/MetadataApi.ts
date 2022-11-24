/* eslint-disable */
type json = any
export type MetadataResponse = json

export const metadataLookup = async (docId: string): Promise<MetadataResponse> => {
    const query = encodeURIComponent(docId)
    const response = await fetch(`api/Metadata/?&docId=${query}`)
    // TODO: Validation
    return await response.json() 
}
/* eslint-enable */