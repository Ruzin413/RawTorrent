namespace TorServices.DHT;

public class DhtNode
{
    public string Ip { get; set; }
    public int Port { get; set; }

    public DhtNode(string ip, int port)
    {
        Ip = ip;
        Port = port;
    }
}