public class FileType
{
    public string Name { get; }
    public string Extension { get; }
    public byte?[] MagicSequence { get; }
    public int MaximumStartLocation { get; }
    public int MinimumStartLocation { get; }

    public FileType(string name, string extension, byte?[] magicSequence)
    {
        Name = name;
        Extension = extension;
        MagicSequence = magicSequence;
        MaximumStartLocation = 0;
    }

    public FileType(string name, string extension, byte?[] magicSequence, int maximumStartLocation)
        : this(name, extension, magicSequence, 0, maximumStartLocation)
    {
    }

    public FileType(string name, string extension, byte?[] magicSequence, int minimumStartLocation, int maximumStartLocation)
    {
        if (minimumStartLocation < 0 || maximumStartLocation < minimumStartLocation)
            throw new ArgumentOutOfRangeException(nameof(maximumStartLocation));
        Name = name;
        Extension = extension;
        MagicSequence = magicSequence;
        MaximumStartLocation = maximumStartLocation;
        MinimumStartLocation = minimumStartLocation;
    }

    internal bool Matches(byte[] content)
    {
        if (MagicSequence.Length == 0) return false;
        int last = Math.Min(MaximumStartLocation, content.Length - MagicSequence.Length);
        for (int offset = MinimumStartLocation; offset <= last; offset++)
        {
            bool match = true;
            for (int i = 0; i < MagicSequence.Length; i++)
                if (MagicSequence[i] is byte value && content[offset + i] != value)
                {
                    match = false;
                    break;
                }
            if (match) return true;
        }
        return false;
    }
}
