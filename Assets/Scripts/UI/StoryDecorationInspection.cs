using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public sealed class DecorationNote
{
    public int level, id;
    public string text;
}

public sealed partial class LevelOneStory
{
    public DecorationInfo[] Decorations { get; private set; } = Array.Empty<DecorationInfo>();
    public List<DecorationNote> DecorationNotes { get; private set; } = new();
    public string InspectionText { get; private set; }
    public int InspectionVersion { get; private set; }
    public float InspectionSeconds { get; private set; }
    private float inspectReadyAt;
    private int selectedDecoration;
    private readonly HashSet<int> remoteInspectableIds = new();
    public int[] InspectableDecorationIds(HealthController reader) =>
        Decorations.Where(d => CanInspect(reader, d)).Select(d => d.id).ToArray();
    private TMP_Text inspectionLabel, decorationFocusLabel;
    private bool InspectionVisible => InspectionSeconds > 0;
    private bool UrgentNarrativeVisible => CueRemaining > 0 && FlowPriority((Beat)(CueId % 100 - 1)) >= 90;

    private void PrepareDecorations()
    {
        InspectionSeconds = 0; InspectionText = ""; inspectReadyAt = 0; selectedDecoration = 0;
        remoteInspectableIds.Clear();
        if (StoryLevel == 1) DecorationNotes.Clear();
        Decorations = IsAuthority ? DecorationInspection.Collect() : Array.Empty<DecorationInfo>();
    }

    public bool CanInspect(HealthController reader, DecorationInfo item)
    {
        if (reader == null || reader.currentHealth <= 0 || item == null ||
            Vector2.Distance(reader.transform.position, item.point) > DecorationInspection.Range(item)) return false;
        Vector2 from = reader.transform.position;
        foreach (var hit in Physics2D.LinecastAll(from, item.point))
        {
            var col = hit.collider;
            if (col == null || col.isTrigger || col.GetComponentInParent<HealthController>() != null ||
                col.GetComponentInParent<EnemyHealth>() != null || col.GetComponentInParent<Enemy>() != null) continue;
            // Chân của chính vật được quan sát không chặn thao tác với nó.
            if (col.GetComponent<DecorationCollision>() != null && item.bounds.Contains(col.bounds.center)) continue;
            // Vật treo ở mặt tường: chỉ chấp nhận đoạn cuối đi vào tường đặt vật.
            if (item.wall && Vector2.Distance(hit.point, item.point) < .8f) continue;
            return false;
        }
        return true;
    }

    public bool TryInspectDecoration(HealthController reader, int id)
    {
        if (!IsAuthority || manager.CurrentState != GameState.Playing || !CanAdvance ||
            Time.unscaledTime < inspectReadyAt) return false;
        var item = Decorations.FirstOrDefault(d => d.id == id);
        if (!CanInspect(reader, item)) return false;
        InspectionText = DecorationInspection.Title(item.kind) + "\n" +
            DecorationInspection.Describe(item.kind, StoryLevel, HasBeat(StoryLevel, Beat.Encounter), HasBeat(10, Beat.GuardianDead),
                NarrativeMasks.Any(mask => (mask & (1 << (int)Beat.Read)) != 0),
                HasBeat(8, Beat.Discover) || HasBeat(8, Beat.Read) || HasBeat(8, Beat.Activated) || HasBeat(8, Beat.Assembled),
                HasBeat(8, Beat.Assembled));
        InspectionVersion++; InspectionSeconds = 7; inspectReadyAt = Time.unscaledTime + .35f;
        // Mỗi vật vẫn đọc lại được; nhật ký không nhân bản cùng vật sau mỗi lần nhấn F.
        var note = DecorationNotes.FirstOrDefault(n => n.level == StoryLevel && n.id == id);
        if (note == null) DecorationNotes.Add(new DecorationNote { level = StoryLevel, id = id, text = InspectionText });
        else note.text = InspectionText;
        return true;
    }

    public void DismissInspection(int version)
    {
        if (IsAuthority && manager.CurrentState == GameState.Playing && version == InspectionVersion) InspectionSeconds = 0;
    }

    private void ApplyDecorationsRemote(LanMessage message)
    {
        remoteInspectableIds.Clear();
        if (message.inspectableDecorationIds != null)
            foreach (int id in message.inspectableDecorationIds) remoteInspectableIds.Add(id);
        if (message.type == "world" && message.decorations != null) Decorations = message.decorations;
        InspectionText = message.inspectionText;
        InspectionVersion = message.inspectionVersion;
        InspectionSeconds = message.inspectionSeconds;
        if (message.decorationNotesIncluded && message.decorationNotes != null) DecorationNotes = new List<DecorationNote>(message.decorationNotes);
    }

