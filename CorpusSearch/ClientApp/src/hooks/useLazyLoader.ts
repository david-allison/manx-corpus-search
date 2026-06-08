import {DependencyList, useCallback, useEffect, useState} from "react"

/** 
 * Delays a computation via useEffect
 * Might want to change the aPI from returning false
 * */
export const useLazyLoader = <T>(factory: () => T, deps: DependencyList) => {
    const [loading, setLoading] = useState(true)
    const [cachedResult, setCachedResult] = useState<T | false>(false)

    // intentionally memoized via deps
    // eslint-disable-next-line react-hooks/exhaustive-deps
    const result = useCallback(factory, deps)
    
    useEffect(() => {
        setCachedResult(result())
        setLoading(false)
        // result should only be computed once
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [])
    
    return loading ? cachedResult : result()
}