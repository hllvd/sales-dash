import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react'

interface BuildInfo {
  buildId: string
}

interface BuildInfoContextType {
  buildInfo: BuildInfo | null
}

const BuildInfoContext = createContext<BuildInfoContextType | undefined>(undefined)

export const BuildInfoProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [buildInfo, setBuildInfo] = useState<BuildInfo | null>(null)

  useEffect(() => {
    // Public endpoint — no auth needed, fetch once on mount
    fetch('/api/build-info')
      .then((res) => res.json())
      .then((data) => setBuildInfo(data))
      .catch(() => setBuildInfo({ buildId: 'unknown' }))
  }, [])

  return (
    <BuildInfoContext.Provider value={{ buildInfo }}>
      {children}
    </BuildInfoContext.Provider>
  )
}

export const useBuildInfo = () => {
  const context = useContext(BuildInfoContext)
  if (!context) {
    throw new Error('useBuildInfo must be used within a BuildInfoProvider')
  }
  return context
}
