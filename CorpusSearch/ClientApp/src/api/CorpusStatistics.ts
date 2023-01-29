export type Statistics = { documentCount: number, manxWordCount: number }

export const getCorpusStatistics = async (): Promise<Statistics> => {
    const response = await fetch("statistics")
    // TODO: Validation
    return await response.json() as Statistics
}