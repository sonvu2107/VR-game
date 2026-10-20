/// <summary>Lời khắc và phản hồi tùy chọn tầng 2–10; không thêm điều kiện qua tầng.</summary>
public sealed class CampaignStoryChapter
{
    public readonly string Title;
    private readonly string[] solo;
    private readonly string[] cooperative;
    public int LineCount => solo.Length;

    private CampaignStoryChapter(string title, string narration, string soloFirst, string coopFirst,
        string knightFirst, string soloSecond, string coopSecond, string knightSecond)
    {
        Title = title;
        solo = new[] { narration, soloFirst, knightFirst, soloSecond, knightSecond };
        cooperative = new[] { narration, coopFirst, knightFirst, coopSecond, knightSecond };
    }

    public string TextAt(int line, bool coop) => (coop ? cooperative : solo)[line];
    public string SpeakerAt(int line, bool coop) => line == 0 ? "DÒNG CHỮ TRÊN ĐÁ" : line % 2 == 0
        ? "KỴ SĨ" : coop ? "NGƯỜI ĐỒNG HÀNH" : "GHI CHÉP CÒN LẠI";

    public static CampaignStoryChapter ForLevel(int level) => level >= 2 && level <= 10 ? Chapters[level - 2] : null;

    private static readonly CampaignStoryChapter[] Chapters = {
        new CampaignStoryChapter("THÀNH CỔ — DẤU CHÂN NGƯỜI ĐI TRƯỚC",
            "Dưới lớp bụi có một lời nhắn: ‘Ta cũng đã tìm lại cửa vào. Vô ích. Hãy hạ lính canh; khi chúng ngã xuống, cổng bên dưới mới mở.’",
            "‘Ta để lại những dấu này để người tới sau biết mình không phải kẻ đầu tiên mắc kẹt.’",
            "Vậy là đã có người lạc vào đây trước chúng ta. Họ cũng không tìm lại được cửa vào.",
            "Ta nhớ mình chỉ bước vào trú mưa… Quay lại thì lối đi đã biến mất. Chắc phải thử đường xuống thôi.",
            "‘Cổng không dẫn ta ra ngoài. Nhưng mỗi lần xuống sâu hơn, ta lại tìm được thêm một lời nhắn.’",
            "Có dấu khắc ở phía trước nữa. Cứ theo chúng thử xem; đứng đây cũng chẳng tìm được đường về.",
            "Được. Nhưng lần này ta sẽ nhìn kỹ mọi ngả rẽ."),
        new CampaignStoryChapter("HẦM RÊU — NHỮNG MŨI TÊN TRONG TỐI",
            "Dòng chữ dưới rêu đã sứt một nửa: ‘Đừng chạy thẳng qua hành lang. Ta chưa thấy người bắn thì mũi tên đã tới rồi.’",
            "‘Kiếm ngắn không với tới chúng. Nếu có cách đánh từ xa, hãy dùng nó để mở đường.’",
            "Có cung thủ ở cuối hành lang. Đừng lao thẳng tới, chúng đang chờ đấy.",
            "Ta còn kiếm khí. Chỉ cần có khoảng trống để ra đòn.",
            "‘Chừa một lối tránh tên. Đừng để những kẻ phía sau dồn mình vào góc.’",
            "Ta sẽ để ý phía sau. Cứ tìm chỗ né trước rồi hãy đánh.",
            "Ta phải sống mà ra khỏi đây. Không đáng đổi mạng vì một nhát chém."),
        new CampaignStoryChapter("NGỤC NGẬP NƯỚC — CHÌA KHÓA THẤT LẠC",
            "Bên hình chiếc chìa có lời dặn: ‘Chìa tháo ấn đỏ. Lính canh giữ phần khóa còn lại. Thiếu một trong hai, cổng sẽ không mở.’",
            "‘Dấu khắc này có thể dò chìa. Nhấn G để đánh thức nó; nó chỉ hướng, còn đường đi phải tự tìm.’",
            "Dấu khắc này dò được chìa. Nhấn G thử đi. Có hướng rồi, chúng ta vẫn phải tránh những cái bẫy kia.",
            "Có chìa, có ổ khóa… Ít ra lần này ta biết mình phải tìm gì.",
            "‘Đừng chỉ nhìn dấu chỉ đường. Chông dưới chân sẽ không nhường lối cho ngươi.’",
            "Ta sẽ nhìn nền đá. Lấy được chìa rồi cũng đừng vội lao về cổng.",
            "Hy vọng phía sau cánh cửa ấy là một lối ra."),
        new CampaignStoryChapter("LÒ RÈN TRO TÀN — CÁNH CỬA KHÁC",
            "Trên đá có nét chữ nguệch ngoạc: ‘Ta tưởng kẻ giữ lò canh lối ra. Nhưng sau lưng nó vẫn chỉ là cầu thang đi xuống.’",
            "‘Nó ra đòn mạnh, nhưng sau mỗi đòn đều có một khoảng dừng. Ta đã qua được nhờ chờ đúng lúc.’",
            "Kẻ giữ cửa này khác những bộ xương vừa rồi. Đừng để nó dồn chúng ta vào góc.",
            "Lại phải đi xuống sao… Được, trước hết phải qua được nó đã.",
            "‘Nếu ngươi đọc được dòng này, hãy đi tiếp. Ta tìm thấy dấu của một thư viện ở phía dưới.’",
            "Một thư viện. Có sách thì có thể có lời giải cho nơi này.",
            "Vậy ta sẽ tìm thư viện. Không thể cứ mở hết cánh cửa này tới cánh cửa khác mãi được."),
        new CampaignStoryChapter("KHO BĂNG — ĐỂ LẠI MỘT NGỌN LỬA",
            "Giữa lớp băng có lời nhắn: ‘Ta đã lạc mất đường trong bóng tối. Hãy thắp lửa ở dấu khắc này để còn nhận ra chỗ mình từng đi qua.’",
            "‘Nhấn G bên dấu khắc để thắp lửa. Nó sẽ ở lại đây, kể cả khi ngươi đi xa.’",
            "Có thể thắp lửa ở đây bằng phím G. Để lại một chỗ sáng, lát nữa còn biết đường mà tìm nhau.",
            "Ánh sáng nhỏ thôi cũng được. Ta không muốn mò mẫm quanh cùng một căn phòng nữa.",
            "‘Thư viện còn ở phía dưới. Ta đã nghe tiếng cửa đóng, nhưng chưa tìm được người nào còn sống.’",
            "Lời ghi chép nói thư viện còn ở phía dưới. Ta vẫn ở đây; nếu mất dấu thì gọi, đừng đi quá xa.",
            "Phải tìm đường tiếp thôi. Đi chậm một chút, để còn nhận ra những lối đã qua."),
        new CampaignStoryChapter("HẦM MỘ XƯƠNG — LỜI NHẮN DỞ DANG",
            "Bên vệt máu khô là lời nhắn: ‘Đừng đứng trên vũng độc. Ta còn thuốc, nhưng đã không kịp rời khỏi chỗ ấy…’",
            "‘Ta chỉ muốn về nhà. Nếu có người đọc được, đừng mắc sai lầm như ta.’",
            "Người này đã đi được xa đến thế… Cẩn thận vũng xanh dưới chân.",
            "Ta sẽ tìm chỗ đứng trước khi đánh. Không thể nằm lại ở đây được.",
            "‘Thuốc có thể giúp ngươi đi tiếp. Nhưng đừng trông chờ nó cứu được mình khi vẫn đứng trong độc.’",
            "Nếu thấy bình thuốc, hãy nhặt khi bị thương. Cứ tìm lối xuống, có thể vẫn còn dấu của người đi trước.",
            "Ta sẽ tìm ra cách thoát. Cho cả những người đã không đi tiếp được."),
        new CampaignStoryChapter("THƯ VIỆN CẤM — HAI NỬA LỜI GIẢI",
            "Hai rãnh khóa nằm giữa dòng chữ: ‘Ta đã giấu lời giải vào hai chiếc chìa. Đặt chúng vào đây để đọc điều mê cung muốn che giấu.’",
            "‘Tìm được chìa thì mang về, nhấn G để ghép thông điệp. Một nửa chỉ hướng chìa còn lại; đủ hai nửa chỉ về cổng.’",
            "Chìa khớp với dấu khắc này. Mang từng chiếc về rồi nhấn G thử ghép nhé. Có lẽ câu trả lời ở ngay đây.",
            "Cuối cùng cũng có thứ nói về lối ra, thay vì chỉ bảo ta đi xuống.",
            "‘Hai chiếc chìa phải nằm đúng chỗ. Khi ấy, phần chữ bị che mới hiện ra.’",
            "Phần chữ ở giữa đã bị che kín. Chúng ta phải tìm đủ hai chiếc chìa.",
            "Lời giải ở ngay đây… Chỉ còn thiếu hai chiếc chìa."),
        new CampaignStoryChapter("THÁNH ĐIỆN VỰC SÂU — BỐN PHÚT",
            "Dấu khắc cảnh báo: ‘Qua lớp khóa thư viện, đường này chỉ giữ được bốn phút. Hạ lính canh và qua cổng trước khi nó khép lại vĩnh viễn.’",
            "‘Đừng dừng lâu ở những ngả rẽ. Điện của Vua Xương nằm ngay bên dưới.’",
            "Chúng ta chỉ có bốn phút. Phải hạ hết lính canh rồi tìm cổng, nhanh lên!",
            "Ta hiểu. Đã tới đây rồi, ta không để lối đi khép lại trước mặt đâu.",
            "‘Nếu đã tìm được lời giải, hãy giữ vững tay kiếm. Lối về chỉ còn cách ngươi một tầng.’",
            "Ta sẽ theo sát. Nhìn thời gian còn lại, đừng để sót kẻ nào.",
            "Đi thôi. Ta muốn thấy bầu trời lần nữa."),
        new CampaignStoryChapter("ĐIỆN VUA XƯƠNG — ĐƯỜNG TRỞ VỀ",
            "Dòng chữ cuối cùng bị cào xước: ‘Đây là kẻ giữ lối ra. Khi nhà vua và lính canh cuối cùng ngã xuống, cổng sẽ dẫn ngươi trở về.’",
            "‘Ta đã tới được đây. Nếu thanh kiếm của ta không đủ, mong người đến sau làm được.’",
            "Vua Xương ở đó. Không còn cầu thang nào nữa. Chúng ta kết thúc chuyện này tại đây.",
            "Chỉ một lần đi lạc mà thành ra thế này… Ngươi giữ đường về của ta đủ lâu rồi.",
            "‘Đừng quên vì sao ngươi đã đi đến tận đây.’",
            "Ta vẫn ở bên cạnh. Cùng đánh bại hắn, rồi cùng bước qua cổng.",
            "Ta sẽ tự mở đường về.")
    };
}
