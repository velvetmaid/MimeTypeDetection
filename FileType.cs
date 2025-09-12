public class FileType
{
    public string Name { get; }
    public string Extension { get; }
    public byte?[] MagicSequence { get; }
    public int MaximumStartLocation { get; }

    public FileType(string name, string extension, byte?[] magicSequence)
    {
        Name = name;
        Extension = extension;
        MagicSequence = magicSequence;
        MaximumStartLocation = 0;
    }

    public FileType(string name, string extension, byte?[] magicSequence, int maximumStartLocation)
    {
        Name = name;
        Extension = extension;
        MagicSequence = magicSequence;
        MaximumStartLocation = maximumStartLocation;
    }
}
