# Thiết lập hiệu ứng giữ Space — Pipoya Time Magic

Game dùng hiệu ứng số 213 trong gói `Pipoya VFX TimeMagic.zip` cho trạng thái
người chơi giữ Space để nạp Power-up Attack.

## Vị trí asset trong dự án

Asset đang dùng được đặt tại:

`Assets/Resources/VFX/Pipoya/TimeMagic/pipo-btleffect213_192.png`

Không cần slice thủ công. `PowerupChargeVfx` đọc sheet 960×768 px theo lưới
5 cột × 4 hàng và tạo 20 frame ở runtime.

Hiệu ứng chạy phần mở đầu màu vàng một lần, sau đó lặp liên tục các frame 6–20
trong suốt thời gian giữ Space. Kim đồng hồ chạy hết toàn bộ chu kỳ vàng → đỏ →
tím rồi mới quay về frame 6. Mỗi frame sử dụng nguyên vùng 192×192, không crop,
không bỏ frame giữa, renderer không bị tắt và không xen frame rỗng nên hiệu ứng
không bị ngắt. Khi thả Space, hiệu ứng tắt ngay. Vòng tròn trắng cũ của
`PowerupCircleController` được tắt mặc định.

VFX dùng trục X của transform `Powerup Attack Circle` để căn giữa theo phần pixel
nhìn thấy của nhân vật, còn trục Y bám vào chân Player. Vì vậy chỉ có một pháp
trận nằm dưới chân thay vì hai vòng bao quanh cơ thể.

Kích thước hiệu ứng tăng dần từ 1,4 đến 1,65 lần chiều cao sprite Player theo
mức nạp. Pháp trận dùng cùng sorting layer nhưng sorting order thấp hơn Player
một bậc, nên nhân vật luôn đứng phía trên vòng phép.

Nếu thiếu texture, gameplay giữ Space vẫn hoạt động; Unity chỉ ghi cảnh báo và
không hiện vòng ma pháp.

## Giấy phép

Tác giả: Pipoya — https://pipoya.net/

Trang chính thức của gói cho phép dùng và chỉnh sửa trong sản phẩm game, nhưng
không cho phép phân phối lại hoặc bán lại asset nguồn. Chủ dự án đã yêu cầu theo
dõi file 213 trong repository để nhóm đồ án dùng chung. Không tái sử dụng hoặc
phát hành riêng asset này bên ngoài phạm vi dự án.
