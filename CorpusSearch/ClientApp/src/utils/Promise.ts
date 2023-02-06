/** Fix for @typescript-eslint/no-misused-promises: Promise-returning function provided to attribute where a void return was expected */
export function floatingPromiseReturn<ARGS extends unknown[]>(fn: (...args: ARGS) => Promise<unknown>): (...args: ARGS) => void {
    return (...args) => {
        void fn(...args)
    }
}