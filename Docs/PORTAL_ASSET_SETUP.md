# Cài đặt asset portal

Portal hoạt ảnh do Frostwindz tạo và được phân phối theo giấy phép nằm trong
`Pixel Art Animated Portal.zip`.

Giấy phép cho phép dùng trong game thương mại và phi thương mại, nhưng cấm phân
phối lại file hình nguồn. Chủ dự án đã yêu cầu theo dõi các frame trong
repository để nhóm đồ án dùng chung. Không tái sử dụng hoặc phát hành riêng các
PNG bên ngoài phạm vi dự án này.

## Vị trí asset trong dự án

Bảy frame được đặt tại:

`Assets/Resources/Portal/Frames/`

- `portal1_frame_1.png`
- `portal1_frame_2.png`
- `portal1_frame_3.png`
- `portal1_frame_4.png`
- `portal1_frame_5.png`
- `portal1_frame_6.png`
- `portal1_frame_7.png`

Unity nhập các file dưới dạng texture. `PortalVisual` tự tạo và lưu cache sprite
khi chạy game, vì vậy không cần cắt sprite trong Sprite Editor hoặc gán tham
chiếu trong scene. Nếu thiếu asset được cấp phép, cổng vẫn dùng hình dự phòng và
tiến trình màn chơi tiếp tục hoạt động bình thường.

Tác giả: Frostwindz — https://frostwindz.itch.io/
