/** Whether the app is being served as the dictionary site
 * (dictionary.gaelg.im): one app answers both hosts, and the chrome slims to
 * the dictionary's own — the corpus links belong to the corpus's front door.
 * Any `dictionary.` host counts, so `dictionary.localhost` tries it on a dev
 * machine without touching DNS. */
export const isDictionaryHost = (
    hostname: string = window.location.hostname,
): boolean => hostname.startsWith("dictionary.")
