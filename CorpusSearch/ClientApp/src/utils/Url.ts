/** True if {@link text} is a web address */
export const isUrl = (text: string): boolean => /^https?:\/\//.test(text)
