using System.Text;

namespace TorServices.Parser;

public static class BencodeEncoder
{
    public static byte[] EncodeDictionary(Dictionary<string, object> dict)
    {
        List<byte> bytes = new();
        bytes.Add((byte)'d');

        var sortedKeys = dict.Keys.OrderBy(k => k).ToList();

        foreach (var key in sortedKeys)
        {
            bytes.AddRange(EncodeString(key));
            
            object value = dict[key];
            if (value is int or long)
            {
                bytes.AddRange(EncodeInteger(Convert.ToInt64(value)));
            }
            else if (value is string str)
            {
                bytes.AddRange(EncodeString(str));
            }
            else if (value is Dictionary<string, object> childDict)
            {
                bytes.AddRange(EncodeDictionary(childDict));
            }
        }

        bytes.Add((byte)'e');
        return bytes.ToArray();
    }

    private static byte[] EncodeString(string str)
    {
        byte[] strBytes = Encoding.UTF8.GetBytes(str);
        string prefix = $"{strBytes.Length}:";
        byte[] prefixBytes = Encoding.ASCII.GetBytes(prefix);

        List<byte> bytes = new(prefixBytes.Length + strBytes.Length);
        bytes.AddRange(prefixBytes);
        bytes.AddRange(strBytes);
        return bytes.ToArray();
    }

    private static byte[] EncodeInteger(long n)
    {
        return Encoding.ASCII.GetBytes($"i{n}e");
    }
}
