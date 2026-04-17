# RawTorrent Engine

RawTorrent is a specialized, lightweight, and aggressively optimized BitTorrent engine built natively from scratch in C# (.NET 10). It provides a lightning-fast, highly concurrent downloading protocol while keeping system overhead to a minimum. 

With built-in support for both `.torrent` files and strictly protocol-compliant `magnet:` links, RawTorrent is designed to be the ultimate headless desktop client.

## ✨ Key Features

* **Intelligent Swarm Logic**
  * **Rarest-First Piece Selection:** Prioritizes downloading the rarest pieces among connected peers first, maximizing overall swarm health and speed.
  * **Optimized Endgame Mode:** Eliminates the notorious "99% stall." When nearing completion, the engine sends non-exclusive redundant requests to all fast peers to retrieve the final missing pieces instantly while strictly timing out unresponsive connections.
  
* **Advanced Peer Discovery**
  * **DHT (Distributed Hash Table):** Capable of finding peers serverlessly via decentralized node routing.
  * **PEX (Peer Exchange):** `ut_pex` extension support allows the engine to trade known peers directly with actively connected clients.
  * **Multi-Tracker Support:** Robust UDP and HTTP tracker fetching with automatic failovers.

* **Magnet Link First-Class Support**
  * Effortlessly resolves `magnet:?xt=urn:btih:..` URIs.
  * Rapidly fetches `.torrent` metadata streams dynamically via the `ut_metadata` extension.

* **High-Performance I/O**
  * Fully asynchronous `Task`-based connection architecture.
  * Block request pipelining to saturate bandwidth.
  * Thread-safe direct-to-disk multi-file piece assembly and SHA1 validation.

## 🚀 Getting Started

Ensure you have the [.NET SDK](https://dotnet.microsoft.com/download) installed natively. 

### Interactive Mode
To run the client with its interactive console UI, simply execute:
```bash
dotnet run
