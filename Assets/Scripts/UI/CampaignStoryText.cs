using System;

public enum StorySpeaker { Narrator, Knight, Companion, BoneKing }
public enum StoryCue { Mid = 1, KeyOne = 2, KeyTwo = 3, BossEntry = 4, BossDefeated = 5, ExitLocked = 6 }

public readonly struct StoryLine
{
    public readonly StorySpeaker Speaker;
    public readonly string Text;
    public StoryLine(StorySpeaker speaker, string text) { Speaker = speaker; Text = text; }
}

/// <summary>Player-facing lines from KICH_BAN_CHIEN_DICH_10_LEVEL.md.</summary>
public static class CampaignStoryText
{
    private static StoryLine N(string text) => new(StorySpeaker.Narrator, text);
    private static StoryLine K(string text) => new(StorySpeaker.Knight, text);
    private static StoryLine C(string text) => new(StorySpeaker.Companion, text);
    private static StoryLine B(string text) => new(StorySpeaker.BoneKing, text);

    public static string Title(int level) => level switch
    {
        1 => "Dungeon gốc", 2 => "Arcane Citadel", 3 => "Hầm Rêu Cổ",
        4 => "Ngục Ngập Nước", 5 => "Lò Rèn Tro Tàn", 6 => "Kho Băng Cổ",
        7 => "Hầm Mộ Xương", 8 => "Thư Viện Cấm", 9 => "Thánh Điện Vực Sâu",
        10 => "Điện Huyết Vương", _ => "The Labyrinth"
    };

    public static StoryLine[] Intro(int level, bool coop) => level switch
    {
        1 => coop ? new[] {
            N("Người ta xây mê cung này để giam một vị vua không chịu chết."),
            N("Qua nhiều đời, tên người canh giữ bị quên lãng."),
            N("Chỉ còn một điều được truyền lại: khi các lối đi bắt đầu đổi khác, phong ấn đã yếu đi."),
            K("Nếu ngai xương còn ở dưới đó, ta sẽ tìm thấy nó."),
            C("Và lần này, ngươi không phải đi một mình.") }
        : new[] {
            N("Người ta xây mê cung này để giam một vị vua không chịu chết."),
            N("Qua nhiều đời, tên người canh giữ bị quên lãng."),
            N("Chỉ còn một điều được truyền lại: khi các lối đi bắt đầu đổi khác, phong ấn đã yếu đi."),
            K("Nếu ngai xương còn ở dưới đó, ta sẽ tìm thấy nó."),
            K("Đường vào đã mở. Ta phải đi trước khi nó khép lại.") },
        2 => coop ? new[] { K("Pháo đài này từng giữ một phần phong ấn."), C("Rune vẫn còn sáng. Có thứ gì đó bên trong đang nuôi chúng.") }
            : new[] { K("Pháo đài này từng giữ một phần phong ấn."), K("Rune vẫn còn sáng. Nguồn sức mạnh của chúng nằm sâu hơn.") },
        3 => coop ? new[] { K("Rêu đã ăn sâu vào đá. Nơi này bị bỏ mặc lâu hơn cả pháo đài."), C("Nhưng tiếng động kia thì mới. Chúng ta vẫn đang bị theo dõi.") }
            : new[] { K("Rêu đã ăn sâu vào đá. Nơi này bị bỏ mặc lâu hơn cả pháo đài."), K("Rêu che vết chân, nhưng không che nổi tiếng động.") },
        4 => coop ? new[] { C("Cổng có ổ khóa. Lần này dọn quái thôi chưa đủ."), K("Chìa nằm đâu đó trong ngục. Cẩn thận mặt sàn; bẫy có thể lẫn trong lối đi.") }
            : new[] { K("Cổng bị khóa. Tìm chìa, dọn sạch quái, rồi mới quay lại đây.") },
        5 => coop ? new[] { K("Lò rèn vẫn nóng. Ai đó đã giữ nó cháy suốt thời gian phong ấn mục nát."), C("Ở cuối xưởng có tiếng kim loại kéo trên đá. Chuẩn bị đi.") }
            : new[] { K("Lò rèn vẫn nóng. Ai đó đã giữ nó cháy suốt thời gian phong ấn mục nát."), K("Có kẻ đang chờ ở cuối xưởng.") },
        6 => coop ? new[] { C("Ánh sáng biến mất ngay ngoài tầm tay."), K("Giữ liên lạc. Nếu tách nhau, gọi tên nơi vừa đi qua.") }
            : new[] { K("Không thấy được xa. Đi theo hành lang, nhớ từng khúc rẽ.") },
        7 => coop ? new[] { K("Đây không phải chiến trường. Họ được chôn ở đây trước khi mê cung thành nhà tù."), C("Vậy ai đã đánh thức họ?") }
            : new[] { K("Đây không phải chiến trường. Họ được chôn ở đây trước khi mê cung thành nhà tù."), K("Ai đó đã kéo người chết khỏi chỗ yên nghỉ.") },
        8 => coop ? new[] { C("Hai dấu ấn. Cần hai chìa để giải chúng."), K("Tìm từng phòng. Đừng tách ra chỉ vì kiến trúc trông đối xứng.") }
            : new[] { K("Cần hai chìa. Phòng đọc và kho lưu trữ đều đáng kiểm tra.") },
        9 => coop ? new[] { C("Phong ấn đang tự khép. Chúng ta có bốn phút."), K("Đi nhanh, nhưng đừng lao vào chỗ không thấy lối ra.") }
            : new[] { K("Bốn phút. Phải dọn đường và tìm cổng trước khi thời gian hết.") },
        10 => coop ? new[] { B("Các ngươi đã đi qua mọi cánh cửa ta để mở. Bây giờ, ai sẽ bước ra?"), C("Hắn muốn ta nghi ngờ nhau."), K("Hắn chỉ cần ta dừng bước. Cứ tiến lên.") }
            : new[] { B("Các ngươi đã đi qua mọi cánh cửa ta để mở. Bây giờ, ai sẽ bước ra?"), K("Hắn cần mình sợ. Cứ tiến lên.") },
        _ => Array.Empty<StoryLine>()
    };

