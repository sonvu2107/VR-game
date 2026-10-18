# Danh sách quái và phân bổ tầng

## Map theo tiến trình

Khi chơi từ StartMenu hoặc Dungeon: level 1 dùng map gốc, hoàn thành và bước vào cổng mới chuyển sang Arcane Citadel ở level 2. Từ level 2 đến 10 dùng Arcane với layout/seed và cơ chế level tương ứng. `campaignSecondMap` trên generator của Dungeon tham chiếu Arcane theme; việc chuyển map nằm trong `GenerateLevel`, không tải scene khác hay reset phiên chơi. Restart quay lại map gốc. Các scene Arcane/Courtyard riêng vẫn phục vụ xem thử.

`CampaignMapIntegration.Configure()` gắn theme vào scene chính; `CampaignMapIntegration.Run()` kiểm tra dọn quái, cổng, giữ Player/GameManager/điểm số, Pause/Resume, Restart và LAN mesh JSON round-trip trong Editor.

## Các loại bổ sung từ asset có sẵn

| Prefab | HP gốc | Tốc độ đuổi | Tầng xuất hiện | Cơ chế |
|---|---:|---:|---|---|
| GoblinTorch | 70 | 2,1 | 1–10 | Cận chiến, animation đánh theo hướng |
| GoblinBarrel | 55 | 2,1 | 1–10 | Vai trò đặc biệt: báo trước rồi lao tới, tự nổ; không phải healer |
| GoblinTNT | 70 | 2,1 | 1–10 | Giữ khoảng cách, ném thuốc nổ; bán kính 1,4, hồi đòn 3,5 giây |

Mỗi phòng Combat có đúng ba quái: một Torch, một TNT, một Barrel. Phòng Boss của level thường cũng dùng bộ ba này. Start/Exit vẫn một quái, Treasure hai quái theo pool cũ. Cấu trúc này thay thế các mốc giới thiệu quái trước đây trong phòng Combat. Các loài cũ vẫn còn trong pool phụ/phòng khác và pool boss cuối.

Mini-boss tầng 5/9 giữ nguyên. Phòng boss thật chỉ có một boss, không sinh kèm bộ ba.

## Boss cuối ngẫu nhiên

Tầng 10 chọn một prefab trong toàn bộ pool đang được generator tham chiếu: Skeleton gốc, Archer, Slime, Bat, Mage, Elite, Skull, Vampire, ba Goblin, Bone Sentinel và Skeleton King (không chọn Scout/Guard). Chọn bằng RNG của map: cùng seed/cấu hình cho cùng lựa chọn; đổi seed có thể đổi boss, không bảo đảm khác lần trước.

`CampaignFinalBoss` tạo bản sao config riêng cho lượt chơi: HP bằng tối thiểu 600 hoặc gấp năm HP gốc (lấy số lớn hơn), đầy máu lúc xuất hiện, thưởng tối thiểu 300. Phóng root tối đa bốn lần kích thước prefab, giảm theo bước 0,25 nếu phòng không đủ chỗ; chừa biên cho animation và né đòn, giữ boss trong phòng. Không sửa prefab gốc. Chưa thiết kế bộ skill boss riêng cho từng loài: tận dụng AI/animation hiện có.

Barrel boss bỏ tự hủy, nổ theo đòn đánh/cú lao; Barrel thường vẫn tự nổ. Mỗi boss chết phát tín hiệu đúng một lần. Người chơi vẫn phải dọn quái còn lại và dùng cổng; không thắng ngay khi vừa sinh map hoặc chỉ vừa giết boss.

## Animation và biến thể

- Giữ Skeleton gốc cùng hai biến thể v1/v2 của Skull và Vampire. Skeleton Scout/Guard đã được loại khỏi roster; asset nguồn và prefab cũ được giữ để có thể khôi phục, không được sinh trong game.
- Goblin Torch thu hình còn 70%, Barrel/TNT còn 75% so với lần tích hợp đầu; collider thân 0,5 × 0,5. Scale cố định cho mọi frame, không thay đổi root, tốc độ, sát thương hay bán kính nổ.
- Torch, Barrel, TNT dùng bốn màu Blue/Red/Purple/Yellow trong `Tiny Swords (Update 010)`. Màu được chọn ổn định theo vị trí sinh; không tính là một loại AI mới.
- Goblin chạy animation theo ô của sheet gốc, cùng kích thước và pivot giữa các frame. Không sinh hình mới, không thay SpriteRect của importer.
- Pixel Dungeon chỉ có bốn frame idle gốc: đi/đánh dùng chuyển động hỗ trợ hiện có. Không tuyên bố đây là bộ sprite walk/attack/hurt/death đầy đủ.
- Goblin dùng idle/run/attack gốc; nhận đòn chớp đỏ, chết mờ dần theo hệ thống EnemyHealth vì không có sprite hurt/death riêng trong các sheet này.
- Thuốc nổ dùng strip Dynamite gốc; nổ khi chạm người/tường hoặc hết thời gian bay. Sát thương diện rộng không xuyên tường, không tính lặp nhiều collider trên cùng người, tôn trọng thời gian bất tử. Chỉ Host tính sát thương.

