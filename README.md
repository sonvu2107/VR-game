# The Labyrinth

**The Labyrinth** là game hành động khám phá mê cung 2D được phát triển bằng Unity. Bản đồ được sinh tự động bằng thuật toán Random Walk; người chơi phải vượt qua chiến dịch 10 level trong cùng một hầm ngục, chiến đấu với Skeleton và mở cổng sang level tiếp theo.

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
- State machine quản lý các trạng thái Generating, Playing, Level Completed, Paused, Game Over và Victory.
- Cổng chuyển level chỉ mở sau khi toàn bộ enemy của level đã bị tiêu diệt.
- Cấu hình chiến dịch đánh dấu level thường, mini-boss và boss cuối.
- Có `GameSessionBridge` làm ranh giới tích hợp quyền Host/Client cho LAN co-op.
- Tự động tạo sàn và tường bằng Tilemap.
- NavMesh 2D hỗ trợ enemy tìm đường và đuổi theo người chơi.
- Di chuyển nhân vật bằng bàn phím hoặc gamepad.
- Đánh thường và kỹ năng tích lực gây sát thương diện rộng.
- Hệ thống máu hiển thị bằng các biểu tượng trái tim.
- Enemy Skeleton có hành vi đi lang thang, truy đuổi và tấn công.
- Hiển thị số lượng enemy còn lại.
- Các trạng thái Pause, Game Over và Victory.
- Menu bắt đầu, màn hình tải và âm thanh cơ bản.

## Cách chơi

Mục tiêu hiện tại của người chơi là khám phá mê cung và tiêu diệt toàn bộ Skeleton trong từng level. Khi số lượng enemy về `0`, cổng thoát được mở. Dungeon được sinh lại theo cấu hình level tiếp theo; Victory chỉ xuất hiện sau khi hoàn thành level 10.

### Bàn phím và chuột

| Thao tác | Phím |
|---|---|
| Di chuyển | `W`, `A`, `S`, `D` hoặc các phím mũi tên |
| Đánh thường | Chuột trái hoặc `Left Alt` |
| Tích lực | Giữ `Space` |
| Tung đòn tích lực | Thả `Space` |
| Tạm dừng/tiếp tục | `Esc` |

Đánh thường chỉ được thực hiện một lần cho mỗi lần nhấn. Người chơi cần thả nút rồi nhấn lại để đánh tiếp.

### Gamepad

| Thao tác | Điều khiển |
|---|---|
| Di chuyển | Left Stick |
| Đánh thường | Right Trigger |

Kỹ năng tích lực và Pause hiện chưa được gán đầy đủ cho gamepad.

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
- LAN co-op hai người trên hai máy bằng Network Transport.
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
- `GameSessionBridge` mới là hợp đồng tích hợp; chưa có package mạng, màn hình Host/Join hoặc đồng bộ Player/Enemy qua LAN.
- AI đang kết hợp NavMeshAgent với thay đổi `transform` trực tiếp và cần được refactor.
- Kỹ năng tích lực vẫn sử dụng API input cũ, chưa hỗ trợ rebind hoặc gamepad đầy đủ.
- Chưa có hệ thống lưu dữ liệu; test tự động hiện mới bao phủ cấu hình Core và seed level.

## Giấy phép

Project sử dụng giấy phép MIT. Xem nội dung chi tiết trong file [LICENSE](LICENSE).
