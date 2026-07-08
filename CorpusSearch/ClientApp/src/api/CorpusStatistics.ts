export type Statistics = {
    documentCount: number
    manxWordCount: number
    uniqueManxWordCount: number
}

export const getCorpusStatistics = async (
    signal?: AbortSignal,
): Promise<Statistics> => {
    const response = await fetch("statistics", { signal })
    // TODO: Validation
    return (await response.json()) as Statistics
}
