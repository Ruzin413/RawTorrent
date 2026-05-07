# RawTorrent Suite

RawTorrent is a high-performance, lightweight BitTorrent suite featuring an aggressively optimized C# (.NET 10) engine and a modern React-based web interface. It provides a lightning-fast, highly concurrent downloading experience with minimal system overhead.

With built-in support for both `.torrent` files and protocol-compliant `magnet:` links, RawTorrent is designed to be the ultimate headless desktop client with a premium web UI.

## ✨ Key Features

*   **Modern Web UI:** Beautiful, responsive React interface for managing torrents, monitoring progress, and configuring download paths.
*   **Intelligent Swarm Logic:**
    *   **Rarest-First Piece Selection:** Prioritizes downloading the rarest pieces first, maximizing swarm health.
    *   **Optimized Endgame Mode:** Eliminates "99% stalls" by redundantly requesting final pieces from fast peers.
*   **Advanced Peer Discovery:**
    *   **DHT (Distributed Hash Table):** Find peers serverlessly via decentralized routing.
    *   **PEX (Peer Exchange):** Trade known peers directly with connected clients.
    *   **Multi-Tracker Support:** Robust UDP and HTTP tracker fetching with automatic failovers.
*   **Magnet Link Support:** Rapidly fetches metadata streams via the `ut_metadata` extension.
*   **High-Performance I/O:** Fully asynchronous `Task`-based architecture with block request pipelining and thread-safe disk assembly.

## 🚀 Getting Started

### Prerequisites

*   [.NET 10 SDK](https://dotnet.microsoft.com/download)
*   [Node.js (v18+)](https://nodejs.org/) & npm

### 1. Backend Setup (TorServices)

The backend is an ASP.NET Core API that handles the torrent engine logic.

```bash
cd TorServices/TorServices
dotnet run
```
The server will start at `http://localhost:5000` (or `https://localhost:5001`).

### 2. Frontend Setup (RawTorrent)

The frontend is a Vite + React application.

```bash
cd RawTorrent
npm install
npm run dev
```
The UI will be available at `http://localhost:5173`.

## 🛠 Architecture

*   **Backend:** ASP.NET Core / C# - Handles peer connections, piece management, and file I/O.
*   **Frontend:** React / Vite / Tailwind CSS - Provides the user interface and interacts with the Backend API.
*   **Database:** SQLite (via Entity Framework Core) - Persists torrent state and progress.
