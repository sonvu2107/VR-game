using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public sealed partial class LevelOneStory
{
    // Tăng qua mỗi lần mở thoại: gói bỏ qua cũ không thể đóng lần đọc mới.
    public int StorySession { get; private set; }
    public string[] DialogueArchive { get; private set; } = new string[10];
    public Vector3 GuideTarget { get; private set; }
    public bool GuideAvailable { get; private set; }
    public Light2D BeaconLight { get; private set; }
    private GameObject mechanismVisual;
    private Image[] mechanismRunes;
    private TMP_Text mechanismLabel, guideLabel;
    private bool journalTranscript;
    private int journalDetailPage;
    private float interactionReadyAt;
    public bool HasMechanism => StoryLevel == 4 || StoryLevel == 6 || StoryLevel == 8;
    public int MechanismStage => HasBeat(StoryLevel, Beat.Assembled) ? 2 : HasBeat(StoryLevel, Beat.Activated) ? 1 : 0;

    public bool CanReachInscription(HealthController reader, float range = 1.8f)
    {
        if (reader == null || reader.currentHealth <= 0 ||
            Vector2.Distance(reader.transform.position, StonePosition) > range) return false;
        foreach (var hit in Physics2D.LinecastAll(reader.transform.position, StonePosition))
        {
            var col = hit.collider;
            if (col == null || col.isTrigger || col == StoneCollider ||
                col.GetComponentInParent<HealthController>() != null ||
                col.GetComponentInParent<EnemyHealth>() != null || col.GetComponentInParent<Enemy>() != null) continue;
            // Tường, cột, thùng và vật thể rắn khác chặn thao tác.
            return false;
        }
        return true;
    }

    public bool TryActivateMechanism(HealthController reader)
    {
        if (!IsAuthority || !HasMechanism || !CanAdvance || manager.CurrentState != GameState.Playing ||
            Time.unscaledTime < interactionReadyAt || !CanReachInscription(reader)) return false;
        if (StoryLevel == 8)
        {
            int desired = Mathf.Min(2, manager.KeysCollected);
            if (desired <= MechanismStage) return false;
            Record(Beat.Activated);
            if (desired == 2) Record(Beat.Assembled);
        }
        else
        {
            if (MechanismStage > 0) return false;
            Record(Beat.Activated);
        }
        interactionReadyAt = Time.unscaledTime + .35f;
        RefreshGuide();
        return true;
    }

    private string MechanismPrompt()
    {
        if (StoryLevel == 4) return MechanismStage > 0 ? "Ấn dò đường đã thức tỉnh" : "[G] Đánh thức ấn dò chìa";
        if (StoryLevel == 6) return MechanismStage > 0 ? "Ngọn lửa đã được thắp" : "[G] Thắp lửa dẫn đường";
        if (StoryLevel == 8)
        {
            if (MechanismStage == 2) return "Thông điệp đã ghép đủ · Ấn đang chỉ về cổng";
            if (manager.KeysCollected > MechanismStage) return "[G] Áp chìa vào dấu khắc để ghép thông điệp";
            return MechanismStage == 1 ? "Đã ghép một nửa · Hãy tìm chìa còn lại" : "Cần tìm một chìa để đánh thức dấu khắc";
        }
        return "";
    }

    private void RefreshGuide()
    {
        GuideAvailable = false;
        if (MechanismStage == 0) return;
        if (StoryLevel == 4 || StoryLevel == 8 && MechanismStage == 1)
        {
            float nearest = float.PositiveInfinity;
            foreach (var key in FindObjectsOfType<DungeonKey>())
            {
                float distance = (key.transform.position - StonePosition).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; GuideTarget = key.transform.position; GuideAvailable = true;
            }
        }
        if (StoryLevel == 8 && MechanismStage == 2 || StoryLevel == 4 && manager.KeysCollected >= manager.KeysRequired)
        { GuideTarget = ExitPosition; GuideAvailable = true; }
    }

    private void TickMechanisms()
    {
        if (mechanismVisual == null) BuildMechanismVisual();
        bool inWorld = manager.CurrentState != GameState.Generating && manager.CurrentState != GameState.GameOver && manager.CurrentState != GameState.Victory;
        mechanismVisual.SetActive(HasMechanism && inWorld);
        mechanismVisual.transform.position = StonePosition;
        BeaconLight.transform.position = StonePosition;
        BeaconLight.enabled = StoryLevel == 6 && MechanismStage > 0 && inWorld;
        TickWorldPresentation(inWorld);
        for (int i = 0; i < mechanismRunes.Length; i++)
        {
            bool lit = MechanismStage > 0 && (StoryLevel != 8 || MechanismStage == 2 || i < 3);
            mechanismRunes[i].color = lit
                ? new Color(1f, .68f, .22f, .8f + Mathf.Sin(Time.unscaledTime * 2f) * .15f)
                : new Color(.32f, .46f, .55f, .65f);
        }
        mechanismLabel.text = StoryLevel == 4 ? "ẤN DÒ ĐƯỜNG" : StoryLevel == 6 ? "LỬA DẪN ĐƯỜNG" : "Hai nửa lời giải";
        mechanismLabel.rectTransform.anchoredPosition = new Vector2(0, StoryLevel == 8 ? 68 : 105);
        mechanismLabel.fontSize = StoryLevel == 8 ? 22 : 20;
        if (StoryLevel == 8)
        {
            mechanismLabel.outlineColor = new Color32(15, 11, 9, 255);
            mechanismLabel.outlineWidth = .2f;
        }
        else mechanismLabel.outlineWidth = 0;
        bool showGuide = GuideAvailable && manager.CurrentState == GameState.Playing && !journalOpen;
        guideLabel.gameObject.SetActive(showGuide);
        if (showGuide)
        {
            Vector2 delta = GuideTarget - manager.PlayerMovement.transform.position;
            string direction = Mathf.Abs(delta.x) < 2 && Mathf.Abs(delta.y) < 2 ? "ở gần"
                : (delta.y > 2 ? "Bắc" : delta.y < -2 ? "Nam" : "") +
                  (delta.x > 2 ? " Đông" : delta.x < -2 ? " Tây" : "");
            string target = manager.KeysCollected < manager.KeysRequired ? "Chìa khóa" : "Cổng";
            guideLabel.text = $"{target}: {direction.Trim()} · {delta.magnitude:0} m · Chỉ hướng, hãy tìm lối đi an toàn";
        }
    }

    private void BuildMechanismVisual()
    {
        mechanismVisual = new GameObject("Pháp trận tương tác", typeof(RectTransform), typeof(Canvas));
        mechanismVisual.transform.SetParent(transform, false);
        mechanismVisual.transform.localScale = Vector3.one * .01f;
        var canvas = mechanismVisual.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 42;
        mechanismRunes = new Image[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            var rune = Panel("Nét pháp trận", mechanismVisual.transform, Color.gray);
            Place(rune, new Vector2(35, 5), new Vector2(Mathf.Cos(angle) * 65, Mathf.Sin(angle) * 38));
            rune.localRotation = Quaternion.Euler(0, 0, i * 60 + 90);
            mechanismRunes[i] = rune.GetComponent<Image>(); mechanismRunes[i].raycastTarget = false;
        }
        mechanismLabel = Label(mechanismVisual.transform, "", 20, new Vector2(0, 80), new Vector2(300, 38), new Color(1, .85f, .6f));
        mechanismLabel.alignment = TextAlignmentOptions.Center;
        var light = new GameObject("Ánh lửa khế ước"); light.transform.SetParent(transform, false);
        BeaconLight = light.AddComponent<Light2D>();
        BeaconLight.lightType = Light2D.LightType.Point;
        BeaconLight.pointLightInnerRadius = 1.2f; BeaconLight.pointLightOuterRadius = 4f;
        BeaconLight.color = new Color(1f, .65f, .25f); BeaconLight.intensity = 1.3f; BeaconLight.enabled = false;
        // Cả hai máy dựng ánh sáng từ bitmask chung; không gửi thêm bản sao qua world replication.
        guideLabel = Label(narrativeHud.transform, "", 18, new Vector2(0, 217), new Vector2(1050, 38), Color.white);
        guideLabel.alignment = TextAlignmentOptions.Center;
    }

    private void ArchiveDialogue()
    {
        var lines = new List<string>();
        for (int i = 0; i < LineCount; i++)
            lines.Add(SpeakerAt(i, LanLaunch.Mode != LanMode.Solo) + ": " + TextAt(i, LanLaunch.Mode != LanMode.Solo));
        DialogueArchive[StoryLevel - 1] = string.Join("\n", lines);
    }

    public string JournalPageText(int level, bool transcript, int page, out int pageCount)
    {
        var entries = new List<string>();
        if (transcript)
        {
            if (!string.IsNullOrEmpty(DialogueArchive[level - 1]))
                entries.AddRange(DialogueArchive[level - 1].Split('\n'));
        }
        else foreach (var entry in NarrativeHistory)
            if (entry.level == level) entries.Add("• " + entry.text);
        if (!transcript)
            foreach (var note in DecorationNotes)
                if (note.level == level) entries.Add("• " + note.text);
        int perPage = transcript ? 2 : 4;
        pageCount = Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)perPage));
        page = Mathf.Clamp(page, 0, pageCount - 1);
        var text = new StringBuilder($"NHẬT KÝ NGƯỜI ĐI LẠC — TẦNG {level}\n");
        text.Append(transcript ? "HỘI THOẠI ĐÃ LƯU" : "DIỄN BIẾN");
        text.Append($" · Trang {page + 1}/{pageCount}\n<size=16>[← / →] Tầng · [PgUp / PgDn] Trang · [Tab] Đổi mục · [J] Đóng\nTrận chiến vẫn tiếp diễn</size>\n\n");
        if (entries.Count == 0) text.Append(transcript ? "Chưa đọc lời nhắn tại tầng này." : "Chưa có ghi chép.");
        for (int i = page * perPage; i < Mathf.Min(entries.Count, (page + 1) * perPage); i++)
            text.Append(entries[i]).Append("\n\n");
        return text.ToString();
    }
}
