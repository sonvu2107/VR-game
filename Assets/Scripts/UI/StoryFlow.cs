using UnityEngine;

// Nhịp truyện theo hành động, chỉ Host phát mốc; Client dùng cùng mask/cue.
public sealed partial class LevelOneStory
{
    private Transform flowReader;
    private Vector3 flowReadPosition, flowStartPosition;
    private float nextFlowCueAt;

    private void ResetStoryFlow()
    {
        flowReader = null;
        flowStartPosition = manager.PlayerMovement.transform.position;
        nextFlowCueAt = Time.time + 2f;
    }

    private void ShowFlowCue(int id)
    {
        CueId = id; CueVersion++; cueRemaining = 7f;
    }

    private bool ThreatNearby()
    {
        if (narrativeEnemies == null) narrativeEnemies = FindObjectsOfType<EnemyHealth>();
        foreach (var enemy in narrativeEnemies)
            if (enemy != null && !enemy.IsDead && PlayerNear(enemy.transform.position, 6f)) return true;
        foreach (var enemy in FindObjectsOfType<Enemy>())
            if (!enemy.IsDead && PlayerNear(enemy.transform.position, 6f)) return true;
        return false;
    }

    private static bool FlowNeedsQuiet(Beat beat) => beat == Beat.Enter || beat == Beat.FirstSteps ||
        beat == Beat.AfterReading || beat == Beat.Kill || beat == Beat.GuardianDead;

    private bool EnteredEnemyRoom()
    {
        var rooms = manager.dungeonGenerator.GeneratedRooms;
        if (rooms == null) return false;
        foreach (var player in FindObjectsOfType<HealthController>())
        {
            if (player.currentHealth <= 0) continue;
            var cell = Vector2Int.FloorToInt(player.transform.position);
            foreach (var room in rooms)
            {
                if (room.Type == RoomType.Start || !room.FloorTiles.Contains(cell)) continue;
                // Host dùng vị trí thật của cả hai người, không raycast qua trang trí.
                foreach (var enemy in FindObjectsOfType<EnemyHealth>())
                    if (!enemy.IsDead && room.FloorTiles.Contains(Vector2Int.FloorToInt(enemy.transform.position)))
                        return true;
                foreach (var enemy in FindObjectsOfType<Enemy>())
                    if (!enemy.IsDead && room.FloorTiles.Contains(Vector2Int.FloorToInt(enemy.transform.position)))
                        return true;
            }
        }
        return false;
    }

    private bool FlowCueObsolete(Beat beat)
    {
        bool exit = HasBeat(StoryLevel, Beat.Exit);
        if (beat == Beat.TwoMinutes && HasBeat(StoryLevel, Beat.OneMinute)) return true;
        if (beat == Beat.Activated && StoryLevel == 8 && HasBeat(8, Beat.Assembled)) return true;
        if (beat == Beat.Exit || beat == Beat.Assembled || beat == Beat.Activated ||
            beat == Beat.Poison || beat == Beat.Heal || beat == Beat.OneMinute || beat == Beat.TwoMinutes) return false;
        // Giữ đủ thời gian đọc kể cả khi quái bị hạ ngay sau lúc xuất hiện.
        if (beat == Beat.Encounter) return exit && CueId % 100 - 1 != (int)Beat.Encounter;
        if (exit) return true;
        if (beat == Beat.Discover) return StoneRead;
        if (beat == Beat.Enter || beat == Beat.FirstSteps)
            return StoneRead || HasBeat(StoryLevel, Beat.Encounter) || HasBeat(StoryLevel, Beat.Kill);
        if (beat == Beat.TwoMinutes) return HasBeat(StoryLevel, Beat.OneMinute);
        if (beat == Beat.GuardianSeen) return HasBeat(StoryLevel, Beat.GuardianDead);
        if (beat == Beat.LockedExit) return false;
        if (beat == Beat.Activated && StoryLevel == 8) return HasBeat(8, Beat.Assembled);
        if (beat == Beat.KeyOne && StoryLevel == 8) return MechanismStage > 0 || HasBeat(8, Beat.KeyTwo);
        if (beat == Beat.KeyTwo) return HasBeat(StoryLevel, Beat.Assembled);
        if (beat == Beat.AfterReading) return HasBeat(StoryLevel, Beat.Activated);
        return false;
    }

