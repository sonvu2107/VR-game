# Skull và Vampire

## Nội dung

- Theo lựa chọn mới nhất: chỉ dùng lại frame gốc có sẵn cho Skull và Vampire; không dùng atlas AI mới. Các thử nghiệm atlas riêng đã được rút khỏi Assets và lưu bản sao ngoài project.

- Dùng bốn PNG `v1` của từng mob trong `Assets/Imports/2D Pixel Dungeon Asset Pack/Character_animation/monsters_idle/`.
- `EnemySpriteAnimation` lặp bốn frame ở 8 FPS khi đứng, tăng nhịp khi di chuyển/đánh. Kết hợp animation theo trạng thái: Skull lướt/lơ lửng, Vampire nhịp bước, lấy đà/lao đánh, giật khi đau và ngã khi chết. Đây là frame gốc kết hợp chuyển động lập trình, không phải bộ sprite vẽ mới cho từng động tác.
- Không cắt lại sprite, không đổi pivot hoặc PPU; kích thước hình được chỉnh trên child `Animated Sprite`, không phóng collider theo ảnh.
- Skull: 65 HP, truy đuổi 3.2 (nhanh hơn Skeleton 2.5 khoảng 28%), cận chiến 1 sát thương, hồi đòn 1.8 giây. Xuất hiện từ tầng 2; tham gia pool tầng 2–9.
- Vampire: 85 HP, truy đuổi 1.85, cận chiến/cú lao 1 sát thương, hồi đòn 3 giây. Cú lao dùng cơ chế Charger hiện có. Xuất hiện từ tầng 7, tham gia pool tầng 7–10.
- Vampire hút 12 HP cho mỗi 1 HP thực sự lấy từ người chơi bằng đánh gần, chạm hoặc cú lao. Đòn bị khung bất tử chặn/đánh hụt không hồi máu; giới hạn 85 HP; không hồi sinh. Chớp xanh báo hồi máu. Chỉ Host/Solo được tính sát thương và hút máu.
- Đảm bảo có Skull ở phòng Combat đầu tầng 2 và Vampire ở phòng Combat thứ hai tầng 7. Giữ các mốc Archer 3, Slime 4, Bat 6, Elite 7, Mage 8 và boss 5/9/10.
- Không tăng quota quái/phòng. Các prefab cũ và API tiến trình giữ nguyên.
- Hai prefab mới được gắn vào Dungeon, DungeonCourtyardPrototype và DungeonArcaneCitadel.
- Host giữ AI/sát thương; frame SpriteRenderer dùng snapshot LAN hiện có. Cả hai máy phải build lại cùng phiên bản/catalog.

## Công cụ kiểm thử

- `SkullVampireIntegration.InstallBatch`: tạo/cập nhật hai prefab và config bằng Unity Editor API, gắn scene, refresh catalog, kiểm tra cấu hình và phân phối 10 tầng, sau đó chạy Play probe. Chỉ chạy khi không Play và không có scene chưa lưu.
- `SkullVampireIntegration.ValidateContent`: kiểm tra prefab, bốn frame/mob, tham chiếu ba scene và đăng ký LAN.
- `SkullVampirePlayValidation.Run`: kiểm tra loop đủ bốn frame, scale/collider cố định, pause/resume, nhận damage/chết và LAN visual JSON round-trip trong một Editor. Không thay thế kiểm thử Wi-Fi trên hai máy.
- Việc tái tạo prefab chỉ dành cho prefab do công cụ này quản lý; lưu chỉnh sửa thủ công trước khi chạy lại.

## Trạng thái kiểm thử

- Unity 2022.3.10f1 / URP: biên dịch và tạo asset thành công.
- Prefab/config: 2/2; animation: 4 frame/mob; tham chiếu scene: 3/3; LAN catalog: 8/8 frame.
- HealthAndLevelValidation: PASS, giữ 5 tim và các mốc quái cũ.
- EnemyDistributionValidation: PASS cả 10 tầng, không tăng quota phòng.
- EditMode CoreProgressionTests: 8/8 PASS.
- Play Mode trên Arcane Citadel: loop đủ frame, scale/collider cố định, Pause/Resume, nhận damage/chết/despawn: PASS.
- Màu báo trước cú lao không bị animation ghi đè: PASS.
- Hút máu: gây damage hồi 12, đòn bị chặn/đánh hụt hồi 0, Client không được thay đổi HP, hồi không vượt trần và không hồi sinh: PASS trong Play Mode.
- LAN visual JSON round-trip trong một Editor: PASS; chưa thử lại hai máy thật/Wi-Fi và chưa tạo build mới.
