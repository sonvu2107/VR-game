# Tích hợp kịch bản chiến dịch

Nguồn thoại: `KICH_BAN_CHIEN_DICH_10_LEVEL.md`. `CampaignStoryText` giữ hai nhánh chơi đơn/LAN cho phần mở màn 10 level, lời thoại trong màn, chìa khóa và kết thúc. `CampaignStory` tạo UI lúc chạy: chuyển cảnh tối sáng, tên level, chân dung người nói ở trái/phải, lời thoại hiện dần và chân dung chuyển động nhẹ. Bấm **Space**, **Enter** hoặc **chuột trái** để hiện hết câu/qua câu; các câu cũng tự chuyển. Lời dẫn level 1 được chia thành ba nhịp để không bị cắt chữ.

Trong Unity Editor, bấm Play từ scene Dungeon sẽ mở StartMenu. Bảng điều khiển hiện ngay khi vào StartMenu: chọn 1 trong 10 level, nhập IP/cổng LAN, tạo phòng, tham gia, chơi một mình hoặc dừng Play. Bản build vẫn bắt đầu ở StartMenu.

Mỗi intro hoặc hội thoại chuyển game sang trạng thái `Story`: thời gian và AI quái dừng, nhân vật không thể di chuyển hoặc chịu sát thương, đồng hồ level dừng. Sau khi xem xong, game tiếp tục từ trạng thái trước đó. Trong LAN, Host chờ cả hai máy hoàn tất hội thoại rồi mới cho quái hoạt động lại.

Font thoại dùng `Assets/Resources/Story/StoryFont.asset`: atlas 2048 đã chứa sẵn toàn bộ ký tự của 10 level, không tạo atlas thứ hai lúc chơi. Font TMP dự phòng bị lỗi atlas rỗng đã được đặt lại. Nếu sửa nội dung thoại, dùng **Tools > The Labyrinth > Refresh Story Font** trước khi build. Bộ hiển thị hội thoại tự trả lại điều khiển nếu coroutine gặp lỗi dựng UI, tránh kẹt ở trạng thái `Story`.

Trong LAN, Host tạo Player 2 từ Player gốc nên hai người dùng cùng di chuyển, máu và kỹ năng. `CompanionVisual` thay khung hình bằng bản `FreeKnight_v1/Colour2` có sẵn trong assets; các sheet cần lúc chạy được chép vào `Assets/Resources/Story/Companion`. Host gửi sprite qua hệ thống đồng bộ hình ảnh hiện có. Hình `SkeletonKing.png` trong cùng thư mục chỉ là chân dung minh họa cho **Tiếng Vua Xương**, không thay boss ngẫu nhiên của level 10. Không tải thêm asset ngoài.

Mỗi level phát intro sau khi sinh map xong. Host phát sự kiện hội thoại một lần mỗi level khi người chơi vào phòng thích hợp; level 1 còn có cảnh báo gần cổng khóa. Level 4 và 8 phát theo lần nhặt chìa thực tế, kèm đúng vai người nhặt. Level 5 phát sau mini boss; level 9/10 có câu khi vào phòng boss. Host gửi mã sự kiện và vai người nhặt trong snapshot LAN để hai máy hiển thị cùng nhánh. Victory chỉ phát sau khi hoàn tất cổng level 10.

`LanBuildTools.RefreshCatalog` lấy dependency của toàn bộ `Resources`, gồm material dùng khi sinh map và texture chân dung/nhân vật. Sau khi đổi tài nguyên hãy refresh catalog hoặc build lại trước khi thử hai máy bằng cùng bản build.

Kiểm chứng ngày 19/10/2026: Unity 2022.3.10f1 biên dịch và build Windows thành công. Smoke test hai tiến trình Host/Client PASS đủ 10 level, lời thoại khóa/mở trạng thái chơi, chìa khóa, pause, restart, game over, Victory, Q/E/R/Space và xác nhận Player 2 hiển thị sheet Colour2. Probe Play Mode trong Editor dừng do lỗi native của Unity tại `HudVisibility` khi đổi Pause; bản build độc lập đã hoàn thành smoke test.

Kiểm chứng thêm sau lỗi atlas level 10: `StoryDialogueProbe` trên bản build Development đạt 27 đoạn intro/hội thoại của cả 10 level, gồm `BossEntry` level 10; quái đứng yên khi hội thoại chạy và trạng thái `Playing` được khôi phục sau mỗi đoạn. Bài kiểm tra LAN Host/Client tiếp tục PASS đủ 10 level. Font mới được tạo với `missing=0`, `atlas=1`.
