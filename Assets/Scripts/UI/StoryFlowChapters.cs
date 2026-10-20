using UnityEngine;

public sealed partial class LevelOneStory
{
    private static int FlowPriority(Beat beat) => beat switch
    {
        Beat.Poison or Beat.OneMinute => 100,
        Beat.Assembled => 98,
        Beat.GuardianSeen => 95,
        Beat.Exit => 90,
        Beat.Encounter => 80,
        Beat.Activated or Beat.KeyOne or Beat.KeyTwo or Beat.TwoMinutes => 70,
        Beat.GuardianDead => 60,
        Beat.AfterReading or Beat.Kill => 30,
        _ => 10
    };

    private void ShowNextFlowCue()
    {
        // Một câu đang đợi an toàn không được chặn sự kiện khác trong hàng đợi.
        int selected = 0, priority = -1, count = cueQueue.Count;
        bool danger = ThreatNearby();
        for (int i = 0; i < count; i++)
        {
            int id = cueQueue.Dequeue();
            var beat = (Beat)(id % 100 - 1);
            if (FlowCueObsolete(beat)) continue;
            int candidate = FlowPriority(beat);
            if ((!FlowNeedsQuiet(beat) || !danger) && candidate > priority)
            {
                if (selected != 0) cueQueue.Enqueue(selected);
                selected = id; priority = candidate;
            }
            else cueQueue.Enqueue(id);
        }
        if (selected != 0) ShowFlowCue(selected);
    }

    private static readonly string[] StepLines = {
        "Có ai ở đây không? …Chẳng có tiếng trả lời. Ta phải tìm đường ra khỏi chỗ này.",
        "Những hành lang này giống nhau quá. Phải nhớ chỗ mình vừa đi qua, kẻo lại quay về đây.",
        "Rêu phủ kín cả nền đá… Có tiếng động sau góc tường. Không nên chạy vội.",
        "Dưới nước có những phiến đá nhô lên. Phải nhìn kỹ từng bước, rồi mới tìm lối qua.",
        "Tro vẫn còn nóng… Ai đã giữ lò rèn này cháy đến tận bây giờ?",
        "Không thấy rõ lối rẽ nữa. Giá mà có một chỗ sáng để đánh dấu đường đi…",
        "Những vết cào trên tường… Có người đã cố thoát khỏi đây. Ta không muốn kết thúc như họ.",
        "Sách đã mục cả rồi. Nhưng trên đá vẫn còn dấu khắc… Thử tìm ở đó xem.",
        "Tiếng đá rạn vẫn đuổi theo sau. Không được phí thời gian quay lại những phòng đã đi qua.",
        "Yên lặng quá… Chỉ còn tiếng bước chân của ta. Cánh cổng cuối cùng phải ở gần đây."
    };
    private static readonly string[] EncounterLines = {
        "Quái vật! Cả căn phòng này… Rốt cuộc đây là nơi nào? Phải tìm cách vượt qua chúng.",
        "Lại có lính canh. Chúng đứng chắn các lối đi… Nơi này không muốn để ai ra ngoài.",
        "Có chúng sau lớp rêu kia. Hành lang hẹp quá, phải giữ một lối để lùi.",
        "Chúng canh cả căn ngục… Phải tìm chỗ đứng vững, đừng để bị dồn vào bẫy.",
        "Quái vật giữa lò rèn. Hơi nóng đã khó thở, còn phải tránh đòn của chúng nữa.",
        "Có bóng chuyển động trong tối! Giữ khoảng cách đã, đừng để mất dấu chúng.",
        "Chúng tràn ra từ hầm mộ… Cẩn thận nền đất, những vũng xanh kia không bình thường.",
        "Ngay cả thư viện cũng có lính canh. Chắc nơi này đang giấu thứ gì đó đáng bảo vệ.",
        "Chúng chặn đường! Không còn nhiều thời gian, phải mở lối qua căn phòng này.",
        "Đám lính cuối cùng… Cổng đã ở rất gần. Ta không thể dừng ở đây."
    };
    private static readonly string[] KillLines = {
        "Nó gục rồi… Rốt cuộc đây là nơi nào mà lắm quái vật đến vậy?",
        "Hạ được một tên. Chúng cũng giống bọn ở trên… Có lẽ cổng chỉ mở khi không còn lính canh.",
        "Qua được đòn đó rồi. Đừng vội tiến lên, phía sau góc tường vẫn có thể còn địch.",
        "Đứng vững đã… Ta vẫn chưa thoát khỏi căn ngục này. Nhìn kỹ những lối rẽ còn lại.",
        "Một tên đã gục. Nhưng tiếng động từ sâu trong lò vẫn chưa dứt…",
        "Hạ được rồi… Trong tối khó biết còn bao nhiêu tên. Phải đi chậm hơn một chút.",
        "Lùi khỏi vũng xanh đã. Có thắng trận cũng vô ích nếu nằm lại trong độc.",
        "Bớt được một kẻ canh giữ. Giờ phải nhìn kỹ những dấu khắc trong căn phòng này.",
        "Một tên nữa. Không được chần chừ, thời gian vẫn đang trôi.",
        "Bớt được một kẻ chắn đường về. Giữ vững tay kiếm… Chưa xong đâu."
    };
    private static readonly string[] ExitLines = {
        "Cổng mở rồi… Ở lại cũng chẳng có đường về. Đi tiếp thôi.",
        "Lính canh vừa ngã thì cổng lại mở. Quy luật này vẫn đúng… Phải lần theo nó.",
        "Qua được những hành lang này rồi… Phía dưới có tiếng nước. Lại một nơi khác.",
        "Chìa và lính canh đều không còn giữ cổng. Có hơi nóng từ phía dưới… Đi xem nào.",
        "Cổng mở rồi. Dấu khắc nói đến một thư viện bên dưới… Lần này ta có thứ để tìm.",
        "Để ngọn lửa lại đây. Nếu còn ai đi lạc, ít ra họ sẽ thấy một chỗ sáng.",
        "Rời được hầm mộ rồi… Thư viện chắc ở rất gần. Ta cần tìm câu trả lời ở đó.",
        "Ta đã có lời giải. Tìm Vua Xương, đánh bại hắn… Rồi trở về.",
        "Kịp rồi! Qua cổng trước đã. Vua Xương đang ở bên dưới… Không còn xa nữa.",
        "Ánh sáng bên kia cổng… Không giống những lần trước. Đường về đây rồi!"
    };
}
