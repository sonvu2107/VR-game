using System;
using UnityEngine;

[Serializable]
public sealed class LanMessage
{
    public string type;
    public const int CurrentProtocol = 12;
    public int protocol = CurrentProtocol;
    public string catalog;
    public string error;
    public int run;
    public int level;
    public int total;
    public int seed;
    public int state;
    public int enemies;
    public int keys;
    public int requiredKeys;
    public float timeRemaining;
    public int score;
    public int sequence;
    public DecorationInfo[] decorations;
    public int[] inspectableDecorationIds;
    public DecorationNote[] decorationNotes;
    public bool decorationNotesIncluded;
    public int decorationId, inspectionVersion;
    public float inspectionSeconds;
    public string inspectionText;
    public int[] narrativeMasks;
    public LevelOneStory.NarrativeEntry[] narrativeHistory;
    public bool narrativeHistoryIncluded;
    public int narrativeCue, narrativeVersion;
    public float narrativeSeconds;
    public Vector3 narrativeExit;
    public bool storyEnding, storyOpening;
    public int storySession, mechanismStage;
    public string[] dialogueArchive;
    public bool guideAvailable;
    public Vector3 guideTarget;
    public int storyLine;
    public bool storySkip;
    public bool stoneRead;
    public Vector3 stonePosition;
    public int audio;
    public float volume = 1f;
    public float pitch = 1f;
    public LanInputFrame input;
    public LanTilemap[] maps;
    public LanMesh[] meshes;
    public LanMeshFrame[] meshFrames;
    public LanSpriteDefinition[] definitions;
    public LanVisual[] visuals;
    public LanLight[] lights;
    public LanPlayerState[] players;
}

[Serializable]
public struct LanInputFrame
{
    public Vector2 movement;
    public bool attackHeld;
    public bool attackPressed;
    public bool powerHeld;
    public bool dash;
    public bool q;
    public bool e;
    public bool r;
    public bool IsValid => !float.IsNaN(movement.x) && !float.IsNaN(movement.y) &&
        !float.IsInfinity(movement.x) && !float.IsInfinity(movement.y) && movement.sqrMagnitude <= 1.01f;
    public void MergeEdges(LanInputFrame next)
    {
        movement = next.movement;
        attackHeld = next.attackHeld;
        powerHeld = next.powerHeld;
        attackPressed |= next.attackPressed;
        dash |= next.dash; q |= next.q; e |= next.e; r |= next.r;
    }
    public void ClearEdges() { attackPressed = dash = q = e = r = false; }
}

[Serializable]
public sealed class LanPlayerState
{
    public Vector3 position;
    public int health;
    public int hearts;
    public float dash;
    public float q;
    public float e;
    public float ultimate;
    public float power;
}

[Serializable]
public sealed class LanTilemap
{
    public string name;
    public bool visible = true;
    public Vector3Int[] cells;
    public int[] tiles;
}

[Serializable]
public sealed class LanMesh
{
    public int id, material, layer, order;
    public string name;
    public Vector3 position, scale;
    public Quaternion rotation;
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uv;
}

[Serializable]
public sealed class LanMeshFrame
{
    public int id;
    public Vector2[] uv;
}

[Serializable]
public sealed class LanSpriteDefinition
{
    public int id;
    public int sprite = -1;
    public int texture = -1;
    public int runtimeTexture;
    public string png;
    public Rect rect;
    public Vector2 pivot;
    public float pixelsPerUnit;
}

[Serializable]
public sealed class LanVisual
{
    public int id;
    public string name;
    public int gameLayer;
    public int sprite;
    public Vector3 position;
    public Vector3 scale;
    public Quaternion rotation;
    public Color color;
    public int layer;
    public int order;
    public bool flipX;
    public bool flipY;
    public int material = -1;
}

[Serializable]
public sealed class LanLight
{
    public int id;
    public string name;
    public Vector3 position;
    public Vector3 scale;
    public Color color;
    public float intensity;
    public float innerRadius;
    public float outerRadius;
}
