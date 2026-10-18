using System;
using System.Collections.Generic;
using UnityEngine;

public enum CampaignLayoutStyle { MossCrypt, SunkenPrison, EmberForge, FrozenVault, BoneCatacombs, ForbiddenLibrary, AbyssSanctum, CrimsonThrone }

// Mỗi theme có đồ thị phòng và hình phòng riêng. Không sửa Random Walk/map Arcane cũ.
public static class CampaignDungeonLayout
{
    private static readonly Vector2Int[][] Centers =
    {
        new[] { P(0,0), P(0,28), P(-30,28), P(30,28), P(-30,58), P(0,58), P(0,88) },
        new[] { P(-32,-30), P(-32,0), P(0,0), P(32,0), P(-32,32), P(32,32), P(32,64) },
        new[] { P(0,-30), P(-32,0), P(32,0), P(-32,30), P(32,30), P(0,60), P(0,92) },
        new[] { P(0,0), P(0,30), P(0,60), P(30,60), P(30,30), P(60,30), P(60,60) },
        new[] { P(0,0), P(0,28), P(-28,28), P(28,28), P(-28,58), P(0,58), P(0,88) },
        new[] { P(0,-2), P(0,30), P(-36,30), P(36,30), P(-36,64), P(0,64), P(0,96) },
        new[] { P(-34,-34), P(-34,0), P(-34,34), P(0,34), P(34,34), P(34,0), P(34,-34) },
        new[] { P(0,0), P(0,32), P(-34,64), P(34,64), P(-34,100), P(0,100), P(0,136) }
    };
    private static Vector2Int P(int x, int y) => new Vector2Int(x,y);

