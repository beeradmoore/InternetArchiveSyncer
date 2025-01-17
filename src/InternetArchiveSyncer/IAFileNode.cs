using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace InternetArchiveSyncer;

public class IAFileNode
{
    public string Name { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long ModifiedTime { get; set; } = 0;
    public long Size { get; set; } = 0;
    public string MD5 { get; set; } = string.Empty;
    public string CRC32 { get; set; } = string.Empty;
    public string SHA1 { get; set; } = string.Empty;

    public static IAFileNode FromXmlNode(XmlNode node)
    {
        if (node.Attributes is null)
        {
            throw new Exception("Attributes property is null.");
        }

        var nameAttribute = node.Attributes["name"]?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(nameAttribute))
        {
            throw new Exception("Attribute name is null or empty.");
        }

        var fileNode = new IAFileNode();
        fileNode.Path = System.IO.Path.GetDirectoryName(nameAttribute) ?? string.Empty;
        fileNode.Filename = System.IO.Path.GetFileName(nameAttribute);
        fileNode.Name = System.IO.Path.GetFileNameWithoutExtension(fileNode.Filename);

        foreach (XmlNode childNode in node.ChildNodes)
        {
            var value = childNode.InnerText ?? string.Empty;
            if (string.IsNullOrEmpty(value))
            {
                Log.Error($"Value for node {childNode.Name} is null or empty.");
                continue;
            }

            if (childNode.Name == "mtime")
            {
                if (long.TryParse(value, out long result))
                {
                    fileNode.ModifiedTime = result;
                }
                else
                {
                    Log.Error($"Could not convert value of {childNode.Name} ({value}) to ulong.");
                }
            }
            else if (childNode.Name == "size")
            {
                if (long.TryParse(value, out long result))
                {
                    fileNode.Size = result;
                }
                else
                {
                    Log.Error($"Could not convert value of {childNode.Name} ({value}) to ulong.");
                }
            }
            else if (childNode.Name == "md5")
            {
                fileNode.MD5 = value;
            }
            else if (childNode.Name == "crc32")
            {
                fileNode.CRC32 = value;
            }
            else if (childNode.Name == "sha1")
            {
                fileNode.SHA1 = value;
            }
            else if (childNode.Name == "format")
            {
                // NO-OP
            }
            else if (childNode.Name == "private")
            {
                // NO-OP
            }
            else if (childNode.Name == "filecount")
            {
                // NO-OP
            }
            else if (childNode.Name == "viruscheck")
            {
                // NO-OP
            }
            else if (childNode.Name == "bitrate")
            {
                // NO-OP
            }
            else if (childNode.Name == "length")
            {
                // NO-OP
            }
            else if (childNode.Name == "original")
            {
                // NO-OP
            }
            else if (childNode.Name == "rotation")
            {
                // NO-OP
            }
            else if (childNode.Name == "summation")
            {
                // NO-OP
            }
            else if (childNode.Name == "btih")
            {
                // NO-OP
            }

            else if (childNode.Name == "width")
            {
                // NO-OP
            }

            else if (childNode.Name == "height")
            {
                // NO-OP
            }
            else
            {
                Log.Warning($"Unknown node: {childNode.Name}.");
            }
        }

        return fileNode;
    }
}
