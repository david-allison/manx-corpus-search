import { usePersistedState } from "../hooks/usePersistedState"

/** Whether the letter listings hide the words no text uses. Remembered: a
 * reader who wants only the spoken language wants it on every letter. */
export const useHideUnattested = () =>
    usePersistedState(
        "dictionary.hideUnattested",
        (stored) => stored === "true",
        String,
    )

/** The words a listing shows under the filter: every chapter's words, or the
 * attested alone, empty chapters gone with their words */
export const visibleChapters = <T extends { words: { attested: boolean }[] }>(
    chapters: T[],
    hideUnattested: boolean,
): T[] =>
    hideUnattested
        ? chapters
              .map((chapter) => ({
                  ...chapter,
                  words: chapter.words.filter((word) => word.attested),
              }))
              .filter((chapter) => chapter.words.length > 0)
        : chapters

/** The checkbox that hides the never-said — and, checked or not, the key to
 * the grey: its name wears the very grey it explains ("unattested words",
 * greyed), which is what makes it a legend rather than a setting. */
export const UnattestedFilter = ({
    hidden,
    onChange,
}: {
    hidden: boolean
    onChange: (hidden: boolean) => void
}) => (
    <label
        className="dict-browse-filter"
        title="Unattested: in no text in the corpus"
    >
        <input
            type="checkbox"
            checked={hidden}
            onChange={(event) => onChange(event.target.checked)}
        />
        {" Hide "}
        <span className="dict-unattested">unattested words</span>
    </label>
)
