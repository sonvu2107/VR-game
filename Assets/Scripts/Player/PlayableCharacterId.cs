public enum PlayableCharacterId
{
    AshenKnight = 0,
    AzureWarden = 1,
    Chronoblade = 2,
    GravewindRanger = 3
}

public static class PlayableCharacterNames
{
    public static string DisplayName(PlayableCharacterId id) => id switch
    {
        PlayableCharacterId.AzureWarden => "AZURE WARDEN",
        PlayableCharacterId.Chronoblade => "CHRONOBLADE",
        PlayableCharacterId.GravewindRanger => "GRAVEWIND RANGER",
        _ => "ASHEN KNIGHT"
    };

    public static string Description(PlayableCharacterId id) => id switch
    {
        PlayableCharacterId.AzureWarden => "Hộ vệ lam · giáp nặng và kiếm năng lượng",
        PlayableCharacterId.Chronoblade => "Kiếm sĩ thời gian · nhanh và sắc gọn",
        PlayableCharacterId.GravewindRanger => "Lãng khách u phong · cơ động và bí ẩn",
        _ => "Kiếm sĩ tro tàn · phong cách nguyên bản"
    };
}
