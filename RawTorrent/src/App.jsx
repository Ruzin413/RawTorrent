import TorrentDownloader from './components/TorrentDownloader'
import DownloadHistory from './components/DownloadHistory'
import './App.css'

function App() {
  return (
    <div className="App">
      <TorrentDownloader />
      <hr style={{ margin: '40px 0' }} />
      <DownloadHistory />
    </div>
  )
}
export default App
