namespace TorServices.Core;
public class TorrentMetadata
{
    public string Announce { get; set; }
    public string Name { get; set; }
    public long Length { get; set; }
    public int PieceLength { get; set; }
    public byte[] Pieces { get; set; }
}