    private void TickStoryFlow()
    {
        if (Vector2.Distance(manager.PlayerMovement.transform.position, flowStartPosition) > 2.5f &&
            !StoneRead && !HasBeat(StoryLevel, Beat.Kill)) Record(Beat.FirstSteps);
        if (!HasBeat(StoryLevel, Beat.Encounter) && EnteredEnemyRoom()) Record(Beat.Encounter);
        if (StoneRead && flowReader != null && Vector2.Distance(flowReader.position, flowReadPosition) > 2f)
            Record(Beat.AfterReading);
        if (!LevelCompletionRules.AreRequirementsMet(manager.enemyCount, manager.KeysCollected, manager.KeysRequired) &&
            PlayerNear(ExitPosition, 2.5f)) Record(Beat.LockedExit);
    }

    private string FlowText(int level, Beat beat)
    {
        if (beat == Beat.Read && level == StoryLevel)
            return "Lời đã đọc: " + TextAt(0, LanLaunch.Mode != LanMode.Solo);
        if (beat == Beat.FirstSteps && level == 6 && HasBeat(6, Beat.Activated))
            return "Ngọn lửa còn ở phía sau. Nhớ lấy chỗ ấy, rồi tìm tiếp những lối chưa đi qua.";
        if (beat == Beat.AfterReading && level == 4)
            return manager.KeysCollected >= manager.KeysRequired
                ? manager.enemyCount > 0 ? "Chìa đã ở đây rồi. Giờ phải tìm đường về cổng và xử lý những kẻ còn chặn lối."
                    : "Chìa đã đủ, lính canh cũng không còn. Quay lại cổng thôi."
                : HasBeat(4, Beat.Activated) ? "Ấn đã sáng. Theo hướng nó chỉ, nhưng vẫn phải nhìn kỹ đường dưới chân."
                : "Dấu khắc dò được chìa… Thử đánh thức nó trước, rồi tìm đường theo hướng nó chỉ.";
        if (beat == Beat.AfterReading && level == 6 && HasBeat(6, Beat.Activated))
            return "Lửa đã cháy rồi. Ta có thể lấy nơi này làm mốc để tìm đường đi tiếp.";
        if (beat == Beat.AfterReading && level == 8)
            return HasBeat(8, Beat.Assembled) ? "Lời giải đã rõ. Qua cổng xuống dưới, tìm Vua Xương và đường trở về."
                : manager.KeysCollected >= 2 ? "Hai chìa đã đủ. Chỉ còn đặt chúng vào bệ để đọc trọn lời giải."
                : HasBeat(8, Beat.Activated) ? "Một nửa thông điệp đã hiện ra. Phải tìm chiếc chìa còn lại."
                : manager.KeysCollected == 1 ? "Chiếc chìa đang mang khớp với một rãnh. Thử đặt vào bệ xem."
                : "Hai rãnh khóa, hai chiếc chìa. Phải tìm chìa rồi mang lại đây.";
        if (beat == Beat.Encounter && HasBeat(level, Beat.Kill))
            return KillLines[level - 1];
        if (beat == Beat.Enter && level == 6 && !HasBeat(5, Beat.Read))
            return "Lạnh quá… Lại một nơi không có ánh mặt trời. Phải tìm lối đi tiếp.";
        if (beat == Beat.Enter && level == 9 && !HasBeat(8, Beat.Assembled))
            return "Nền đá đang rung… Không thể đứng lại ở đây. Phải qua nơi này trước khi hết giờ.";
        if (beat == Beat.Enter && level == 10 && !HasBeat(8, Beat.Assembled))
            return "Một ngai vàng ở cuối đường… Kẻ ngồi đó đang chặn cánh cổng cuối cùng.";
        if (beat == Beat.AfterReading && level == 1 && HasBeat(1, Beat.Kill))
            return "Quái vật, rồi lời nhắn trên bia… Nơi này đang giữ ta lại. Phải tìm lối xuống.";
        if (beat == Beat.LockedExit && manager.KeysRequired == 0)
            return "Cổng vẫn còn bị giữ lại… Chắc còn lính canh ở những phòng chưa đi qua.";
        if (beat == Beat.LockedExit)
            return manager.KeysCollected < manager.KeysRequired ?
                "Dấu khóa vẫn còn sáng. Phải tìm đủ chìa rồi mới quay lại đây." :
                "Chìa không giữ cổng nữa… Nhưng vẫn còn lính canh ở đâu đó.";
        if ((beat == Beat.KeyOne || beat == Beat.KeyTwo) && level == 8 && !HasBeat(8, Beat.Discover) && !HasBeat(8, Beat.Read))
            return beat == Beat.KeyOne ? "Chiếc chìa này có chữ khắc ở mép… Nó còn phải ghép với thứ gì đó." : "Hai chiếc chìa có những nét khắc nối nhau. Phải tìm xem chúng được dùng ở đâu.";
        if (beat == Beat.KeyOne && level == 4 && manager.enemyCount == 0)
            return "Chiếc chìa cuối cùng đây rồi… Cổng đã mở. Quay lại đó thôi.";
        if (beat == Beat.Activated && level == 4 && manager.KeysCollected >= manager.KeysRequired)
            return "Dấu sáng đang chỉ về cổng. Có chìa rồi, giờ phải tìm đường quay lại.";
        if (beat == Beat.Activated && level == 8 && manager.KeysCollected >= 2)
            return "Cả hai chìa đã được đặt vào bệ. Những nét khắc đang nối lại…";
        if (beat == Beat.KeyTwo && HasBeat(8, Beat.Assembled)) return "Hai chìa đã khớp với bệ. Lời giải hiện đủ rồi.";
        if (beat == Beat.Exit && level == 5 && !HasBeat(5, Beat.Read))
            return "Qua được rồi… Phía dưới yên tĩnh hơn. Thử tìm xem còn dấu vết nào không.";
        if (beat == Beat.Exit && level == 6 && !HasBeat(6, Beat.Activated))
            return "Cuối cùng cũng thấy cổng giữa bóng tối. Không thể ở lại nơi lạnh lẽo này nữa.";
        if (beat == Beat.Exit && level == 7 && !HasBeat(5, Beat.Read) && !HasBeat(6, Beat.Read))
            return "Lối xuống mở rồi. Mong bên dưới không còn mùi độc và những bộ xương này.";
        if (beat == Beat.Exit && level == 8 && !HasBeat(8, Beat.Assembled))
            return HasBeat(8, Beat.Discover) || HasBeat(8, Beat.Read) || HasBeat(8, Beat.Activated)
                ? "Cổng đã mở, nhưng bệ hai chìa vẫn chưa được ghép đủ. Ta có thể quay lại xem trước khi đi tiếp."
                : "Cổng đã mở. Những nét chữ trên hai chiếc chìa vẫn chưa rõ nghĩa… Ta có thể tìm thêm dấu vết trước khi đi tiếp.";
        if (beat == Beat.Exit && level == 9 && !HasBeat(8, Beat.Assembled) && !HasBeat(9, Beat.Read))
            return "Kịp rồi… Lối đi vẫn còn mở. Phải xuống ngay trước khi nơi này sập lại.";
        if (beat == Beat.Exit && level == 1)
            return StoneRead ? "Cổng mở rồi… Đúng như lời trên bia, lối đi dẫn xuống dưới. Đành đi tiếp vậy." :
                "Bọn chúng vừa ngã xuống thì cổng mở… Ở lại cũng chẳng có đường về. Thử lối này xem.";
        if (beat == Beat.GuardianDead && level == 10)
            return GuardianDeathText(manager.enemyCount);
        return BeatText(level, beat);
    }

