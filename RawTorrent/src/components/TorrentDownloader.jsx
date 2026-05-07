import { useState, useEffect, useRef } from 'react'
const FileIcon = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M13 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z" /><polyline points="13 2 13 9 20 9" /></svg>
)

const DownloadIcon = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" /><polyline points="7 10 12 15 17 10" /><line x1="12" y1="15" x2="12" y2="3" /></svg>
)

const FolderIcon = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"></path></svg>
)
const PathPickerModal = ({ isOpen, onClose, onSelect, initialPath, API_BASE, showNotification, setOutputDir, initialMetadata }) => {
  const [drives, setDrives] = useState([])
  const [entries, setEntries] = useState([])
  const [currentPath, setCurrentPath] = useState(initialPath || 'C:\\')
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (isOpen) {
      fetchDrives()
      loadPath(initialPath || 'C:\\')
    }
  }, [isOpen, initialPath])

  const fetchDrives = async () => {
    try {
      const res = await fetch(`${API_BASE}/list-drives`)
      if (res.ok) setDrives(await res.json())
    } catch (err) {
      console.error("Failed to fetch drives", err)
    }
  }

  const loadPath = async (targetPath) => {
    setLoading(true)
    try {
      const res = await fetch(`${API_BASE}/list-directories?path=${encodeURIComponent(targetPath)}`)
      if (res.ok) {
        const data = await res.json()
        setEntries(data.entries)
        setCurrentPath(data.currentPath)
      } else {
        const err = await res.text()
        showNotification("Error loading path: " + err, "error")
      }
    } catch (err) {
      showNotification("Failed to connect to backend", "error")
    } finally {
      setLoading(false)
    }
  }

  const goUp = () => {
    const parts = currentPath.split(/[\/\\]/).filter(Boolean)
    if (parts.length > 0) {
      parts.pop()
      const parent = parts.join('\\') + (parts.length > 0 ? '\\' : '')
      loadPath(parent || 'C:\\')
    }
  }

  if (!isOpen) return null

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content picker-modal" onClick={e => e.stopPropagation()}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h2 style={{ margin: 0, fontSize: '1.8rem', color: '#6366f1' }}>Select folder</h2>
          <button className="secondary" onClick={onClose} style={{ border: 'none', background: 'none', fontSize: '1.5rem', padding: '4px' }}>✕</button>
        </div>

        {initialMetadata && (
          <div style={{ background: 'rgba(99, 102, 241, 0.05)', padding: '16px', borderRadius: '16px', marginTop: '20px', border: '1px solid rgba(99, 102, 241, 0.1)' }}>
            <div style={{ fontWeight: '600', color: '#a78bfa', marginBottom: '4px' }}>{initialMetadata.name}</div>
            <div style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>Size: {(initialMetadata.size / 1024 / 1024).toFixed(2)} MB</div>
          </div>
        )}

        <div style={{ marginTop: '20px' }}>
          <label style={{ color: 'var(--text-muted)', fontSize: '0.9rem', display: 'block', marginBottom: '8px', fontWeight: '500' }}>Folder path</label>
          <input 
            type="text" 
            value={currentPath} 
            onChange={(e) => setCurrentPath(e.target.value)} 
            onKeyDown={(e) => e.key === 'Enter' && loadPath(currentPath)}
            style={{ background: 'rgba(255,255,255,0.03)', borderRadius: '12px' }}
          />
        </div>

        <div className="picker-layout">
          <div className="picker-sidebar">
            <div style={{ color: 'var(--text-muted)', fontSize: '0.75rem', fontWeight: '700', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '12px', paddingLeft: '12px' }}>Devices</div>
            {drives.map(drive => (
              <div 
                key={drive.path} 
                className={`drive-item ${currentPath.startsWith(drive.path) ? 'active' : ''}`}
                onClick={() => loadPath(drive.path)}
              >
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="2" y="2" width="20" height="8" rx="2" ry="2"></rect><rect x="2" y="14" width="20" height="8" rx="2" ry="2"></rect><line x1="6" y1="6" x2="6" y2="6"></line><line x1="6" y1="18" x2="6" y2="18"></line></svg>
                {drive.name}
              </div>
            ))}
          </div>
          <div className="picker-main">
            <div className="picker-list">
              <div className="back-btn" onClick={goUp}>
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="19" y1="12" x2="5" y2="12"></line><polyline points="12 19 5 12 12 5"></polyline></svg>
                Back
              </div>
              {loading ? (
                <div style={{ padding: '60px', textAlign: 'center', color: 'var(--text-muted)', fontWeight: '500' }}>Scanning files...</div>
              ) : (
                entries.map(entry => (
                  <div 
                    key={entry.path} 
                    className={`dir-item ${currentPath === entry.path ? 'active' : ''}`}
                    style={{ opacity: entry.isDirectory ? 1 : 0.4, borderBottom: '1px solid rgba(255,255,255,0.03)' }}
                    onClick={() => entry.isDirectory && setCurrentPath(entry.path)}
                    onDoubleClick={() => entry.isDirectory && loadPath(entry.path)}
                  >
                    {entry.isDirectory ? <FolderIcon /> : <FileIcon />}
                    <span style={{ flex: 1 }}>{entry.name}</span>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '32px' }}>
          <button className="secondary" style={{ padding: '12px 32px', borderRadius: '14px' }} onClick={onClose}>Cancel</button>
          <button className="action-btn" style={{ padding: '12px 48px', borderRadius: '14px', fontSize: '1rem' }} onClick={() => {
            setOutputDir(currentPath)
            onSelect(currentPath)
            onClose()
          }}>Select</button>
        </div>
      </div>
    </div>
  )
}


