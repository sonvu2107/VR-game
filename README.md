# The Labyrinth

**The Labyrinth** là game hành động khám phá mê cung 2D được phát triển bằng Unity. Chiến dịch gồm 10 level: level 1 dùng dungeon gốc, hoàn thành và qua cổng sẽ sang map Arcane Citadel từ level 2. Các level sau tiếp tục dùng Arcane với cấu hình độ khó riêng cho đến khi có thêm theme khác.

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

## Tính năng hiện có

- Sinh phòng và hành lang ngẫu nhiên bằng Procedural Content Generation (PCG).
- Một lượt chơi gồm 10 level với seed, số phòng và độ khó riêng cho từng level.
- Đổi từ map gốc ở level 1 sang Arcane ở level 2 trong cùng phiên chơi; không tải lại Player, điểm số hay kết nối LAN. Restart trở lại level 1/map gốc.
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
