# Hệ thống trang trí hầm ngục tự động (Dungeon Decoration System)

Hệ thống trang trí ngẫu nhiên theo thuật toán sinh phòng, kết hợp hài hòa giữa **trang trí tường (Wall Decor)** và **trang trí sàn/cỏ cây (Floor & Foliage Decor)**, mang lại vẻ đẹp sinh động, cổ kính và ma mị cho The Labyrinth.

## 1. Các thành phần trang trí

Tất cả asset trang trí được tối ưu và đặt tại `Assets/Resources/Decor/`:

### A. Trang trí vách tường & Ngọn đuốc lấp lửng (Floating Fire & Wall Decor)
* **Đuốc gắn tường (`Decor/Torches/torch_1` đến `torch_4`):**
  * Trong các vị trí ứng viên được xét, xác suất chọn đuốc là ~65% ở tầng thường, 70% ở tầng 6 và boss cuối; không phải tỷ lệ phủ toàn bộ tường.
  * **Hiệu ứng ngọn lửa lấp lửng (`Floating Flame`):** Sprite ngọn lửa nhấp nhô lơ lửng bồng bềnh trên trục Y (`Mathf.Sin`), tạo cảm giác ngọn lửa thực sự đang bay bổng và cháy cuộn trong không khí.
  * **Nguồn sáng đa tầng (`Breathing Light2D`):** URP Point Light màu cam vàng ấm áp với độ sáng dao động đa tần (Perlin noise + Sine wave) và bán kính vầng sáng co giãn nhẹ nhàng, phản chiếu quầng sáng chao đảo lấp lửng lên các vách đá xung quanh.
  * Thuật toán duyệt danh sách vị trí tường với bước nhảy 5–7 ô và khoảng cách tối thiểu giữa 2 đuốc là 4.5 ô (ngăn việc đuốc dồn dày); kích thước đuốc thu gọn tỉ lệ 0.58 vừa vặn với tầm vóc nhân vật.
* **Biểu ngữ lâu đài (`Decor/Flags/flag_1` đến `flag_4`):**
  * Xác suất 8% ở vị trí được xét, cách nhau tối thiểu 14 chỉ số ứng viên; tầng 6 không đặt cờ.
  * Các vị trí không được chọn giữ nguyên tường gốc.

### B. Thùng gỗ tiếp tế & Hộp đồ góc phòng (Dungeon Storage Crates)
* **Thùng gỗ hầm ngục (`Decor/Props/box_1` đến `box_4`):**
  * Tích hợp 4 kiểu thùng gỗ và hộp chứa mini từ Pixel Dungeon Asset Pack.
  * Xuất hiện tinh tế ở các góc phòng (`cornerCells`), tạo cảm giác đây là một kho chứa ngầm cổ kính của lâu đài.
  * Bố trí tối đa 1 đến 2 cụm mỗi phòng, đặt sát chân tường để giữ toàn bộ mặt sàn rộng rãi, thông thoáng cho việc di chuyển và né đòn.

### C. Mặt sàn hầm ngục sạch sẽ & Rương kho báu (Clear Floor & Treasure Chest)
* **Mặt sàn hầm ngục thông thoáng 100%:**
  * Toàn bộ mặt sàn phòng chính và các hành lang được giữ phẳng phiu, sạch sẽ tuyệt đối: không cây cối, không đá sỏi, không rêu cỏ dại và không đầu lâu xương xẩu.
  * Tối ưu hóa tối đa cho trải nghiệm gameplay chiến đấu: góc nhìn rộng mở, dễ dàng quan sát bẫy chông, quái vật và các đường đòn của kẻ thù.
* **Rương kho báu (`Decor/Props/chest_1`):**
  * Xuất hiện trang trọng ở chính giữa phòng Kho Báu.
  * Hiện là vật trang trí; hệ thống này không thêm thao tác mở rương hoặc phần thưởng.

---

## 2. Mã nguồn triển khai

* **`Assets/Scripts/Decor/AnimatedDecor.cs`:**
  * Điều khiển hoạt ảnh sprite ngọn lửa đuốc nhấp nhô lơ lửng và nguồn sáng 2D lung linh bồng bềnh.
* **`Assets/Scripts/Decor/DungeonDecorator.cs`:**
  * Thuật toán trang trí vách tường với đuốc và biểu ngữ lâu đài, thùng gỗ ở các góc phòng và rương báu trong phòng kho báu.
  * Toàn bộ mặt sàn và hành lang được dọn sạch sẽ, tối giản và chuyên nghiệp.
* **Tích hợp vào `InfiniteWorldGenerator.cs`:**
  * Tự động khởi tạo sau khi sinh map, tự động dọn dẹp sạch sẽ khi chuyển tầng.