function TorrentDownloader() {
  const [torrents, setTorrents] = useState([])
  const [input, setInput] = useState('')
  const [notification, setNotification] = useState(null)
  const [outputDir, setOutputDir] = useState('C:\\Users\\Dell\\Downloads')
  const API_BASE = 'http://localhost:5000/api/torrents'
  const [clientId, setClientId] = useState('')
  const [showPathPicker, setShowPathPicker] = useState(false)
  const [metadata, setMetadata] = useState(null)
  const [isFetchingMetadata, setIsFetchingMetadata] = useState(false)
  const dirInputRef = useRef(null)
  useEffect(() => {
    let id = localStorage.getItem('rawtorrent_clientId')
    if (!id) {
      id = crypto.randomUUID()
      localStorage.setItem('rawtorrent_clientId', id)
    }
    console.log("[App] Client ID initialized:", id)
    setClientId(id)

  }, [])

  const showNotification = (message, type = 'info') => {
    setNotification({ message, type })
    setTimeout(() => setNotification(null), 5000)
  }

  useEffect(() => {
    const interval = setInterval(async () => {
      if (!clientId) return
      try {
        const res = await fetch(`${API_BASE}?clientId=${clientId}`)
        const data = await res.json()
        console.log(`[App] Polled ${data.length} active torrents`)
        setTorrents(data)


      } catch (err) {
        console.error("Failed to fetch torrents", err)
      }
    }, 1000)
    return () => clearInterval(interval)
  }, [clientId])


  const startDownload = async (customPath) => {
    if (!input) return

    const isMagnet = input.startsWith('magnet:')
    const body = {
      [isMagnet ? 'magnet' : 'path']: input,
      outputDir: customPath || outputDir,
      clientId: clientId
    }

    try {
      const res = await fetch(`${API_BASE}/download`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
      })
      if (res.ok) {
        setInput('')
        showNotification("Download started successfully", "success")
      } else {
        const error = await res.text()
        showNotification("Failed: " + error, "error")
      }
    } catch (err) {
      showNotification("Failed to connect to backend", "error")
    }
  }

  const handleFolderChange = (e) => {
    const files = e.target.files
    if (files.length > 0) {
      // Use the name of the selected folder as the path (standard browser behavior)
      const path = files[0].path || files[0].webkitRelativePath.split('/')[0]
      if (path) {
        setOutputDir(path)
        startDownload(path)
      }
    }
  }

  const handleFileChange = (e) => {
    const file = e.target.files[0]
    if (file) {
      // For local apps, we can sometimes get the path if it's not a real browser
      // but here we'll just use the name if it's a relative path or handle upload
      // For now, let's just use the name or the path if available
      setInput(file.path || file.name)
    }
  }





  const stopTorrent = async (id) => {
    await fetch(`${API_BASE}/${id}/stop`, { method: 'POST' })
  }

  const resumeTorrent = async (id) => {
    await fetch(`${API_BASE}/${id}/resume`, { method: 'POST' })
  }

  const removeTorrent = async (id) => {
    try {
      const res = await fetch(`${API_BASE}/${id}`, { method: 'DELETE' })
      if (res.ok) {
        showNotification("Torrent removed", "success")
      } else {
        const err = await res.text()
        showNotification("Failed to remove: " + err, "error")
      }

    } catch {
      showNotification("Error connecting to backend", "error")
    }
  }

  const openFolder = async (path) => {
    if (!path) return
    try {
      const res = await fetch(`${API_BASE}/open-folder?path=${encodeURIComponent(path)}`, { method: 'POST' })
      if (!res.ok) {
        const err = await res.text()
        showNotification("Could not open folder: " + err, "error")
      }

    } catch {
      showNotification("Error connecting to backend", "error")
    }
  }

  const browseNative = async () => {
    try {
      const res = await fetch(`${API_BASE}/browse-native`, { method: 'POST' })
      if (res.ok) {

        const data = await res.json()
        if (data.path) setOutputDir(data.path)
      } else {
        const err = await res.text()
        showNotification("Folder browser error: " + err, "error")
      }
    } catch {
      showNotification("Failed to connect to folder browser", "error")
    }
  }


  const browseFileNative = async () => {
    try {
      const res = await fetch(`${API_BASE}/browse-file-native`, { method: 'POST' })
      if (res.ok) {

        const data = await res.json()
        if (data.path) setInput(data.path)
      } else {
        const err = await res.text()
        showNotification("File browser error: " + err, "error")
      }
    } catch {
      showNotification("Failed to connect to file browser", "error")
    }
  }


  const clearAllData = async () => {
    if (window.confirm("Are you sure you want to delete ALL data? This cannot be undone.")) {
      try {
        const res = await fetch(`${API_BASE}/clear-all?clientId=${clientId}`, { method: 'DELETE' })
        if (res.ok) showNotification("All data cleared", "success")
        else showNotification("Failed to clear data", "error")
      } catch {

        showNotification("Error connecting to backend", "error")
      }
    }
  }


  return (
    <div className="animate-fade-in">
      {notification && (
        <div className={`notification notification-${notification.type}`}>
          {notification.message}
        </div>
      )}
      <header style={{ marginBottom: '40px', display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div>
          <h1>RawTorrent</h1>
          <p style={{ color: 'var(--text-muted)', fontSize: '1.1rem' }}>Powerful, clean, and local torrent management.</p>
        </div>
        <button className="secondary" style={{ color: '#f87171', borderColor: 'rgba(248, 113, 113, 0.2)' }} onClick={clearAllData}>
          Clear All Data
        </button>
      </header>
      <div className="glass-panel">
        <h3 style={{ marginBottom: '20px' }}>New Download</h3>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
            <div style={{ flex: 1 }}>
              <input
                type="text"
                value={input}
                onChange={(e) => setInput(e.target.value)}
                placeholder="Paste Magnet URI or File Path"
              />
            </div>
            <div style={{ position: 'relative' }}>
              <button className="secondary" onClick={browseFileNative} title="Select .torrent file">
                <FileIcon /> Select File
              </button>
            </div>
          </div>
          <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
            <button className="action-btn" style={{ flex: 1 }} onClick={async () => {
              if (!input) {
                showNotification("Please provide a magnet link or torrent file", "error")
                return
              }
              setIsFetchingMetadata(true)
              try {
                const res = await fetch(`${API_BASE}/metadata`, {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ [input.startsWith('magnet:') ? 'magnet' : 'path']: input })
                })
                if (res.ok) {
                  const data = await res.json()
                  setMetadata(data)
                  setShowPathPicker(true)
                } else {
                  const err = await res.text()
                  showNotification("Failed to fetch metadata: " + err, "error")
                }
              } catch (err) {
                showNotification("Error connecting to backend", "error")
              } finally {
                setIsFetchingMetadata(false)
              }
            }}>
              {isFetchingMetadata ? "Fetching Metadata..." : <><DownloadIcon /> Start Download</>}
            </button>
          </div>
        </div>
      </div>

      <PathPickerModal
        isOpen={showPathPicker}
        onClose={() => setShowPathPicker(false)}
        onSelect={(path) => startDownload(path)}
        initialPath={outputDir}
        API_BASE={API_BASE}
        showNotification={showNotification}
        setOutputDir={setOutputDir}
        initialMetadata={metadata}
      />
      <div style={{ marginTop: '48px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
          <h2>Active Downloads</h2>
          <span style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>{torrents.length} active</span>
        </div>

        {torrents.map(t => (
          <div key={t.id} className="torrent-card animate-fade-in">
            <div style={{ flex: 1 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '8px' }}>
                <strong style={{ fontSize: '1.1rem' }}>{t.name}</strong>
                <span className={`status-badge status-${t.status.toLowerCase().split(':')[0]}`}>
                  {t.status.includes(':') ? t.status.split(':')[0] : t.status}
                </span>
              </div>
              {t.status.toLowerCase().startsWith('error') && (
                <div style={{ color: '#f87171', fontSize: '0.85rem', marginBottom: '8px', fontWeight: '500' }}>
                  {t.status}
                </div>
              )}
              <div style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>
                {t.completedPieces} / {t.totalPieces} pieces completed • {t.activePeers} active peers
              </div>

              <div className="progress-container">
                <div className="progress-bar" style={{ width: `${t.progress}%` }}></div>
              </div>
              <div style={{ marginTop: '8px', textAlign: 'right', fontSize: '0.85rem', fontWeight: '600', color: 'var(--primary)' }}>
                {t.progress.toFixed(1)}%
              </div>
            </div>
            <div style={{ display: 'flex', gap: '8px', marginLeft: '24px' }}>
              {t.status === 'Stopped' || t.status === 'Error' ? (
                <button className="secondary" onClick={() => resumeTorrent(t.id)}>Resume</button>
              ) : (
                <button className="secondary" onClick={() => stopTorrent(t.id)}>Stop</button>
              )}
              {t.outputDir && (
                <button className="secondary" onClick={() => openFolder(t.outputDir)} title="Open Download Folder">
                  <FolderIcon />
                </button>
              )}
              <button className="secondary" style={{ color: '#f87171' }} onClick={() => removeTorrent(t.id)}>Remove</button>
            </div>
          </div>
        ))}

        {torrents.length === 0 && (
          <div style={{ textAlign: 'center', padding: '60px', color: 'var(--text-muted)', background: 'rgba(255,255,255,0.02)', borderRadius: '24px', border: '1px dashed var(--glass-border)' }}>
            <p>No active downloads at the moment.</p>
          </div>
        )}
      </div>
    </div>
  )
}
export default TorrentDownloader