    public static DungeonLayout Generate(IReadOnlyList<RoomType> sequence, SeededRandom random, CampaignLayoutStyle style)
    {
        if (sequence == null || sequence.Count < 5 || sequence.Count > 7 || (int)style < 0 || (int)style >= Centers.Length)
            throw new ArgumentException("Campaign maps require five to seven rooms and a valid style.");
        var layout = new DungeonLayout();
        // Giữ Treasure/Boss/Exit đúng vị trí thiết kế dù campaign dùng 6 hay 7 phòng.
        var template = Centers[(int)style];
        var centers = new Vector2Int[sequence.Count];
        int combatIndex=1;
        for (int i = 0; i < sequence.Count; i++)
        {
            int slot=sequence[i]==RoomType.Start?0:sequence[i]==RoomType.Treasure?4:
                sequence[i]==RoomType.Boss?5:sequence[i]==RoomType.Exit?6:combatIndex++;
            centers[i]=template[slot];
            bool boss = sequence[i] == RoomType.Boss;
            int halfX = boss ? (style == CampaignLayoutStyle.CrimsonThrone ? 12 : 10) :
                style == CampaignLayoutStyle.ForbiddenLibrary ? 10 : style == CampaignLayoutStyle.FrozenVault ? 7 : random.Range(7, 9);
            int halfY = boss ? 10 : style == CampaignLayoutStyle.EmberForge ? 9 :
                style == CampaignLayoutStyle.ForbiddenLibrary ? 6 : random.Range(7, 9);
            if(style==CampaignLayoutStyle.ForbiddenLibrary && sequence[i]==RoomType.Combat && i==1) { halfX=14; halfY=9; }
            if(style==CampaignLayoutStyle.CrimsonThrone && boss) halfY=12;
            var tiles = new HashSet<Vector2Int>();
            for (int x = -halfX; x <= halfX; x++)
            for (int y = -halfY; y <= halfY; y++)
            {
                bool clipped;
                if(boss) clipped=style!=CampaignLayoutStyle.CrimsonThrone && Mathf.Abs(x)+Mathf.Abs(y)>halfX+halfY-3;
                else switch(style)
                {
                    case CampaignLayoutStyle.MossCrypt:
                        clipped=(long)x*x*halfY*halfY+(long)y*y*halfX*halfX>(long)halfX*halfX*halfY*halfY; break;
                    case CampaignLayoutStyle.FrozenVault:
                        clipped=Mathf.Abs(x)*halfY+Mathf.Abs(y)*halfX>halfX*halfY; break;
                    case CampaignLayoutStyle.BoneCatacombs:
                        clipped=Mathf.Abs(x)>3 && Mathf.Abs(y)>3; break;
                    case CampaignLayoutStyle.SunkenPrison:
                        clipped=Mathf.Abs(x)>halfX-3 && Mathf.Abs(y)>halfY-3; break;
                    case CampaignLayoutStyle.AbyssSanctum:
                        clipped=Mathf.Abs(x)+Mathf.Abs(y)>halfX+halfY-4; break;
                    case CampaignLayoutStyle.EmberForge:
                        // Xưởng có hai hốc sâu ở vách bên; trung tâm luôn rộng để chiến đấu.
                        clipped=Mathf.Abs(x)>halfX-2 && Mathf.Abs(y)<=1; break;
                    default: clipped=false; break;
                }
                if (!clipped) tiles.Add(centers[i] + new Vector2Int(x,y));
            }
            layout.AddRoom(new DungeonRoom(sequence[i], tiles, centers[i]));
        }
        int treasure=sequence.Count-3, bossRoom=sequence.Count-2, exit=sequence.Count-1;
        int combatCount=sequence.Count-4;
        void Link(int from,int to,bool verticalFirst=false)
        {
            int width=style==CampaignLayoutStyle.SunkenPrison || style==CampaignLayoutStyle.ForbiddenLibrary?2:1;
            if(style==CampaignLayoutStyle.CrimsonThrone && centers[from].x==0 && centers[to].x==0) width=3;
            Connect(layout,centers[from],centers[to],width,verticalFirst);
        }
        switch(style)
        {
            case CampaignLayoutStyle.MossCrypt:
                Link(0,1); Link(1,2); if(combatCount==3) { Link(1,3); Link(3,bossRoom,true); }
                Link(2,treasure); Link(treasure,bossRoom); Link(1,bossRoom); Link(bossRoom,exit); break;
            case CampaignLayoutStyle.SunkenPrison:
                Link(0,1); Link(1,2); if(combatCount==3) { Link(2,3); Link(3,bossRoom); } else Link(2,bossRoom);
                Link(1,treasure); Link(treasure,bossRoom); Link(bossRoom,exit); break;
            case CampaignLayoutStyle.EmberForge:
                Link(0,1,true); Link(0,2,true); Link(2,treasure); Link(treasure,bossRoom,true);
                if(combatCount==3) { Link(1,3); Link(3,bossRoom,true); } else Link(1,bossRoom,true);
                Link(bossRoom,exit); break;
            case CampaignLayoutStyle.BoneCatacombs:
                // Trục chính và các nhánh hầm mộ cụt: không nối thành cùng một vòng như map khác.
                Link(0,1); Link(1,2); if(combatCount==3) Link(1,3);
                Link(2,treasure); Link(1,bossRoom); Link(bossRoom,exit); break;
            case CampaignLayoutStyle.ForbiddenLibrary:
                Link(0,1); Link(1,2); if(combatCount==3) { Link(1,3); Link(3,bossRoom,true); }
                Link(2,treasure); Link(1,bossRoom); Link(bossRoom,exit); break;
            case CampaignLayoutStyle.CrimsonThrone:
                Link(0,1); Link(1,bossRoom); Link(1,2,true); Link(2,treasure);
                if(combatCount==3) { Link(1,3,true); Link(3,bossRoom,true); }
                Link(treasure,bossRoom); Link(bossRoom,exit); break;
            default:
                for(int i=1;i<sequence.Count;i++)
                {
                    if(style==CampaignLayoutStyle.FrozenVault && centers[i-1].x==centers[i].x)
                    {
                        var middle=new Vector2Int(centers[i].x+6,(centers[i-1].y+centers[i].y)/2);
                        Connect(layout,centers[i-1],middle,1,true); Connect(layout,middle,centers[i],1);
                    }
                    else Link(i-1,i);
                }
                if(style==CampaignLayoutStyle.AbyssSanctum) Link(exit,0); break;
        }
        return layout;
    }

    private static void Connect(DungeonLayout layout, Vector2Int from, Vector2Int to, int radius, bool verticalFirst=false)
    {
        var tiles = new HashSet<Vector2Int>();
        Vector2Int point = from;
        while (true)
        {
            for (int x = -radius; x <= radius; x++) for (int y = -radius; y <= radius; y++) tiles.Add(point + new Vector2Int(x,y));
            if (point == to) break;
            if(verticalFirst && point.y!=to.y) point.y+=Math.Sign(to.y-point.y);
            else if (point.x != to.x) point.x += Math.Sign(to.x-point.x); else point.y += Math.Sign(to.y-point.y);
        }
        layout.AddCorridor(tiles);
    }
}
