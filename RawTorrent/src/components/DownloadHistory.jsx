import { useState, useEffect } from 'react'

const FolderIcon = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"></path></svg>
)

function DownloadHistory() {
  const [history, setHistory] = useState([])
  const API_BASE = 'http://localhost:5000/api/torrents/history'
  const [clientId, setClientId] = useState('')


  useEffect(() => {
    let id = localStorage.getItem('rawtorrent_clientId')
    if (id) setClientId(id)
  }, [])

  useEffect(() => {
    const fetchHistory = async () => {
      if (!clientId) return
      try {
        const res = await fetch(`${API_BASE}?clientId=${clientId}`)
        const data = await res.json()
        setHistory(data)
      } catch (err) {
        console.error("Failed to fetch history", err)
      }
    }
    fetchHistory()

    const interval = setInterval(fetchHistory, 5000)
    return () => clearInterval(interval)
  }, [clientId])


  const openFolder = async (path) => {
    if (!path) return
    await fetch(`http://localhost:5000/api/torrents/open-folder?path=${encodeURIComponent(path)}`, { method: 'POST' })
  }

  return (
    <div className="glass-panel animate-fade-in" style={{ marginTop: '40px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div>
          <h2 style={{ marginBottom: '4px' }}>Download History</h2>
          <p style={{ fontSize: '0.9rem', color: 'var(--text-muted)' }}>Previous downloads from all clients</p>
        </div>
        <span className="status-badge" style={{ background: 'rgba(255,255,255,0.05)', color: 'var(--text-muted)' }}>
          {history.length} records
        </span>
      </div>
      
      <div style={{ overflowX: 'auto' }}>
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Status</th>
              <th>Progress</th>
              <th>Client ID</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {history.map(item => (
              <tr key={item.id} className="history-row">
                <td style={{ fontWeight: '500' }}>{item.name}</td>
                <td>
                  <span className={`status-badge status-${item.status.toLowerCase()}`}>
                    {item.status}
                  </span>
                </td>
                <td>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <div className="progress-container" style={{ width: '80px', margin: 0 }}>
                      <div className="progress-bar" style={{ width: `${item.progress}%` }}></div>
                    </div>
                    <span style={{ fontSize: '0.85rem' }}>{item.progress.toFixed(0)}%</span>
                  </div>
                </td>
                <td style={{ fontSize: '0.8rem', color: 'var(--text-muted)', fontFamily: 'monospace' }}>
                  {item.clientId?.substring(0, 8) || 'N/A'}...
                </td>
                <td>
                   {item.outputDir && (
                      <button className="secondary" onClick={() => openFolder(item.outputDir)} style={{ padding: '6px 10px' }} title="Open Download Folder">
                        <FolderIcon />
                      </button>
                    )}
                </td>
              </tr>
            ))}
            {history.length === 0 && (
              <tr>
                <td colSpan="5" style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
                  No download history found.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}

export default DownloadHistory
