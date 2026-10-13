using Godot;

namespace LibreKO;

public static class ClanMarkCodec
{
    public const int Side = 32;
    public const int ByteCount = Side * Side * 2;

    public static byte[] Encode(Image source)
    {
        var image = (Image)source.Duplicate();
        if (image.IsCompressed()) image.Decompress();
        image.Resize(Side, Side, Image.Interpolation.Lanczos);
        image.Convert(Image.Format.Rgb565);
        return image.GetData();
    }

    public static ImageTexture? Decode(byte[] data)
    {
        if (data.Length != ByteCount) return null;
        var image = Image.CreateFromData(Side, Side, false, Image.Format.Rgb565, data);
        image.Convert(Image.Format.Rgba8);
        return ImageTexture.CreateFromImage(image);
    }
}
