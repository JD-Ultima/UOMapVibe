using System.Text;
using UOMapVibe.Core.Models;

namespace UOMapVibe.Core.MulFiles;

/// <summary>
/// Parses tiledata.mul to extract item names, flags, and heights.
/// Format auto-detected from file size: >= 3,188,736 bytes = 64-bit flags.
/// </summary>
public sealed class TileDataReader
{
    private readonly ItemInfo[] _items;
    private readonly LandInfo[] _lands;

    public IReadOnlyList<ItemInfo> Items => _items;
    public IReadOnlyList<LandInfo> Lands => _lands;

    public TileDataReader(string tiledataPath)
    {
        if (!File.Exists(tiledataPath))
            throw new FileNotFoundException("tiledata.mul not found", tiledataPath);

        using var fs = new FileStream(tiledataPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var bin = new BinaryReader(fs);

        bool is64BitFlags;
        const int landLength = 0x4000;
        int itemLength;

        if (fs.Length >= 3_188_736)
        {
            is64BitFlags = true;
            itemLength = 0x10000;
        }
        else if (fs.Length >= 1_644_544)
        {
            is64BitFlags = false;
            itemLength = 0x8000;
        }
        else
        {
            is64BitFlags = false;
            itemLength = 0x4000;
        }

        _lands = new LandInfo[landLength];
        _items = new ItemInfo[itemLength];

        Span<byte> nameBuffer = stackalloc byte[20];

        // Read land data
        for (int i = 0; i < landLength; i++)
        {
            // Skip group header every 32 entries
            if (is64BitFlags ? (i == 1 || (i > 0 && (i & 0x1F) == 0)) : (i & 0x1F) == 0)
                bin.ReadInt32();

            var flags = (TileFlag)(is64BitFlags ? bin.ReadUInt64() : bin.ReadUInt32());
            bin.ReadInt16(); // textureID

            bin.Read(nameBuffer);
            var name = ReadNullTerminatedString(nameBuffer);

            _lands[i] = new LandInfo { TileId = i, Name = name, Flags = flags };
        }

        // Read item data
        for (int i = 0; i < itemLength; i++)
        {
            if ((i & 0x1F) == 0)
                bin.ReadInt32(); // group header

            var flags = (TileFlag)(is64BitFlags ? bin.ReadUInt64() : bin.ReadUInt32());
            int weight = bin.ReadByte();
            int quality = bin.ReadByte();
            int animation = bin.ReadUInt16();
            bin.ReadByte(); // skip
            int quantity = bin.ReadByte();
            bin.ReadInt32(); // skip 4 bytes
            bin.ReadByte(); // skip
            int value = bin.ReadByte();
            int height = bin.ReadByte();

            bin.Read(nameBuffer);
            var name = ReadNullTerminatedString(nameBuffer);

            _items[i] = new ItemInfo
            {
                ItemId = i,
                Name = name,
                Flags = flags,
                Weight = weight,
                Height = height,
                Quality = quality,
                Animation = animation,
                Quantity = quantity,
                Value = value
            };
        }
    }

    public ItemInfo? GetItem(int itemId)
    {
        if (itemId < 0 || itemId >= _items.Length)
            return null;
        return _items[itemId];
    }

    public LandInfo? GetLand(int tileId)
    {
        if (tileId < 0 || tileId >= _lands.Length)
            return null;
        return _lands[tileId];
    }

    public List<ItemInfo> SearchItems(string query)
    {
        return _items
            .Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(i.Name))
            .ToList();
    }

    private static string ReadNullTerminatedString(Span<byte> buffer)
    {
        int end = buffer.IndexOf((byte)0);
        if (end < 0) end = buffer.Length;
        return Encoding.ASCII.GetString(buffer[..end]);
    }
}
