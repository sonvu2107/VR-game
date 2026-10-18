# The Labyrinth

**The Labyrinth** là game hành động khám phá mê cung 2D được phát triển bằng Unity. Chiến dịch gồm 10 level, mỗi level có bộ map riêng. Level 1 giữ dungeon gốc, level 2 giữ Arcane Citadel; level 3–10 dùng tám atlas và bố cục mới.

> Trạng thái: Đồ án đang được tiếp tục phát triển bởi nhóm 5 thành viên.

## Nội dung

- [Tính năng hiện có](#tính-năng-hiện-có)
- [Cách chơi](#cách-chơi)
- [Công nghệ sử dụng](#công-nghệ-sử-dụng)
- [Cài đặt và chạy project](#cài-đặt-và-chạy-project)
- [Cấu trúc project](#cấu-trúc-project)
- [Định hướng phát triển](#định-hướng-phát-triển)
- [Làm việc nhóm](#làm-việc-nhóm)
- [Hạn chế hiện tại](#hạn-chế-hiện-tại)
- [Giấy phép](#giấy-phép)

## Bản đồ theo level

Trong menu chính, bấm **Start/Play** để mở màn hình **CHỌN MÀN**. Có thể chọn trực tiếp **LV 1–10**, xem tên map rồi bấm **Chơi một mình** hoặc **Tạo phòng LAN (Host)**. Không khóa level theo save. Người Join nhận màn của Host, không tự quyết định level. Game luôn tải scene `Dungeon` và chọn theme đúng level, không dùng các scene preview để chơi chiến dịch. Qua cổng tiếp tục level kế; Restart quay về level đã chọn lúc bắt đầu lượt chơi. Chọn LV 1 để chơi toàn bộ chiến dịch từ đầu; mặc định vẫn là LV 1.

| Level | Map | Bố cục chính |
|---|---|---|
| 1 | Dungeon gốc | PCG Random Walk cũ |
| 2 | Arcane Citadel | Pháo đài rune xanh ngọc cũ |
| 3 | Hầm Rêu Cổ | Phòng oval, trục rẽ nhánh và đường vòng qua phòng kho báu |
| 4 | Ngục Ngập Nước | Phòng góc vát, hai tuyến cầu rộng qua khoảng trống |
| 5 | Lò Rèn Tro Tàn | Hai cánh xưởng có hốc tường, hội tụ trước boss |
| 6 | Kho Băng Cổ | Phòng kim cương nối bằng hành lang gấp khúc |
| 7 | Hầm Mộ Xương | Phòng chữ thập, trục chính và nhánh hầm mộ cụt |
| 8 | Thư Viện Cấm | Đại sảnh rộng ở giữa, phòng đọc hai cánh và nhánh lưu trữ |
| 9 | Thánh Điện Vực Sâu | Phòng bát giác tạo vòng bao quanh khoảng trống |
| 10 | Điện Huyết Vương | Trục đại điện rộng bảy ô, hai cánh phụ và phòng boss lớn |

Mỗi map mới có atlas riêng gồm nền, tường, viền, cột, rương, vật trang trí và bốn trạng thái bẫy. Đuốc dùng animation cũ với màu lửa theo theme. Nước/băng/lava trong art hiện chỉ là bối cảnh; chưa thêm cơ chế bơi, trượt hay sát thương môi trường.

Chiến dịch chạy qua `StartMenu` → `Dungeon`, chọn map từ `CampaignMapSet`, không load scene mới giữa tầng để giữ phiên LAN. Các scene `Assets/Scenes/Level03_MossCrypt.unity` đến `Level10_CrimsonThrone.unity` dùng xem trước bố cục trong Editor; mở riêng một scene xem thử không tương đương bắt đầu chiến dịch ở đúng số level đó.

Editor menu: **Tools → The Labyrinth → Campaign Maps** có lệnh dựng asset/preview, kiểm tra layout, sinh thử chiến dịch và chạy Play qua mười map. Lệnh dựng không ghi đè preview scene đã tồn tại và yêu cầu lưu scene/dừng Play trước. Atlas được tạo bằng built-in `image_gen`; prompt ở [CAMPAIGN_MAP_ART_PROMPTS.json](Docs/CAMPAIGN_MAP_ART_PROMPTS.json). Đây là asset bản thử, chưa coi các mép nền là bộ tile seamless hoàn thiện.

Lệnh **Refresh Owned Preview Geometry** chỉ dựng lại root preview do công cụ tạo, sao lưu scene và mesh trước khi ghi; không thay map gốc hoặc Arcane. **Export Actual Floor Plans** xuất `Captures/CampaignMapStructures.png` từ ô sàn thật (seed 12345): xanh lá = Start, vàng = Treasure, đỏ = Boss, xanh ngọc = Exit, hành lang màu xanh xám. Sơ đồ này không phải ảnh gameplay hoặc concept vẽ tay.

### Trang trí pixel art và hiệu ứng môi trường

Không còn sinh rương trang trí hoặc footprint của rương; vẫn giữ phòng Treasure, chìa khóa và logic phần thưởng. Atlas nguồn có hình rương được giữ lại, không xóa dữ liệu ảnh.

`DungeonAmbienceSet` cấu hình 10 phối màu: lửa cháy lặp 4 frame, linh hồn lơ lửng, mạng nhện bám mép tường, nấm ở hầm rêu, tinh thể ở kho băng/vực sâu, nến trong thư viện/điện vua và xương trong hầm mộ. Map gốc (level 1 chiến dịch) giữ đuốc tường cũ, không thêm chậu lửa; chỉ bổ sung mạng nhện, xương và linh hồn thưa. Lò rèn ưu tiên lửa, không rải linh hồn. Tất cả là trang trí thuần hình ảnh, không gây sát thương và không thêm collider.

Texture dùng Point, không mipmap/compression; mesh giữ nguyên kích thước, shader đổi UV liên tục qua 4 frame không có frame trống và dùng thời gian scaled để dừng khi Pause. Trang trí gắn theo mép phòng thật, không rải vào tâm phòng hoặc hành lang. LAN dùng cùng mesh/material trong catalog; pha hoạt ảnh mang tính thẩm mỹ trên từng máy, không phải trạng thái gameplay đồng bộ chính xác từng frame.

**Tools → The Labyrinth → Campaign Maps → Install Ambience and Refresh Chest-Free Previews** cài bộ material và cập nhật preview Arcane cùng level 3–10, sao lưu scene/mesh trước khi ghi và yêu cầu Edit Mode với mọi scene đã lưu. Công cụ không chạy Play hoặc sửa save. Ảnh chụp góc phòng xuất vào `Captures/Ambience/`; báo cáo kiểm tra tại `Temp/DungeonAmbience.result.txt`. Atlas được tạo bằng built-in `image_gen`, prompt lưu ở [DUNGEON_AMBIENCE_ART_PROMPT.json](Docs/DUNGEON_AMBIENCE_ART_PROMPT.json).

## Tính năng hiện có

- Sinh phòng và hành lang ngẫu nhiên bằng Procedural Content Generation (PCG).
- Một lượt chơi gồm 10 level với seed, số phòng và độ khó riêng cho từng level.
- Qua cổng sẽ đổi map theo từng level trong cùng phiên chơi; không tải lại Player, điểm số hay kết nối LAN. Restart trở lại level 1/map gốc.
- State machine quản lý các trạng thái Generating, Playing, Level Completed, Paused, Game Over và Victory.
- Cổng chuyển level chỉ mở sau khi toàn bộ enemy bị tiêu diệt và đủ chìa khóa ở level có khóa.
- Level 4 có bẫy gai và một chìa khóa; level 6 giới hạn tầm nhìn; level 8 yêu cầu hai chìa khóa; level 9 có giới hạn 4 phút.
- Cấu hình chiến dịch đánh dấu level thường, mini-boss và boss cuối.
- Phòng chiến đấu gồm một Goblin Torch, một TNT và một Barrel; boss tầng 10 chọn ngẫu nhiên từ pool quái hiện có và phóng lớn tối đa bốn lần nếu phòng đủ chỗ.
- Bẫy dùng mẫu chia đường, che tuyến TNT, bảo vệ mục tiêu và hai bên phòng boss; giữ mạng đường né không có bẫy. Phòng không đủ chỗ sẽ giảm/bỏ bẫy; có cảnh báo 0,85 giây trước khi kích hoạt.
- LAN co-op hai người trên hai máy: Host/Join bằng IPv4, Host xử lý gameplay và đồng bộ bản đồ, Player, Enemy, cổng, VFX cùng trạng thái chiến dịch.
- Tự động tạo sàn và tường bằng Tilemap.
- NavMesh 2D hỗ trợ enemy tìm đường và đuổi theo người chơi.
- Di chuyển nhân vật bằng bàn phím hoặc gamepad.
- Đánh thường và kỹ năng tích lực gây sát thương diện rộng.
- Hệ thống máu hiển thị bằng các biểu tượng trái tim.
- HUD gồm minimap, 5 tim và mũi tên chỉ quái gần nhất. Mỗi lần trúng đòn mất tối đa nửa tim, kèm 0,85 giây bất tử.
- Q tự ngắm mục tiêu gần trong phạm vi 14 đơn vị; E chém xoay; R sẵn sàng khi bắt đầu, tự hồi nộ và nhận nộ khi Q/E trúng quái.
- Trang trí hầm ngục tự sinh: đuốc và ánh sáng nhấp nháy, cờ, thùng góc phòng, rương phòng kho báu; boss có bóng đổ, vòng ma pháp và chuyển động giả lập chiều sâu.
- Enemy gồm Skeleton, Archer, Slime, Bat, Mage, Elite, Skull, Vampire và ba loại bổ sung: Goblin Torch/Barrel/TNT. Mỗi loại được phân bổ theo tiến trình 10 tầng; xem [danh sách quái](Docs/ENEMY_ROSTER.md).
- Hiển thị số lượng enemy còn lại.
- Các trạng thái Pause, Game Over và Victory.
- Menu bắt đầu, màn hình tải và âm thanh cơ bản.

## Cách chơi

Mục tiêu hiện tại của người chơi là khám phá mê cung và tiêu diệt toàn bộ Skeleton trong từng level. Ở level có khóa, người chơi còn phải thu thập đủ chìa khóa trước khi cổng thoát mở. Dungeon được sinh lại theo cấu hình level tiếp theo; Victory chỉ xuất hiện sau khi hoàn thành level 10.

### Bàn phím và chuột

| Thao tác | Phím |
|---|---|
| Di chuyển | `W`, `A`, `S`, `D` hoặc các phím mũi tên |
| Đánh thường | Chuột trái hoặc `Left Alt` |
| Tích lực | Giữ `Space` |
| Tung đòn tích lực | Thả `Space` |
| Lướt | `Left Shift` hoặc chuột phải (Solo và LAN) |
| Kiếm khí tự ngắm / chém xoay / tuyệt kỹ | `Q` / `E` / `R` |
| Tạm dừng/tiếp tục | `Esc` |
| Chơi lại tại Game Over | `R` (LAN: chỉ Host) |

Giữ nút đánh thường để tiếp tục chuỗi đòn. Kỹ năng và lướt sử dụng cooldown riêng.

### Gamepad

| Thao tác | Điều khiển |
|---|---|
| Di chuyển | Left Stick |
| Đánh thường | Right Trigger |

Kỹ năng tích lực và Pause hiện chưa được gán đầy đủ cho gamepad.

## Chơi co-op qua LAN

Hai máy dùng **cùng một bản build Windows**, kết nối cùng mạng LAN/Wi-Fi.

1. Trong menu, chọn **Start**, rồi chọn **Tạo phòng LAN (Host)**. Cổng mặc định là `7777` (TCP).
2. Host gửi địa chỉ IPv4 hiển thị trên thanh LAN cho người chơi còn lại.
3. Máy thứ hai chọn **Tham gia phòng (Join)**, nhập IP Host và cùng cổng.
4. Nếu Windows Firewall hỏi, cho phép game trên mạng **Private**. Nếu không vào được, kiểm tra hai máy có liên lạc được và mạng Wi-Fi không bật cô lập thiết bị.

Mỗi người dùng bàn phím/chuột trên máy của mình, có máu và hồi chiêu riêng. Cả hai có thể đánh thường, giữ chuột để đánh mạnh, Dash, Q/E/R và giữ/thả Space. Quái chọn người còn sống gần nhất. Người hết máu tạm ngừng điều khiển, được hồi sinh khi đồng đội sang level tiếp theo; cả hai hết máu thì Game Over. Esc tạm dừng/tiếp tục **cả phòng**; Restart do Host thực hiện và reset đồng thời chiến dịch, máu, kỹ năng. Chọn **Rời phòng** để về menu. Host chờ khi người thứ hai ngắt kết nối và cho phép Join lại.

Client nhận Tilemap thực tế từ Host, không tự sinh map hoặc tự tính sát thương. Host gửi hình ảnh trạng thái 15 lần/giây; Client gửi input 30 lần/giây và nội suy vị trí hiển thị. Đây là LAN IPv4/TCP, chưa có lobby Internet, tự tìm phòng, host migration hoặc dự đoán di chuyển Client. Cần cùng bản build để ID asset khớp nhau; phiên bản không khớp sẽ báo lỗi.

`LanAssetCatalog` được Unity tự tạo khi import và cập nhật khi build. Khi đổi asset rồi thử hai Editor/build khác nhau, chạy **Tools > The Labyrinth > Refresh LAN Catalog** trước khi build. Không cần gắn thủ công script vào scene. Chi tiết kỹ thuật và kiểm thử: [LAN_COOP.md](Docs/LAN_COOP.md).

## Công nghệ sử dụng

- Unity `2022.3.10f1` (LTS).
- C#.
- Universal Render Pipeline 2D.
- Unity Input System.
- Unity Tilemap.
- Cinemachine.
- TextMesh Pro.
- NavMesh Plus/AI Navigation cho môi trường 2D.

Các package và phiên bản chi tiết được khai báo trong `Packages/manifest.json`.

## Cài đặt và chạy project

### Yêu cầu

- Unity Hub.
- Unity Editor `2022.3.10f1`.
- IDE hỗ trợ C#, ví dụ Visual Studio, Visual Studio Code hoặc JetBrains Rider.
- Git nếu làm việc theo nhóm.

Nên sử dụng đúng phiên bản Unity của project để hạn chế thay đổi asset, scene và package ngoài ý muốn.

### Các bước chạy

1. Clone hoặc tải project về máy.
2. Mở Unity Hub và chọn **Add project from disk**.
3. Chọn thư mục gốc `The-Labyrinth-main`.
4. Mở project bằng Unity `2022.3.10f1`.
5. Chờ Unity import toàn bộ asset và package.
6. Mở scene `Assets/Scenes/StartMenu.unity`.
7. Nhấn nút **Play** trong Unity Editor.

### Scene trong bản build

Project đang sử dụng hai scene theo thứ tự:

1. `Assets/Scenes/StartMenu.unity`
2. `Assets/Scenes/Dungeon.unity`

Để build bản Windows, mở **File > Build Settings**, chọn nền tảng Windows và kiểm tra hai scene trên đã được thêm vào danh sách **Scenes In Build**.

## Cấu trúc project

```text
Assets/
├── Input/                      # Input Actions của người chơi và UI
├── NavMeshComponents/          # Thành phần NavMesh dành cho game 2D
├── Prefabs/                    # Prefab, bao gồm Skeleton
├── Scenes/                     # StartMenu và Dungeon
├── Scripts/
│   ├── Core/                   # State machine, cấu hình level và cầu nối phiên chơi
│   ├── Enemy/                  # AI và chiến đấu của enemy
│   ├── ItemPlacement/          # Bố trí enemy trong phòng
│   ├── PCG/                    # Sinh phòng và hành lang ngẫu nhiên
│   ├── Player/                 # Di chuyển, máu và kỹ năng người chơi
│   ├── ScriptableObjects/      # Dữ liệu cấu hình map và enemy
│   ├── Tilemap/                # Vẽ sàn, tường và nhận dạng loại tường
│   ├── UI/                     # Điều khiển giao diện
│   ├── AudioManager.cs         # Quản lý nhạc và hiệu ứng âm thanh
│   └── GameManager.cs          # Trạng thái và luồng chơi chính
├── Tests/                      # Edit Mode test cho Core progression
└── Tilesets/                   # Tile và palette của dungeon
```

## Định hướng phát triển

Phiên bản tiếp theo dự kiến bổ sung:

- Prefab và nội dung riêng cho phòng kho báu, boss và cổng thoát.
- Kỹ năng Dash và các nâng cấp cho người chơi.
- Hệ thống vật phẩm.
- Enemy cận chiến, enemy tầm xa và boss nhiều giai đoạn.
- Điểm số, high score và lưu cài đặt.
- HUD, minimap và màn hình kết quả hoàn chỉnh.
- Cải thiện LAN co-op: tìm phòng tự động, dự đoán di chuyển và tối ưu đồng bộ.
- Mở rộng kiểm thử Play Mode và kiểm thử tích hợp mạng.

## Làm việc nhóm

Tài liệu phân công cho 5 thành viên được lưu tại:

- [Phân công thành viên](Docs/PHAN_CONG_THANH_VIEN.docx)
- [Kế hoạch nâng cấp 10 level và LAN co-op](Docs/BAO_CAO_KE_HOACH_NANG_CAP_THE_LABYRINTH.docx)

Quy ước làm việc:

- Mỗi nhóm chức năng sử dụng một branch riêng.
- Không commit các thư mục Unity tự sinh như `Library/`, `Temp/`, `Logs/` và `Obj/`.
- Hạn chế để nhiều thành viên cùng chỉnh sửa một scene trong cùng thời điểm.
- Không sửa trực tiếp `Assets/Input/PlayerControls.cs` vì đây là file được Unity tự sinh.
- Khi thay đổi điều khiển, chỉnh `Assets/Input/PlayerControls.inputactions` rồi để Unity sinh lại mã nguồn.

## Hạn chế hiện tại

- Chỉ có một loại enemy là Skeleton.
- Cổng thoát hiện dùng hình đại diện runtime khi chưa cấu hình prefab chính thức.
- Phòng kho báu và boss chưa có prefab gameplay chính thức.
- Level mini-boss và boss mới có cấu hình Core; gameplay boss phụ thuộc prefab của module Enemy/Boss.
- LAN hiện hỗ trợ hai người bằng IP trực tiếp; chưa hỗ trợ lobby Internet, host migration và tự tìm phòng.
- AI đang kết hợp NavMeshAgent với thay đổi `transform` trực tiếp và cần được refactor.
- Kỹ năng tích lực vẫn sử dụng API input cũ, chưa hỗ trợ rebind hoặc gamepad đầy đủ.
- Chưa có hệ thống lưu dữ liệu; test tự động hiện mới bao phủ cấu hình Core và seed level.

## Giấy phép

Project sử dụng giấy phép MIT. Xem nội dung chi tiết trong file [LICENSE](LICENSE).