## Tích hợp và kiểm tra

### Bẫy chiến thuật

Bẫy được đặt sau khi đã sinh quái, chìa khóa, cổng và vật phẩm. Seed chọn mẫu/hướng, không chọn các ô rời rạc ngẫu nhiên:

- `Chicane`: dải bẫy ngắn chia không gian né, có khe/lối vòng luôn an toàn.
- `RangedScreen`: dải bẫy phía trước vị trí sinh của TNT, không phủ kín đường tiếp cận.
- `TreasureGuard`: bảo vệ vùng mục tiêu/chìa khóa trong phòng kho báu, giữ khoảng đứng quan sát và tiếp cận.
- `BossFlanks`: bẫy hai bên phòng, tránh vùng thân boss lớn và chừa đường tiếp cận/né.

`TacticalTrapPlanner` tìm đường trên lưới sàn bằng phép quét collider thật của người chơi, tính cả hướng quay mặt, tường và đồ trang trí. Mạng đường nối cửa phòng, vị trí quái, vật phẩm và cổng được giữ riêng; bẫy và decor sinh sau không được chiếm mạng này. Tránh điểm xuất hiện bán kính 3 đơn vị, cửa phòng tối thiểu hai ô. Kiểm tra độc lập quét cả thân người qua kích thước bẫy tối đa khi đang bật.

Nếu không xác nhận được mạng đường, cả tầng bỏ bẫy (`TACTICAL_TRAPS_SKIPPED`); nếu một mẫu thiếu chỗ thì giảm/bỏ cụm đó, không rải ngẫu nhiên để bù. Đây là bảo đảm lối tránh bẫy/tường/props tĩnh, không phải miễn nhiễm đạn hoặc quái chắn đường khi chiến đấu.

Bẫy trong cùng cụm cùng nhịp: nghỉ 3,2 giây ở tầng đầu hoặc 2,7 giây ở tầng sau → cảnh báo 0,85 giây → bật 0,9 giây. Mỗi bẫy chỉ thử gây sát thương một lần cho mỗi người trong một nhịp bật. Pause dừng chu kỳ; Client không tự tính sát thương. Mật độ tối đa 1 bẫy/phòng ở tầng 1, 2 trước tầng 4, 3 trước tầng 7 và 4 ở các tầng sau; vẫn có thể ít hơn nếu thiếu chỗ an toàn. Dungeon gốc bắt đầu thêm mẫu từ tầng 3 (tầng 4 giữ cấu hình bẫy của chiến dịch); Arcane dùng ngân sách theme nhưng cũng bị giới hạn theo tiến trình.

`TilemapVisualizer.SynchronizeColliders()` cập nhật collider tường trước khi bake/kiểm tra đường, không đợi LateUpdate của Unity. Giữ nguyên sprite bẫy/theme hiện có, không thay importer, PPU hay tạo hình mới.

Kiểm tra: `TacticalTrapValidation.ValidateScene(scenePath, seedOffset)` kiểm tra 10 tầng; `TacticalTrapPlayValidation.Run()` kiểm tra chu kỳ, va chạm gây sát thương, pause và Client trong map Arcane.

Prefab: `Assets/Prefabs/EnemyBoss/`. Config: `Assets/Scripts/ScriptableObjects/Enemies/`. Roster và cấu hình sheet: `Assets/Scripts/ScriptableObjects/EnemyArt/`.

`additionalEnemyRoster` đã được gắn trên generator trong Dungeon, DungeonCourtyardPrototype và DungeonArcaneCitadel. Prefab/config cũ và API chọn quái cũ được giữ lại. Scene không gắn roster vẫn chạy theo danh sách cũ.

Editor helpers (chạy ngoài Play và lưu scene trước khi cài lại):

- `ExpandedEnemyIntegration.Install()` tạo/gắn lại asset; thao tác này ghi lại thông số mặc định của ba Goblin.
- `ExpandedEnemyIntegration.ApplyRosterAndScale()` cập nhật roster và kích thước ba Goblin mà không ghi lại scene hoặc cấu hình chiến đấu.
- `ExpandedEnemyIntegration.Validate()` kiểm tra prefab và texture trong LAN catalog.
- `EnemyDistributionValidation.Validate()` sinh thật cả 10 tầng, kiểm tra quota và đúng bộ ba trong từng phòng Combat.
- `ExpandedEnemyPlayValidation.Run()` kiểm tra animation, pause, tự nổ, thuốc nổ, chết và LAN visual JSON round-trip trong Editor.
- `SkullVampirePlayValidation.Run()` kiểm tra hồi máu Vampire và hồi quy hai mob trước đó.

LAN visual round-trip trong cùng Editor không thay thế kiểm thử hai máy qua Wi-Fi. Hai máy phải dùng cùng bản build/catalog mới.