    private bool UpdateDecorationPrompt(bool stonePrompt, bool playing)
    {
        if (decorationFocusLabel != null) decorationFocusLabel.gameObject.SetActive(false);
        if (!playing || journalOpen) return false;
        var player = manager.PlayerMovement.GetComponent<HealthController>();
        if (!CanAdvance || player.currentHealth <= 0) return false;
        var nearby = Decorations.Where(d => IsAuthority ? CanInspect(player, d) :
                remoteInspectableIds.Contains(d.id) &&
                Vector2.Distance(player.transform.position, d.point) <= DecorationInspection.Range(d))
            .OrderBy(d => Vector2.SqrMagnitude((Vector2)player.transform.position - d.point)).ThenBy(d => d.id).ToArray();
        if (nearby.Length == 0) { selectedDecoration = 0; return false; }
        int index = Array.FindIndex(nearby, d => d.id == selectedDecoration);
        if (index < 0 && !stonePrompt) index = 0;
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            index = index + 1 >= nearby.Length ? (stonePrompt ? -1 : 0) : index + 1;
        if (index < 0)
        {
            selectedDecoration = 0;
            return false;
        }
        var target = nearby[index]; selectedDecoration = target.id;
        ShowInteractionMarker(new Vector3(target.bounds.center.x, target.bounds.yMax + .15f, 0), "F");
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (IsAuthority) TryInspectDecoration(player, target.id);
            else LanSessionBridge.Active?.RequestDecorationInspect(target.id);
        }
        return true;
    }

    private void ShowInteractionMarker(Vector3 position, string keys)
    {
        explorationHud.SetActive(true);
        if (decorationFocusLabel == null)
        {
            font.TryAddCharacters("▼");
            decorationFocusLabel = Label(explorationHud.transform, "", 18, Vector2.zero, new Vector2(90, 52), new Color(1, .85f, .45f));
            decorationFocusLabel.alignment = TextAlignmentOptions.Center;
            decorationFocusLabel.rectTransform.pivot = new Vector2(.5f, 0);
            decorationFocusLabel.gameObject.AddComponent<UnityEngine.UI.Shadow>().effectDistance = new Vector2(1, -1);
        }
        decorationFocusLabel.text = keys + "\n<size=12>▼</size>";
        if (Camera.main != null)
        {
            var screen = Camera.main.WorldToScreenPoint(position);
            if (screen.z > 0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                explorationHud.GetComponent<RectTransform>(), screen, null, out var local))
            {
                decorationFocusLabel.rectTransform.anchoredPosition = local + Vector2.up * (3f + Mathf.Sin(Time.unscaledTime * 3f) * 3f);
                decorationFocusLabel.gameObject.SetActive(true);
            }
        }
    }

    private void TickInspection()
    {
        if (inspectionLabel == null)
        {
            var card = Panel("Quan sát đồ vật", narrativeHud.transform, new Color(.025f, .04f, .065f, .9f));
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, 0);
            card.anchoredPosition = new Vector2(0, 150);
            card.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            inspectionLabel = Label(card, "", 20, Vector2.zero, new Vector2(800, 80), Color.white);
        }
        if (IsAuthority && InspectionVisible) InspectionSeconds = Mathf.Max(0, InspectionSeconds - Time.deltaTime);
        bool active = InspectionVisible && !journalOpen;
        inspectionLabel.transform.parent.gameObject.SetActive(active);
        var cueCard = cueText.transform.parent.GetComponent<RectTransform>();
        cueCard.anchoredPosition = new Vector2(0, 150);
        if (!active) return;
        inspectionLabel.text = InspectionText + "\n<size=16>[Backspace] Bỏ qua · [J] Nhật ký</size>";
        FitNarrativeCard(inspectionLabel, Mathf.Min(818, narrativeHud.GetComponent<RectTransform>().rect.width - 80), new Vector2(32, 20));
        // Thao tác F luôn có phản hồi ngay. Tin nguy hiểm vẫn hiện ở khung riêng phía trên.
        cueText.transform.parent.gameObject.SetActive(UrgentNarrativeVisible);
        if (UrgentNarrativeVisible)
            cueCard.anchoredPosition = new Vector2(0, 162 + inspectionLabel.transform.parent.GetComponent<RectTransform>().rect.height);
    }
}
