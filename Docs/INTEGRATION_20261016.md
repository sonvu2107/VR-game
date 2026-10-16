# Tích hợp Combat, Decor và LAN — 16/10/2026

## Phạm vi

- Nền: `main` tại `416db1a`.
- Merge nhánh `feature/combat-balance-and-dungeon-decor` tại `0b55551`, giữ lịch sử ba commit `e156a8c`, `8d52640`, `0b55551`.
- Đưa module LAN hai người đang có ở local vào repository theo yêu cầu của chủ project.
- Không đưa font động, scene tùy chỉnh và DOCX chưa commit của người dùng vào bản tích hợp. Scene `Dungeon` trong Git vẫn giữ cấu hình từ `main`; LAN được gắn tự động khi chọn Host/Join.

## Xử lý giao nhau

- Máu: giữ giới hạn nửa tim/đòn và bất tử 0,85 giây; Client không tự tính sát thương. Một người gục không kết thúc trận khi đồng đội còn sống; cả hai gục mới Game Over. Sang tầng/Restart xóa trạng thái bất tử và nhấp nháy còn dư.
- Kỹ năng: Host đọc input Q/E/R của Client, không lấy bàn phím Host cho actor thứ hai. Q giữ auto-aim. Q/E được cộng nộ trong cửa sổ thi triển; tuyệt kỹ không tự cộng nộ từ sát thương của chính nó. Restart LAN khôi phục nộ đầy như khi bắt đầu.
- Hình ảnh: texture procedural của boss giữ dữ liệu pixel trong LAN để truyền PNG, tránh biến vòng ma pháp/bóng đổ thành ô trắng ở Client. Đồng bộ nguồn sáng Point Light2D của đuốc/boss, gồm vị trí, tỷ lệ, màu, bán kính và cường độ.
- Giữ minimap, 5 tim, chỉ hướng quái, chìa khóa, thời gian, điểm và tiến trình 10 tầng đã có.

## Kiểm chứng

Log/build được giữ ngoài repository tại `C:\tl-merge-artifacts-20261016\`.

- Unity 2022.3.10f1: biên dịch và build Windows x64 Development thành công (`build-verified.log`), không có lỗi shader/C# trong lượt build này.
- Edit Mode trên source cuối: 8/8 Passed (`editmode-final.xml`). Chơi đơn source cuối: PASS 10/10 tầng, Pause/Resume, Game Over, Restart, Victory, cổng animated và PowerUp VFX (`solo-final.log`).
- `LanTransportProbe`: 9/9; `HealthAndLevelValidation`: PASS sau khi cập nhật kỳ vọng scale theo prefab mới; `EnemyDistributionValidation`: PASS 10/10 tầng.
- `CombatDecorIntegrationProbe`: PASS minimap, 5 tim, chỉ hướng quái, decor/đuốc, giới hạn nửa tim, thời gian bất tử và reset, Q auto-aim, Q/E cộng nộ, R sẵn sàng và texture cache còn pixel khi chuyển Solo -> Host (`combat-decor-final.log`).
- Hai tiến trình Windows D3D11, loopback `127.0.0.1:28777`: cả hai exit 0. Host PASS di chuyển, input kỹ năng, Pause/Resume, chìa khóa, Restart, một người gục vẫn chơi, cả hai gục Game Over, 10 tầng và Victory. Client PASS HUD, Q/E/R/Space, ánh sáng, boss art, các trạng thái và texture pixel chính xác; nhận 156 snapshot, 35 texture runtime (`host-verified.log`, `client-verified.log`).
- Lượt kiểm tra đầu bắt mũi tên trước khi component nhận `Start`; probe đã chờ frame gameplay ổn định và chạy lại PASS. Fixture đo input LAN đầu tiên dùng cửa sổ ngắn khi khởi tạo đồ họa; lượt cuối đo tác động thực tế của di chuyển và toàn bộ kỹ năng trong thời hạn 25 giây, không bỏ qua điều kiện di chuyển.

Bản chạy: `C:\tl-merge-artifacts-20261016\build\TheLabyrinth.exe`. Chép toàn bộ thư mục `build` sang máy thứ hai; không chỉ chép EXE. Không đưa binary build hoặc file save vào Git.

Các probe chỉ chạy khi được gọi rõ ràng; `LanSmokeProbe` không có trong Release và cần cờ `-lanSmoke` ở Development Build. Các tiến trình kiểm thử dùng `LABYRINTH_TEST_SAVE_DIR` để không ghi đè save người chơi.

## Giới hạn nghiệm thu

Kiểm thử hai tiến trình trên cùng máy không chứng minh khả năng kết nối hai máy Wi-Fi vật lý, Firewall/router hay độ trễ thực tế. Cần dùng cùng toàn bộ thư mục build mới trên hai máy. Không có thay đổi tự động đối với Firewall, loại mạng Windows hoặc cấu hình router.

LAN hiện dùng IPv4/TCP với Host có quyền quyết định gameplay, chưa có discovery, mật khẩu phòng, Internet lobby, host migration hoặc dự đoán chuyển động Client. Cần đo băng thông/hiệu năng với lượng đuốc và VFX mới trước khi tăng số lượng quái/người chơi.