    private static string GuardianDeathText(int remaining) => remaining > 0
        ? "Vua Xương đã gục, nhưng cổng vẫn chưa mở. Còn lính canh!"
        : "Hắn gục rồi… Không còn kẻ nào giữ cổng nữa. Đường về đã mở!";

    private static string WrittenFlowText(int level, Beat beat)
    {
        if (beat == Beat.FirstSteps) return StepLines[level - 1];
        if (beat == Beat.Encounter) return EncounterLines[level - 1];
        if (beat == Beat.LockedExit) return "Lối này bị chặn rồi. Những dấu khóa vẫn còn sáng… Chắc ta còn bỏ sót điều gì đó.";
        if (beat == Beat.AfterReading) return new[] {
            "Đường xuống… Ta cần ra ngoài, đâu phải đi sâu hơn. Nhưng người để lại bia cũng không tìm được lối cũ.",
            "Vậy là không chỉ mình ta mắc kẹt. Cứ theo những dấu khắc này, có thể sẽ tìm được người còn sống.",
            "Mũi tên có thể tới trước khi ta thấy chúng. Phải chừa chỗ né, không được lao bừa vào hành lang.",
            "Dấu khắc dò được chìa… Thử đánh thức nó trước, rồi tìm đường theo hướng nó chỉ.",
            "Một thư viện ở phía dưới… Có lẽ ở đó sẽ có lời giải, thay vì chỉ thêm một cánh cửa.",
            "Trong hốc vẫn còn than. Thử nhóm lại xem… Có một chỗ sáng thì sẽ dễ nhận đường hơn.",
            "Người ấy cũng chỉ muốn về nhà… Ta phải cẩn thận hơn, không thể nằm lại ở đây.",
            "Hai rãnh khóa, hai chiếc chìa. Đặt chúng vào bệ, phần chữ còn thiếu mới hiện ra.",
            "Lối đi không chờ ta. Phải hạ lính canh và đến cổng trước khi thời gian cạn.",
            "Đây là lối cuối cùng. Ta đã đi tới tận đây rồi… Phải tự mở đường về thôi."
        }[Mathf.Clamp(level, 1, 10) - 1];
        if (beat == Beat.Enter) return new[] {
            "Phía sau chỉ còn tường đá… Bình tĩnh. Nhìn quanh trước đã.",
            "Vẫn chưa ra ngoài… Lại một hành lang khác. Những dấu khắc có còn ở đây không?",
            "Tiếng xương va vào đá ở phía trước. Nơi này cũng có lính canh… Đi chậm thôi.",
            "Nước ngập cả lối đi… Cổng đằng kia có một dấu khóa khác những tầng trước.",
            "Nóng quá… Tiếng búa từ đâu vọng lại? Có thứ gì vẫn đang hoạt động trong lò rèn.",
            "Từ lò rèn xuống đây mà lạnh buốt… Thư viện vẫn còn ở phía dưới. Phải tìm đường tiếp.",
            "Mùi gì thế này… Toàn xương và những vệt xanh trên nền. Phải nhìn kỹ chỗ đặt chân.",
            "Sách… Một thư viện! Có lẽ câu trả lời vẫn còn đâu đó giữa những kệ này.",
            "Vua Xương giữ lối về… Nhưng đường tới hắn đang rung chuyển. Không còn thời gian chần chừ.",
            "Đến rồi… Nếu lời giải trên bệ đúng, chỉ cần đánh bại hắn và lính canh là có thể trở về."
        }[Mathf.Clamp(level, 1, 10) - 1];
        if (beat == Beat.Discover) return level == 1 ? "Một tấm bia… Chữ vẫn còn đọc được." :
            level == 8 ? "Hai rãnh nhỏ trên bệ đá… Có chữ khắc ở bên cạnh." : "Ở đây có chữ khắc. Có thể là dấu của người đi trước.";
        if (beat == Beat.Kill) return KillLines[level - 1];
        if (beat == Beat.KeyOne) return level == 8 ? "Một chiếc chìa có nét khắc bị đứt ở mép… Có lẽ phải đặt vào bệ mới đọc được." :
            "Một chiếc chìa! Dấu khóa trên cổng vừa tắt đi. Giờ còn phải vượt qua lính canh.";
        if (beat == Beat.KeyTwo) return "Chiếc chìa còn lại đây rồi. Mang về bệ xem hai phần ghép thành điều gì.";
        if (beat == Beat.Activated) return level == 8 ? "Chữ hiện ra rồi: ‘Mạng sống nhà vua…’ Vẫn thiếu nửa sau. Dấu sáng đang chỉ tới chiếc chìa còn lại." :
            level == 6 ? "Lửa bén rồi… Ít ra giờ đã có một chỗ sáng để nhận đường." :
            "Dấu sáng đang chỉ về một phía… Theo hướng đó thử xem, nhưng vẫn phải tự tìm đường vòng qua tường.";
        if (beat == Beat.Assembled) return "‘Mạng sống nhà vua là khóa của mê cung.’ Vua Xương… Muốn trở về, ta phải đánh bại hắn. Cuối cùng cũng biết phải tìm ai.";
        if (beat == Beat.GuardianDead && level != 10) return "Nó gục rồi… Nhưng phía sau vẫn là đường xuống. Chưa phải lối ra.";
        if (beat == Beat.Exit) return ExitLines[level - 1];
        return null;
    }
}