    public static StoryLine[] Cue(int level, StoryCue cue, bool coop, int collector = 1)
    {
        if (cue == StoryCue.ExitLocked && level == 1)
            return coop ? new[] { K("Cổng chưa sáng. Còn thứ gì đó trong mê cung.") }
                : new[] { K("Cổng chưa mở. Phải tìm nốt kẻ còn sót lại.") };
        if (cue == StoryCue.KeyOne && level == 4)
            return coop ? KeyPair(collector, "Có chìa rồi.", "Giữ chắc. Cổng chỉ chịu mở khi nơi này sạch quái.")
                : new[] { K("Một chiếc chìa. Giờ phải dọn hết quái trước khi cổng mở.") };
        if (level == 8 && cue == StoryCue.KeyOne)
            return coop ? KeyPair(collector, "Được một chiếc.", "Còn một. Đừng bỏ sót phòng đã đi ngang.")
                : new[] { K("Một trong hai. Cần tìm chiếc còn lại.") };
        if (level == 8 && cue == StoryCue.KeyTwo)
            return coop ? KeyPair(collector, "Đủ hai chìa rồi.", "Dọn hết quái, rồi trở lại cổng.")
                : new[] { K("Đủ chìa. Cổng sẽ mở khi không còn quái.") };
        if (level == 5 && cue == StoryCue.BossDefeated)
            return coop ? new[] { C("Lò rèn im rồi."), K("Nhưng tiếng gọi ở dưới vẫn còn.") }
                : new[] { K("Hắn ngã rồi. Còn nhiều tầng phía dưới.") };
        if (level == 9 && cue == StoryCue.BossEntry)
            return new[] { K(coop ? "Hộ vệ cuối. Hạ hắn trước khi đồng hồ cạn." : "Hộ vệ chắn lối. Không còn nhiều thời gian.") };
        if (level == 10 && cue == StoryCue.BossEntry)
            return coop ? new[] { B("Các ngươi tìm người giữ cửa sao? Hắn đã để ngai này một mình từ lâu."), C("Đừng nghe hắn. Kết thúc chuyện này cùng nhau.") }
                : new[] { B("Các ngươi tìm người giữ cửa sao? Hắn đã để ngai này một mình từ lâu."), K("Vậy hôm nay chiếc ngai sẽ trống thật sự.") };
        if (cue != StoryCue.Mid) return Array.Empty<StoryLine>();
        return level switch
        {
            1 => coop ? new[] { C("Vừa quay đầu mà lối cũ đã khác rồi."), K("Đừng đo đường bằng trí nhớ. Nhìn dấu đuốc, và nhớ chỗ chúng ta đã dọn sạch.") }
                : new[] { K("Lối cũ đã khác. Cứ theo dấu đuốc và tìm phòng kế tiếp.") },
            2 => coop ? new[] { C("Ánh sáng trên vách vừa đổi hướng. Nó chỉ đường hay đang dẫn dụ chúng ta?"), K("Dù là gì, cứ giữ cổng ra trong tầm mắt.") }
                : new[] { K("Rune đổi hướng theo bước chân mình. Đừng để nó kéo mình đi vòng.") },
            3 => coop ? new[] { C("Có đường vòng qua phòng bên. Ta kiểm tra chứ?"), K("Kiểm tra. Đừng để một lối khuất ở sau lưng.") }
                : new[] { K("Lối vòng có thể dẫn tới thứ hữu ích. Xem qua rồi quay lại trục chính.") },
            4 => coop ? new[] { C("Nhìn dấu khắc dưới chân. Nó phát sáng trước khi gai bật lên."), K("Ta thấy rồi. Chờ nhịp, rồi qua.") }
                : new[] { K("Bẫy có nhịp cảnh báo. Đừng bước tiếp khi dấu khắc sáng.") },
            5 => coop ? new[] { C("Hai lối đều dẫn tới đây. Hắn muốn chặn đường xuống dưới."), K("Vậy hắn sẽ phải rời khỏi cửa.") }
                : new[] { K("Hộ vệ giữ lối xuống. Quan sát đòn của hắn rồi tìm khoảng trống.") },
            6 => coop ? new[] { C("Ta nghe tiếng chúng, nhưng không thấy."), K("Đừng đuổi theo tiếng động. Đợi chúng bước vào vùng sáng.") }
                : new[] { K("Tiếng động ở gần. Giữ khoảng trống để né khi chúng hiện ra.") },
            7 => coop ? new[] { C("Ngõ này cụt. Ta quay về trục chính."), K("Nhớ dấu ở vách. Mỗi nhánh đều giống nhau trong bóng tối.") }
                : new[] { K("Ngõ cụt. Quay lại trục chính và kiểm tra nhánh kế.") },
            8 => coop ? new[] { C("Trang sách này ghi: ‘Hai khóa giữ cửa, nhưng người giữ cửa đã biến mất.’"), K("Người giữ cửa biến mất trước khi phong ấn vỡ. Có lẽ hắn đã xuống điện vua.") }
                : new[] { K("Người giữ cửa biến mất trước khi phong ấn vỡ. Hai chiếc chìa thì vẫn ở đây.") },
            9 => coop ? new[] { C("Đường vòng đưa ta trở lại gần chỗ cũ!"), K("Vậy đổi hướng qua phòng chưa dọn. Đừng phí thêm một vòng.") }
                : new[] { K("Mình vừa đi trọn vòng. Phải cắt qua phòng còn quái.") },
            10 => coop ? new[] { C("Từ đây nhìn thấy ngai rồi."), K("Dọn đường sau lưng trước. Ta không muốn bị kẹp giữa đại điện.") }
                : new[] { K("Ngai ở ngay trước mắt. Dọn đường rồi vào.") },
            _ => Array.Empty<StoryLine>()
        };
    }

    private static StoryLine[] KeyPair(int collector, string picked, string reply) => collector == 2
        ? new[] { C(picked), K(reply) } : new[] { K(picked), C(reply) };

    public static StoryLine[] Ending(bool coop) => coop
        ? new[] { N("Tiếng gọi dưới ngai đã im. Những con đường vẫn còn đó, nhưng lần đầu tiên sau rất lâu, không còn ai ra lệnh cho chúng dẫn người sống xuống sâu."), C("Ngươi có nghe thấy không?"), K("Không. Và đó là điều ta muốn nghe.") }
        : new[] { N("Tiếng gọi dưới ngai đã im. Những con đường vẫn còn đó, nhưng lần đầu tiên sau rất lâu, không còn ai ra lệnh cho chúng dẫn người sống xuống sâu."), K("Cuối cùng cũng yên tĩnh.") };
}
